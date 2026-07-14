namespace KnappMiddleware.Infrastructure.Configuration;

/// <summary>Configuración común a los dos canales TCP persistentes hacia KiSoft (9801/9802).</summary>
public abstract class KiSoftTcpChannelOptions
{
    public required string Host { get; set; }
    public required int Port { get; set; }

    public int ConnectTimeoutSeconds { get; set; } = 10;

    /// <summary>Ventana de espera de respuesta de KiSoft (10-30s por diseño; pasado esto, timeout y reconexión).</summary>
    public int ResponseTimeoutSeconds { get; set; } = 20;

    /// <summary>Segundos de silencio antes de emitir 1HR. Ajustable a 60/30/15 durante Go Live.</summary>
    public int HeartbeatIdleSeconds { get; set; } = 60;

    /// <summary>Doble timeout sin heartbeat ni tráfico antes de considerar la sesión caída.</summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 120;

    public int ReconnectDelaySeconds { get; set; } = 5;

    public TimeSpan ConnectTimeout => TimeSpan.FromSeconds(ConnectTimeoutSeconds);
    public TimeSpan ResponseTimeout => TimeSpan.FromSeconds(ResponseTimeoutSeconds);
    public TimeSpan HeartbeatIdle => TimeSpan.FromSeconds(HeartbeatIdleSeconds);
    public TimeSpan HeartbeatTimeout => TimeSpan.FromSeconds(HeartbeatTimeoutSeconds);
    public TimeSpan ReconnectDelay => TimeSpan.FromSeconds(ReconnectDelaySeconds);
}
