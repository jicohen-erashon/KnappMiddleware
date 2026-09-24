namespace KnappMiddleware.Auditing;

/// <summary>CreadoEn ya viene en horario Guatemala (America/Guatemala), convertido en la consulta SQL.</summary>
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
    DateTime CreadoEn);
