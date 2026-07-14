using KnappMiddleware.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Infrastructure.Sftp;

/// <summary>Mismo contrato que <see cref="ISftpService"/>, con las credenciales del canal de inventario.</summary>
public interface IInventorySftpService : ISftpService
{
}

public sealed class InventorySftpService : SftpService, IInventorySftpService
{
    public InventorySftpService(IOptions<InventorySftpOptions> options) : base(options.Value)
    {
    }
}
