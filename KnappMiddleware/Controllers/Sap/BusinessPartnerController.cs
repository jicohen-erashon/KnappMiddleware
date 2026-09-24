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
[Route("api/v1/sap/businesspartner")]
[Authorize(Policy = AuthorizationPolicies.SapOrSuperUsuario)]
[Tags("BusinessPartner")]
public sealed class BusinessPartnerController : ControllerBase
{
    private readonly ILogger<BusinessPartnerController> _logger;
    private readonly ClsMatrixGate _matrixGate;
    private readonly ICanalPedidoKiSoft _orderChannel;
    private readonly ClsAuditWriter _auditWriter;

    public BusinessPartnerController(ILogger<BusinessPartnerController> logger, ClsMatrixGate matrixGate, ICanalPedidoKiSoft orderChannel, ClsAuditWriter auditWriter)
    {
        _logger = logger;
        _matrixGate = matrixGate;
        _orderChannel = orderChannel;
        _auditWriter = auditWriter;
    }

    [HttpPost("15N")]
    public async Task<IActionResult> Post([FromBody] SolicitudSocioDto dto, CancellationToken cancellationToken)
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
            // Los datos maestros de socio no están ligados a una estación concreta; se resuelve la
            // matriz con estación comodín "*" (gana la regla más específica configurada, o el fail-safe).
            var accion = _matrixGate.Resolve(dto.Mandante, "15N", "*");
            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Socio {Socio} rechazado por matriz (mandante {Mandante}).", dto.PartnerNumber, dto.Mandante);
                errorSalida = "Socio deshabilitado por la matriz de mensajes.";
                return resultado = Conflict(new { message = errorSalida });
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Socio {Socio} ignorado por matriz (mandante {Mandante}).", dto.PartnerNumber, dto.Mandante);
                return resultado = Ok();
            }

            string dataPayload;
            try
            {
                dataPayload = MapeadorTelegramaSocio.BuildNew(dto);
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
                    _orderChannel, MapeadorTelegramaSocio.OpenUpsert, dataPayload, MapeadorTelegramaSocio.Close, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Timeout enviando socio {Socio} a KiSoft.", dto.PartnerNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Canal KiSoft no disponible enviando socio {Socio}.", dto.PartnerNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Conexión con KiSoft perdida enviando socio {Socio}.", dto.PartnerNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (ExcepcionFormatoTelegrama ex)
            {
                _logger.LogError(ex, "Respuesta de KiSoft malformada enviando socio {Socio}.", dto.PartnerNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            if (!transmission.Open.IsOk || !transmission.Data.IsOk || !transmission.Close.IsOk)
            {
                _logger.LogError("Bracket de datos maestros de socio rechazado: abrir={AbrirEstado}, registro={DatoEstado}, cerrar={CerrarEstado}.",
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

    private void LogEntrada(Guid correlationId, SolicitudSocioDto dto, AuditHttpContext http, string? idObjeto, string? creadoPorSap) =>
        _auditWriter.EnqueueEntrada(new AuditRecord(correlationId, "15N", "SAP", "Middleware", AuditEstado.Recibido,
            JsonSerializer.Serialize(dto), Usuario: http.Usuario, Ruta: http.Ruta, IpOrigen: http.IpOrigen, IdObjeto: idObjeto, CreadoPorSap: creadoPorSap));

    private void LogSalida(Guid correlationId, AuditEstado estado, string? payload, string? errorDetalle, int durationMs, AuditHttpContext http, int? httpStatus, string? idObjeto, string? creadoPorSap) =>
        _auditWriter.EnqueueSalida(new AuditRecord(correlationId, "15N", "Middleware", "KiSoft", estado, payload, errorDetalle, durationMs,
            httpStatus, http.Usuario, http.Ruta, http.IpOrigen, idObjeto, creadoPorSap));

    private void LogRespuestaKiSoft(Guid correlationId, AuditHttpContext http, TransmisionDatosMaestros transmission, AuditEstado estado, string? idObjeto, string? creadoPorSap)
    {
        var framesHex = string.Join('|',
            TramaHex.Encode(transmission.Open.RecordId + transmission.Open.Estado),
            TramaHex.Encode(transmission.Data.RecordId + transmission.Data.Estado),
            TramaHex.Encode(transmission.Close.RecordId + transmission.Close.Estado));
        _auditWriter.EnqueueEntrada(new AuditRecord(correlationId, "15N", "KiSoft", "Middleware", estado,
            framesHex, Usuario: http.Usuario, Ruta: http.Ruta, IpOrigen: http.IpOrigen, IdObjeto: idObjeto, CreadoPorSap: creadoPorSap));
    }
}
