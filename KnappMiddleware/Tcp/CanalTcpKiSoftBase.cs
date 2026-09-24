using System.Net.Sockets;
using System.Text;
using KnappMiddleware.Telegramas;
using KnappMiddleware.Configuration;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Tcp;

/// <summary>
/// Base común de los canales 9801/9802: el middleware es siempre el cliente que abre y
/// reconecta la conexión. Serializa los envíos en FIFO estricto (un solo telegrama en vuelo),
/// correlaciona la respuesta de forma posicional vía <see cref="TaskCompletionSource{TResult}"/>
/// (sin persistirla) y mantiene el heartbeat 1HR/2HR/3HR/4HR con reconexión automática.
/// </summary>
public abstract class CanalTcpKiSoftBase : ICanalTcpKiSoft
{
    private readonly ILogger _logger;
    private readonly LectorTramaTelegrama _frameReader = new();
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
    private int _retryAttempt;

    protected CanalTcpKiSoftBase(ILogger logger)
    {
        _logger = logger;
    }

    public bool IsConnected => _client?.Connected == true;

    public DateTimeOffset? LastActivityUtc => _hasConnectedOnce ? LatestActivityUtc() : null;

    public int ReconnectCount => Math.Max(0, Volatile.Read(ref _connectCount) - 1);

    /// <summary>
    /// Lee la configuración vigente desde la tabla configuracion. Se llama en cada intento de conexión
    /// y en cada vuelta del monitor de heartbeat, nunca se cachea en un campo — así el ajuste de
    /// host/puerto/timeouts es en caliente (ver <see cref="Configuration.KiSoftTcpChannelOptions.PopulateFrom"/>).
    /// </summary>
    protected abstract KiSoftTcpChannelOptions ReadOptions();

    protected KiSoftTcpChannelOptions Options => ReadOptions();

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

    /// <summary>
    /// Escribe una trama sin esperar respuesta (p. ej. el mensaje de estado 42R que el canal 9802
    /// envía como acuse de un evento empujado por KiSoft). No pasa por el FIFO de solicitud/respuesta
    /// porque no correlaciona con nada pendiente.
    /// </summary>
    protected Task SendFrameAsync(string telegramData, CancellationToken cancellationToken) =>
        WriteFrameAsync(telegramData, cancellationToken);

    /// <summary>Envía un telegrama y espera la siguiente trama del socket como su respuesta (correlación posicional).</summary>
    protected async Task<string> SendAndAwaitResponseAsync(string telegramData, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            return await SendAndAwaitResponseCoreAsync(telegramData, timeout, cancellationToken);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    /// <summary>
    /// Envía varios telegramas en secuencia sin soltar el FIFO entre ellos (p. ej. abrir bloque de datos
    /// maestros → registro → cerrar bloque, HIS §3.1: "en el tiempo entre el inicio y el cierre de una
    /// transmisión de datos maestros no se deberán transmitir datos de pedido"). Si un envío de la
    /// secuencia falla (timeout), el resto se aborta.
    /// </summary>
    protected async Task<IReadOnlyList<string>> SendSequenceAndAwaitResponsesAsync(
        IReadOnlyList<string> telegrams, TimeSpan timeoutPerMessage, CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            var responses = new List<string>(telegrams.Count);
            foreach (var telegramData in telegrams)
            {
                responses.Add(await SendAndAwaitResponseCoreAsync(telegramData, timeoutPerMessage, cancellationToken));
            }

            return responses;
        }
        finally
        {
            _requestGate.Release();
        }
    }

    /// <summary>Cuerpo de un envío+espera individual, sin adquirir <see cref="_requestGate"/> (el llamador ya lo sostiene).</summary>
    private async Task<string> SendAndAwaitResponseCoreAsync(string telegramData, TimeSpan timeout, CancellationToken cancellationToken)
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

    private async Task RunConnectionLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var options = ReadOptions();
            try
            {
                await ConnectAsync(options, cancellationToken);
                await ReadLoopAndHeartbeatAsync(cancellationToken);
                if (Volatile.Read(ref _retryAttempt) > 0)
                {
                    _logger.LogInformation("Canal KiSoft {Channel} reconectado a {Host}:{Port} tras {Attempts} intentos.",
                        GetType().Name, options.Host, options.Port, Volatile.Read(ref _retryAttempt));
                    Volatile.Write(ref _retryAttempt, 0);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                var attempt = Interlocked.Increment(ref _retryAttempt);
                if (ShouldLogReconnect(attempt))
                {
                    _logger.LogWarning("Canal KiSoft {Channel} intento #{Attempt} fallo ({Host}:{Port}): {Reason}. Reintentando en {Delay}s.",
                        GetType().Name, attempt, options.Host, options.Port,
                        ShortReason(ex), options.ReconnectDelay.TotalSeconds);
                }
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
                    await Task.Delay(options.ReconnectDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private static bool ShouldLogReconnect(int attempt) =>
        attempt == 1 || attempt == 5 || attempt == 30
        || attempt == 60 || attempt == 300 || attempt == 900
        || attempt == 3600;

    private static string ShortReason(Exception ex) => ex switch
    {
        SocketException se => $"SocketErrorCode={(int)se.SocketErrorCode} ({se.SocketErrorCode})",
        TimeoutException => "Timeout",
        IOException io => $"IO: {FirstLine(io.Message)}",
        _ => $"{ex.GetType().Name}: {FirstLine(ex.Message)}"
    };

    private static string FirstLine(string s)
    {
        var i = s.IndexOfAny(new[] { '\r', '\n' });
        return i < 0 ? s : s[..i];
    }

    private async Task ConnectAsync(KiSoftTcpChannelOptions options, CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        using var timeoutCts = new CancellationTokenSource(options.ConnectTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await client.ConnectAsync(options.Host, options.Port, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            client.Dispose();
            throw new TimeoutException(
                $"No se pudo conectar a KiSoft en {options.Host}:{options.Port} en {options.ConnectTimeout.TotalSeconds}s.");
        }

        _client = client;
        _stream = client.GetStream();
        _frameReader.Reset();

        var now = DateTimeOffset.UtcNow;
        _lastSendUtc = now;
        _lastReceiveUtc = now;
        _hasConnectedOnce = true;
        Interlocked.Increment(ref _connectCount);

        _logger.LogInformation("Canal KiSoft {Channel} conectado a {Host}:{Port}.", GetType().Name, options.Host, options.Port);
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
            catch (ExcepcionFormatoTelegrama ex)
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
        if (string.Equals(data, LatidoKiSoft.PingEntrante, StringComparison.Ordinal))
        {
            await WriteFrameAsync(LatidoKiSoft.AcusePingEntrante, cancellationToken);
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

            var options = ReadOptions();
            var idle = DateTimeOffset.UtcNow - LatestActivityUtc();
            if (idle >= options.HeartbeatTimeout)
            {
                throw new TimeoutException(
                    $"Sin heartbeat ni tráfico en {options.HeartbeatTimeout.TotalSeconds}s en {GetType().Name}; se reconecta.");
            }

            if (idle >= options.HeartbeatIdle)
            {
                var ack = await SendAndAwaitResponseAsync(LatidoKiSoft.PingSaliente, options.ResponseTimeout, cancellationToken);
                if (!string.Equals(ack, LatidoKiSoft.AcusePingSaliente, StringComparison.Ordinal))
                {
                    _logger.LogWarning("Respuesta de heartbeat inesperada en {Channel}: '{Ack}'.", GetType().Name, ack);
                }
            }
        }
    }

    private DateTimeOffset LatestActivityUtc() => _lastSendUtc > _lastReceiveUtc ? _lastSendUtc : _lastReceiveUtc;

    private async Task WriteFrameAsync(string telegramData, CancellationToken cancellationToken)
    {
        var frame = CodecTramaTelegrama.Encode(telegramData);
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
