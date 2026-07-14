namespace KnappMiddleware.Infrastructure.Configuration;

/// <summary>Canal SFTP para recoger el InventorySnapshot tras el aviso 3RR (usuario típico "customer_osr").</summary>
public sealed class InventorySftpOptions : SftpChannelOptions
{
    public const string SectionName = "Sftp:Inventory";

    public required string InboundDirectory { get; set; }
    public required string OutboundDirectory { get; set; }
}
