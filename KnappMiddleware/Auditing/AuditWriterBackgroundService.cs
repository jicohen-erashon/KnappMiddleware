using Dapper;
using KnappMiddleware.Auditing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KnappMiddleware.Auditing;

/// <summary>Drena la cola de auditoría y escribe cada registro vía stored procedure. Errores por ítem se
/// registran y se descartan; nunca se reintenta ni se detiene el drenaje del resto de la cola.</summary>
public sealed class AuditWriterBackgroundService : BackgroundService
{
    private const string InsertEntradaSql =
        "CALL sp_insertar_buzon_entrada(@CorrelationId, @TipoTelegrama, @Source, @Target, @Estado, @Payload, @ErrorDetalle, @DurationMs, @HttpStatus, @Usuario, @Ruta, @IpOrigen, @IdObjeto, @CreadoPorSap)";

    private const string InsertSalidaSql =
        "CALL sp_insertar_buzon_salida(@CorrelationId, @TipoTelegrama, @Source, @Target, @Estado, @Payload, @ErrorDetalle, @DurationMs, @HttpStatus, @Usuario, @Ruta, @IpOrigen, @IdObjeto, @CreadoPorSap)";

    private static readonly int[] LogThresholds = { 1, 5, 30, 60, 300, 900, 3600 };

    private readonly ClsAuditWriter _writer;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<AuditWriterBackgroundService> _logger;

    public AuditWriterBackgroundService(ClsAuditWriter writer, NpgsqlDataSource dataSource, ILogger<AuditWriterBackgroundService> logger)
    {
        _writer = writer;
        _dataSource = dataSource;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int consecutiveFailures = 0;
        await foreach (var item in _writer.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await WriteAsync(item, stoppingToken);
                consecutiveFailures = 0;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                if (Array.IndexOf(LogThresholds, consecutiveFailures) >= 0)
                {
                    _logger.LogWarning("Fallo #{Count} consecutivo escribiendo auditoria {CorrelationId} ({Direccion}): {Reason}. Se descarta el item.",
                        consecutiveFailures, item.Record.CorrelationId, item.Direccion, ShortReason(ex));
                }
            }
        }
    }

    private static string ShortReason(Exception ex) => ex switch
    {
        NpgsqlException npg => $"Npgsql ({npg.SqlState ?? "?"}): {FirstLine(npg.Message)}",
        TimeoutException => "Timeout",
        IOException io => $"IO: {FirstLine(io.Message)}",
        _ => $"{ex.GetType().Name}: {FirstLine(ex.Message)}"
    };

    private static string FirstLine(string s)
    {
        var i = s.IndexOfAny(new[] { '\r', '\n' });
        return i < 0 ? s : s[..i];
    }

    private async Task WriteAsync(AuditWorkItem item, CancellationToken cancellationToken)
    {
        var sql = item.Direccion == AuditDireccion.Entrada ? InsertEntradaSql : InsertSalidaSql;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            item.Record.CorrelationId,
            item.Record.TipoTelegrama,
            item.Record.Source,
            item.Record.Target,
            Estado = item.Record.Estado.ToString(),
            item.Record.Payload,
            item.Record.ErrorDetalle,
            item.Record.DurationMs,
            item.Record.HttpStatus,
            item.Record.Usuario,
            item.Record.Ruta,
            item.Record.IpOrigen,
            item.Record.IdObjeto,
            item.Record.CreadoPorSap
        }, cancellationToken: cancellationToken));
    }
}
