using Dapper;
using KnappMiddleware.Domain.Configuration;
using Npgsql;

namespace KnappMiddleware.Infrastructure.Postgres;

public sealed class PostgresConfigRepository : IConfigRepository
{
    private const string SelectAllSql = """
        SELECT clave AS "Clave", valor AS "Valor"
        FROM configuracion
        """;

    private const string UpsertSql = """
        INSERT INTO configuracion (clave, valor, updated_at_utc)
        VALUES (@Clave, @Valor, now())
        ON CONFLICT (clave) DO UPDATE SET valor = EXCLUDED.valor, updated_at_utc = now()
        """;

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
}
