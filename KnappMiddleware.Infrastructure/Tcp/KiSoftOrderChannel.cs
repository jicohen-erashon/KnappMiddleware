using KnappMiddleware.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Infrastructure.Tcp;

public sealed class KiSoftOrderChannel : KiSoftTcpChannelBase, IKiSoftOrderChannel
{
    private readonly ILogger<KiSoftOrderChannel> _logger;

    public KiSoftOrderChannel(IOptions<KiSoftOrderChannelOptions> options, ILogger<KiSoftOrderChannel> logger)
        : base(options.Value, logger)
    {
        _logger = logger;
    }

    public Task<string> SendAsync(string telegramData, CancellationToken cancellationToken = default) =>
        SendAndAwaitResponseAsync(telegramData, Options.ResponseTimeout, cancellationToken);

    protected override Task OnUnsolicitedFrameAsync(string data, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Trama recibida en el canal de pedidos (9801) sin solicitud pendiente: '{Data}'.", data);
        return Task.CompletedTask;
    }
}
