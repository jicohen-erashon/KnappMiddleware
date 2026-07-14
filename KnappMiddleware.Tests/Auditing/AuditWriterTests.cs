using KnappMiddleware.Domain.Auditing;
using KnappMiddleware.Infrastructure.Auditing;
using KnappMiddleware.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Tests.Auditing;

public class AuditWriterTests
{
    private static AuditRecord BuildRecord() =>
        new(Guid.NewGuid(), "12N", "SAP", "KiSoft", AuditEstado.Recibido);

    private static AuditWriter BuildWriter(bool enabled, int queueCapacity = 10) =>
        new(new AuditToggle(Options.Create(new AuditOptions { Enabled = enabled })),
            Options.Create(new AuditOptions { QueueCapacity = queueCapacity }));

    [Fact]
    public void EnqueueEntrada_WhenDisabled_DoesNotQueueAnything()
    {
        var writer = BuildWriter(enabled: false);

        writer.EnqueueEntrada(BuildRecord());

        Assert.False(writer.Reader.TryRead(out _));
    }

    [Fact]
    public void EnqueueEntrada_WhenEnabled_QueuesTheItemAsEntrada()
    {
        var writer = BuildWriter(enabled: true);
        var record = BuildRecord();

        writer.EnqueueEntrada(record);

        Assert.True(writer.Reader.TryRead(out var item));
        Assert.Equal(AuditDireccion.Entrada, item.Direccion);
        Assert.Equal(record.CorrelationId, item.Record.CorrelationId);
    }

    [Fact]
    public void EnqueueSalida_WhenEnabled_QueuesTheItemAsSalida()
    {
        var writer = BuildWriter(enabled: true);
        var record = BuildRecord();

        writer.EnqueueSalida(record);

        Assert.True(writer.Reader.TryRead(out var item));
        Assert.Equal(AuditDireccion.Salida, item.Direccion);
    }

    [Fact]
    public void Enqueue_WhenQueueIsFull_DropsWithoutThrowing()
    {
        var writer = BuildWriter(enabled: true, queueCapacity: 1);

        writer.EnqueueEntrada(BuildRecord());
        writer.EnqueueEntrada(BuildRecord());

        Assert.True(writer.Reader.TryRead(out _));
        Assert.False(writer.Reader.TryRead(out _));
    }
}
