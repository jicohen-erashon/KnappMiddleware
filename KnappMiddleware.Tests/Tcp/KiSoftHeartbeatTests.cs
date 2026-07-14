using KnappMiddleware.Infrastructure.Configuration;
using KnappMiddleware.Infrastructure.Tcp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Tests.Tcp;

public class KiSoftHeartbeatTests
{
    private static KiSoftOrderChannelOptions BuildOptions(int port) => new()
    {
        Host = "127.0.0.1",
        Port = port,
        ConnectTimeoutSeconds = 5,
        ResponseTimeoutSeconds = 5,
        HeartbeatIdleSeconds = 60,
        HeartbeatTimeoutSeconds = 120,
        ReconnectDelaySeconds = 1
    };

    [Fact]
    public async Task Channel_SendsOutgoingHeartbeatAfterIdlePeriod()
    {
        await using var server = new FakeKiSoftServer();
        var options = BuildOptions(server.Port);
        options.HeartbeatIdleSeconds = 1;

        await using var channel = new KiSoftOrderChannel(
            Options.Create(options), NullLogger<KiSoftOrderChannel>.Instance);

        await channel.StartAsync();
        await server.AcceptAsync();

        var heartbeat = await server.ReceiveFrameAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.Equal("1HR", heartbeat);

        await server.SendFrameAsync("2HR");
        await Task.Delay(200);

        Assert.True(channel.IsConnected);
    }

    [Fact]
    public async Task Channel_RespondsImmediatelyToIncomingHeartbeat()
    {
        await using var server = new FakeKiSoftServer();
        var options = BuildOptions(server.Port);
        options.HeartbeatIdleSeconds = 60;

        await using var channel = new KiSoftOrderChannel(
            Options.Create(options), NullLogger<KiSoftOrderChannel>.Instance);

        await channel.StartAsync();
        await server.AcceptAsync();

        await server.SendFrameAsync("3HR");
        var reply = await server.ReceiveFrameAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        Assert.Equal("4HR", reply);
    }

    [Fact]
    public async Task Channel_ReconnectsAfterDoubleHeartbeatTimeout()
    {
        await using var server = new FakeKiSoftServer();
        var options = BuildOptions(server.Port);
        options.HeartbeatIdleSeconds = 1000;
        options.HeartbeatTimeoutSeconds = 1;
        options.ReconnectDelaySeconds = 0;

        await using var channel = new KiSoftOrderChannel(
            Options.Create(options), NullLogger<KiSoftOrderChannel>.Instance);

        await channel.StartAsync();
        await server.AcceptAsync();

        await server.AcceptAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
    }
}
