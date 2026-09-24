using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Logging;

/// <summary>
/// Registra cada request/response HTTP como archivos JSON separados en
/// {BasePath}/JSON/año/mes/dia/, correlacionados por un GUID v7 compartido.
/// </summary>
public sealed class RequestResponseLoggingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly RequestDelegate _next;
    private readonly FileLoggingOptions _options;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, IOptions<FileLoggingOptions> options, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = Guid.CreateVersion7();
        var endpoint = LogFilePaths.SanitizeEndpoint(context.Request.Path);

        await WriteRequestLogAsync(context, requestId, endpoint);

        var originalResponseBody = context.Response.Body;
        using var capturedResponseBody = new MemoryStream();
        context.Response.Body = capturedResponseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            await WriteResponseLogAsync(context, requestId, endpoint, capturedResponseBody);

            capturedResponseBody.Position = 0;
            context.Response.Body = originalResponseBody;
            await capturedResponseBody.CopyToAsync(originalResponseBody);
        }
    }

    private async Task WriteRequestLogAsync(HttpContext context, Guid requestId, string endpoint)
    {
        try
        {
            var request = context.Request;
            request.EnableBuffering();

            string body;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
            }
            request.Body.Position = 0;

            var timestamp = DateTime.Now;
            var entry = new
            {
                requestId,
                timestamp,
                method = request.Method,
                path = request.Path.Value,
                endpoint,
                queryString = request.QueryString.Value,
                headers = HeaderSnapshot(request.Headers),
                body
            };

            await WriteJsonAsync("request", requestId, endpoint, timestamp, entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo registrar el request JSON para {Endpoint}.", endpoint);
        }
    }

    private async Task WriteResponseLogAsync(HttpContext context, Guid requestId, string endpoint, MemoryStream capturedResponseBody)
    {
        try
        {
            capturedResponseBody.Position = 0;
            using var reader = new StreamReader(capturedResponseBody, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var body = await reader.ReadToEndAsync();

            var response = context.Response;
            var timestamp = DateTime.Now;
            var entry = new
            {
                requestId,
                timestamp,
                endpoint,
                statusCode = response.StatusCode,
                headers = HeaderSnapshot(response.Headers),
                body
            };

            await WriteJsonAsync("response", requestId, endpoint, timestamp, entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo registrar el response JSON para {Endpoint}.", endpoint);
        }
    }

    private async Task WriteJsonAsync(string kind, Guid requestId, string endpoint, DateTime timestamp, object entry)
    {
        var directory = LogFilePaths.BuildDailyDirectory(_options.BasePath, "JSON", timestamp);
        Directory.CreateDirectory(directory);

        var fileName = $"{kind}_{requestId}_{endpoint}_{LogFilePaths.Timestamp(timestamp)}.json";
        var filePath = Path.Combine(directory, fileName);

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(entry, JsonOptions));
    }

    private static Dictionary<string, string> HeaderSnapshot(IHeaderDictionary headers)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            snapshot[header.Key] = string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase)
                ? "***"
                : header.Value.ToString();
        }

        return snapshot;
    }
}
