namespace KnappMiddleware.Auditing;

/// <summary>Un registro de auditoría de telegrama, destinado a BuzonEntrada o BuzonSalida según quien lo encole.</summary>
public sealed record AuditRecord(
    Guid CorrelationId,
    string TipoTelegrama,
    string Source,
    string Target,
    AuditEstado Estado,
    string? Payload = null,
    string? ErrorDetalle = null,
    int? DurationMs = null,
    int? HttpStatus = null,
    string? Usuario = null,
    string? Ruta = null,
    string? IpOrigen = null,
    string? IdObjeto = null,
    string? CreadoPorSap = null);
