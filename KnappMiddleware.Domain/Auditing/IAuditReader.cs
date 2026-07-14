namespace KnappMiddleware.Domain.Auditing;

public interface IAuditReader
{
    Task<IReadOnlyList<AuditQueryResult>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default);
}
