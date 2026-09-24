namespace KnappMiddleware.RabbitMq;

/// <summary>Operaciones de observación/mantenimiento sobre una cola, sin consumir sus mensajes.</summary>
public interface IRabbitMqQueueMonitor
{
    Task<uint> GetMessageCountAsync(string queue, CancellationToken cancellationToken = default);

    Task<uint> PurgeAsync(string queue, CancellationToken cancellationToken = default);
}
