using KnappMiddleware.Infrastructure.Configuration;
using KnappMiddleware.Infrastructure.Tcp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Tests.Tcp;

public class KiSoftEventChannelTests
{
    private static KiSoftEventChannelOptions BuildOptions(int port) => new()
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
    public async Task EventChannel_RaisesTelegramReceivedForFrameThatKiSoftPushes()
    {
        await using var server = new FakeKiSoftServer();
        await using var channel = new KiSoftEventChannel(
            Options.Create(BuildOptions(server.Port)), NullLogger<KiSoftEventChannel>.Instance);

        var received = new TaskCompletionSource<string>();
        channel.TelegramReceived += (data, _) =>
        {
            received.TrySetResult(data);
            return Task.CompletedTask;
        };

        await channel.StartAsync();
        await server.AcceptAsync();

        await server.SendFrameAsync("32R0001CONFIRM");

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(received.Task, completed);
        Assert.Equal("32R0001CONFIRM", await received.Task);
    }

    [Fact]
    public async Task EventChannel_WithNoHandler_DoesNotThrow()
    {
        await using var server = new FakeKiSoftServer();
        await using var channel = new KiSoftEventChannel(
            Options.Create(BuildOptions(server.Port)), NullLogger<KiSoftEventChannel>.Instance);

        await channel.StartAsync();
        await server.AcceptAsync();

        await server.SendFrameAsync("3RR0001INV");
        await server.SendFrameAsync("32R0002CONFIRM");
        await Task.Delay(200);

        Assert.True(channel.IsConnected);
    }
}
