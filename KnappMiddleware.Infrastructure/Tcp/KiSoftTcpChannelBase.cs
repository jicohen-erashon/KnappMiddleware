using System.Net.Sockets;
using System.Text;
using KnappMiddleware.Domain.Telegramas;
using KnappMiddleware.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Infrastructure.Tcp;

/// <summary>
/// Base común de los canales 9801/9802: el middleware es siempre el cliente que abre y
/// reconecta la conexión. Serializa los envíos en FIFO estricto (un solo telegrama en vuelo),
/// correlaciona la respuesta de forma posicional vía <see cref="TaskCompletionSource{TResult}"/>
/// (sin persistirla) y mantiene el heartbeat 1HR/2HR/3HR/4HR con reconexión automática.
/// </summary>
public abstract class KiSoftTcpChannelBase : IKiSoftTcpChannel
{
    private readonly KiSoftTcpChannelOptions _options;
    private readonly ILogger _logger;
    private readonly TelegramFrameReader _frameReader = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _requestGate = new(1, 1);

    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _lifetimeCts;
    private Task? _connectionLoopTask;
    private TaskCompletionSource<string>? _pendingResponse;
    private DateTimeOffset _lastSendUtc;
    private DateTimeOffset _lastReceiveUtc;
    private volatile bool _hasConnectedOnce;
    private int _connectCount;

    protected KiSoftTcpChannelBase(KiSoftTcpChannelOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsConnected => _client?.Connected == true;

    public DateTimeOffset? LastActivityUtc => _hasConnectedOnce ? LatestActivityUtc() : null;

    public int ReconnectCount => Math.Max(0, Volatile.Read(ref _connectCount) - 1);

    protected KiSoftTcpChannelOptions Options => _options;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _connectionLoopTask = RunConnectionLoopAsync(_lifetimeCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _lifetimeCts?.Cancel();

        if (_connectionLoopTask is not null)
        {
            try
            {
                await _connectionLoopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        DisposeConnection();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _lifetimeCts?.Dispose();
        _writeLock.Dispose();
        _requestGate.Dispose();
    }

    /// <summary>Cierra el socket actual; el bucle de conexión ya en marcha detecta la caída y reconecta.</summary>
    public Task ForceReconnectAsync(CancellationToken cancellationToken = default)
    {
        ForceDisconnect();
        FailPendingResponse(new OperationCanceledException("Reconexión forzada por operador."));
        return Task.CompletedTask;
    }

    /// <summary>Maneja una trama recibida que no corresponde a ninguna solicitud pendiente ni a un heartbeat.</summary>
    protected abstract Task OnUnsolicitedFrameAsync(string data, CancellationToken cancellationToken);

    /// <summary>Envía un telegrama y espera la siguiente trama del socket como su respuesta (correlación posicional).</summary>
    protected async Task<string> SendAndAwaitResponseAsync(string telegramData, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Exchange(ref _pendingResponse, tcs);

            await WriteFrameAsync(telegramData, cancellationToken);

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            await using var registration = linkedCts.Token.Register(static state => ((TaskCompletionSource<string>)state!).TrySetCanceled(), tcs);

            try
            {
                return await tcs.Task;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout de RPC: la conexión queda en un estado no confiable (la respuesta tardía
                // podría llegar después y romper la correlación posicional del siguiente envío),
                // así que se fuerza el cierre para que el bucle de conexión reconecte.
                ForceDisconnect();
                throw new TimeoutException(
                    $"Tiempo de espera agotado ({timeout.TotalSeconds}s) esperando respuesta de KiSoft para '{telegramData}'.");
            }
            finally
            {
                Interlocked.CompareExchange(ref _pendingResponse, null, tcs);
            }
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private async Task RunConnectionLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(cancellationToken);
                await ReadLoopAndHeartbeatAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Canal KiSoft {Channel} desconectado ({Host}:{Port}); reintentando.",
                    GetType().Name, _options.Host, _options.Port);
            }
            finally
            {
                DisposeConnection();
                FailPendingResponse(new IOException("Conexión con KiSoft perdida."));
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_options.ReconnectDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        using var timeoutCts = new CancellationTokenSource(_options.ConnectTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            client.Dispose();
            throw new TimeoutException(
                $"No se pudo conectar a KiSoft en {_options.Host}:{_options.Port} en {_options.ConnectTimeout.TotalSeconds}s.");
        }

        _client = client;
        _stream = client.GetStream();
        _frameReader.Reset();

        var now = DateTimeOffset.UtcNow;
        _lastSendUtc = now;
        _lastReceiveUtc = now;
        _hasConnectedOnce = true;
        Interlocked.Increment(ref _connectCount);

        _logger.LogInformation("Canal KiSoft {Channel} conectado a {Host}:{Port}.", GetType().Name, _options.Host, _options.Port);
    }

    private async Task ReadLoopAndHeartbeatAsync(CancellationToken cancellationToken)
    {
        using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var readTask = ReadLoopAsync(connectionCts.Token);
        var heartbeatTask = HeartbeatMonitorAsync(connectionCts.Token);

        var finished = await Task.WhenAny(readTask, heartbeatTask);
        connectionCts.Cancel();

        await SwallowCancellationAsync(readTask);
        await SwallowCancellationAsync(heartbeatTask);

        await finished;
    }

    private static async Task SwallowCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // La excepción "real" (si la hay) ya se propaga vía `finished` en el llamador.
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var stream = _stream ?? throw new InvalidOperationException("El canal no está conectado.");

        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                throw new IOException("El servidor KiSoft cerró la conexión.");
            }

            _lastReceiveUtc = DateTimeOffset.UtcNow;
            var text = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            IReadOnlyList<string> frames;
            try
            {
                frames = _frameReader.Feed(text);
            }
            catch (TelegramFormatException ex)
            {
                _logger.LogError(ex, "Trama KiSoft malformada recibida en {Channel}.", GetType().Name);
                continue;
            }

            foreach (var frame in frames)
            {
                await DispatchFrameAsync(frame, cancellationToken);
            }
        }
    }

    private async Task DispatchFrameAsync(string data, CancellationToken cancellationToken)
    {
        if (string.Equals(data, KiSoftHeartbeat.IncomingPing, StringComparison.Ordinal))
        {
            await WriteFrameAsync(KiSoftHeartbeat.IncomingPingAck, cancellationToken);
            return;
        }

        var pending = Interlocked.Exchange(ref _pendingResponse, null);
        if (pending is not null)
        {
            pending.TrySetResult(data);
            return;
        }

        await OnUnsolicitedFrameAsync(data, cancellationToken);
    }

    private async Task HeartbeatMonitorAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);

            var idle = DateTimeOffset.UtcNow - LatestActivityUtc();
            if (idle >= _options.HeartbeatTimeout)
            {
                throw new TimeoutException(
                    $"Sin heartbeat ni tráfico en {_options.HeartbeatTimeout.TotalSeconds}s en {GetType().Name}; se reconecta.");
            }

            if (idle >= _options.HeartbeatIdle)
            {
                var ack = await SendAndAwaitResponseAsync(KiSoftHeartbeat.OutgoingPing, _options.ResponseTimeout, cancellationToken);
                if (!string.Equals(ack, KiSoftHeartbeat.OutgoingPingAck, StringComparison.Ordinal))
                {
                    _logger.LogWarning("Respuesta de heartbeat inesperada en {Channel}: '{Ack}'.", GetType().Name, ack);
                }
            }
        }
    }

    private DateTimeOffset LatestActivityUtc() => _lastSendUtc > _lastReceiveUtc ? _lastSendUtc : _lastReceiveUtc;

    private async Task WriteFrameAsync(string telegramData, CancellationToken cancellationToken)
    {
        var frame = TelegramFrameCodec.Encode(telegramData);
        var bytes = Encoding.ASCII.GetBytes(frame);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var stream = _stream ?? throw new InvalidOperationException("El canal no está conectado.");
            await stream.WriteAsync(bytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            _lastSendUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void FailPendingResponse(Exception exception)
    {
        var pending = Interlocked.Exchange(ref _pendingResponse, null);
        pending?.TrySetException(exception);
    }

    private void ForceDisconnect()
    {
        try
        {
            _stream?.Dispose();
        }
        catch
        {
        }

        try
        {
            _client?.Dispose();
        }
        catch
        {
        }
    }

    private void DisposeConnection()
    {
        try
        {
            _stream?.Dispose();
        }
        catch
        {
        }

        try
        {
            _client?.Dispose();
        }
        catch
        {
        }

        _stream = null;
        _client = null;
    }
}
