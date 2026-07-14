namespace KnappMiddleware.Infrastructure.RabbitMq;

public sealed class RabbitMqQueueMonitor : IRabbitMqQueueMonitor
{
    private readonly RabbitMqConnectionManager _connectionManager;

    public RabbitMqQueueMonitor(RabbitMqConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task<uint> GetMessageCountAsync(string queue, CancellationToken cancellationToken = default)
    {
        var channel = await _connectionManager.CreateChannelAsync(cancellationToken);
        await using (channel)
        {
            var result = await channel.QueueDeclarePassiveAsync(queue, cancellationToken);
            return result.MessageCount;
        }
    }

    public async Task<uint> PurgeAsync(string queue, CancellationToken cancellationToken = default)
    {
        var channel = await _connectionManager.CreateChannelAsync(cancellationToken);
        await using (channel)
        {
            return await channel.QueuePurgeAsync(queue, cancellationToken);
        }
    }
}
