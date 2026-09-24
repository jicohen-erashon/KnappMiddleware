using RabbitMQ.Client;

namespace KnappMiddleware.RabbitMq;

public sealed class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly RabbitMqConnectionManager _connectionManager;

    public RabbitMqPublisher(RabbitMqConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task PublishAsync(string exchange, string routingKey, byte[] body, CancellationToken cancellationToken = default)
    {
        var channel = await _connectionManager.CreateChannelAsync(cancellationToken);
        await using (channel)
        {
            var properties = new BasicProperties { Persistent = true };
            await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, basicProperties: properties, body: body, cancellationToken: cancellationToken);
        }
    }
}
