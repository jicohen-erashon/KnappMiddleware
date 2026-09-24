using KnappMiddleware.Configuration;
using Renci.SshNet;

namespace KnappMiddleware.Sftp;

public class SftpService : ISftpService
{
    private readonly SftpChannelOptions _options;

    public SftpService(SftpChannelOptions options)
    {
        _options = options;
    }

    public async Task<IReadOnlyList<string>> ListFilesAsync(string remoteDirectory, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        return await Task.Run(() =>
        {
            client.Connect();
            try
            {
                return (IReadOnlyList<string>)client.ListDirectory(remoteDirectory)
                    .Where(f => f.IsRegularFile)
                    .Select(f => f.FullName)
                    .ToList();
            }
            finally
            {
                client.Disconnect();
            }
        }, cancellationToken);
    }

    public async Task<Stream> DownloadAsync(string remoteFilePath, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        return await Task.Run(() =>
        {
            client.Connect();
            try
            {
                var buffer = new MemoryStream();
                client.DownloadFile(remoteFilePath, buffer);
                buffer.Position = 0;
                return (Stream)buffer;
            }
            finally
            {
                client.Disconnect();
            }
        }, cancellationToken);
    }

    public async Task UploadAsync(string remoteFilePath, Stream content, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        await Task.Run(() =>
        {
            client.Connect();
            try
            {
                client.UploadFile(content, remoteFilePath, canOverride: true);
            }
            finally
            {
                client.Disconnect();
            }
        }, cancellationToken);
    }

    public async Task DeleteAsync(string remoteFilePath, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        await Task.Run(() =>
        {
            client.Connect();
            try
            {
                client.DeleteFile(remoteFilePath);
            }
            finally
            {
                client.Disconnect();
            }
        }, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string remoteFilePath, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        return await Task.Run(() =>
        {
            client.Connect();
            try
            {
                return client.Exists(remoteFilePath);
            }
            finally
            {
                client.Disconnect();
            }
        }, cancellationToken);
    }

    private SftpClient CreateClient()
    {
        var authMethods = new List<AuthenticationMethod>();

        if (!string.IsNullOrEmpty(_options.PrivateKeyPath))
        {
            var keyFile = string.IsNullOrEmpty(_options.PrivateKeyPassphrase)
                ? new PrivateKeyFile(_options.PrivateKeyPath)
                : new PrivateKeyFile(_options.PrivateKeyPath, _options.PrivateKeyPassphrase);
            authMethods.Add(new PrivateKeyAuthenticationMethod(_options.Username, keyFile));
        }

        if (!string.IsNullOrEmpty(_options.Password))
        {
            authMethods.Add(new PasswordAuthenticationMethod(_options.Username, _options.Password));
        }

        var connectionInfo = new Renci.SshNet.ConnectionInfo(_options.Host, _options.Port, _options.Username, authMethods.ToArray());
        return new SftpClient(connectionInfo);
    }
}
