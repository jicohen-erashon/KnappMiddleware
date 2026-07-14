namespace KnappMiddleware.Domain.Auditing;

/// <summary>
/// Estado en caliente del flag de auditoría (PUT /audit/toggle). Respaldado por la tabla configuracion
/// (clave audit.enabled); si no hay valor ahí, cae al default de appsettings.
/// </summary>
public interface IAuditToggle
{
    bool IsEnabled { get; }

    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}
