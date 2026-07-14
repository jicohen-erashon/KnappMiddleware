using KnappMiddleware.Domain.Auditing;
using KnappMiddleware.Domain.Configuration;
using KnappMiddleware.Infrastructure.Auditing;
using KnappMiddleware.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Tests.Auditing;

public class AuditWriterTests
{
    private sealed class FakeConfigRepository : IConfigRepository
    {
        public Task<IReadOnlyList<ConfigEntry>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ConfigEntry>>([]);

        public Task SetValueAsync(string clave, string valor, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static AuditRecord BuildRecord() =>
        new(Guid.NewGuid(), "12N", "SAP", "KiSoft", AuditEstado.Recibido);

    private static AuditWriter BuildWriter(bool enabled, int queueCapacity = 10)
    {
        var repository = new FakeConfigRepository();
        var toggle = new AuditToggle(new ConfigGate(repository), repository, Options.Create(new AuditOptions { Enabled = enabled }));
        return new(toggle, Options.Create(new AuditOptions { QueueCapacity = queueCapacity }));
    }

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
