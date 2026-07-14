using KnappMiddleware.Domain.Auditing;
using KnappMiddleware.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Infrastructure.Auditing;

public sealed class AuditToggle : IAuditToggle
{
    private volatile bool _enabled;

    public AuditToggle(IOptions<AuditOptions> options)
    {
        _enabled = options.Value.Enabled;
    }

    public bool IsEnabled => _enabled;

    public void SetEnabled(bool enabled) => _enabled = enabled;
}
