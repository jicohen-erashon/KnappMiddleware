namespace KnappMiddleware.Domain.Auth;

/// <summary>Cuenta de acceso para el canal HTTP entrante (Basic Auth). Fila de la tabla usuarios en Postgres.</summary>
public sealed record UserAccount(string Username, string PasswordHash, bool Enabled);
