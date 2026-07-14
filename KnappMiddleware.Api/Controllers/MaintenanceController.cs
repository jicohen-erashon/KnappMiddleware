using KnappMiddleware.Infrastructure.Configuration;
using KnappMiddleware.Infrastructure.RabbitMq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Api.Controllers;

[ApiController]
public sealed class MaintenanceController : ControllerBase
{
    private readonly IRabbitMqQueueMonitor _queueMonitor;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(IRabbitMqQueueMonitor queueMonitor, IOptions<RabbitMqOptions> options, ILogger<MaintenanceController> logger)
    {
        _queueMonitor = queueMonitor;
        _options = options.Value;
        _logger = logger;
    }

    // Acción destructiva: descarta todos los mensajes pendientes de la cola FIFO. Protegida por el
    // esquema Basic global (ver Program.cs) igual que el resto de la Api salvo /health.
    [HttpPost("clear")]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        try
        {
            var purged = await _queueMonitor.PurgeAsync(_options.InboundQueue, cancellationToken);
            _logger.LogWarning("Cola {Queue} purgada manualmente: {Count} mensajes descartados.", _options.InboundQueue, purged);
            return Ok(new { queue = _options.InboundQueue, purgedCount = purged });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo purgar la cola.");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo purgar la cola.");
        }
    }
}
