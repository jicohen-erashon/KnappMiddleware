namespace KnappMiddleware.Infrastructure.Tcp;

/// <summary>Canal 9801: envío síncrono de un telegrama y espera de su estado (22N, 24N…) por el mismo socket.</summary>
public interface IKiSoftOrderChannel : IKiSoftTcpChannel
{
    /// <summary>
    /// Envía el telegrama y espera la siguiente trama como respuesta (FIFO estricto: un solo
    /// telegrama en vuelo). El timeout se ajusta a la ventana de estado de KiSoft (10-30s).
    /// </summary>
    Task<string> SendAsync(string telegramData, CancellationToken cancellationToken = default);
}
