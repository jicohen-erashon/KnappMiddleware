using KnappMiddleware.Domain.Auth;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Infrastructure.Postgres;

/// <summary>
/// Carga el snapshot inicial de usuarios al arrancar la Api. Si Postgres no está disponible, no aborta
/// el arranque: el Gate se queda sin usuarios (fail-safe, toda autenticación falla) hasta el próximo
/// /auth/reload.
/// </summary>
public sealed class UserGateStartupService : IHostedService
{
    private readonly IUserGate _gate;
    private readonly ILogger<UserGateStartupService> _logger;

    public UserGateStartupService(IUserGate gate, ILogger<UserGateStartupService> logger)
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
            _logger.LogWarning(ex, "No se pudieron cargar los usuarios desde Postgres al arrancar; el Gate queda en modo fail-safe.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
