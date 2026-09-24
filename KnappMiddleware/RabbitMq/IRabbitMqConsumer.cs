namespace KnappMiddleware.RabbitMq;

public interface IRabbitMqConsumer : IAsyncDisposable
{
    Task StartConsumingAsync(string queue, Func<byte[], CancellationToken, Task> onMessage, CancellationToken cancellationToken = default);
}
