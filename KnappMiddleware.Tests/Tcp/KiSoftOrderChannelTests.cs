using KnappMiddleware.Infrastructure.Configuration;
using KnappMiddleware.Infrastructure.Tcp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Tests.Tcp;

public class KiSoftOrderChannelTests
{
    private static KiSoftOrderChannelOptions BuildOptions(int port) => new()
    {
        Host = "127.0.0.1",
        Port = port,
        ConnectTimeoutSeconds = 5,
        ResponseTimeoutSeconds = 3,
        HeartbeatIdleSeconds = 60,
        HeartbeatTimeoutSeconds = 120,
        ReconnectDelaySeconds = 1
    };

    [Fact]
    public async Task SendAsync_ReturnsServerResponse()
    {
        await using var server = new FakeKiSoftServer();
        await using var channel = new KiSoftOrderChannel(
            Options.Create(BuildOptions(server.Port)), NullLogger<KiSoftOrderChannel>.Instance);

        await channel.StartAsync();
        await server.AcceptAsync();

        var sendTask = channel.SendAsync("12N0001ABC");
        var received = await server.ReceiveFrameAsync();
        Assert.Equal("12N0001ABC", received);

        await server.SendFrameAsync("22N0001OK");
        var response = await sendTask;

        Assert.Equal("22N0001OK", response);
    }

    [Fact]
    public async Task SendAsync_SecondCallWaitsForFirstResponse_EnforcingFifo()
    {
        await using var server = new FakeKiSoftServer();
        await using var channel = new KiSoftOrderChannel(
            Options.Create(BuildOptions(server.Port)), NullLogger<KiSoftOrderChannel>.Instance);

        await channel.StartAsync();
        await server.AcceptAsync();

        var firstSend = channel.SendAsync("12N0001AAA");
        var secondSend = channel.SendAsync("12N0002BBB");

        var firstReceivedByServer = await server.ReceiveFrameAsync();
        Assert.Equal("12N0001AAA", firstReceivedByServer);
        Assert.False(firstSend.IsCompleted);
        Assert.False(secondSend.IsCompleted);

        await server.SendFrameAsync("22N0001OK");
        Assert.Equal("22N0001OK", await firstSend);

        var secondReceivedByServer = await server.ReceiveFrameAsync();
        Assert.Equal("12N0002BBB", secondReceivedByServer);

        await server.SendFrameAsync("22N0002OK");
        Assert.Equal("22N0002OK", await secondSend);
    }

    [Fact]
    public async Task SendAsync_NoResponseWithinTimeout_ThrowsTimeoutException()
    {
        await using var server = new FakeKiSoftServer();
        var options = BuildOptions(server.Port);
        options.ResponseTimeoutSeconds = 1;

        await using var channel = new KiSoftOrderChannel(
            Options.Create(options), NullLogger<KiSoftOrderChannel>.Instance);

        await channel.StartAsync();
        await server.AcceptAsync();

        await Assert.ThrowsAsync<TimeoutException>(() => channel.SendAsync("12N0001AAA"));
    }

    [Fact]
    public async Task Channel_ReconnectsAfterServerClosesConnection()
    {
        await using var server = new FakeKiSoftServer();
        var options = BuildOptions(server.Port);
        options.ReconnectDelaySeconds = 0;

        await using var channel = new KiSoftOrderChannel(
            Options.Create(options), NullLogger<KiSoftOrderChannel>.Instance);

        await channel.StartAsync();
        await server.AcceptAsync();
        Assert.True(channel.IsConnected);

        server.CloseClientConnection();
        await server.AcceptAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);

        var sendTask = channel.SendAsync("12N0003CCC");
        var received = await server.ReceiveFrameAsync();
        Assert.Equal("12N0003CCC", received);

        await server.SendFrameAsync("22N0003OK");
        Assert.Equal("22N0003OK", await sendTask);
    }
}
