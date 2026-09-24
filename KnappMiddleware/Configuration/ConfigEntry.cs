namespace KnappMiddleware.Configuration;

/// <summary>
/// Fila de la tabla configuracion (flags y ajustes hot-reloadable, p. ej. audit.enabled).
/// CreadoEn/ActualizadoEn ya vienen en horario Guatemala (America/Guatemala), convertidos en la
/// consulta SQL — no dependen de la zona horaria configurada en el servidor/sesión de Postgres.
/// </summary>
public sealed record ConfigEntry(string Clave, string Valor, string? Descripcion, DateTime CreadoEn, DateTime ActualizadoEn);
