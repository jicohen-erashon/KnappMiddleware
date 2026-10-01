namespace KnappMiddleware.Contratos.Admin;

/// <summary>Cuerpo de POST /api/v1/admin/auth/login.</summary>
public sealed record AdminLoginRequest(string Username, string Password);

/// <summary>Devuelto por login y por GET /api/v1/admin/auth/me.</summary>
public sealed record AdminSessionDto(string Username, string Role);
