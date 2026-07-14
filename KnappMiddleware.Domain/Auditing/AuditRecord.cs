namespace KnappMiddleware.Domain.Auditing;

/// <summary>Un registro de auditoría de telegrama, destinado a BuzonEntrada o BuzonSalida según quien lo encole.</summary>
public sealed record AuditRecord(
    Guid CorrelationId,
    string TipoTelegrama,
    string Source,
    string Target,
    AuditEstado Estado,
    string? Payload = null,
    string? ErrorDetalle = null,
    int? DurationMs = null);
