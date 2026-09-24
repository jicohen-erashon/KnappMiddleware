namespace KnappMiddleware.Configuration;

/// <summary>Canal SFTP para el push de datos de impresión (albarán/etiqueta) Host -> KiSoft One (usuario típico "sftpuser").</summary>
public sealed class PrintSftpOptions : SftpChannelOptions
{
    public string OutboundDirectory { get; set; } = string.Empty;

    public static PrintSftpOptions ReadFrom(ClsConfigGate configGate)
    {
        var options = new PrintSftpOptions
        {
            OutboundDirectory = configGate.GetValue("sftp.print.outboundDirectory")
                ?? throw new InvalidOperationException("Falta la clave 'sftp.print.outboundDirectory' en la tabla configuracion.")
        };
        PopulateChannelFields(options, configGate, "sftp.print");
        return options;
    }
}
