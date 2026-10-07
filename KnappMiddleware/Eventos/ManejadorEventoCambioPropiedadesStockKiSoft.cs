using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Matrix;
using KnappMiddleware.Sap;
using KnappMiddleware.Tcp;
using KnappMiddleware.Telegramas.Mapeo;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Eventos;

/// <summary>
/// Traduce la respuesta de cambio de propiedades de stock por artículo/lote (3AU) que KiSoft empuja
/// al Host: decodifica → acusa SIEMPRE en ≤10s → gate de matriz → POST fire-and-forget a SAP. Add-on
/// de pago (ver <see cref="EventoCambioPropiedadesStockDto"/>): confirmar con KNAPP antes de habilitarlo.
/// </summary>
public sealed class ManejadorEventoCambioPropiedadesStockKiSoft : IManejadorEventoKiSoft
{
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly ClsMatrixGate _matrixGate;
    private readonly IClienteWebhookSap _webhookClient;
    private readonly ILogger<ManejadorEventoCambioPropiedadesStockKiSoft> _logger;

    public ManejadorEventoCambioPropiedadesStockKiSoft(
        ICanalEventoKiSoft eventChannel, ClsMatrixGate matrixGate, IClienteWebhookSap webhookClient,
        ILogger<ManejadorEventoCambioPropiedadesStockKiSoft> logger)
    {
        _eventChannel = eventChannel;
        _matrixGate = matrixGate;
        _webhookClient = webhookClient;
        _logger = logger;
    }

    public bool PuedeManejar(string data) => MapeadorTelegramaEventoCambioPropiedadesStock.IsArticleStockChangeEvent(data);

    public async Task ManejarAsync(string data, CancellationToken cancellationToken)
    {
        EventoCambioPropiedadesStockDto? changeEvent = null;
        try
        {
            changeEvent = MapeadorTelegramaEventoCambioPropiedadesStock.Decode(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo decodificar el evento de cambio de propiedades de stock 3AU: '{Data}'.", data);
        }

        await _eventChannel.AcknowledgeAsync(MapeadorTelegramaEventoCambioPropiedadesStock.AckOk, cancellationToken);

        if (changeEvent is null)
        {
            return;
        }

        _ = DispatchToSapAsync(changeEvent, cancellationToken);
    }

    private async Task DispatchToSapAsync(EventoCambioPropiedadesStockDto changeEvent, CancellationToken cancellationToken)
    {
        try
        {
            var mandante = changeEvent.Mandante ?? "*";
            var accion = _matrixGate.Resolve(mandante, MapeadorTelegramaEventoCambioPropiedadesStock.RecordId, changeEvent.Station);

            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Cambio de propiedades de stock del artículo {Articulo} descartado: estación {Estacion} deshabilitada por matriz (mandante {Mandante}).",
                    changeEvent.ProductNumber, changeEvent.Station, mandante);
                return;
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Cambio de propiedades de stock del artículo {Articulo} ignorado por matriz (mandante {Mandante}, estación {Estacion}).",
                    changeEvent.ProductNumber, mandante, changeEvent.Station);
                return;
            }

            await _webhookClient.NotifyArticleStockChangeEventAsync(changeEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado despachando a SAP el cambio de propiedades de stock del artículo {Articulo}.", changeEvent.ProductNumber);
        }
    }
}
