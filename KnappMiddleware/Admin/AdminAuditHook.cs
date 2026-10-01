using System.Security.Claims;
using System.Text.Json;
using KnappMiddleware.Auditing;

namespace KnappMiddleware.Admin;

/// <summary>
/// Helper para enqueuear CADA acción admin al <see cref="ClsAuditWriter"/> (la cola no
/// bloqueante ya integrada al proyecto). Usa <c>EnqueueEntrada</c> porque admin → store
/// es flujo entrante al store de configuración.
///
/// Forma del payload:
///   { "action": "create|update|delete", "clave": "...", "old": "...", "new": "...", "user": "..." }
///
/// Por qué no bloquear nunca: el <c>ClsAuditWriter</c> es BoundedChannel con DropWrite
/// y respeta <see cref="ClsAuditToggle"/> — si la auditoría está off o la BD está
/// caída, el item se descarta sin afectar la respuesta HTTP.
/// </summary>
public static class AdminAuditHook
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public static void Record(
        ClsAuditWriter writer,
        ClaimsPrincipal user,
        string action,
        string clave,
        string? oldValue,
        string? newValue)
    {
        var payload = JsonSerializer.Serialize(new
        {
            action,
            clave,
            old = oldValue,
            @new = newValue,
            user = user.Identity?.Name ?? "anonymous",
            roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
        }, JsonOpts);

        var record = new AuditRecord(
            CorrelationId: Guid.NewGuid(),
            TipoTelegrama: $"ADMIN/{action}",
            // origen/destino tienen CHECK (IN ('SAP','KiSoft','Middleware')) en buzon_entrada — una
            // acción admin es enteramente interna al Middleware (no viene de/hacia SAP ni KiSoft).
            Source: "Middleware",
            Target: "Middleware",
            Estado: AuditEstado.Recibido,
            Payload: payload,
            Ruta: $"/api/v1/configurations/{clave}",
            Usuario: user.Identity?.Name);

        writer.EnqueueEntrada(record);
    }

    /// <summary>
    /// Igual que <see cref="Record"/> pero para acciones sobre la tabla usuarios (create/update/
    /// reset_password/delete). El payload nunca lleva contraseñas — ni la nueva ni ninguna anterior;
    /// para "reset_password" ni siquiera se registran los campos rol/old/new.
    /// </summary>
    public static void RecordUsuario(
        ClsAuditWriter writer,
        ClaimsPrincipal user,
        string action,
        string nombreUsuario,
        string? rolAnterior,
        string? rolNuevo)
    {
        var payload = JsonSerializer.Serialize(new
        {
            action,
            nombreUsuario,
            old = rolAnterior,
            @new = rolNuevo,
            user = user.Identity?.Name ?? "anonymous",
            roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
        }, JsonOpts);

        var record = new AuditRecord(
            CorrelationId: Guid.NewGuid(),
            TipoTelegrama: $"ADMIN/{action}",
            Source: "Middleware",
            Target: "Middleware",
            Estado: AuditEstado.Recibido,
            Payload: payload,
            Ruta: $"/api/v1/users/{nombreUsuario}",
            Usuario: user.Identity?.Name);

        writer.EnqueueEntrada(record);
    }
}
