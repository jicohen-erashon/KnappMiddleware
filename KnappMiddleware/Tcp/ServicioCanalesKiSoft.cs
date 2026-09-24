using Microsoft.Extensions.Hosting;

namespace KnappMiddleware.Tcp;

/// <summary>Arranca y detiene los dos canales TCP persistentes (9801/9802) junto con el ciclo de vida de la Api.</summary>
public sealed class ServicioCanalesKiSoft : IHostedService
{
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly ICanalPedidoKiSoft _orderChannel;

    public ServicioCanalesKiSoft(ICanalEventoKiSoft eventChannel, ICanalPedidoKiSoft orderChannel)
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
