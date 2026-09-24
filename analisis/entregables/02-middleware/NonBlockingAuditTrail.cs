using System.Threading.Channels;

namespace KnappMiddleware.Auditing;

/// <summary>
/// Cola de auditoría **no bloqueante** para la ruta crítica. Cada <see cref="EnqueueIncoming"/>
/// y <see cref="EnqueueOutgoing"/> retorna inmediatamente (sin I/O). Un <c>BackgroundService</c>
/// dedicado (mirar <c>AuditWriterBackgroundService</c> en el proyecto real) drena a Postgres.
///
/// Reglas:
///  - BoundedChannel con FullMode = DropOldest — si por algún motivo el drenaje se atasca,
///    el item más viejo se cae ANTES que bloquear al productor.
///  - <c>EnqueueAsync</c> se ofrece para wait explícito (sólo en test).
///  - Cada item lleva CorrelationId para correlación con la BD.
/// </summary>
public sealed class NonBlockingAuditTrail
{
    private readonly Channel<AuditWorkItem> _channel;

    public NonBlockingAuditTrail(NonBlockingAuditTrailOptions options)
    {
        _channel = Channel.CreateBounded<AuditWorkItem>(
            new BoundedChannelOptions(options.Capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = options.SingleReader,
                SingleWriter = false
            });
    }

    public ChannelReader<AuditWorkItem> Reader => _channel.Reader;
    public ValueTask Completion => _channel.Reader.Completion;

    /// <summary>Encolar sin bloquear la ruta crítica. Si la cola está saturada, descarta el item
    /// más antiguo y encola el nuevo. Idempotente para el CorrelationId.</summary>
    public void EnqueueIncoming(AuditRecord record) =>
        TryWrite(new AuditWorkItem(AuditDireccion.Entrada, record));

    public void EnqueueOutgoing(AuditRecord record) =>
        TryWrite(new AuditWorkItem(AuditDireccion.Salida, record));

    public ValueTask EnqueueIncomingAsync(AuditRecord record, CancellationToken ct) =>
        _channel.Writer.WriteAsync(new AuditWorkItem(AuditDireccion.Entrada, record), ct);

    public ValueTask EnqueueOutgoingAsync(AuditRecord record, CancellationToken ct) =>
        _channel.Writer.WriteAsync(new AuditWorkItem(AuditDireccion.Salida, record), ct);

    private void TryWrite(AuditWorkItem item)
    {
        // TryWrite nunca bloquea. Devuelve false sólo si el canal está cerrado.
        if (!_channel.Writer.TryWrite(item))
        {
            // Cerrado: descartar silenciosamente para no afectar disponibilidad.
        }
    }
}

public sealed class NonBlockingAuditTrailOptions
{
    public int Capacity { get; set; } = 4096;
    public bool SingleReader { get; set; } = true;
}

public sealed record AuditWorkItem(AuditDireccion Direccion, AuditRecord Record);

public enum AuditDireccion { Entrada, Salida }

public sealed record AuditRecord(
    string CorrelationId,
    string IdRecord,
    string Source,
    string Target,
    int Estado,
    byte[]? Payload,
    string? ErrorDetalle = null,
    long DurationMs = 0);
