using KnappMiddleware.Api.Contracts;
using KnappMiddleware.Infrastructure.Configuration;
using KnappMiddleware.Infrastructure.RabbitMq;
using KnappMiddleware.Infrastructure.Tcp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Api.Controllers;

[ApiController]
public sealed class StatusController : ControllerBase
{
    private readonly IKiSoftOrderChannel _orderChannel;
    private readonly IKiSoftEventChannel _eventChannel;
    private readonly IRabbitMqQueueMonitor _queueMonitor;
    private readonly RabbitMqOptions _rabbitMqOptions;

    public StatusController(
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

    [HttpGet("status")]
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
            fifoQueue = new { queue = _rabbitMqOptions.InboundQueue, reachable = false, error = ex.Message };
        }

        return Ok(new
        {
            orderChannel = TcpChannelStatusDto.From("OrderChannel9801", _orderChannel),
            eventChannel = TcpChannelStatusDto.From("EventChannel9802", _eventChannel),
            fifoQueue
        });
    }
}
