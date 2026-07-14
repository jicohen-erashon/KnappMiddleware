using Dapper;
using KnappMiddleware.Domain.Auth;
using Npgsql;

namespace KnappMiddleware.Infrastructure.Postgres;

public sealed class PostgresUserRepository : IUserRepository
{
    private const string SelectAllSql = """
        SELECT username AS "Username", password_hash AS "PasswordHash", role AS "Role", enabled AS "Enabled"
        FROM usuarios
        """;

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
