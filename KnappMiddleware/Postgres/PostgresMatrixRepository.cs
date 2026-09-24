using Dapper;
using KnappMiddleware.Matrix;
using Npgsql;

namespace KnappMiddleware.Postgres;

public sealed class PostgresMatrixRepository : IMatrixRepository
{
    // Todo el acceso a datos pasa por funciones/procedimientos (fn_matriz_listar) — nada de nombres de
    // tabla/columna embebidos acá.
    private const string SelectAllSql = "SELECT * FROM fn_matriz_listar()";

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
