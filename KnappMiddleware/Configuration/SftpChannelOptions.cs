namespace KnappMiddleware.Configuration;

/// <summary>Credenciales de conexión comunes a un canal SFTP hacia KiSoft One.</summary>
public abstract class SftpChannelOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? PrivateKeyPassphrase { get; set; }

    /// <summary>Rellena los campos comunes (Host/Port/Username/Password/clave privada) desde la tabla configuracion.</summary>
    protected static void PopulateChannelFields(SftpChannelOptions options, ClsConfigGate configGate, string keyPrefix)
    {
        options.Host = configGate.GetValue($"{keyPrefix}.host")
            ?? throw new InvalidOperationException($"Falta la clave '{keyPrefix}.host' en la tabla configuracion.");
        options.Port = configGate.GetInt($"{keyPrefix}.port", 22);
        options.Username = configGate.GetValue($"{keyPrefix}.username")
            ?? throw new InvalidOperationException($"Falta la clave '{keyPrefix}.username' en la tabla configuracion.");
        options.Password = configGate.GetValue($"{keyPrefix}.password");
        options.PrivateKeyPath = configGate.GetValue($"{keyPrefix}.privateKeyPath");
        options.PrivateKeyPassphrase = configGate.GetValue($"{keyPrefix}.privateKeyPassphrase");
    }
}
