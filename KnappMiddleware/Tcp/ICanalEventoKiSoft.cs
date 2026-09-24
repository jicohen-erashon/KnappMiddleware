namespace KnappMiddleware.Tcp;

/// <summary>Canal 9802: eventos que KiSoft empuja hacia el middleware (confirmaciones, inventario, etc.).</summary>
public interface ICanalEventoKiSoft : ICanalTcpKiSoft
{
    /// <summary>Se invoca por cada trama de evento recibida (ya sin delimitadores). Sin buffer de entrega.</summary>
    event Func<string, CancellationToken, Task>? TelegramReceived;

    /// <summary>
    /// Escribe el mensaje de estado (p. ej. "42R00") que acusa la trama recibida. Debe llamarse SIEMPRE,
    /// en ≤10s, sin importar qué pase después (gate de matriz, webhook a SAP) — HIS §2.6.
    /// </summary>
    Task AcknowledgeAsync(string statusTelegramData, CancellationToken cancellationToken = default);
}
