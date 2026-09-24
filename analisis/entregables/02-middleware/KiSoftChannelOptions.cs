namespace KnappMiddleware.Telegramas.Dispatching;

/// <summary>
/// Opciones de los dos canales TCP/IP con KiSoft. Copia comentada del existente con foco
/// en los puntos de fricción (KNAPP es el cuello de botella, no SAP).
/// </summary>
public sealed class KiSoftChannelOptions
{
    /// <summary>Puerto "Host → KiSoft": el Host es cliente, KiSoft es servidor.</summary>
    public KiSoftOutboundOptions Outbound { get; set; } = new();

    /// <summary>Puerto "KiSoft → Host": KiSoft publica eventos; el Host también es cliente del mismo servidor.</summary>
    public KiSoftInboundOptions Inbound { get; set; } = new();
}

public sealed class KiSoftOutboundOptions
{
    /// <summary>Puerto fijo 9801 (HISP §2.4).</summary>
    public int Port { get; set; } = 9801;
    public string Host { get; set; } = "127.0.0.1";
    public int ConnectRetryMs { get; set; } = 2_000;
    public int MaxReconnectBackoffMs { get; set; } = 30_000;

    /// <summary>HISP §2.7: ventana en la que DEBE llegar el ack antes de cortar.</summary>
    public TimeSpan AckTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>HISP §2.7: doble timeout = forzar reconexión desde el cliente.</summary>
    public TimeSpan HeartbeatDoubleTimeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>Knapp es lento — buffer chico para forzar backpressure al SAP HTTP.</summary>
    public int OutboundQueueCapacity { get; set; } = 64;
}

public sealed class KiSoftInboundOptions
{
    public int Port { get; set; } = 9802;
    public string Host { get; set; } = "127.0.0.1";
    public int BufferBytes { get; set; } = 16 * 1024;

    /// <summary>Una sola trama por entrega → el dispatcher las procesa en serie.</summary>
    public int InboundQueueCapacity { get; set; } = 1024;
}
