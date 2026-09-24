using Dapper;
using KnappMiddleware.Auth;
using Npgsql;

namespace KnappMiddleware.Postgres;

public sealed class PostgresUserRepository : IUserRepository
{
    // Todo el acceso a datos pasa por funciones/procedimientos (fn_usuarios_listar) — nada de nombres de
    // tabla/columna embebidos acá.
    private const string SelectAllSql = "SELECT * FROM fn_usuarios_listar()";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresUserRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<UserAccountRow>(
            new CommandDefinition(SelectAllSql, cancellationToken: cancellationToken));

        return rows
            .Select(r => new UserAccount(r.Username, r.PasswordHash, Enum.Parse<UserRole>(r.Role, ignoreCase: true), r.Enabled))
            .ToList();
    }

    private sealed record UserAccountRow(string Username, string PasswordHash, string Role, bool Enabled);
}
