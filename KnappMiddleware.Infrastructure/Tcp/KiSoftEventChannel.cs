using KnappMiddleware.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Infrastructure.Tcp;

public sealed class KiSoftEventChannel : KiSoftTcpChannelBase, IKiSoftEventChannel
{
    private readonly ILogger<KiSoftEventChannel> _logger;

    public event Func<string, CancellationToken, Task>? TelegramReceived;

    public KiSoftEventChannel(IOptions<KiSoftEventChannelOptions> options, ILogger<KiSoftEventChannel> logger)
        : base(options.Value, logger)
    {
        _logger = logger;
    }

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
