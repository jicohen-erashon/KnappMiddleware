namespace KnappMiddleware.Domain.Configuration;

/// <summary>Par clave/valor de la tabla configuracion (flags y ajustes hot-reloadable, p. ej. audit.enabled).</summary>
public sealed record ConfigEntry(string Clave, string Valor);
