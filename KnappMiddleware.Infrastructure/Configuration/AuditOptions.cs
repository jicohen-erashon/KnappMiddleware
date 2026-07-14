namespace KnappMiddleware.Infrastructure.Configuration;

public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>Activable/desactivable en caliente (futuro PUT /audit/toggle); por defecto apagado.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Capacidad del buffer en memoria antes de que se empiecen a descartar registros nuevos.</summary>
    public int QueueCapacity { get; set; } = 10_000;
}
