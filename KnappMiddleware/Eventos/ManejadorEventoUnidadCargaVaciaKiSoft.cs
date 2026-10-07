using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Matrix;
using KnappMiddleware.Sap;
using KnappMiddleware.Tcp;
using KnappMiddleware.Telegramas.Mapeo;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Eventos;

/// <summary>
/// Traduce el aviso de unidad de carga vacía (3UE) que KiSoft empuja al Host: decodifica → acusa
/// SIEMPRE en ≤10s → gate de matriz → POST fire-and-forget a SAP. El telegrama no trae mandante ni
/// estación (solo el código de unidad de carga), así que el gate de matriz usa comodín "*" en ambos.
/// </summary>
public sealed class ManejadorEventoUnidadCargaVaciaKiSoft : IManejadorEventoKiSoft
{
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly ClsMatrixGate _matrixGate;
    private readonly IClienteWebhookSap _webhookClient;
    private readonly ILogger<ManejadorEventoUnidadCargaVaciaKiSoft> _logger;

    public ManejadorEventoUnidadCargaVaciaKiSoft(
        ICanalEventoKiSoft eventChannel, ClsMatrixGate matrixGate, IClienteWebhookSap webhookClient,
        ILogger<ManejadorEventoUnidadCargaVaciaKiSoft> logger)
    {
        _eventChannel = eventChannel;
        _matrixGate = matrixGate;
        _webhookClient = webhookClient;
        _logger = logger;
    }

    public bool PuedeManejar(string data) => MapeadorTelegramaEventoUnidadCargaVacia.IsLoadUnitEmptyEvent(data);

    public async Task ManejarAsync(string data, CancellationToken cancellationToken)
    {
        EventoUnidadCargaVaciaDto? emptyEvent = null;
        try
        {
            emptyEvent = MapeadorTelegramaEventoUnidadCargaVacia.Decode(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo decodificar el aviso de unidad de carga vacía 3UE: '{Data}'.", data);
        }

        await _eventChannel.AcknowledgeAsync(MapeadorTelegramaEventoUnidadCargaVacia.AckOk, cancellationToken);

        if (emptyEvent is null)
        {
            return;
        }

        _ = DispatchToSapAsync(emptyEvent, cancellationToken);
    }

    private async Task DispatchToSapAsync(EventoUnidadCargaVaciaDto emptyEvent, CancellationToken cancellationToken)
    {
        try
        {
            var accion = _matrixGate.Resolve("*", MapeadorTelegramaEventoUnidadCargaVacia.RecordId, "*");

            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Aviso de unidad de carga vacía {UnidadCarga} descartado por matriz.", emptyEvent.LoadUnitCode);
                return;
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Aviso de unidad de carga vacía {UnidadCarga} ignorado por matriz.", emptyEvent.LoadUnitCode);
                return;
            }

            await _webhookClient.NotifyLoadUnitEmptyEventAsync(emptyEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado despachando a SAP el aviso de unidad de carga vacía {UnidadCarga}.", emptyEvent.LoadUnitCode);
        }
    }
}
