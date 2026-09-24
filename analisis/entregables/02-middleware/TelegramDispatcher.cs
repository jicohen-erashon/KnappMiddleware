using System.Text.RegularExpressions;

namespace KnappMiddleware.Telegramas.Dispatching;

/// <summary>
/// Despachador central. Se suscribe al Reader (sockets 9801/9802) y enruta cada telegrama
/// al handler correspondiente al identificador (1XX, 2XX, 3XX, 4XX o HR).
///
/// Reglas (HISP §2 + CONTEXT.md):
///  - FIFO estricto: en el canal 9801 sólo se envía UN telegrama en vuelo.
///  - 9801: el handler resuelve el TaskCompletionSource con el ack 2XX (timeout 10 s).
///  - 9802: el handler devuelve el acuse 4XX en cuanto identifica el idrecord (&lt;10 s).
///  - La auditoría se encola en NonBlockingAuditTrail — nunca se bloquea la ruta crítica.
/// </summary>
public sealed class TelegramDispatcher
{
    private readonly ReaderPort _reader9801;            // Host → KiSoft (envíos)
    private readonly WriterPort _writer9801;            // hacia el socket 9801
    private readonly ReaderPort _reader9802;            // KiSoft → Host (eventos)
    private readonly WriterPort _writer9802;            // hacia el socket 9802
    private readonly IDictionary<string, ITelegramHandler> _handlers;
    private readonly NonBlockingAuditTrail _audit;
    private readonly ILogger<TelegramDispatcher> _logger;
    private readonly MasterDataState _masterState = new();
    private readonly SemaphoreSlim _fifo9801 = new(1, 1);
    private readonly TimeSpan _ackTimeout = TimeSpan.FromSeconds(10);

    public TelegramDispatcher(
        ReaderPort reader9801, WriterPort writer9801,
        ReaderPort reader9802, WriterPort writer9802,
        IEnumerable<ITelegramHandler> handlers,
        NonBlockingAuditTrail audit,
        ILogger<TelegramDispatcher> logger)
    {
        _reader9801 = reader9801; _writer9801 = writer9801;
        _reader9802 = reader9802; _writer9802 = writer9802;
        _audit = audit; _logger = logger;
        _handlers = handlers.ToDictionary(h => h.IdRecord, StringComparer.Ordinal);
    }

    /// <summary>Envía un telegrama al canal 9801 con FIFO + TCS por ack. Llamada síncrona para la API SAP.</summary>
    public async Task<AckResult> SendAsync(string idrecord, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        // FIFO: si hay otro envío en vuelo, esperamos aquí. (SemaphoreSlim, no async lock.)
        await _fifo9801.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var tcs = new TaskCompletionSource<AckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingSends[idrecord] = tcs;             // índice por idrecord — los acks están emparejados posicionalmente

            var correlationId = Guid.NewGuid().ToString("N");
            _audit.EnqueueOutgoing(new AuditRecord(idrecord, correlationId, body));

            var resp = await _writer9801.WriteAsync(body, ct).ConfigureAwait(false);
            if (!resp.Ok) return AckResult.FromTransport(resp.Error);

            // El ReadPumpAsyncReceived (9802 NO es este) — los 2XX llegan por 9801 con SemaphoreSlim ya tomado.
            // Aquí esperamos el 2XX por el canal de vuelta (mismo 9801 — los bytes vuelven dentro del stream de salida).
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_ackTimeout);
            return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            _fifo9801.Release();
        }
    }

    /// <summary>Arranca los lectores. Llamar desde el HostedService al iniciar.</summary>
    public Task StartAsync(CancellationToken ct) => Task.WhenAll(
        ReadPumpAsync(_reader9801, Direction.Outbound, ct),
        ReadPumpAsync(_reader9802, Direction.Inbound, ct));

    private async Task ReadPumpAsync(ReaderPort port, Direction dir, CancellationToken ct)
    {
        await foreach (var frame in port.ReadAllAsync(ct))
        {
            // 1) audit inmediatamente (no bloquea)
            _audit.EnqueueIncoming(new AuditRecord(frame.IdRecord, frame.CorrelationId, frame.Payload));

            // 2) acuse obligatorio < 10 s — se prioriza sobre todo lo demás
            if (frame.IsAck)
            {
                await TryDeliverAck(frame, ct).ConfigureAwait(false);
                continue;
            }

            // 3) clasifica y enruta
            if (!_handlers.TryGetValue(frame.IdRecord, out var handler))
            {
                _logger.LogError("idrecord {Id} sin handler; se descarta para evitar bloqueo.", frame.IdRecord);
                // Aún así devolvemos un ack genérico vacío para no bloquear más a KiSoft
                await _writer9802.WriteAckAsync(frame.IdRecord, EmptyAck(), ct).ConfigureAwait(false);
                continue;
            }

            try
            {
                await handler.HandleAsync(frame, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handler {Handler} lanzó {Id}", handler.GetType().Name, frame.IdRecord);
            }
        }
    }

    private async Task TryDeliverAck(IncomingFrame frame, CancellationToken ct)
    {
        // Reglas: 2XX cierra 1XX — emparejamiento POSICIONAL (no por correlation-id).
        // _pendingSends indexado por el idrecord del envío original.
        foreach (var kv in _pendingSends.Where(k => k.Key.StartsWith(frame.IdRecord[..1], StringComparison.Ordinal)))
        {
            if (kv.Value.TrySetResult(AckResult.FromStatus(frame.StatusCode)))
            {
                _pendingSends.Remove(kv.Key);
                return;
            }
        }
        await Task.CompletedTask;
    }

    private static ReadOnlyMemory<byte> EmptyAck() =>
        Encoding.ASCII.GetBytes("00");

    private readonly Dictionary<string, TaskCompletionSource<AckResult>> _pendingSends = new();
}

public enum Direction { Outbound, Inbound }

public interface ITelegramHandler
{
    string IdRecord { get; }                          // "12N", "32R", etc.
    Task HandleAsync(IncomingFrame frame, CancellationToken ct);
}

public readonly record struct IncomingFrame(
    string IdRecord,
    string CorrelationId,
    ReadOnlyMemory<byte> Payload,
    bool IsAck,
    string StatusCode);

public sealed record AckResult(bool Ok, string Estado, string? Error = null)
{
    public static AckResult FromStatus(string e) => new(e == "00", e, null);
    public static AckResult FromTransport(string err) => new(false, "99", err);
    public static AckResult Timeout() => new(false, "99", "timeout 10 s");
}
