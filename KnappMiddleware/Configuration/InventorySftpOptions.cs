namespace KnappMiddleware.Configuration;

/// <summary>Canal SFTP para recoger el InventorySnapshot tras el aviso 3RR (usuario típico "customer_osr").</summary>
public sealed class InventorySftpOptions : SftpChannelOptions
{
    public string InboundDirectory { get; set; } = string.Empty;
    public string OutboundDirectory { get; set; } = string.Empty;

    public static InventorySftpOptions ReadFrom(ClsConfigGate configGate)
    {
        var options = new InventorySftpOptions
        {
            InboundDirectory = configGate.GetValue("sftp.inventory.inboundDirectory") ?? MissingKey("sftp.inventory.inboundDirectory"),
            OutboundDirectory = configGate.GetValue("sftp.inventory.outboundDirectory") ?? MissingKey("sftp.inventory.outboundDirectory")
        };
        PopulateChannelFields(options, configGate, "sftp.inventory");
        return options;
    }

    private static string MissingKey(string clave) =>
        throw new InvalidOperationException($"Falta la clave '{clave}' en la tabla configuracion.");
}
