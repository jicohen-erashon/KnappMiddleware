namespace KnappMiddleware.Infrastructure.Configuration;

/// <summary>Credenciales de conexión comunes a un canal SFTP hacia KiSoft One.</summary>
public abstract class SftpChannelOptions
{
    public required string Host { get; set; }
    public int Port { get; set; } = 22;
    public required string Username { get; set; }
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? PrivateKeyPassphrase { get; set; }
}
