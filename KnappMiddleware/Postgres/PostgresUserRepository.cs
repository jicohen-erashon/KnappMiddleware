using Dapper;
using KnappMiddleware.Auth;
using Npgsql;

namespace KnappMiddleware.Postgres;

public sealed class PostgresUserRepository : IUserRepository
{
    // Todo el acceso a datos pasa por funciones/procedimientos (fn_usuarios_listar y las agregadas en
    // 0017_usuarios_gestion.sql para el CRUD del panel admin) — nada de nombres de tabla/columna
    // embebidos acá.
    private const string SelectAllSql = "SELECT * FROM fn_usuarios_listar()";
    private const string SelectAllForAdminSql = "SELECT * FROM fn_usuarios_administrar_listar()";
    private const string CreateSql = "CALL sp_crear_usuario(@NombreUsuario, @Contrasena, @Rol)";
    private const string UpdateSql = "CALL sp_actualizar_usuario(@NombreUsuario, @Rol, @Habilitado)";
    private const string ResetPasswordSql = "CALL sp_restablecer_contrasena_usuario(@NombreUsuario, @ContrasenaNueva)";
    private const string DeleteSql = "SELECT fn_eliminar_usuario(@NombreUsuario)";

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

    public async Task<IReadOnlyList<UserAccountSummary>> GetAllForAdminAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<UserAccountSummaryRow>(
            new CommandDefinition(SelectAllForAdminSql, cancellationToken: cancellationToken));

        return rows
            .Select(r => new UserAccountSummary(
                r.NombreUsuario,
                Enum.Parse<UserRole>(r.Rol, ignoreCase: true),
                r.Habilitado,
                r.CreadoEn,
                r.ActualizadoEn))
            .ToList();
    }

    public async Task CreateAsync(string nombreUsuario, string contrasena, string rol, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            CreateSql,
            new { NombreUsuario = nombreUsuario, Contrasena = contrasena, Rol = rol },
            cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(string nombreUsuario, string rol, bool habilitado, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            UpdateSql,
            new { NombreUsuario = nombreUsuario, Rol = rol, Habilitado = habilitado },
            cancellationToken: cancellationToken));
    }

    public async Task ResetPasswordAsync(string nombreUsuario, string contrasenaNueva, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            ResetPasswordSql,
            new { NombreUsuario = nombreUsuario, ContrasenaNueva = contrasenaNueva },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(string nombreUsuario, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            DeleteSql,
            new { NombreUsuario = nombreUsuario },
            cancellationToken: cancellationToken));
    }

    private sealed record UserAccountRow(string Username, string PasswordHash, string Role, bool Enabled);

    private sealed record UserAccountSummaryRow(string NombreUsuario, string Rol, bool Habilitado, DateTime CreadoEn, DateTime ActualizadoEn);
}
