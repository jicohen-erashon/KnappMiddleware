using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Matrix;
using KnappMiddleware.Sap;
using KnappMiddleware.Tcp;
using KnappMiddleware.Telegramas.Mapeo;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Eventos;

/// <summary>
/// Traduce el ajuste de stock (3SC) que KiSoft empuja al Host: decodifica → acusa SIEMPRE en ≤10s →
/// gate de matriz → POST fire-and-forget a SAP.
/// </summary>
public sealed class ManejadorEventoAjusteStockKiSoft : IManejadorEventoKiSoft
{
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly ClsMatrixGate _matrixGate;
    private readonly IClienteWebhookSap _webhookClient;
    private readonly ILogger<ManejadorEventoAjusteStockKiSoft> _logger;

    public ManejadorEventoAjusteStockKiSoft(
        ICanalEventoKiSoft eventChannel, ClsMatrixGate matrixGate, IClienteWebhookSap webhookClient,
        ILogger<ManejadorEventoAjusteStockKiSoft> logger)
    {
        _eventChannel = eventChannel;
        _matrixGate = matrixGate;
        _webhookClient = webhookClient;
        _logger = logger;
    }

    public bool PuedeManejar(string data) => MapeadorTelegramaEventoAjusteStock.IsStockAdjustmentEvent(data);

    public async Task ManejarAsync(string data, CancellationToken cancellationToken)
    {
        EventoAjusteStockDto? adjustmentEvent = null;
        try
        {
            adjustmentEvent = MapeadorTelegramaEventoAjusteStock.Decode(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo decodificar el ajuste de stock 3SC: '{Data}'.", data);
        }

        await _eventChannel.AcknowledgeAsync(MapeadorTelegramaEventoAjusteStock.AckOk, cancellationToken);

        if (adjustmentEvent is null)
        {
            return;
        }

        _ = DispatchToSapAsync(adjustmentEvent, cancellationToken);
    }

    private async Task DispatchToSapAsync(EventoAjusteStockDto adjustmentEvent, CancellationToken cancellationToken)
    {
        try
        {
            var mandante = adjustmentEvent.Mandante ?? "*";
            var estacion = adjustmentEvent.Station ?? "*";
            var accion = _matrixGate.Resolve(mandante, MapeadorTelegramaEventoAjusteStock.RecordId, estacion);

            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Ajuste de stock {Correccion} descartado: estación {Estacion} deshabilitada por matriz (mandante {Mandante}).",
                    adjustmentEvent.CorrectionNumber, estacion, mandante);
                return;
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Ajuste de stock {Correccion} ignorado por matriz (mandante {Mandante}, estación {Estacion}).",
                    adjustmentEvent.CorrectionNumber, mandante, estacion);
                return;
            }

            await _webhookClient.NotifyStockAdjustmentEventAsync(adjustmentEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado despachando a SAP el ajuste de stock {Correccion}.", adjustmentEvent.CorrectionNumber);
        }
    }
}
