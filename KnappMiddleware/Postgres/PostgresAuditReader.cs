using Dapper;
using KnappMiddleware.Auditing;
using Npgsql;

namespace KnappMiddleware.Postgres;

public sealed class PostgresAuditReader : IAuditReader
{
    private const int MaxTake = 500;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresAuditReader(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    // Todo el acceso a datos pasa por funciones/procedimientos (fn_buzon_entrada_consultar,
    // fn_buzon_salida_consultar) — nada de nombres de tabla/columna embebidos acá.
    private const string SelectEntradaSql = "SELECT * FROM fn_buzon_entrada_consultar(@CorrelationId, @Take)";
    private const string SelectSalidaSql = "SELECT * FROM fn_buzon_salida_consultar(@CorrelationId, @Take)";

    public async Task<IReadOnlyList<AuditQueryResult>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        var sql = query.Direccion == AuditDireccion.Entrada ? SelectEntradaSql : SelectSalidaSql;
        var take = Math.Clamp(query.Take, 1, MaxTake);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AuditRow>(
            new CommandDefinition(sql, new { query.CorrelationId, Take = take }, cancellationToken: cancellationToken));

        return rows
            .Select(r => new AuditQueryResult(r.Id, r.CorrelationId, r.TipoTelegrama, r.Source, r.Target,
                Enum.Parse<AuditEstado>(r.Estado, ignoreCase: true), r.Payload, r.ErrorDetalle, r.DurationMs, r.CreadoEn))
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
        DateTime CreadoEn);
}
