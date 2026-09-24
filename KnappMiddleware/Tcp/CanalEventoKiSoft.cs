using KnappMiddleware.Configuration;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Tcp;

public sealed class CanalEventoKiSoft : CanalTcpKiSoftBase, ICanalEventoKiSoft
{
    private readonly ClsConfigGate _configGate;
    private readonly ILogger<CanalEventoKiSoft> _logger;

    public event Func<string, CancellationToken, Task>? TelegramReceived;

    public CanalEventoKiSoft(ClsConfigGate configGate, ILogger<CanalEventoKiSoft> logger)
        : base(logger)
    {
        _configGate = configGate;
        _logger = logger;
    }

    protected override KiSoftTcpChannelOptions ReadOptions() => KiSoftEventChannelOptions.ReadFrom(_configGate);

    public Task AcknowledgeAsync(string statusTelegramData, CancellationToken cancellationToken = default) =>
        SendFrameAsync(statusTelegramData, cancellationToken);

    protected override async Task OnUnsolicitedFrameAsync(string data, CancellationToken cancellationToken)
    {
        var handler = TelegramReceived;
        if (handler is null)
        {
            _logger.LogWarning("Evento KiSoft recibido en 9802 sin manejador registrado: '{Data}'.", data);
            return;
        }

        try
        {
            await handler(data, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar evento KiSoft (9802): '{Data}'.", data);
        }
    }
}
