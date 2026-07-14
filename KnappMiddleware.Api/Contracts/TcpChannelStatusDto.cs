using KnappMiddleware.Infrastructure.Tcp;

namespace KnappMiddleware.Api.Contracts;

public sealed record TcpChannelStatusDto(string Channel, bool Connected, DateTimeOffset? LastActivityUtc, int ReconnectCount)
{
    public static TcpChannelStatusDto From(string channelName, IKiSoftTcpChannel channel) =>
        new(channelName, channel.IsConnected, channel.LastActivityUtc, channel.ReconnectCount);
}
