using KnappMiddleware.Infrastructure.Configuration;
using KnappMiddleware.Infrastructure.Sftp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Api.Controllers;

/// <summary>
/// Push de datos de impresión (albarán/etiqueta) Host -> KiSoft One vía SFTP (spec 8.4). No genera
/// telegrama TCP; el disparo es la disponibilidad del archivo en el Host. Canal SFTP independiente
/// del de inventario (credenciales distintas).
/// </summary>
[ApiController]
[Route("sftp-file")]
public sealed class SftpFileController : ControllerBase
{
    private const long AlbaranMaxBytes = 40 * 1024;
    private const long EtiquetaMaxBytes = 2500;
    private const string AlbaranTipoDocumento = "001";
    private const string EtiquetaTipoDocumento = "008";

    private readonly IPrintSftpService _sftpService;
    private readonly PrintSftpOptions _options;
    private readonly ILogger<SftpFileController> _logger;

    public SftpFileController(IPrintSftpService sftpService, IOptions<PrintSftpOptions> options, ILogger<SftpFileController> logger)
    {
        _sftpService = sftpService;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || string.IsNullOrWhiteSpace(file.FileName))
        {
            return BadRequest(new { error = "Falta el archivo." });
        }

        var fileName = file.FileName;
        var isEndSignal = fileName.EndsWith(".end", StringComparison.OrdinalIgnoreCase);

        if (file.Length == 0 && !isEndSignal)
        {
            return BadRequest(new { error = "No se aceptan archivos de tamaño 0 salvo la señal '.end'." });
        }

        if (!isEndSignal)
        {
            var validationError = ValidateSize(fileName, file.Length);
            if (validationError is not null)
            {
                return BadRequest(new { error = validationError });
            }
        }

        var remotePath = $"{_options.OutboundDirectory.TrimEnd('/')}/{fileName}";

        try
        {
            await using var stream = file.OpenReadStream();
            await _sftpService.UploadAsync(remotePath, stream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar {FileName} a KiSoft One vía SFTP.", fileName);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo enviar el archivo por SFTP.");
        }

        _logger.LogInformation("Archivo de impresión {FileName} enviado a KiSoft One vía SFTP.", fileName);
        return Ok(new { fileName, remotePath });
    }

    private static string? ValidateSize(string fileName, long length)
    {
        var parts = fileName.Split('.');
        var tipoDocumento = parts.Length >= 3 ? parts[2] : null;

        return tipoDocumento switch
        {
            AlbaranTipoDocumento when length > AlbaranMaxBytes => $"El albarán excede el máximo de {AlbaranMaxBytes} bytes.",
            EtiquetaTipoDocumento when length > EtiquetaMaxBytes => $"La etiqueta excede el máximo de {EtiquetaMaxBytes} bytes.",
            _ => null
        };
    }
}
