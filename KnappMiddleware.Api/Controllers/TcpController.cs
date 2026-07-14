using KnappMiddleware.Api.Contracts;
using KnappMiddleware.Infrastructure.Configuration;
using KnappMiddleware.Infrastructure.RabbitMq;
using KnappMiddleware.Infrastructure.Tcp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Api.Controllers;

[ApiController]
[Route("tcp")]
public sealed class TcpController : ControllerBase
{
    private readonly IKiSoftOrderChannel _orderChannel;
    private readonly IKiSoftEventChannel _eventChannel;
    private readonly IRabbitMqQueueMonitor _queueMonitor;
    private readonly RabbitMqOptions _rabbitMqOptions;

    public TcpController(
        IKiSoftOrderChannel orderChannel,
        IKiSoftEventChannel eventChannel,
        IRabbitMqQueueMonitor queueMonitor,
        IOptions<RabbitMqOptions> rabbitMqOptions)
    {
        _orderChannel = orderChannel;
        _eventChannel = eventChannel;
        _queueMonitor = queueMonitor;
        _rabbitMqOptions = rabbitMqOptions.Value;
    }

    [HttpGet("connections")]
    public IActionResult GetConnections() => Ok(new[]
    {
        TcpChannelStatusDto.From("OrderChannel9801", _orderChannel),
        TcpChannelStatusDto.From("EventChannel9802", _eventChannel)
    });

    [HttpPost("reconnect")]
    public async Task<IActionResult> Reconnect(CancellationToken cancellationToken)
    {
        await _orderChannel.ForceReconnectAsync(cancellationToken);
        await _eventChannel.ForceReconnectAsync(cancellationToken);
        return Accepted();
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
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo consultar la cola.");
        }
    }
}
