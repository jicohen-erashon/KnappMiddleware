using KnappMiddleware.Domain.Configuration;
using KnappMiddleware.Infrastructure.Auditing;
using KnappMiddleware.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Tests.Auditing;

public class AuditToggleTests
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEnabled_WithoutDbValue_FallsBackToOptions(bool enabled)
    {
        var repository = new FakeConfigRepository();
        var toggle = new AuditToggle(new ConfigGate(repository), repository, Options.Create(new AuditOptions { Enabled = enabled }));

        Assert.Equal(enabled, toggle.IsEnabled);
    }

    [Fact]
    public async Task IsEnabled_PrefersDbValueOverOptionsFallback()
    {
        var repository = new FakeConfigRepository();
        repository.Values["audit.enabled"] = "true";
        var gate = new ConfigGate(repository);
        await gate.ReloadAsync();
        var toggle = new AuditToggle(gate, repository, Options.Create(new AuditOptions { Enabled = false }));

        Assert.True(toggle.IsEnabled);
    }

    [Fact]
    public async Task SetEnabledAsync_PersistsAndIsReflectedImmediately()
    {
        var repository = new FakeConfigRepository();
        var gate = new ConfigGate(repository);
        var toggle = new AuditToggle(gate, repository, Options.Create(new AuditOptions { Enabled = false }));

        await toggle.SetEnabledAsync(true);

        Assert.True(toggle.IsEnabled);
        Assert.Equal("True", repository.Values["audit.enabled"]);
    }
}
