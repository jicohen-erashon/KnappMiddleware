using Microsoft.AspNetCore.Http;

namespace KnappMiddleware.Auditing;

/// <summary>Contexto HTTP común capturado para las columnas de trazabilidad de buzon_entrada/buzon_salida
/// (quién autenticó el request, qué endpoint exacto, desde qué IP) — el mismo en todos los controllers SAP.</summary>
public readonly record struct AuditHttpContext(string? Usuario, string? Ruta, string? IpOrigen)
{
    public static AuditHttpContext From(HttpContext httpContext) => new(
        httpContext.User.Identity?.Name,
        httpContext.Request.Path.Value,
        httpContext.Connection.RemoteIpAddress?.ToString());
}
