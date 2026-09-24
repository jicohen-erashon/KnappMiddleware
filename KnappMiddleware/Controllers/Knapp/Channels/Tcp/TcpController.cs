using KnappMiddleware.Contratos;
using KnappMiddleware.Configuration;
using KnappMiddleware.RabbitMq;
using KnappMiddleware.Tcp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Controllers.Knapp.Channels.Tcp;

[ApiController]
[Route("api/v1/tcp")]
[Tags("Tcp")]
public sealed class TcpController : ControllerBase
{
    private readonly ICanalPedidoKiSoft _orderChannel;
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly IRabbitMqQueueMonitor _queueMonitor;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ILogger<TcpController> _logger;

    public TcpController(
        ICanalPedidoKiSoft orderChannel,
        ICanalEventoKiSoft eventChannel,
        IRabbitMqQueueMonitor queueMonitor,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        ILogger<TcpController> logger)
    {
        _orderChannel = orderChannel;
        _eventChannel = eventChannel;
        _queueMonitor = queueMonitor;
        _rabbitMqOptions = rabbitMqOptions.Value;
        _logger = logger;
    }

    [HttpGet("connections")]
    public IActionResult GetConnections() => Ok(new[]
    {
        EstadoCanalTcpDto.From("OrderChannel9801", _orderChannel),
        EstadoCanalTcpDto.From("EventChannel9802", _eventChannel)
    });

    [HttpPost("reconnect")]
    public async Task<IActionResult> Reconnect(CancellationToken cancellationToken)
    {
        try
        {
            await _orderChannel.ForceReconnectAsync(cancellationToken);
            await _eventChannel.ForceReconnectAsync(cancellationToken);
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo reconectar los canales KiSoft.");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo reconectar los canales KiSoft.");
        }
    }

    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue(CancellationToken cancellationToken)
    {
        try
        {
            var count = await _queueMonitor.GetMessageCountAsync(_rabbitMqOptions.InboundQueue, cancellationToken);
            return Ok(new { queue = _rabbitMqOptions.InboundQueue, pendingCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo consultar la cola.");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo consultar la cola.");
        }
    }
}
