using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KnappMiddleware.Infrastructure.RabbitMq;

public sealed class RabbitMqConsumer : IRabbitMqConsumer
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private IChannel? _channel;

    public RabbitMqConsumer(RabbitMqConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task StartConsumingAsync(string queue, Func<byte[], CancellationToken, Task> onMessage, CancellationToken cancellationToken = default)
    {
        _channel = await _connectionManager.CreateChannelAsync(cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                await onMessage(delivery.Body.ToArray(), cancellationToken);
                await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
            }
            catch
            {
                await _channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
            }
        };

        await _channel.BasicConsumeAsync(queue, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }
    }
}
