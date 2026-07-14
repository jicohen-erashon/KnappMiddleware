using Dapper;
using KnappMiddleware.Domain.Matrix;
using Npgsql;

namespace KnappMiddleware.Infrastructure.Postgres;

public sealed class PostgresMatrixRepository : IMatrixRepository
{
    private const string SelectAllSql = """
        SELECT emisor AS "Emisor", tipo_telegrama AS "TipoTelegrama", estacion AS "Estacion", accion AS "Accion"
        FROM matriz
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresMatrixRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<MatrixEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatrixEntryRow>(
            new CommandDefinition(SelectAllSql, cancellationToken: cancellationToken));

        return rows
            .Select(r => new MatrixEntry(r.Emisor, r.TipoTelegrama, r.Estacion, Enum.Parse<MatrixAction>(r.Accion, ignoreCase: true)))
            .ToList();
    }

    private sealed record MatrixEntryRow(string Emisor, string TipoTelegrama, string Estacion, string Accion);
}
