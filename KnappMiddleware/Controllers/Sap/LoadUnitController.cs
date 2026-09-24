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
[Route("api/v1/sap/loadunit")]
[Authorize(Policy = AuthorizationPolicies.SapOrSuperUsuario)]
[Tags("LoadUnit")]
public sealed class LoadUnitController : ControllerBase
{
    private readonly ILogger<LoadUnitController> _logger;
    private readonly ClsMatrixGate _matrixGate;
    private readonly ICanalPedidoKiSoft _orderChannel;
    private readonly ClsAuditWriter _auditWriter;

    public LoadUnitController(ILogger<LoadUnitController> logger, ClsMatrixGate matrixGate, ICanalPedidoKiSoft orderChannel, ClsAuditWriter auditWriter)
    {
        _logger = logger;
        _matrixGate = matrixGate;
        _orderChannel = orderChannel;
        _auditWriter = auditWriter;
    }

    [HttpPost("modify/1UU")]
    public async Task<IActionResult> PostModify([FromBody] SolicitudUnidadCargaModificarDto dto, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        var http = AuditHttpContext.From(HttpContext);
        LogEntrada(correlationId, "1UU", dto, http, dto.ObjectId, dto.TeCreatedBy);

        var estadoSalida = AuditEstado.Gate;
        string? payloadSalida = null;
        string? errorSalida = null;
        IActionResult resultado = null!;

        try
        {
            if (dto.Items.Count == 0)
            {
                errorSalida = "La unidad de carga debe tener al menos una línea de stock.";
                return resultado = BadRequest(new { message = errorSalida });
            }

            var accion = _matrixGate.Resolve(dto.Mandante, "1UU", dto.Station);
            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Modificación de unidad de carga {UDC} rechazada: estación {Estacion} deshabilitada por matriz.",
                    dto.LoadUnitCode, dto.Station);
                errorSalida = "Estación deshabilitada por la matriz de mensajes.";
                return resultado = Conflict(new { message = errorSalida });
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Modificación de unidad de carga {UDC} ignorada por matriz.", dto.LoadUnitCode);
                return resultado = Ok();
            }

            string dataPayload;
            try
            {
                dataPayload = MapeadorTelegramaUnidadCargaModificar.BuildRequest(dto);
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
                _logger.LogWarning(ex, "Timeout modificando unidad de carga {UDC} en KiSoft.", dto.LoadUnitCode);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Canal KiSoft no disponible modificando unidad de carga {UDC}.", dto.LoadUnitCode);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Conexión con KiSoft perdida modificando unidad de carga {UDC}.", dto.LoadUnitCode);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (ExcepcionFormatoTelegrama ex)
            {
                _logger.LogError(ex, "Respuesta de KiSoft malformada modificando unidad de carga {UDC}.", dto.LoadUnitCode);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            if (!status.IsOk)
            {
                _logger.LogError("Modificación de unidad de carga {UDC} rechazada por KiSoft: estado={Estado}.", dto.LoadUnitCode, status.Estado);
                estadoSalida = AuditEstado.Error;
                errorSalida = $"KiSoft rechazó la modificación: estado={status.Estado}.";
                LogRespuestaKiSoft(correlationId, "1UU", http, response, AuditEstado.Error, dto.ObjectId, dto.TeCreatedBy);
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { recordId = status.RecordId, estado = status.Estado, ok = status.IsOk });
            }

            estadoSalida = AuditEstado.Entregado;
            LogRespuestaKiSoft(correlationId, "1UU", http, response, AuditEstado.Entregado, dto.ObjectId, dto.TeCreatedBy);
            return resultado = Ok(new { recordId = status.RecordId, estado = status.Estado, ok = status.IsOk });
        }
        finally
        {
            var httpStatus = (resultado as IStatusCodeActionResult)?.StatusCode;
            LogSalida(correlationId, "1UU", estadoSalida, payloadSalida, errorSalida, (int)stopwatch.ElapsedMilliseconds, http, httpStatus, dto.ObjectId, dto.TeCreatedBy);
        }
    }

    [HttpPost("available/1UN")]
    public async Task<IActionResult> PostAvailable([FromBody] SolicitudUnidadCargaDisponibleDto dto, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        var http = AuditHttpContext.From(HttpContext);
        LogEntrada(correlationId, "1UN", dto, http, dto.ObjectId, dto.TeCreatedBy);

        var estadoSalida = AuditEstado.Gate;
        string? payloadSalida = null;
        string? errorSalida = null;
        IActionResult resultado = null!;

        try
        {
            if (dto.Items.Count == 0)
            {
                errorSalida = "La unidad de carga debe tener al menos una línea de stock.";
                return resultado = BadRequest(new { message = errorSalida });
            }

            var accion = _matrixGate.Resolve(dto.Mandante, "1UN", dto.Station);
            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Unidad de carga disponible {UDC} rechazada: estación {Estacion} deshabilitada por matriz.",
                    dto.LoadUnitCode, dto.Station);
                errorSalida = "Estación deshabilitada por la matriz de mensajes.";
                return resultado = Conflict(new { message = errorSalida });
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Unidad de carga disponible {UDC} ignorada por matriz.", dto.LoadUnitCode);
                return resultado = Ok();
            }

            string dataPayload;
            try
            {
                dataPayload = MapeadorTelegramaUnidadCargaDisponible.BuildRequest(dto);
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
                _logger.LogWarning(ex, "Timeout registrando unidad de carga disponible {UDC} en KiSoft.", dto.LoadUnitCode);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Canal KiSoft no disponible registrando unidad de carga disponible {UDC}.", dto.LoadUnitCode);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Conexión con KiSoft perdida registrando unidad de carga disponible {UDC}.", dto.LoadUnitCode);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (ExcepcionFormatoTelegrama ex)
            {
                _logger.LogError(ex, "Respuesta de KiSoft malformada registrando unidad de carga disponible {UDC}.", dto.LoadUnitCode);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            if (!status.IsOk)
            {
                _logger.LogError("Unidad de carga disponible {UDC} rechazada por KiSoft: estado={Estado}.", dto.LoadUnitCode, status.Estado);
                estadoSalida = AuditEstado.Error;
                errorSalida = $"KiSoft rechazó el registro: estado={status.Estado}.";
                LogRespuestaKiSoft(correlationId, "1UN", http, response, AuditEstado.Error, dto.ObjectId, dto.TeCreatedBy);
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { recordId = status.RecordId, estado = status.Estado, ok = status.IsOk });
            }

            estadoSalida = AuditEstado.Entregado;
            LogRespuestaKiSoft(correlationId, "1UN", http, response, AuditEstado.Entregado, dto.ObjectId, dto.TeCreatedBy);
            return resultado = Ok(new { recordId = status.RecordId, estado = status.Estado, ok = status.IsOk });
        }
        finally
        {
            var httpStatus = (resultado as IStatusCodeActionResult)?.StatusCode;
            LogSalida(correlationId, "1UN", estadoSalida, payloadSalida, errorSalida, (int)stopwatch.ElapsedMilliseconds, http, httpStatus, dto.ObjectId, dto.TeCreatedBy);
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
