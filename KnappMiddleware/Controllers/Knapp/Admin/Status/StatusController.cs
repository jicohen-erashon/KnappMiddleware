using KnappMiddleware.Contratos;
using KnappMiddleware.Configuration;
using KnappMiddleware.RabbitMq;
using KnappMiddleware.Tcp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Controllers.Knapp.Admin.Status;

[ApiController]
[Route("api/v1/status")]
[Tags("Status")]
public sealed class StatusController : ControllerBase
{
    private readonly ICanalPedidoKiSoft _orderChannel;
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly IRabbitMqQueueMonitor _queueMonitor;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ILogger<StatusController> _logger;

    public StatusController(
        ICanalPedidoKiSoft orderChannel,
        ICanalEventoKiSoft eventChannel,
        IRabbitMqQueueMonitor queueMonitor,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        ILogger<StatusController> logger)
    {
        _orderChannel = orderChannel;
        _eventChannel = eventChannel;
        _queueMonitor = queueMonitor;
        _rabbitMqOptions = rabbitMqOptions.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        object fifoQueue;
        try
        {
            var count = await _queueMonitor.GetMessageCountAsync(_rabbitMqOptions.InboundQueue, cancellationToken);
            fifoQueue = new { queue = _rabbitMqOptions.InboundQueue, pendingCount = count, reachable = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo consultar la cola {Queue} para el status.", _rabbitMqOptions.InboundQueue);
            fifoQueue = new { queue = _rabbitMqOptions.InboundQueue, reachable = false, error = ex.Message };
        }

        return Ok(new
        {
            orderChannel = EstadoCanalTcpDto.From("OrderChannel9801", _orderChannel),
            eventChannel = EstadoCanalTcpDto.From("EventChannel9802", _eventChannel),
            fifoQueue
        });
    }
}
