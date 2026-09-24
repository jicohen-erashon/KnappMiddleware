namespace KnappMiddleware.Logging;

internal static class LogFilePaths
{
    public static string BuildDailyDirectory(string basePath, string category, DateTime timestamp) =>
        Path.Combine(basePath, category, timestamp.ToString("yyyy"), timestamp.ToString("MM"), timestamp.ToString("dd"));

    public static string Timestamp(DateTime value) => value.ToString("yyyy-MM-dd-HHmmss.fff");

    public static string SanitizeEndpoint(PathString path)
    {
        var trimmed = path.Value?.Trim('/');
        return string.IsNullOrEmpty(trimmed) ? "root" : trimmed.Replace('/', '-');
    }
}
