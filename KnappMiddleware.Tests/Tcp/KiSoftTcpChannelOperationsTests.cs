using KnappMiddleware.Infrastructure.Configuration;
using KnappMiddleware.Infrastructure.Tcp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Tests.Tcp;

public class KiSoftTcpChannelOperationsTests
{
    private static KiSoftOrderChannelOptions BuildOptions(int port) => new()
    {
        Host = "127.0.0.1",
        Port = port,
        ConnectTimeoutSeconds = 5,
        ResponseTimeoutSeconds = 5,
        HeartbeatIdleSeconds = 60,
        HeartbeatTimeoutSeconds = 120,
        ReconnectDelaySeconds = 0
    };

    [Fact]
    public async Task BeforeFirstConnect_LastActivityUtcIsNullAndReconnectCountIsZero()
    {
        await using var server = new FakeKiSoftServer();
        await using var channel = new KiSoftOrderChannel(
            Options.Create(BuildOptions(server.Port)), NullLogger<KiSoftOrderChannel>.Instance);

        Assert.Null(channel.LastActivityUtc);
        Assert.Equal(0, channel.ReconnectCount);
    }

    [Fact]
    public async Task AfterFirstConnect_LastActivityUtcIsSetAndReconnectCountIsZero()
    {
        await using var server = new FakeKiSoftServer();
        await using var channel = new KiSoftOrderChannel(
            Options.Create(BuildOptions(server.Port)), NullLogger<KiSoftOrderChannel>.Instance);

        await channel.StartAsync();
        await server.AcceptAsync();
        await Task.Delay(100);

        Assert.NotNull(channel.LastActivityUtc);
        Assert.Equal(0, channel.ReconnectCount);
    }

    [Fact]
    public async Task ForceReconnectAsync_TriggersReconnectAndIncrementsCount()
    {
        await using var server = new FakeKiSoftServer();
        await using var channel = new KiSoftOrderChannel(
            Options.Create(BuildOptions(server.Port)), NullLogger<KiSoftOrderChannel>.Instance);

        await channel.StartAsync();
        await server.AcceptAsync();

        await channel.ForceReconnectAsync();
        await server.AcceptAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        await Task.Delay(100);

        Assert.Equal(1, channel.ReconnectCount);
        Assert.True(channel.IsConnected);
    }
}
