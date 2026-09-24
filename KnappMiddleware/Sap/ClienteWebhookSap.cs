using System.Net.Http.Json;
using KnappMiddleware.Configuration;
using KnappMiddleware.Contratos.Sap;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Sap;

/// <summary>
/// Implementación HTTP de <see cref="IClienteWebhookSap"/>. Toda su configuración (URL base, ruta,
/// timeout) vive en la tabla <c>configuracion</c> (vía <see cref="ClsConfigGate"/>), no en
/// appsettings, para poder cambiarla en caliente sin reiniciar la Api — igual que audit.enabled.
/// No reintenta ni encola: un fallo se registra y se descarta (CONTEXT.md: "sin buffer de entrega;
/// si SAP está caído, el evento se pierde, riesgo aceptado").
/// </summary>
public sealed class ClienteWebhookSap : IClienteWebhookSap
{
    public const string BaseUrlKey = "sap.webhook.baseUrl";
    public const string OrderEventPathKey = "sap.webhook.orderEventPath";
    public const string TimeoutSecondsKey = "sap.webhook.timeoutSeconds";

    private const string DefaultOrderEventPath = "/kisoft/order-events";
    private const int DefaultTimeoutSeconds = 10;

    private readonly HttpClient _httpClient;
    private readonly ClsConfigGate _configGate;
    private readonly ILogger<ClienteWebhookSap> _logger;

    public ClienteWebhookSap(HttpClient httpClient, ClsConfigGate configGate, ILogger<ClienteWebhookSap> logger)
    {
        _httpClient = httpClient;
        _configGate = configGate;
        _logger = logger;
    }

    public async Task NotifyOrderEventAsync(EventoPedidoDto orderEvent, CancellationToken cancellationToken = default)
    {
        var baseUrl = _configGate.GetValue(BaseUrlKey);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("'{Key}' no está configurado en la tabla configuracion; se descarta el evento de pedido {Pedido}/{Hoja}.",
                BaseUrlKey, orderEvent.OrderNumber, orderEvent.SheetNumber);
            return;
        }

        var path = _configGate.GetValue(OrderEventPathKey) ?? DefaultOrderEventPath;
        var timeoutSeconds = _configGate.GetInt(TimeoutSecondsKey, DefaultTimeoutSeconds);
        var url = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path.TrimStart('/'));

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var response = await _httpClient.PostAsJsonAsync(url, orderEvent, linkedCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SAP respondió {Status} al evento de pedido {Pedido}/{Hoja}; se descarta (sin reintento).",
                    response.StatusCode, orderEvent.OrderNumber, orderEvent.SheetNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo notificar a SAP el evento de pedido {Pedido}/{Hoja}; se descarta (sin reintento).",
                orderEvent.OrderNumber, orderEvent.SheetNumber);
        }
    }
}
