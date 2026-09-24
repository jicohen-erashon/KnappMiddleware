namespace KnappMiddleware.RabbitMq;

public interface IRabbitMqPublisher
{
    Task PublishAsync(string exchange, string routingKey, byte[] body, CancellationToken cancellationToken = default);
}
