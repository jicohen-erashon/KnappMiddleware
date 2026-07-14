using KnappMiddleware.Domain.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Infrastructure.Postgres;

/// <summary>
/// Carga el snapshot inicial de configuracion al arrancar la Api. Si Postgres no está disponible, no
/// aborta el arranque: el Gate queda vacío y cada flag cae a su valor de respaldo (appsettings) hasta
/// el próximo /config/reload.
/// </summary>
public sealed class ConfigGateStartupService : IHostedService
{
    private readonly IConfigGate _gate;
    private readonly ILogger<ConfigGateStartupService> _logger;

    public ConfigGateStartupService(IConfigGate gate, ILogger<ConfigGateStartupService> logger)
    {
        _gate = gate;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _gate.ReloadAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo cargar la configuración desde Postgres al arrancar; cada flag cae a su valor de respaldo.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
