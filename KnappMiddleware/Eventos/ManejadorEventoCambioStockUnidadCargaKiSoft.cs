using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Matrix;
using KnappMiddleware.Sap;
using KnappMiddleware.Tcp;
using KnappMiddleware.Telegramas.Mapeo;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Eventos;

/// <summary>
/// Traduce la respuesta de cambio de stock por unidad de carga (3UU) que KiSoft empuja al Host:
/// decodifica → acusa SIEMPRE en ≤10s → gate de matriz → POST fire-and-forget a SAP. Add-on de pago
/// (ver <see cref="EventoCambioStockUnidadCargaDto"/>): confirmar con KNAPP antes de habilitarlo.
/// </summary>
public sealed class ManejadorEventoCambioStockUnidadCargaKiSoft : IManejadorEventoKiSoft
{
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly ClsMatrixGate _matrixGate;
    private readonly IClienteWebhookSap _webhookClient;
    private readonly ILogger<ManejadorEventoCambioStockUnidadCargaKiSoft> _logger;

    public ManejadorEventoCambioStockUnidadCargaKiSoft(
        ICanalEventoKiSoft eventChannel, ClsMatrixGate matrixGate, IClienteWebhookSap webhookClient,
        ILogger<ManejadorEventoCambioStockUnidadCargaKiSoft> logger)
    {
        _eventChannel = eventChannel;
        _matrixGate = matrixGate;
        _webhookClient = webhookClient;
        _logger = logger;
    }

    public bool PuedeManejar(string data) => MapeadorTelegramaEventoCambioStockUnidadCarga.IsLoadUnitStockChangeEvent(data);

    public async Task ManejarAsync(string data, CancellationToken cancellationToken)
    {
        EventoCambioStockUnidadCargaDto? changeEvent = null;
        try
        {
            changeEvent = MapeadorTelegramaEventoCambioStockUnidadCarga.Decode(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo decodificar el evento de cambio de stock por unidad de carga 3UU: '{Data}'.", data);
        }

        await _eventChannel.AcknowledgeAsync(MapeadorTelegramaEventoCambioStockUnidadCarga.AckOk, cancellationToken);

        if (changeEvent is null)
        {
            return;
        }

        _ = DispatchToSapAsync(changeEvent, cancellationToken);
    }

    private async Task DispatchToSapAsync(EventoCambioStockUnidadCargaDto changeEvent, CancellationToken cancellationToken)
    {
        try
        {
            var estacion = changeEvent.Station ?? "*";
            var accion = _matrixGate.Resolve("*", MapeadorTelegramaEventoCambioStockUnidadCarga.RecordId, estacion);

            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Cambio de stock de la unidad de carga {UnidadCarga} descartado: estación {Estacion} deshabilitada por matriz.",
                    changeEvent.LoadUnitCode, estacion);
                return;
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Cambio de stock de la unidad de carga {UnidadCarga} ignorado por matriz (estación {Estacion}).",
                    changeEvent.LoadUnitCode, estacion);
                return;
            }

            await _webhookClient.NotifyLoadUnitStockChangeEventAsync(changeEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado despachando a SAP el cambio de stock de la unidad de carga {UnidadCarga}.", changeEvent.LoadUnitCode);
        }
    }
}
