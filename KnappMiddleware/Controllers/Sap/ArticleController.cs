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
[Route("api/v1/sap/article")]
[Authorize(Policy = AuthorizationPolicies.SapOrSuperUsuario)]
[Tags("Article")]
public sealed class ArticleController : ControllerBase
{
    private readonly ILogger<ArticleController> _logger;
    private readonly ClsMatrixGate _matrixGate;
    private readonly ICanalPedidoKiSoft _orderChannel;
    private readonly ClsAuditWriter _auditWriter;

    public ArticleController(ILogger<ArticleController> logger, ClsMatrixGate matrixGate, ICanalPedidoKiSoft orderChannel, ClsAuditWriter auditWriter)
    {
        _logger = logger;
        _matrixGate = matrixGate;
        _orderChannel = orderChannel;
        _auditWriter = auditWriter;
    }

    [HttpPost("14N")]
    public async Task<IActionResult> Post([FromBody] SolicitudArticuloDto dto, CancellationToken cancellationToken)
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
            var accion = _matrixGate.Resolve(dto.Mandante, "14N", dto.Station);
            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Artículo {Producto} rechazado: estación {Estacion} deshabilitada por matriz (mandante {Mandante}).",
                    dto.ProductNumber, dto.Station, dto.Mandante);
                errorSalida = "Estación deshabilitada por la matriz de mensajes.";
                return resultado = Conflict(new { message = errorSalida });
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Artículo {Producto} ignorado por matriz (mandante {Mandante}, estación {Estacion}).",
                    dto.ProductNumber, dto.Mandante, dto.Station);
                return resultado = Ok();
            }

            string dataPayload;
            try
            {
                dataPayload = MapeadorTelegramaArticulo.BuildNew(dto);
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
                    _orderChannel, MapeadorTelegramaArticulo.OpenUpsert, dataPayload, MapeadorTelegramaArticulo.Close, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Timeout enviando artículo {Producto} a KiSoft.", dto.ProductNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Canal KiSoft no disponible enviando artículo {Producto}.", dto.ProductNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Conexión con KiSoft perdida enviando artículo {Producto}.", dto.ProductNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (ExcepcionFormatoTelegrama ex)
            {
                _logger.LogError(ex, "Respuesta de KiSoft malformada enviando artículo {Producto}.", dto.ProductNumber);
                estadoSalida = AuditEstado.Error;
                errorSalida = ex.Message;
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }

            if (!transmission.Open.IsOk || !transmission.Data.IsOk || !transmission.Close.IsOk)
            {
                _logger.LogError("Bracket de datos maestros de artículo rechazado: abrir={AbrirEstado}, registro={DatoEstado}, cerrar={CerrarEstado}.",
                    transmission.Open.Estado, transmission.Data.Estado, transmission.Close.Estado);
                estadoSalida = AuditEstado.Error;
                errorSalida = $"KiSoft rechazó el registro: abrir={transmission.Open.Estado}, registro={transmission.Data.Estado}, cerrar={transmission.Close.Estado}.";
                LogRespuestaKiSoft(correlationId, http, transmission, AuditEstado.Error, dto.ObjectId, dto.TeCreatedBy);
                return resultado = StatusCode(StatusCodes.Status502BadGateway, new
                {
                    open = transmission.Open,
                    data = transmission.Data,
                    close = transmission.Close
                });
            }

            estadoSalida = AuditEstado.Entregado;
            LogRespuestaKiSoft(correlationId, http, transmission, AuditEstado.Entregado, dto.ObjectId, dto.TeCreatedBy);
            return resultado = Ok(new
            {
                recordId = transmission.Data.RecordId,
                estado = transmission.Data.Estado,
                ok = transmission.Data.IsOk
            });
        }
        finally
        {
            var httpStatus = (resultado as IStatusCodeActionResult)?.StatusCode;
            LogSalida(correlationId, estadoSalida, payloadSalida, errorSalida, (int)stopwatch.ElapsedMilliseconds, http, httpStatus, dto.ObjectId, dto.TeCreatedBy);
        }
    }

    private void LogEntrada(Guid correlationId, SolicitudArticuloDto dto, AuditHttpContext http, string? idObjeto, string? creadoPorSap) =>
        _auditWriter.EnqueueEntrada(new AuditRecord(correlationId, "14N", "SAP", "Middleware", AuditEstado.Recibido,
            JsonSerializer.Serialize(dto), Usuario: http.Usuario, Ruta: http.Ruta, IpOrigen: http.IpOrigen, IdObjeto: idObjeto, CreadoPorSap: creadoPorSap));

    private void LogSalida(Guid correlationId, AuditEstado estado, string? payload, string? errorDetalle, int durationMs, AuditHttpContext http, int? httpStatus, string? idObjeto, string? creadoPorSap) =>
        _auditWriter.EnqueueSalida(new AuditRecord(correlationId, "14N", "Middleware", "KiSoft", estado, payload, errorDetalle, durationMs,
            httpStatus, http.Usuario, http.Ruta, http.IpOrigen, idObjeto, creadoPorSap));

    /// <summary>Registra en buzon_entrada (KiSoft -> Middleware) la trama real de red recibida en cada
    /// parte del bracket (abrir/registro/cerrar), reconstruida a partir de MensajeEstadoKiSoft
    /// (recordId+estado son su forma cruda completa, sin pérdida) y codificada en hex.</summary>
    private void LogRespuestaKiSoft(Guid correlationId, AuditHttpContext http, TransmisionDatosMaestros transmission, AuditEstado estado, string? idObjeto, string? creadoPorSap)
    {
        var framesHex = string.Join('|',
            TramaHex.Encode(transmission.Open.RecordId + transmission.Open.Estado),
            TramaHex.Encode(transmission.Data.RecordId + transmission.Data.Estado),
            TramaHex.Encode(transmission.Close.RecordId + transmission.Close.Estado));
        _auditWriter.EnqueueEntrada(new AuditRecord(correlationId, "14N", "KiSoft", "Middleware", estado,
            framesHex, Usuario: http.Usuario, Ruta: http.Ruta, IpOrigen: http.IpOrigen, IdObjeto: idObjeto, CreadoPorSap: creadoPorSap));
    }
}
