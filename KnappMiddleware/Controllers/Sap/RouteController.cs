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
[Route("api/v1/sap/route")]
[Authorize(Policy = AuthorizationPolicies.SapOrSuperUsuario)]
[Tags("Route")]
public sealed class RouteController : ControllerBase
{
    private readonly ILogger<RouteController> _logger;
    private readonly ClsMatrixGate _matrixGate;
    private readonly ICanalPedidoKiSoft _orderChannel;
    private readonly ClsAuditWriter _auditWriter;

    public RouteController(ILogger<RouteController> logger, ClsMatrixGate matrixGate, ICanalPedidoKiSoft orderChannel, ClsAuditWriter auditWriter)
    {
        _logger = logger;
        _matrixGate = matrixGate;
        _orderChannel = orderChannel;
        _auditWriter = auditWriter;
    }

    [HttpPost("16N")]
    public async Task<IActionResult> Post([FromBody] SolicitudRutaDto dto, CancellationToken cancellationToken)
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
            var accion = _matrixGate.Resolve(dto.Mandante, "16N", "*");
            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Ruta {Ruta} rechazada por matriz (mandante {Mandante}).", dto.RouteNumber, dto.Mandante);
                errorSalida = "Ruta deshabilitada por la matriz de mensajes.";
                return resultado = Conflict(new { message = errorSalida });
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Ruta {Ruta} ignorada por matriz (mandante {Mandante}).", dto.RouteNumber, dto.Mandante);
                return resultado = Ok();
            }

            string dataPayload;
            try
            {
                dataPayload = MapeadorTelegramaRuta.BuildNew(dto);
            }
            catch (ExcepcionFormatoTelegrama ex)
            {
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = UnprocessableEntity(new { message = ex.Message });
            }

            payloadSalida = TramaHex.Encode(dataPayload);

            TransmisionDatosMaestros transmission;
            try
            {
                transmission = await TransmisionDatosMaestros.SendAsync(
                    _orderChannel, MapeadorTelegramaRuta.OpenUpsert, dataPayload, MapeadorTelegramaRuta.Close, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Timeout enviando ruta {Ruta} a KiSoft.", dto.RouteNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Canal KiSoft no disponible enviando ruta {Ruta}.", dto.RouteNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Conexión con KiSoft perdida enviando ruta {Ruta}.", dto.RouteNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (ExcepcionFormatoTelegrama ex)
            {
                _logger.LogError(ex, "Respuesta de KiSoft malformada enviando ruta {Ruta}.", dto.RouteNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            if (!transmission.Open.IsOk || !transmission.Data.IsOk || !transmission.Close.IsOk)
            {
                _logger.LogError("Bracket de datos maestros de ruta rechazado: abrir={AbrirEstado}, registro={DatoEstado}, cerrar={CerrarEstado}.",
                    transmission.Open.Estado, transmission.Data.Estado, transmission.Close.Estado);
                estadoSalida = AuditEstado.Error;
                errorSalida = $"KiSoft rechazó el registro: abrir={transmission.Open.Estado}, registro={transmission.Data.Estado}, cerrar={transmission.Close.Estado}.";
                LogRespuestaKiSoft(correlationId, http, transmission, AuditEstado.Error, dto.ObjectId, dto.TeCreatedBy);
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { open = transmission.Open, data = transmission.Data, close = transmission.Close });
            }

            estadoSalida = AuditEstado.Entregado;
            LogRespuestaKiSoft(correlationId, http, transmission, AuditEstado.Entregado, dto.ObjectId, dto.TeCreatedBy);
            return resultado = Ok(new { recordId = transmission.Data.RecordId, estado = transmission.Data.Estado, ok = transmission.Data.IsOk });
        }
        finally
        {
            var httpStatus = (resultado as IStatusCodeActionResult)?.StatusCode;
            LogSalida(correlationId, estadoSalida, payloadSalida, errorSalida, (int)stopwatch.ElapsedMilliseconds, http, httpStatus, dto.ObjectId, dto.TeCreatedBy);
        }
    }

    private void LogEntrada(Guid correlationId, SolicitudRutaDto dto, AuditHttpContext http, string? idObjeto, string? creadoPorSap) =>
        _auditWriter.EnqueueEntrada(new AuditRecord(correlationId, "16N", "SAP", "Middleware", AuditEstado.Recibido,
            JsonSerializer.Serialize(dto), Usuario: http.Usuario, Ruta: http.Ruta, IpOrigen: http.IpOrigen, IdObjeto: idObjeto, CreadoPorSap: creadoPorSap));

    private void LogSalida(Guid correlationId, AuditEstado estado, string? payload, string? errorDetalle, int durationMs, AuditHttpContext http, int? httpStatus, string? idObjeto, string? creadoPorSap) =>
        _auditWriter.EnqueueSalida(new AuditRecord(correlationId, "16N", "Middleware", "KiSoft", estado, payload, errorDetalle, durationMs,
            httpStatus, http.Usuario, http.Ruta, http.IpOrigen, idObjeto, creadoPorSap));

    private void LogRespuestaKiSoft(Guid correlationId, AuditHttpContext http, TransmisionDatosMaestros transmission, AuditEstado estado, string? idObjeto, string? creadoPorSap)
    {
        var framesHex = string.Join('|',
            TramaHex.Encode(transmission.Open.RecordId + transmission.Open.Estado),
            TramaHex.Encode(transmission.Data.RecordId + transmission.Data.Estado),
            TramaHex.Encode(transmission.Close.RecordId + transmission.Close.Estado));
        _auditWriter.EnqueueEntrada(new AuditRecord(correlationId, "16N", "KiSoft", "Middleware", estado,
            framesHex, Usuario: http.Usuario, Ruta: http.Ruta, IpOrigen: http.IpOrigen, IdObjeto: idObjeto, CreadoPorSap: creadoPorSap));
    }
}
