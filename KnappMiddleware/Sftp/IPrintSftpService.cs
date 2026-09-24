using KnappMiddleware.Configuration;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Sftp;

/// <summary>Mismo contrato que <see cref="ISftpService"/>, con las credenciales del canal de impresión.</summary>
public interface IPrintSftpService : ISftpService
{
}

public sealed class PrintSftpService : SftpService, IPrintSftpService
{
    public PrintSftpService(IOptions<PrintSftpOptions> options) : base(options.Value)
    {
    }
}
