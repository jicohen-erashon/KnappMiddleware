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
[Route("api/v1/sap/inventory")]
[Authorize(Policy = AuthorizationPolicies.SapOrSuperUsuario)]
[Tags("Inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly ILogger<InventoryController> _logger;
    private readonly ClsMatrixGate _matrixGate;
    private readonly ICanalPedidoKiSoft _orderChannel;
    private readonly ClsAuditWriter _auditWriter;

    public InventoryController(ILogger<InventoryController> logger, ClsMatrixGate matrixGate, ICanalPedidoKiSoft orderChannel, ClsAuditWriter auditWriter)
    {
        _logger = logger;
        _matrixGate = matrixGate;
        _orderChannel = orderChannel;
        _auditWriter = auditWriter;
    }

    [HttpPost("request/1IA")]
    public async Task<IActionResult> PostRequest([FromBody] SolicitudInventarioDto dto, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        var http = AuditHttpContext.From(HttpContext);
        LogEntrada(correlationId, "1IA", dto, http, dto.ObjectId, dto.TeCreatedBy);

        var estadoSalida = AuditEstado.Gate;
        string? payloadSalida = null;
        string? errorSalida = null;
        IActionResult resultado = null!;

        try
        {
            if (dto.Items.Count == 0)
            {
                errorSalida = "La solicitud de inventario debe tener al menos una línea de filtro.";
                return resultado = BadRequest(new { message = errorSalida });
            }

            var estacion = dto.Items[0].Station ?? "*";
            var accion = _matrixGate.Resolve(dto.Mandante, "1IA", estacion);
            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Solicitud de inventario {Solicitud} rechazada por matriz (mandante {Mandante}, estación {Estacion}).",
                    dto.InventoryRequestNumber, dto.Mandante, estacion);
                errorSalida = "Estación deshabilitada por la matriz de mensajes.";
                return resultado = Conflict(new { message = errorSalida });
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Solicitud de inventario {Solicitud} ignorada por matriz (mandante {Mandante}).",
                    dto.InventoryRequestNumber, dto.Mandante);
                return resultado = Ok();
            }

            string dataPayload;
            try
            {
                dataPayload = MapeadorTelegramaInventario.BuildRequest(dto);
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
                _logger.LogWarning(ex, "Timeout enviando solicitud de inventario {Solicitud} a KiSoft.", dto.InventoryRequestNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Canal KiSoft no disponible enviando solicitud de inventario {Solicitud}.", dto.InventoryRequestNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Conexión con KiSoft perdida enviando solicitud de inventario {Solicitud}.", dto.InventoryRequestNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (ExcepcionFormatoTelegrama ex)
            {
                _logger.LogError(ex, "Respuesta de KiSoft malformada enviando solicitud de inventario {Solicitud}.", dto.InventoryRequestNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            if (!status.IsOk)
            {
                _logger.LogError("Solicitud de inventario {Solicitud} rechazada por KiSoft: estado={Estado}.", dto.InventoryRequestNumber, status.Estado);
                estadoSalida = AuditEstado.Error;
                errorSalida = $"KiSoft rechazó la solicitud: estado={status.Estado}.";
                LogRespuestaKiSoft(correlationId, "1IA", http, response, AuditEstado.Error, dto.ObjectId, dto.TeCreatedBy);
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { recordId = status.RecordId, estado = status.Estado, ok = status.IsOk });
            }

            estadoSalida = AuditEstado.Entregado;
            LogRespuestaKiSoft(correlationId, "1IA", http, response, AuditEstado.Entregado, dto.ObjectId, dto.TeCreatedBy);
            return resultado = Ok(new { recordId = status.RecordId, estado = status.Estado, ok = status.IsOk });
        }
        finally
        {
            var httpStatus = (resultado as IStatusCodeActionResult)?.StatusCode;
            LogSalida(correlationId, "1IA", estadoSalida, payloadSalida, errorSalida, (int)stopwatch.ElapsedMilliseconds, http, httpStatus, dto.ObjectId, dto.TeCreatedBy);
        }
    }

    [HttpPost("realtime/1RR")]
    public async Task<IActionResult> PostRealTimeView([FromBody] SolicitudInventarioTiempoRealDto dto, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        var http = AuditHttpContext.From(HttpContext);
        LogEntrada(correlationId, "1RR", dto, http, dto.ObjectId, dto.TeCreatedBy);

        var estadoSalida = AuditEstado.Gate;
        string? payloadSalida = null;
        string? errorSalida = null;
        IActionResult resultado = null!;

        try
        {
            var accion = _matrixGate.Resolve("*", "1RR", dto.Station);
            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Visualización de inventario en tiempo real rechazada: estación {Estacion} deshabilitada por matriz.", dto.Station);
                errorSalida = "Estación deshabilitada por la matriz de mensajes.";
                return resultado = Conflict(new { message = errorSalida });
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Visualización de inventario en tiempo real ignorada por matriz (estación {Estacion}).", dto.Station);
                return resultado = Ok();
            }

            string dataPayload;
            try
            {
                dataPayload = MapeadorTelegramaInventarioTiempoReal.BuildRequest(dto);
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
                _logger.LogWarning(ex, "Timeout enviando solicitud de visualización de inventario en tiempo real a KiSoft.");
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Canal KiSoft no disponible enviando solicitud de visualización de inventario en tiempo real.");
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Conexión con KiSoft perdida enviando solicitud de visualización de inventario en tiempo real.");
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (ExcepcionFormatoTelegrama ex)
            {
                _logger.LogError(ex, "Respuesta de KiSoft malformada enviando solicitud de visualización de inventario en tiempo real.");
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            if (!status.IsOk)
            {
                _logger.LogError("Visualización de inventario en tiempo real rechazada por KiSoft: estado={Estado}.", status.Estado);
                estadoSalida = AuditEstado.Error;
                errorSalida = $"KiSoft rechazó la solicitud: estado={status.Estado}.";
                LogRespuestaKiSoft(correlationId, "1RR", http, response, AuditEstado.Error, dto.ObjectId, dto.TeCreatedBy);
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { recordId = status.RecordId, estado = status.Estado, ok = status.IsOk });
            }

            estadoSalida = AuditEstado.Entregado;
            LogRespuestaKiSoft(correlationId, "1RR", http, response, AuditEstado.Entregado, dto.ObjectId, dto.TeCreatedBy);
            return resultado = Ok(new { recordId = status.RecordId, estado = status.Estado, ok = status.IsOk });
        }
        finally
        {
            var httpStatus = (resultado as IStatusCodeActionResult)?.StatusCode;
            LogSalida(correlationId, "1RR", estadoSalida, payloadSalida, errorSalida, (int)stopwatch.ElapsedMilliseconds, http, httpStatus, dto.ObjectId, dto.TeCreatedBy);
        }
    }

    [HttpPost("stock/1XR")]
    public async Task<IActionResult> PostStock([FromBody] SolicitudConsultaStockDto dto, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        var http = AuditHttpContext.From(HttpContext);
        LogEntrada(correlationId, "1XR", dto, http, dto.ObjectId, dto.TeCreatedBy);

        var estadoSalida = AuditEstado.Gate;
        string? payloadSalida = null;
        string? errorSalida = null;
        IActionResult resultado = null!;

        try
        {
            var accion = _matrixGate.Resolve(dto.Mandante, "1XR", dto.Station);
            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Consulta de stock de {Producto} rechazada: estación {Estacion} deshabilitada por matriz (mandante {Mandante}).",
                    dto.ProductNumber, dto.Station, dto.Mandante);
                errorSalida = "Estación deshabilitada por la matriz de mensajes.";
                return resultado = Conflict(new { message = errorSalida });
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Consulta de stock de {Producto} ignorada por matriz (mandante {Mandante}, estación {Estacion}).",
                    dto.ProductNumber, dto.Mandante, dto.Station);
                return resultado = Ok();
            }

            string dataPayload;
            try
            {
                dataPayload = MapeadorTelegramaConsultaStock.BuildRequest(dto);
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
                _logger.LogWarning(ex, "Timeout enviando consulta de stock de {Producto} a KiSoft.", dto.ProductNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Canal KiSoft no disponible enviando consulta de stock de {Producto}.", dto.ProductNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Conexión con KiSoft perdida enviando consulta de stock de {Producto}.", dto.ProductNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (ExcepcionFormatoTelegrama ex)
            {
                _logger.LogError(ex, "Respuesta de KiSoft malformada enviando consulta de stock de {Producto}.", dto.ProductNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            if (!status.IsOk)
            {
                _logger.LogError("Consulta de stock de {Producto} rechazada por KiSoft: estado={Estado}.", dto.ProductNumber, status.Estado);
                estadoSalida = AuditEstado.Error;
                errorSalida = $"KiSoft rechazó la consulta: estado={status.Estado}.";
                LogRespuestaKiSoft(correlationId, "1XR", http, response, AuditEstado.Error, dto.ObjectId, dto.TeCreatedBy);
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { recordId = status.RecordId, estado = status.Estado, ok = status.IsOk });
            }

            estadoSalida = AuditEstado.Entregado;
            LogRespuestaKiSoft(correlationId, "1XR", http, response, AuditEstado.Entregado, dto.ObjectId, dto.TeCreatedBy);
            return resultado = Ok(new { recordId = status.RecordId, estado = status.Estado, ok = status.IsOk });
        }
        finally
        {
            var httpStatus = (resultado as IStatusCodeActionResult)?.StatusCode;
            LogSalida(correlationId, "1XR", estadoSalida, payloadSalida, errorSalida, (int)stopwatch.ElapsedMilliseconds, http, httpStatus, dto.ObjectId, dto.TeCreatedBy);
        }
    }

    private void LogEntrada<T>(Guid correlationId, string tipoTelegrama, T dto, AuditHttpContext http, string? idObjeto, string? creadoPorSap) =>
        _auditWriter.EnqueueEntrada(new AuditRecord(correlationId, tipoTelegrama, "SAP", "Middleware", AuditEstado.Recibido,
            JsonSerializer.Serialize(dto), Usuario: http.Usuario, Ruta: http.Ruta, IpOrigen: http.IpOrigen, IdObjeto: idObjeto, CreadoPorSap: creadoPorSap));

    private void LogSalida(Guid correlationId, string tipoTelegrama, AuditEstado estado, string? payload, string? errorDetalle, int durationMs, AuditHttpContext http, int? httpStatus, string? idObjeto, string? creadoPorSap) =>
        _auditWriter.EnqueueSalida(new AuditRecord(correlationId, tipoTelegrama, "Middleware", "KiSoft", estado, payload, errorDetalle, durationMs,
            httpStatus, http.Usuario, http.Ruta, http.IpOrigen, idObjeto, creadoPorSap));

    private void LogRespuestaKiSoft(Guid correlationId, string tipoTelegrama, AuditHttpContext http, string rawResponse, AuditEstado estado, string? idObjeto, string? creadoPorSap) =>
        _auditWriter.EnqueueEntrada(new AuditRecord(correlationId, tipoTelegrama, "KiSoft", "Middleware", estado,
            TramaHex.Encode(rawResponse), Usuario: http.Usuario, Ruta: http.Ruta, IpOrigen: http.IpOrigen, IdObjeto: idObjeto, CreadoPorSap: creadoPorSap));
}
