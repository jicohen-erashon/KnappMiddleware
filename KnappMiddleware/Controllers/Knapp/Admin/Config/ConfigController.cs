using KnappMiddleware.Auditing;
using KnappMiddleware.Configuration;
using KnappMiddleware.Sap;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KnappMiddleware.Controllers.Knapp.Admin.Config;

[ApiController]
[Route("api/v1/config")]
[Tags("Config")]
public sealed class ConfigController : ControllerBase
{
    private const string AuditQueueCapacityKey = "audit.queueCapacity";
    private const int DefaultAuditQueueCapacity = 10_000;

    private readonly IOptions<RabbitMqOptions> _rabbitMq;
    private readonly IOptions<InventorySftpOptions> _inventorySftp;
    private readonly IOptions<PrintSftpOptions> _printSftp;
    private readonly ClsAuditToggle _auditToggle;
    private readonly ClsConfigGate _configGate;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(
        IOptions<RabbitMqOptions> rabbitMq,
        IOptions<InventorySftpOptions> inventorySftp,
        IOptions<PrintSftpOptions> printSftp,
        ClsAuditToggle auditToggle,
        ClsConfigGate configGate,
        ILogger<ConfigController> logger)
    {
        _rabbitMq = rabbitMq;
        _inventorySftp = inventorySftp;
        _printSftp = printSftp;
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
            orderChannel = ChannelSummary(KiSoftOrderChannelOptions.ReadFrom(_configGate)),
            eventChannel = ChannelSummary(KiSoftEventChannelOptions.ReadFrom(_configGate))
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
        audit = new { enabled = _auditToggle.IsEnabled, queueCapacity = _configGate.GetInt(AuditQueueCapacityKey, DefaultAuditQueueCapacity) },
        sap = new
        {
            webhookBaseUrlConfigured = !string.IsNullOrWhiteSpace(_configGate.GetValue(ClienteWebhookSap.BaseUrlKey)),
            orderEventPath = _configGate.GetValue(ClienteWebhookSap.OrderEventPathKey),
            timeoutSeconds = _configGate.GetInt(ClienteWebhookSap.TimeoutSecondsKey, 10)
        }
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
