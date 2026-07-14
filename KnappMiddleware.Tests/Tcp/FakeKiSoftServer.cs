using System.Net;
using System.Net.Sockets;
using System.Text;
using KnappMiddleware.Domain.Telegramas;

namespace KnappMiddleware.Tests.Tcp;

/// <summary>Servidor TCP de loopback que simula el lado KiSoft para probar los canales sin sockets reales.</summary>
internal sealed class FakeKiSoftServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly TelegramFrameReader _reader = new();
    private TcpClient? _client;
    private NetworkStream? _stream;

    public FakeKiSoftServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public async Task AcceptAsync(CancellationToken cancellationToken = default)
    {
        _client = await _listener.AcceptTcpClientAsync(cancellationToken);
        _stream = _client.GetStream();
    }

    public async Task<string> ReceiveFrameAsync(CancellationToken cancellationToken = default)
    {
        var stream = _stream ?? throw new InvalidOperationException("No hay cliente conectado.");
        var buffer = new byte[4096];

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                throw new IOException("El cliente cerró la conexión.");
            }

            var frames = _reader.Feed(Encoding.ASCII.GetString(buffer, 0, bytesRead));
            if (frames.Count > 0)
            {
                return frames[0];
            }
        }
    }

    public async Task SendFrameAsync(string data, CancellationToken cancellationToken = default)
    {
        var stream = _stream ?? throw new InvalidOperationException("No hay cliente conectado.");
        var bytes = Encoding.ASCII.GetBytes(TelegramFrameCodec.Encode(data));
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public void CloseClientConnection()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
    }

    public ValueTask DisposeAsync()
    {
        CloseClientConnection();
        _listener.Stop();
        return ValueTask.CompletedTask;
    }
}
