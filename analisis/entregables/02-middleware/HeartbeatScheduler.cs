using System.Threading.Channels;

namespace KnappMiddleware.Telegramas.Dispatching;

/// <summary>
/// Emisor/receptor de heartbeat (HISP §2.7). Detecta silencio en cada canal y
/// dispara 1HR (saliente) o responde 4HR (entrante). Al doble timeout de silencio,
/// marca el canal como "necesita reconectar" (la reconexión la hace el HostedService).
///
/// Knapp es el cuello de botella: si Knapp no responde a un 1HR dentro de 2 minutos,
/// ya no sirve seguir esperando — abrimos el socket y re-conectamos.
/// </summary>
public sealed class HeartbeatScheduler : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan HeartbeatDoubleTimeout = TimeSpan.FromSeconds(120);

    private readonly ReaderPort _reader9801;
    private readonly WriterPort _writer9801;
    private readonly ReaderPort _reader9802;
    private readonly WriterPort _writer9802;
    private readonly ChannelWriter<HeartbeatEvent> _events;
    private readonly ChannelReader<HeartbeatEvent> _eventsReader;
    private readonly ILogger<HeartbeatScheduler> _logger;

    private DateTime _lastSend9801 = DateTime.UtcNow;
    private DateTime _lastRecv9802 = DateTime.UtcNow;

    public HeartbeatScheduler(
        ReaderPort reader9801, WriterPort writer9801,
        ReaderPort reader9802, WriterPort writer9802,
        ILogger<HeartbeatScheduler> logger)
    {
        _reader9801 = reader9801; _writer9801 = writer9801;
        _reader9802 = reader9802; _writer9802 = writer9802;
        _logger = logger;
        var ch = Channel.CreateUnbounded<HeartbeatEvent>(new UnboundedChannelOptions { SingleReader = true });
        _events = ch.Writer; _eventsReader = ch.Reader;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Reloj maestro
        var timer = new PeriodicTimer(HeartbeatInterval / 4);
        try
        {
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckAndSendHeartbeatsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    private async Task CheckAndSendHeartbeatsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Saliente: si no hemos escrito ni leído hace 60 s en 9801 → enviar 1HR
        if (now - _lastSend9801 >= HeartbeatInterval)
        {
            await SendSafeAsync(KiSoftHeartbeat.OutgoingPing, ct);
            _lastSend9801 = now;
        }

        // Doble timeout: avisar al HostedService para reconectar
        if (now - _lastSend9801 >= HeartbeatDoubleTimeout)
        {
            await _events.WriteAsync(new HeartbeatEvent(HeartbeatEventKind.ReconnectOutbound), ct);
            _lastSend9801 = now;
        }

        // Entrante: si Knapp nos mandó 3HR, responder 4HR
        await foreach (var frame in _reader9802.PeekAsync(ct))
        {
            if (frame.IdRecord == KiSoftHeartbeat.IncomingPing)
            {
                await SendAckAsync(KiSoftHeartbeat.IncomingPingAck, ct);
            }
            _lastRecv9802 = DateTime.UtcNow;
            break; // una sola iteración por tick
        }
    }

    private async Task SendSafeAsync(string idrecord, CancellationToken ct)
    {
        try { await _writer9801.WriteAsync(Encoding.ASCII.GetBytes(idrecord), ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Fallo escribiendo {Id}", idrecord); }
    }

    private async Task SendAckAsync(string idrecord, CancellationToken ct)
    {
        var payload = Encoding.ASCII.GetBytes(idrecord + "00");
        try { await _writer9802.WriteAsync(payload, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Fallo escribiendo ack {Id}", idrecord); }
    }

    public ChannelReader<HeartbeatEvent> Events => _eventsReader;
}

public enum HeartbeatEventKind { ReconnectOutbound, ReconnectInbound }

public sealed record HeartbeatEvent(HeartbeatEventKind Kind);
