using KnappMiddleware.Domain.Matrix;

namespace KnappMiddleware.Tests.Matrix;

public class MatrixGateTests
{
    private sealed class FakeMatrixRepository : IMatrixRepository
    {
        public List<MatrixEntry> Entries { get; } = [];

        public Task<IReadOnlyList<MatrixEntry>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MatrixEntry>>(Entries);
    }

    [Fact]
    public void Resolve_WithoutReload_DefaultsToDeshabilitado()
    {
        var gate = new MatrixGate(new FakeMatrixRepository());

        Assert.Equal(MatrixAction.Deshabilitado, gate.Resolve("SAP", "12N", "EST01"));
    }

    [Fact]
    public async Task Resolve_UnmatchedCombination_DefaultsToDeshabilitado()
    {
        var repository = new FakeMatrixRepository();
        repository.Entries.Add(new MatrixEntry("SAP", "12N", "EST01", MatrixAction.Procesar));
        var gate = new MatrixGate(repository);
        await gate.ReloadAsync();

        Assert.Equal(MatrixAction.Deshabilitado, gate.Resolve("SAP", "12N", "EST02"));
    }

    [Theory]
    [InlineData(MatrixAction.Procesar)]
    [InlineData(MatrixAction.Ignorar)]
    [InlineData(MatrixAction.Deshabilitado)]
    public async Task Resolve_MatchedCombination_ReturnsConfiguredAction(MatrixAction accion)
    {
        var repository = new FakeMatrixRepository();
        repository.Entries.Add(new MatrixEntry("SAP", "12N", "EST01", accion));
        var gate = new MatrixGate(repository);
        await gate.ReloadAsync();

        Assert.Equal(accion, gate.Resolve("SAP", "12N", "EST01"));
    }

    [Fact]
    public async Task Resolve_IsCaseInsensitive()
    {
        var repository = new FakeMatrixRepository();
        repository.Entries.Add(new MatrixEntry("SAP", "12N", "EST01", MatrixAction.Procesar));
        var gate = new MatrixGate(repository);
        await gate.ReloadAsync();

        Assert.Equal(MatrixAction.Procesar, gate.Resolve("sap", "12n", "est01"));
    }

    [Fact]
    public async Task ReloadAsync_ReplacesPreviousSnapshotEntirely()
    {
        var repository = new FakeMatrixRepository();
        repository.Entries.Add(new MatrixEntry("SAP", "12N", "EST01", MatrixAction.Procesar));
        var gate = new MatrixGate(repository);
        await gate.ReloadAsync();

        repository.Entries.Clear();
        repository.Entries.Add(new MatrixEntry("SAP", "12N", "EST02", MatrixAction.Procesar));
        await gate.ReloadAsync();

        Assert.Equal(MatrixAction.Deshabilitado, gate.Resolve("SAP", "12N", "EST01"));
        Assert.Equal(MatrixAction.Procesar, gate.Resolve("SAP", "12N", "EST02"));
    }

    [Fact]
    public void Resolve_NullArgument_Throws()
    {
        var gate = new MatrixGate(new FakeMatrixRepository());

        Assert.Throws<ArgumentNullException>(() => gate.Resolve(null!, "12N", "EST01"));
    }
}
