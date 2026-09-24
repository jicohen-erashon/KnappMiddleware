using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KnappMiddleware.RabbitMq;

public sealed class RabbitMqConsumer : IRabbitMqConsumer
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly ILogger<RabbitMqConsumer> _logger;
    private IChannel? _channel;
    private readonly Dictionary<string, int> _consecutiveFailuresByQueue = new();

    public RabbitMqConsumer(RabbitMqConnectionManager connectionManager, ILogger<RabbitMqConsumer> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
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
                if (_consecutiveFailuresByQueue.Remove(queue))
                {
                    _logger.LogInformation("Cola {Queue} vuelve a procesar mensajes correctamente.", queue);
                }
                await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
            }
            catch (Exception ex)
            {
                var count = _consecutiveFailuresByQueue.TryGetValue(queue, out var c) ? c + 1 : 1;
                _consecutiveFailuresByQueue[queue] = count;
                if (count == 1 || count == 5 || count == 30 || count % 60 == 0)
                {
                    _logger.LogWarning("Fallo #{Count} consecutivo procesando mensaje de cola {Queue}: {Reason}. Se reencola.",
                        count, queue, ShortReason(ex));
                }
                await _channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
            }
        };

        await _channel.BasicConsumeAsync(queue, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
    }

    private static string ShortReason(Exception ex) => ex switch
    {
        OperationCanceledException => "Operacion cancelada",
        IOException io => $"IO: {FirstLine(io.Message)}",
        RabbitMQ.Client.Exceptions.AlreadyClosedException => "RabbitMQ canal cerrado",
        _ => $"{ex.GetType().Name}: {FirstLine(ex.Message)}"
    };

    private static string FirstLine(string s)
    {
        var i = s.IndexOfAny(new[] { '\r', '\n' });
        return i < 0 ? s : s[..i];
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }
    }
}
