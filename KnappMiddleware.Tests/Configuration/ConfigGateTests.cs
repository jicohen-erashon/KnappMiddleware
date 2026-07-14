using KnappMiddleware.Domain.Configuration;

namespace KnappMiddleware.Tests.Configuration;

public class ConfigGateTests
{
    private sealed class FakeConfigRepository : IConfigRepository
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<ConfigEntry>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ConfigEntry>>(Values.Select(kv => new ConfigEntry(kv.Key, kv.Value)).ToList());

        public Task SetValueAsync(string clave, string valor, CancellationToken cancellationToken = default)
        {
            Values[clave] = valor;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void GetValue_WithoutReload_ReturnsNull()
    {
        var gate = new ConfigGate(new FakeConfigRepository());

        Assert.Null(gate.GetValue("audit.enabled"));
    }

    [Fact]
    public void GetBool_WithoutReload_ReturnsDefault()
    {
        var gate = new ConfigGate(new FakeConfigRepository());

        Assert.True(gate.GetBool("audit.enabled", true));
        Assert.False(gate.GetBool("audit.enabled", false));
    }

    [Fact]
    public async Task GetValue_AfterReload_ReturnsStoredValue()
    {
        var repository = new FakeConfigRepository();
        repository.Values["audit.enabled"] = "true";
        var gate = new ConfigGate(repository);

        await gate.ReloadAsync();

        Assert.Equal("true", gate.GetValue("audit.enabled"));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("not-a-bool", true)]
    public async Task GetBool_ParsesStoredValueOrFallsBackToDefault(string storedValue, bool expected)
    {
        var repository = new FakeConfigRepository();
        repository.Values["flag"] = storedValue;
        var gate = new ConfigGate(repository);
        await gate.ReloadAsync();

        Assert.Equal(expected, gate.GetBool("flag", defaultValue: true));
    }

    [Fact]
    public async Task ReloadAsync_ReplacesPreviousSnapshotEntirely()
    {
        var repository = new FakeConfigRepository();
        repository.Values["audit.enabled"] = "true";
        var gate = new ConfigGate(repository);
        await gate.ReloadAsync();

        repository.Values.Clear();
        await gate.ReloadAsync();

        Assert.Null(gate.GetValue("audit.enabled"));
    }

    [Fact]
    public void GetValue_NullArgument_Throws()
    {
        var gate = new ConfigGate(new FakeConfigRepository());

        Assert.Throws<ArgumentNullException>(() => gate.GetValue(null!));
    }
}
