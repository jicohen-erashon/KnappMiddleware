using System.Threading.Channels;
using KnappMiddleware.Domain.Auditing;
using KnappMiddleware.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Infrastructure.Auditing;

internal readonly record struct AuditWorkItem(AuditDireccion Direccion, AuditRecord Record);

/// <summary>
/// Encola en un canal en memoria; nunca espera ni lanza. Si la auditoría está deshabilitada (flag) o el
/// canal está lleno, el registro se descarta silenciosamente en vez de afectar la ruta crítica.
/// </summary>
public sealed class AuditWriter : IAuditWriter
{
    private readonly Channel<AuditWorkItem> _channel;
    private readonly IAuditToggle _toggle;

    public AuditWriter(IAuditToggle toggle, IOptions<AuditOptions> options)
    {
        _toggle = toggle;
        _channel = Channel.CreateBounded<AuditWorkItem>(new BoundedChannelOptions(options.Value.QueueCapacity)
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
