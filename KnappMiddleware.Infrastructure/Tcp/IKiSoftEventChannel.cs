namespace KnappMiddleware.Infrastructure.Tcp;

/// <summary>Canal 9802: eventos que KiSoft empuja hacia el middleware (confirmaciones, inventario, etc.).</summary>
public interface IKiSoftEventChannel : IKiSoftTcpChannel
{
    /// <summary>Se invoca por cada trama de evento recibida (ya sin delimitadores). Sin buffer de entrega.</summary>
    event Func<string, CancellationToken, Task>? TelegramReceived;
}
