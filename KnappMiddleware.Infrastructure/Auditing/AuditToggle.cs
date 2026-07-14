using KnappMiddleware.Domain.Auditing;
using KnappMiddleware.Domain.Configuration;
using KnappMiddleware.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Infrastructure.Auditing;

public sealed class AuditToggle : IAuditToggle
{
    private const string AuditEnabledKey = "audit.enabled";

    private readonly IConfigGate _configGate;
    private readonly IConfigRepository _configRepository;
    private readonly bool _fallbackDefault;

    public AuditToggle(IConfigGate configGate, IConfigRepository configRepository, IOptions<AuditOptions> options)
    {
        _configGate = configGate;
        _configRepository = configRepository;
        _fallbackDefault = options.Value.Enabled;
    }

    public bool IsEnabled => _configGate.GetBool(AuditEnabledKey, _fallbackDefault);

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await _configRepository.SetValueAsync(AuditEnabledKey, enabled.ToString(), cancellationToken);
        await _configGate.ReloadAsync(cancellationToken);
    }
}
