using KnappMiddleware.Tcp;

namespace KnappMiddleware.Contratos;

public sealed record EstadoCanalTcpDto(string Channel, bool Connected, DateTimeOffset? LastActivityUtc, int ReconnectCount)
{
    public static EstadoCanalTcpDto From(string channelName, ICanalTcpKiSoft channel) =>
        new(channelName, channel.IsConnected, channel.LastActivityUtc, channel.ReconnectCount);
}
