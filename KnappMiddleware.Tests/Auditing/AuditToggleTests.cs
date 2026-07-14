using KnappMiddleware.Infrastructure.Auditing;
using KnappMiddleware.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Tests.Auditing;

public class AuditToggleTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEnabled_SeedsFromOptions(bool enabled)
    {
        var toggle = new AuditToggle(Options.Create(new AuditOptions { Enabled = enabled }));

        Assert.Equal(enabled, toggle.IsEnabled);
    }

    [Fact]
    public void SetEnabled_OverridesSeedValue()
    {
        var toggle = new AuditToggle(Options.Create(new AuditOptions { Enabled = false }));

        toggle.SetEnabled(true);

        Assert.True(toggle.IsEnabled);
    }
}
