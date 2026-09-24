using KnappMiddleware.Logging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.ErrorHandling;

/// <summary>
/// Red de seguridad: solo se activa cuando un endpoint deja escapar una excepción sin capturarla (los
/// controladores manejan sus propios errores esperados con Problem(502), p. ej. Postgres/RabbitMq caídos).
/// Cualquier excepción que llegue hasta acá es un fallo verdaderamente inesperado, así que responde 500.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly FileLoggingOptions _fileLoggingOptions;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IOptions<FileLoggingOptions> fileLoggingOptions, ILogger<GlobalExceptionHandler> logger)
    {
        _fileLoggingOptions = fileLoggingOptions.Value;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Excepción no controlada en {Path}.", httpContext.Request.Path);
        await WriteExceptionLogAsync(httpContext, exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ocurrió un error inesperado.",
                Detail = exception.Message
            },
            cancellationToken);

        return true;
    }

    private async Task WriteExceptionLogAsync(HttpContext httpContext, Exception exception)
    {
        try
        {
            var timestamp = DateTime.Now;
            var directory = LogFilePaths.BuildDailyDirectory(_fileLoggingOptions.BasePath, "Exceptions", timestamp);
            Directory.CreateDirectory(directory);

            var filePath = Path.Combine(directory, $"log-{LogFilePaths.Timestamp(timestamp)}.txt");
            var content = $"""
                Timestamp: {timestamp:O}
                Method: {httpContext.Request.Method}
                Path: {httpContext.Request.Path}
                Exception: {exception}
                """;

            await File.WriteAllTextAsync(filePath, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo escribir el archivo de log de excepción para {Path}.", httpContext.Request.Path);
        }
    }
}
