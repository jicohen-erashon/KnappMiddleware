using KnappMiddleware.Matrix;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Postgres;

/// <summary>
/// Carga el snapshot inicial de la matriz al arrancar la Api. Si Postgres no está disponible, no aborta
/// el arranque: el Gate se queda con su default fail-safe (Deshabilitado) hasta el próximo /matrix/reload.
/// </summary>
public sealed class MatrixGateStartupService : IHostedService
{
    private readonly ClsMatrixGate _gate;
    private readonly ILogger<MatrixGateStartupService> _logger;

    public MatrixGateStartupService(ClsMatrixGate gate, ILogger<MatrixGateStartupService> logger)
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
            _logger.LogWarning(ex, "No se pudo cargar la matriz desde Postgres al arrancar; el Gate queda en modo fail-safe.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
