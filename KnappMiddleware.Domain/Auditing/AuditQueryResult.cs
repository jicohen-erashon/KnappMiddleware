namespace KnappMiddleware.Domain.Auditing;

public sealed record AuditQueryResult(
    long Id,
    Guid CorrelationId,
    string TipoTelegrama,
    string Source,
    string Target,
    AuditEstado Estado,
    string? Payload,
    string? ErrorDetalle,
    int? DurationMs,
    DateTimeOffset CreatedAtUtc);
