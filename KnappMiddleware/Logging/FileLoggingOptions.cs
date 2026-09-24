namespace KnappMiddleware.Logging;

public sealed class FileLoggingOptions
{
    public const string SectionName = "FileLogging";

    public required string BasePath { get; set; }
}
