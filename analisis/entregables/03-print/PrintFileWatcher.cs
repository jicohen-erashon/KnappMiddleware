using System.Threading.Channels;
using KnappMiddleware.Auditing;
using KnappMiddleware.Print;
using Microsoft.Extensions.Logging;
using SSH.NET.Sftp;   // ver proyecto real — esta referencia es la del SftpService existente

namespace KnappMiddleware.Print;

/// <summary>
/// Vigila el canal SFTP de impresión (usuario sftpuser@knapp, dir configurable),
/// descarga cada archivo, lo valida y lo enruta al destino (ABB001 / LBA001 / ABA001).
///
/// Reglas:
///  - KNAPP es el cuello de botella, no el SFTP. Polling cada N segundos.
///  - Auditoría NO bloqueante vía NonBlockingAuditTrail.
///  - Backpressure interno: <see cref="Channel"/> bounded con DropOldest al enrutar.
///  - Reconexión automática si el SFTP se cae.
/// </summary>
public sealed class PrintFileWatcher : BackgroundService
{
    private readonly IPrintSftpService _sftp;
    private readonly NonBlockingAuditTrail _audit;
    private readonly PrintParserOptions _options;
    private readonly ChannelWriter<ValidatedPrintJob> _outbound;
    private readonly ILogger<PrintFileWatcher> _logger;

    public PrintFileWatcher(
        IPrintSftpService sftp,
        NonBlockingAuditTrail audit,
        PrintParserOptions options,
        ChannelWriter<ValidatedPrintJob> outbound,
        ILogger<PrintFileWatcher> logger)
    {
        _sftp = sftp; _audit = audit; _options = options;
        _outbound = outbound; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Set<String> de archivos ya procesados — idempotencia ante retries del SFTP.
        var processed = new HashSet<string>(StringComparer.Ordinal);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var files = await _sftp.ListAsync("./*.zpl ./*.pdf ./*.end", stoppingToken).ConfigureAwait(false);
                foreach (var file in files.OrderBy(f => f).ToList())
                {
                    if (processed.Contains(file)) continue;
                    processed.Add(file);

                    var bytes = await _sftp.DownloadAsync(file, stoppingToken).ConfigureAwait(false);
                    var result = PrintFileParser.TryParse(file, bytes, _options);

                    if (result.IsEndMarker)
                    {
                        _logger.LogInformation("Fin de transmisión recibido: {File}", file);
                        _audit.EnqueueIncoming(new AuditRecord(Guid.NewGuid().ToString("N"),
                            "PRINT_END", "KNAPP_SFTP", "INTERNAL", 0, null));
                        continue; // no se enruta
                    }

                    if (!result.Ok)
                    {
                        _logger.LogWarning("Impresión rechazada {File}: {Reasons}",
                            file, string.Join("; ", result.Reasons));
                        _audit.EnqueueIncoming(new AuditRecord(Guid.NewGuid().ToString("N"),
                            "PRINT", "KNAPP_SFTP", "INTERNAL", 1, null,
                            ErrorDetalle: string.Join("; ", result.Reasons)));
                        // No se reintenta — se notifica y se descarta. Knapp operario se entera por el listado de error.
                        continue;
                    }

                    var payload = new ValidatedPrintJob(result, bytes);
                    _audit.EnqueueIncoming(new AuditRecord(Guid.NewGuid().ToString("N"),
                        "PRINT", "KNAPP_SFTP", result.Station ?? "UNKNOWN", 0, null));
                    if (!await _outbound.WaitToWriteAsync(stoppingToken)) break;
                    if (!_outbound.TryWrite(payload))
                    {
                        _logger.LogWarning("Cola outbound de impresión saturada; se descarta el item más antiguo.");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Fallo en el watcher SFTP. Reintento en 5 s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }
}

public sealed record ValidatedPrintJob(PrintValidation Validation, ReadOnlyMemory<byte> Payload);
