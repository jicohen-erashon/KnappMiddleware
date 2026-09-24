namespace KnappMiddleware.Configuration;

/// <summary>Configuración común a los dos canales TCP persistentes hacia KiSoft (9801/9802).</summary>
public abstract class KiSoftTcpChannelOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }

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

    /// <summary>
    /// Rellena los campos desde la tabla configuracion (no vive en appsettings) bajo el prefijo dado
    /// (p. ej. "kisoft.eventChannel"). Se llama en cada intento de conexión, no en el constructor del
    /// canal: los dos canales TCP se construyen como dependencias de un IHostedService, y todos los
    /// IHostedService se instancian en un mismo lote ANTES de que corra el StartAsync de
    /// ConfigGateStartupService — leer en el constructor vería el snapshot todavía vacío.
    /// </summary>
    protected static void PopulateFrom(KiSoftTcpChannelOptions options, ClsConfigGate configGate, string keyPrefix)
    {
        options.Host = configGate.GetValue($"{keyPrefix}.host")
            ?? throw new InvalidOperationException($"Falta la clave '{keyPrefix}.host' en la tabla configuracion.");

        var port = configGate.GetInt($"{keyPrefix}.port", 0);
        options.Port = port > 0 ? port : throw new InvalidOperationException($"Falta la clave '{keyPrefix}.port' en la tabla configuracion.");
        options.ConnectTimeoutSeconds = configGate.GetInt($"{keyPrefix}.connectTimeoutSeconds", 10);
        options.ResponseTimeoutSeconds = configGate.GetInt($"{keyPrefix}.responseTimeoutSeconds", 20);
        options.HeartbeatIdleSeconds = configGate.GetInt($"{keyPrefix}.heartbeatIdleSeconds", 60);
        options.HeartbeatTimeoutSeconds = configGate.GetInt($"{keyPrefix}.heartbeatTimeoutSeconds", 120);
        options.ReconnectDelaySeconds = configGate.GetInt($"{keyPrefix}.reconnectDelaySeconds", 5);
    }
}
