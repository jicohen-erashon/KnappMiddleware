using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Matrix;
using KnappMiddleware.Sap;
using KnappMiddleware.Tcp;
using KnappMiddleware.Telegramas.Mapeo;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Eventos;

/// <summary>
/// Traduce el evento con los datos de stock en tiempo real de un artículo (3XR) que KiSoft empuja al
/// Host: decodifica → acusa SIEMPRE en ≤10s → gate de matriz → POST fire-and-forget a SAP. Add-on de
/// pago (ver <see cref="EventoStockArticuloDto"/>): confirmar con KNAPP antes de habilitarlo.
/// </summary>
public sealed class ManejadorEventoStockArticuloKiSoft : IManejadorEventoKiSoft
{
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly ClsMatrixGate _matrixGate;
    private readonly IClienteWebhookSap _webhookClient;
    private readonly ILogger<ManejadorEventoStockArticuloKiSoft> _logger;

    public ManejadorEventoStockArticuloKiSoft(
        ICanalEventoKiSoft eventChannel, ClsMatrixGate matrixGate, IClienteWebhookSap webhookClient,
        ILogger<ManejadorEventoStockArticuloKiSoft> logger)
    {
        _eventChannel = eventChannel;
        _matrixGate = matrixGate;
        _webhookClient = webhookClient;
        _logger = logger;
    }

    public bool PuedeManejar(string data) => MapeadorTelegramaEventoStockArticulo.IsStockArticleEvent(data);

    public async Task ManejarAsync(string data, CancellationToken cancellationToken)
    {
        EventoStockArticuloDto? stockEvent = null;
        try
        {
            stockEvent = MapeadorTelegramaEventoStockArticulo.Decode(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo decodificar el evento de stock de artículo 3XR: '{Data}'.", data);
        }

        await _eventChannel.AcknowledgeAsync(MapeadorTelegramaEventoStockArticulo.AckOk, cancellationToken);

        if (stockEvent is null)
        {
            return;
        }

        _ = DispatchToSapAsync(stockEvent, cancellationToken);
    }

    private async Task DispatchToSapAsync(EventoStockArticuloDto stockEvent, CancellationToken cancellationToken)
    {
        try
        {
            var primeraLinea = stockEvent.Lines.FirstOrDefault();
            var mandante = primeraLinea?.Mandante ?? "*";
            var estacion = primeraLinea?.Station ?? "*";
            var accion = _matrixGate.Resolve(mandante, MapeadorTelegramaEventoStockArticulo.RecordId, estacion);

            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Evento de stock de artículo descartado: estación {Estacion} deshabilitada por matriz (mandante {Mandante}).", estacion, mandante);
                return;
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Evento de stock de artículo ignorado por matriz (mandante {Mandante}, estación {Estacion}).", mandante, estacion);
                return;
            }

            await _webhookClient.NotifyStockArticleEventAsync(stockEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado despachando a SAP el evento de stock de artículo.");
        }
    }
}
