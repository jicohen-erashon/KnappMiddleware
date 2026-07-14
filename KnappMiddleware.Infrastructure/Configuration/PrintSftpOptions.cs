namespace KnappMiddleware.Infrastructure.Configuration;

/// <summary>Canal SFTP para el push de datos de impresión (albarán/etiqueta) Host -> KiSoft One (usuario típico "sftpuser").</summary>
public sealed class PrintSftpOptions : SftpChannelOptions
{
    public const string SectionName = "Sftp:Print";

    public required string OutboundDirectory { get; set; }
}
