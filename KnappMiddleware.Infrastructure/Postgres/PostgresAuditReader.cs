using Dapper;
using KnappMiddleware.Domain.Auditing;
using Npgsql;

namespace KnappMiddleware.Infrastructure.Postgres;

public sealed class PostgresAuditReader : IAuditReader
{
    private const int MaxTake = 500;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresAuditReader(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<AuditQueryResult>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        var table = query.Direccion == AuditDireccion.Entrada ? "buzon_entrada" : "buzon_salida";
        var take = Math.Clamp(query.Take, 1, MaxTake);

        var sql = $"""
            SELECT id AS "Id", correlation_id AS "CorrelationId", tipo_telegrama AS "TipoTelegrama",
                   source AS "Source", target AS "Target", estado AS "Estado", payload AS "Payload",
                   error_detalle AS "ErrorDetalle", duration_ms AS "DurationMs", created_at_utc AS "CreatedAtUtc"
            FROM {table}
            WHERE (@CorrelationId::uuid IS NULL OR correlation_id = @CorrelationId)
            ORDER BY created_at_utc DESC
            LIMIT @Take
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AuditRow>(
            new CommandDefinition(sql, new { query.CorrelationId, Take = take }, cancellationToken: cancellationToken));

        return rows
            .Select(r => new AuditQueryResult(r.Id, r.CorrelationId, r.TipoTelegrama, r.Source, r.Target,
                Enum.Parse<AuditEstado>(r.Estado, ignoreCase: true), r.Payload, r.ErrorDetalle, r.DurationMs, r.CreatedAtUtc))
            .ToList();
    }

    private sealed record AuditRow(
        long Id,
        Guid CorrelationId,
        string TipoTelegrama,
        string Source,
        string Target,
        string Estado,
        string? Payload,
        string? ErrorDetalle,
        int? DurationMs,
        DateTimeOffset CreatedAtUtc);
}
