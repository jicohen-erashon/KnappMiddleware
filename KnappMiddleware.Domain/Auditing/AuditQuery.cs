namespace KnappMiddleware.Domain.Auditing;

public sealed record AuditQuery(AuditDireccion Direccion, Guid? CorrelationId = null, int Take = 100);
