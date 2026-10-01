using Dapper;
using KnappMiddleware.Configuration;
using Npgsql;

namespace KnappMiddleware.Postgres;

public sealed class PostgresConfigRepository : IConfigRepository
{
    // Todo el acceso a datos pasa por funciones/procedimientos (fn_configuracion_listar,
    // sp_upsert_configuracion) — nada de nombres de tabla/columna embebidos acá.
    private const string SelectAllSql = "SELECT * FROM fn_configuracion_listar()";

    private const string UpsertSql = "CALL sp_guardar_configuracion(@Clave, @Valor)";

    private const string DeleteSql = "CALL sp_eliminar_configuracion(@Clave)";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresConfigRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<ConfigEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ConfigEntry>(
            new CommandDefinition(SelectAllSql, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task SetValueAsync(string clave, string valor, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(UpsertSql, new { Clave = clave, Valor = valor }, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteValueAsync(string clave, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(DeleteSql, new { Clave = clave }, cancellationToken: cancellationToken));
        // El SP devuelve void; la capa controller hace GET previo para distinguir 404 vs 204.
        return true;
    }
}
