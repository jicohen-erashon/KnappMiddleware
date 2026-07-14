using KnappMiddleware.Domain.Auditing;
using KnappMiddleware.Domain.Configuration;

namespace KnappMiddleware.Infrastructure.Auditing;

public sealed class AuditToggle : IAuditToggle
{
    private const string AuditEnabledKey = "audit.enabled";
    private const bool DefaultEnabled = false;

    private readonly IConfigGate _configGate;
    private readonly IConfigRepository _configRepository;

    public AuditToggle(IConfigGate configGate, IConfigRepository configRepository)
    {
        _configGate = configGate;
        _configRepository = configRepository;
    }

    public bool IsEnabled => _configGate.GetBool(AuditEnabledKey, DefaultEnabled);

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await _configRepository.SetValueAsync(AuditEnabledKey, enabled.ToString(), cancellationToken);
        await _configGate.ReloadAsync(cancellationToken);
    }
}
