using Dapper;
using KnappMiddleware.Domain.Auditing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KnappMiddleware.Infrastructure.Auditing;

/// <summary>Drena la cola de auditoría y escribe cada registro vía stored procedure. Errores por ítem se
/// registran y se descartan; nunca se reintenta ni se detiene el drenaje del resto de la cola.</summary>
public sealed class AuditWriterBackgroundService : BackgroundService
{
    private const string InsertEntradaSql =
        "CALL sp_insert_buzon_entrada(@CorrelationId, @TipoTelegrama, @Source, @Target, @Estado, @Payload, @ErrorDetalle, @DurationMs)";

    private const string InsertSalidaSql =
        "CALL sp_insert_buzon_salida(@CorrelationId, @TipoTelegrama, @Source, @Target, @Estado, @Payload, @ErrorDetalle, @DurationMs)";

    private readonly AuditWriter _writer;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<AuditWriterBackgroundService> _logger;

    public AuditWriterBackgroundService(AuditWriter writer, NpgsqlDataSource dataSource, ILogger<AuditWriterBackgroundService> logger)
    {
        _writer = writer;
        _dataSource = dataSource;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _writer.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await WriteAsync(item, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo escribir el registro de auditoría {CorrelationId} ({Direccion}).",
                    item.Record.CorrelationId, item.Direccion);
            }
        }
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
            item.Record.DurationMs
        }, cancellationToken: cancellationToken));
    }
}
