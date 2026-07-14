using KnappMiddleware.Domain.Auditing;
using KnappMiddleware.Domain.Configuration;
using KnappMiddleware.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Api.Controllers;

[ApiController]
[Route("config")]
public sealed class ConfigController : ControllerBase
{
    private const string AuditQueueCapacityKey = "audit.queueCapacity";
    private const int DefaultAuditQueueCapacity = 10_000;

    private readonly IOptions<RabbitMqOptions> _rabbitMq;
    private readonly IOptions<InventorySftpOptions> _inventorySftp;
    private readonly IOptions<PrintSftpOptions> _printSftp;
    private readonly IOptions<KiSoftOrderChannelOptions> _orderChannel;
    private readonly IOptions<KiSoftEventChannelOptions> _eventChannel;
    private readonly IAuditToggle _auditToggle;
    private readonly IConfigGate _configGate;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(
        IOptions<RabbitMqOptions> rabbitMq,
        IOptions<InventorySftpOptions> inventorySftp,
        IOptions<PrintSftpOptions> printSftp,
        IOptions<KiSoftOrderChannelOptions> orderChannel,
        IOptions<KiSoftEventChannelOptions> eventChannel,
        IAuditToggle auditToggle,
        IConfigGate configGate,
        ILogger<ConfigController> logger)
    {
        _rabbitMq = rabbitMq;
        _inventorySftp = inventorySftp;
        _printSftp = printSftp;
        _orderChannel = orderChannel;
        _eventChannel = eventChannel;
        _auditToggle = auditToggle;
        _configGate = configGate;
        _logger = logger;
    }

    [HttpPost("reload")]
    public async Task<IActionResult> Reload(CancellationToken cancellationToken)
    {
        try
        {
            await _configGate.ReloadAsync(cancellationToken);
            return Ok(new { reloaded = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo recargar la configuración.");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "No se pudo recargar la configuración.");
        }
    }

    // Nunca incluye credenciales (passwords, connection strings): solo lo necesario para diagnosticar.
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        kiSoft = new
        {
            orderChannel = ChannelSummary(_orderChannel.Value),
            eventChannel = ChannelSummary(_eventChannel.Value)
        },
        rabbitMq = new
        {
            host = _rabbitMq.Value.HostName,
            port = _rabbitMq.Value.Port,
            inboundQueue = _rabbitMq.Value.InboundQueue,
            outboundExchange = _rabbitMq.Value.OutboundExchange
        },
        sftp = new
        {
            inventory = new { host = _inventorySftp.Value.Host, port = _inventorySftp.Value.Port },
            print = new { host = _printSftp.Value.Host, port = _printSftp.Value.Port }
        },
        audit = new { enabled = _auditToggle.IsEnabled, queueCapacity = _configGate.GetInt(AuditQueueCapacityKey, DefaultAuditQueueCapacity) }
    });

    private static object ChannelSummary(KiSoftTcpChannelOptions options) => new
    {
        host = options.Host,
        port = options.Port,
        connectTimeoutSeconds = options.ConnectTimeoutSeconds,
        responseTimeoutSeconds = options.ResponseTimeoutSeconds,
        heartbeatIdleSeconds = options.HeartbeatIdleSeconds,
        heartbeatTimeoutSeconds = options.HeartbeatTimeoutSeconds,
        reconnectDelaySeconds = options.ReconnectDelaySeconds
    };
}
