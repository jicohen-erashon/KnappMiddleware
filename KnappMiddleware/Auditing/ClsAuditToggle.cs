using KnappMiddleware.Configuration;

namespace KnappMiddleware.Auditing;

public sealed class ClsAuditToggle
{
    private const string AuditEnabledKey = "audit.enabled";
    private const bool DefaultEnabled = false;

    private readonly ClsConfigGate _configGate;
    private readonly IConfigRepository _configRepository;

    public ClsAuditToggle(ClsConfigGate configGate, IConfigRepository configRepository)
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
