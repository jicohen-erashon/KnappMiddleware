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
    public const string InventoryEventPathKey = "sap.webhook.inventoryEventPath";
    public const string InventoryFileReadyPathKey = "sap.webhook.inventoryFileReadyPath";
    public const string StockArticleEventPathKey = "sap.webhook.stockArticleEventPath";
    public const string StockAdjustmentEventPathKey = "sap.webhook.stockAdjustmentEventPath";
    public const string LoadUnitEmptyEventPathKey = "sap.webhook.loadUnitEmptyEventPath";
    public const string LoadUnitStockChangeEventPathKey = "sap.webhook.loadUnitStockChangeEventPath";
    public const string ArticleStockChangeEventPathKey = "sap.webhook.articleStockChangeEventPath";
    public const string TimeoutSecondsKey = "sap.webhook.timeoutSeconds";

    private const string DefaultOrderEventPath = "/kisoft/order-events";
    private const string DefaultInventoryEventPath = "/kisoft/inventory-events";
    private const string DefaultInventoryFileReadyPath = "/kisoft/inventory-file-ready";
    private const string DefaultStockArticleEventPath = "/kisoft/stock-article-events";
    private const string DefaultStockAdjustmentEventPath = "/kisoft/stock-adjustment-events";
    private const string DefaultLoadUnitEmptyEventPath = "/kisoft/load-unit-empty-events";
    private const string DefaultLoadUnitStockChangeEventPath = "/kisoft/load-unit-stock-change-events";
    private const string DefaultArticleStockChangeEventPath = "/kisoft/article-stock-change-events";
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

    public Task NotifyOrderEventAsync(EventoPedidoDto orderEvent, CancellationToken cancellationToken = default) =>
        PostEventAsync(OrderEventPathKey, DefaultOrderEventPath, orderEvent,
            $"evento de pedido {orderEvent.OrderNumber}/{orderEvent.SheetNumber}", cancellationToken);

    public Task NotifyInventoryEventAsync(EventoInventarioDto inventoryEvent, CancellationToken cancellationToken = default) =>
        PostEventAsync(InventoryEventPathKey, DefaultInventoryEventPath, inventoryEvent,
            $"evento de resultado de inventario {inventoryEvent.InventoryRequestNumber}", cancellationToken);

    public Task NotifyInventoryFileReadyAsync(EventoArchivoInventarioDto fileReadyEvent, CancellationToken cancellationToken = default) =>
        PostEventAsync(InventoryFileReadyPathKey, DefaultInventoryFileReadyPath, fileReadyEvent,
            $"aviso de archivo de inventario listo (estación {fileReadyEvent.Station})", cancellationToken);

    public Task NotifyStockArticleEventAsync(EventoStockArticuloDto stockEvent, CancellationToken cancellationToken = default) =>
        PostEventAsync(StockArticleEventPathKey, DefaultStockArticleEventPath, stockEvent,
            $"evento de stock de artículo en tiempo real ({stockEvent.Lines.Count} línea(s))", cancellationToken);

    public Task NotifyStockAdjustmentEventAsync(EventoAjusteStockDto adjustmentEvent, CancellationToken cancellationToken = default) =>
        PostEventAsync(StockAdjustmentEventPathKey, DefaultStockAdjustmentEventPath, adjustmentEvent,
            $"ajuste de stock {adjustmentEvent.CorrectionNumber}", cancellationToken);

    public Task NotifyLoadUnitEmptyEventAsync(EventoUnidadCargaVaciaDto emptyEvent, CancellationToken cancellationToken = default) =>
        PostEventAsync(LoadUnitEmptyEventPathKey, DefaultLoadUnitEmptyEventPath, emptyEvent,
            $"aviso de unidad de carga vacía {emptyEvent.LoadUnitCode}", cancellationToken);

    public Task NotifyLoadUnitStockChangeEventAsync(EventoCambioStockUnidadCargaDto changeEvent, CancellationToken cancellationToken = default) =>
        PostEventAsync(LoadUnitStockChangeEventPathKey, DefaultLoadUnitStockChangeEventPath, changeEvent,
            $"cambio de stock de la unidad de carga {changeEvent.LoadUnitCode}", cancellationToken);

    public Task NotifyArticleStockChangeEventAsync(EventoCambioPropiedadesStockDto changeEvent, CancellationToken cancellationToken = default) =>
        PostEventAsync(ArticleStockChangeEventPathKey, DefaultArticleStockChangeEventPath, changeEvent,
            $"cambio de propiedades de stock del artículo {changeEvent.ProductNumber}", cancellationToken);

    private async Task PostEventAsync<T>(string pathKey, string defaultPath, T payload, string logContext, CancellationToken cancellationToken)
    {
        var baseUrl = _configGate.GetValue(BaseUrlKey);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("'{Key}' no está configurado en la tabla configuracion; se descarta el {Contexto}.",
                BaseUrlKey, logContext);
            return;
        }

        var path = _configGate.GetValue(pathKey) ?? defaultPath;
        var timeoutSeconds = _configGate.GetInt(TimeoutSecondsKey, DefaultTimeoutSeconds);
        var url = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path.TrimStart('/'));

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var response = await _httpClient.PostAsJsonAsync(url, payload, linkedCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SAP respondió {Status} al {Contexto}; se descarta (sin reintento).",
                    response.StatusCode, logContext);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo notificar a SAP el {Contexto}; se descarta (sin reintento).", logContext);
        }
    }
}
