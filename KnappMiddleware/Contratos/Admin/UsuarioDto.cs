namespace KnappMiddleware.Contratos.Admin;

/// <summary>Snapshot de una fila de usuarios tal como la devuelve GET /api/v1/users. Nunca lleva el hash de contraseña.</summary>
public sealed record UsuarioDto(string NombreUsuario, string Rol, bool Habilitado, DateTime CreadoEn, DateTime ActualizadoEn);

/// <summary>Cuerpo de POST /api/v1/users.</summary>
public sealed record CrearUsuarioRequest(string NombreUsuario, string Contrasena, string Rol);

/// <summary>Cuerpo de PUT /api/v1/users/{nombreUsuario}.</summary>
public sealed record ActualizarUsuarioRequest(string Rol, bool Habilitado);

/// <summary>Cuerpo de POST /api/v1/users/{nombreUsuario}/reset-password.</summary>
public sealed record RestablecerContrasenaUsuarioRequest(string ContrasenaNueva);
