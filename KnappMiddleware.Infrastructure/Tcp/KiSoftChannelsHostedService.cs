using Microsoft.Extensions.Hosting;

namespace KnappMiddleware.Infrastructure.Tcp;

/// <summary>Arranca y detiene los dos canales TCP persistentes (9801/9802) junto con el ciclo de vida de la Api.</summary>
public sealed class KiSoftChannelsHostedService : IHostedService
{
    private readonly IKiSoftEventChannel _eventChannel;
    private readonly IKiSoftOrderChannel _orderChannel;

    public KiSoftChannelsHostedService(IKiSoftEventChannel eventChannel, IKiSoftOrderChannel orderChannel)
    {
        _eventChannel = eventChannel;
        _orderChannel = orderChannel;
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(
            _eventChannel.StartAsync(cancellationToken),
            _orderChannel.StartAsync(cancellationToken));

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(
            _eventChannel.StopAsync(),
            _orderChannel.StopAsync());
}
