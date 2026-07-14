namespace KnappMiddleware.Domain.Auth;

/// <summary>Verifica credenciales HTTP Basic contra el snapshot en memoria de cuentas de acceso.</summary>
public interface IUserGate
{
    /// <summary>Verdadero si el usuario existe, está habilitado y la contraseña coincide. Lectura O(1) contra el snapshot en memoria.</summary>
    bool TryAuthenticate(string username, string password);

    /// <summary>Recarga el snapshot en memoria desde <see cref="IUserRepository"/>. Seguro de llamar mientras hay lecturas en curso.</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
