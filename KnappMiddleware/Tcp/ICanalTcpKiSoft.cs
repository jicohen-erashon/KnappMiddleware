namespace KnappMiddleware.Tcp;

/// <summary>Canal TCP persistente y auto-reconectante hacia KiSoft One (puerto 9801 o 9802).</summary>
public interface ICanalTcpKiSoft : IAsyncDisposable
{
    bool IsConnected { get; }

    /// <summary>Momento del último envío o recepción (heartbeat o tráfico normal); null si nunca se conectó.</summary>
    DateTimeOffset? LastActivityUtc { get; }

    /// <summary>Cantidad de veces que el canal se ha reconectado desde que arrancó (excluye la conexión inicial).</summary>
    int ReconnectCount { get; }

    /// <summary>Abre la conexión y arranca el bucle de lectura/heartbeat/reconexión en segundo plano.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Detiene el bucle de conexión y cierra el socket.</summary>
    Task StopAsync();

    /// <summary>Fuerza el cierre del socket actual; el bucle de conexión lo detecta y reconecta según su lógica normal.</summary>
    Task ForceReconnectAsync(CancellationToken cancellationToken = default);
}
