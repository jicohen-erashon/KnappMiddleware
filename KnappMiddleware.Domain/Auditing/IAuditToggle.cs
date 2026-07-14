namespace KnappMiddleware.Domain.Auditing;

/// <summary>Estado en caliente del flag de auditoría (PUT /audit/toggle), seedeado desde configuración al arrancar.</summary>
public interface IAuditToggle
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
