using KnappMiddleware.Configuration;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Tcp;

public sealed class CanalPedidoKiSoft : CanalTcpKiSoftBase, ICanalPedidoKiSoft
{
    private readonly ClsConfigGate _configGate;
    private readonly ILogger<CanalPedidoKiSoft> _logger;

    public CanalPedidoKiSoft(ClsConfigGate configGate, ILogger<CanalPedidoKiSoft> logger)
        : base(logger)
    {
        _configGate = configGate;
        _logger = logger;
    }

    protected override KiSoftTcpChannelOptions ReadOptions() => KiSoftOrderChannelOptions.ReadFrom(_configGate);

    public Task<string> SendAsync(string telegramData, CancellationToken cancellationToken = default) =>
        SendAndAwaitResponseAsync(telegramData, Options.ResponseTimeout, cancellationToken);

    public Task<IReadOnlyList<string>> SendSequenceAsync(IReadOnlyList<string> telegrams, CancellationToken cancellationToken = default) =>
        SendSequenceAndAwaitResponsesAsync(telegrams, Options.ResponseTimeout, cancellationToken);

    protected override Task OnUnsolicitedFrameAsync(string data, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Trama recibida en el canal de pedidos (9801) sin solicitud pendiente: '{Data}'.", data);
        return Task.CompletedTask;
    }
}
