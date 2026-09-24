using System.Diagnostics;
using System.Text.Json;
using KnappMiddleware.Auditing;
using KnappMiddleware.Auth;
using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Matrix;
using KnappMiddleware.Tcp;
using KnappMiddleware.Telegramas;
using KnappMiddleware.Telegramas.Mapeo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace KnappMiddleware.Controllers.Sap;

[ApiController]
[Route("api/v1/sap/order")]
[Authorize(Policy = AuthorizationPolicies.SapOrSuperUsuario)]
[Tags("Order")]
public sealed class OrderController : ControllerBase
{
    private readonly ILogger<OrderController> _logger;
    private readonly ClsMatrixGate _matrixGate;
    private readonly ICanalPedidoKiSoft _orderChannel;
    private readonly ClsAuditWriter _auditWriter;

    public OrderController(ILogger<OrderController> logger, ClsMatrixGate matrixGate, ICanalPedidoKiSoft orderChannel, ClsAuditWriter auditWriter)
    {
        _logger = logger;
        _matrixGate = matrixGate;
        _orderChannel = orderChannel;
        _auditWriter = auditWriter;
    }

    [HttpPost("12N")]
    public async Task<IActionResult> Post([FromBody] SolicitudPedidoDto dto, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        var http = AuditHttpContext.From(HttpContext);
        LogEntrada(correlationId, dto, http, dto.ObjectId, dto.TeCreatedBy);

        var estadoSalida = AuditEstado.Gate;
        string? payloadSalida = null;
        string? errorSalida = null;
        IActionResult resultado = null!;

        try
        {
            if (dto.Items.Count == 0)
            {
                errorSalida = "El pedido debe tener al menos una línea.";
                return resultado = BadRequest(new { message = errorSalida });
            }

            // La matriz filtra por estación, pero el encabezado de 12N no trae una única estación (cada
            // línea tiene la suya); se resuelve con la estación de la primera línea como aproximación.
            var estacion = dto.Items[0].Station;
            var accion = _matrixGate.Resolve(dto.Mandante, "12N", estacion);
            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Pedido {Pedido} rechazado: estación {Estacion} deshabilitada por matriz (mandante {Mandante}).",
                    dto.OrderNumber, estacion, dto.Mandante);
                errorSalida = "Estación deshabilitada por la matriz de mensajes.";
                return resultado = Conflict(new { message = errorSalida });
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Pedido {Pedido} ignorado por matriz (mandante {Mandante}, estación {Estacion}).",
                    dto.OrderNumber, dto.Mandante, estacion);
                return resultado = Ok();
            }

            string dataPayload;
            try
            {
                dataPayload = MapeadorTelegramaPedido.BuildNew(dto);
            }
            catch (NotSupportedException ex)
            {
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = UnprocessableEntity(new { message = ex.Message });
            }
            catch (ExcepcionFormatoTelegrama ex)
            {
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = UnprocessableEntity(new { message = ex.Message });
            }

            payloadSalida = TramaHex.Encode(dataPayload);

            string response;
            MensajeEstadoKiSoft status;
            try
            {
                response = await _orderChannel.SendAsync(dataPayload, cancellationToken);
                status = MensajeEstadoKiSoft.Parse(response);
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Timeout enviando pedido {Pedido} a KiSoft.", dto.OrderNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Canal KiSoft no disponible enviando pedido {Pedido}.", dto.OrderNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Conexión con KiSoft perdida enviando pedido {Pedido}.", dto.OrderNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (ExcepcionFormatoTelegrama ex)
            {
                _logger.LogError(ex, "Respuesta de KiSoft malformada enviando pedido {Pedido}.", dto.OrderNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            if (!status.IsOk)
            {
                _logger.LogError("Pedido {Pedido} rechazado por KiSoft: estado={Estado}.", dto.OrderNumber, status.Estado);
                estadoSalida = AuditEstado.Error;
                errorSalida = $"KiSoft rechazó el pedido: estado={status.Estado}.";
                LogRespuestaKiSoft(correlationId, http, response, AuditEstado.Error, dto.ObjectId, dto.TeCreatedBy);
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { recordId = status.RecordId, estado = status.Estado, ok = status.IsOk });
            }

            estadoSalida = AuditEstado.Entregado;
            LogRespuestaKiSoft(correlationId, http, response, AuditEstado.Entregado, dto.ObjectId, dto.TeCreatedBy);
            return resultado = Ok(new { recordId = status.RecordId, estado = status.Estado, ok = status.IsOk });
        }
        finally
        {
            var httpStatus = (resultado as IStatusCodeActionResult)?.StatusCode;
            LogSalida(correlationId, estadoSalida, payloadSalida, errorSalida, (int)stopwatch.ElapsedMilliseconds, http, httpStatus, dto.ObjectId, dto.TeCreatedBy);
        }
    }

    private void LogEntrada(Guid correlationId, SolicitudPedidoDto dto, AuditHttpContext http, string? idObjeto, string? creadoPorSap) =>
        _auditWriter.EnqueueEntrada(new AuditRecord(correlationId, "12N", "SAP", "Middleware", AuditEstado.Recibido,
            JsonSerializer.Serialize(dto), Usuario: http.Usuario, Ruta: http.Ruta, IpOrigen: http.IpOrigen, IdObjeto: idObjeto, CreadoPorSap: creadoPorSap));

    private void LogSalida(Guid correlationId, AuditEstado estado, string? payload, string? errorDetalle, int durationMs, AuditHttpContext http, int? httpStatus, string? idObjeto, string? creadoPorSap) =>
        _auditWriter.EnqueueSalida(new AuditRecord(correlationId, "12N", "Middleware", "KiSoft", estado, payload, errorDetalle, durationMs,
            httpStatus, http.Usuario, http.Ruta, http.IpOrigen, idObjeto, creadoPorSap));

    private void LogRespuestaKiSoft(Guid correlationId, AuditHttpContext http, string rawResponse, AuditEstado estado, string? idObjeto, string? creadoPorSap) =>
        _auditWriter.EnqueueEntrada(new AuditRecord(correlationId, "12N", "KiSoft", "Middleware", estado,
            TramaHex.Encode(rawResponse), Usuario: http.Usuario, Ruta: http.Ruta, IpOrigen: http.IpOrigen, IdObjeto: idObjeto, CreadoPorSap: creadoPorSap));
}
