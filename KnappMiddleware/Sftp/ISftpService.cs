namespace KnappMiddleware.Sftp;

public interface ISftpService
{
    Task<IReadOnlyList<string>> ListFilesAsync(string remoteDirectory, CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(string remoteFilePath, CancellationToken cancellationToken = default);

    Task UploadAsync(string remoteFilePath, Stream content, CancellationToken cancellationToken = default);

    Task DeleteAsync(string remoteFilePath, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string remoteFilePath, CancellationToken cancellationToken = default);
}
