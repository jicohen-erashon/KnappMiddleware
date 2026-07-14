using System.Threading.Channels;
using KnappMiddleware.Domain.Auditing;
using KnappMiddleware.Domain.Configuration;

namespace KnappMiddleware.Infrastructure.Auditing;

internal readonly record struct AuditWorkItem(AuditDireccion Direccion, AuditRecord Record);

/// <summary>
/// Encola en un canal en memoria; nunca espera ni lanza. Si la auditoría está deshabilitada (flag) o el
/// canal está lleno, el registro se descarta silenciosamente en vez de afectar la ruta crítica. La
/// capacidad del canal se lee una sola vez, al construir (limitación de Channel.CreateBounded: no se
/// puede redimensionar en caliente); cambiar audit.queueCapacity en Postgres requiere reiniciar la Api
/// para tomar efecto, a diferencia de audit.enabled que sí es una verificación en vivo en cada escritura.
/// </summary>
public sealed class AuditWriter : IAuditWriter
{
    private const string QueueCapacityKey = "audit.queueCapacity";
    private const int DefaultQueueCapacity = 10_000;

    private readonly Channel<AuditWorkItem> _channel;
    private readonly IAuditToggle _toggle;

    public AuditWriter(IAuditToggle toggle, IConfigGate configGate)
    {
        _toggle = toggle;
        _channel = Channel.CreateBounded<AuditWorkItem>(new BoundedChannelOptions(configGate.GetInt(QueueCapacityKey, DefaultQueueCapacity))
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
    }

    internal ChannelReader<AuditWorkItem> Reader => _channel.Reader;

    public void EnqueueEntrada(AuditRecord record) => Enqueue(AuditDireccion.Entrada, record);

    public void EnqueueSalida(AuditRecord record) => Enqueue(AuditDireccion.Salida, record);

    private void Enqueue(AuditDireccion direccion, AuditRecord record)
    {
        if (!_toggle.IsEnabled)
        {
            return;
        }

        _channel.Writer.TryWrite(new AuditWorkItem(direccion, record));
    }
}
