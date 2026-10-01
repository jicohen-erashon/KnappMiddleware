namespace KnappMiddleware.Auth;

/// <summary>
/// Origen de las cuentas de acceso (tabla usuarios en Postgres). <see cref="GetAllAsync"/> lo usa
/// <see cref="ClsUserGate"/> para recargar su snapshot en memoria (hot-path de Basic + Cookie auth,
/// incluye el hash); el resto de métodos sirve al CRUD del panel admin (mismo doble uso que
/// IConfigRepository con ClsConfigGate/ConfigurationCrudController).
/// </summary>
public interface IUserRepository
{
    Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Listado para el panel admin — nunca incluye PasswordHash. Requiere fn_usuarios_administrar_listar().</summary>
    Task<IReadOnlyList<UserAccountSummary>> GetAllForAdminAsync(CancellationToken cancellationToken = default);

    /// <summary>Alta de usuario. La contraseña se hashea en Postgres (pgcrypto). Requiere sp_crear_usuario.</summary>
    Task CreateAsync(string nombreUsuario, string contrasena, string rol, CancellationToken cancellationToken = default);

    /// <summary>Actualiza rol + habilitado juntos. Requiere sp_actualizar_usuario.</summary>
    Task UpdateAsync(string nombreUsuario, string rol, bool habilitado, CancellationToken cancellationToken = default);

    /// <summary>Restablece la contraseña (rehashea en Postgres). Requiere sp_restablecer_contrasena_usuario.</summary>
    Task ResetPasswordAsync(string nombreUsuario, string contrasenaNueva, CancellationToken cancellationToken = default);

    /// <summary>Elimina el usuario. Devuelve true si existía y fue eliminado. Requiere fn_eliminar_usuario.</summary>
    Task<bool> DeleteAsync(string nombreUsuario, CancellationToken cancellationToken = default);
}

/// <summary>Fila de usuario para el panel admin — nunca lleva el hash de contraseña.</summary>
public sealed record UserAccountSummary(string NombreUsuario, UserRole Rol, bool Habilitado, DateTime CreadoEn, DateTime ActualizadoEn);
