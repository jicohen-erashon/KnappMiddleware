using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Matrix;
using KnappMiddleware.Sap;
using KnappMiddleware.Tcp;
using KnappMiddleware.Telegramas.Mapeo;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Eventos;

/// <summary>
/// Traduce el aviso de archivo listo para la visualización de inventario en tiempo real (3RR) que
/// KiSoft empuja al Host: decodifica → acusa SIEMPRE en ≤10s → gate de matriz → POST fire-and-forget
/// a SAP con el aviso. La descarga/parseo del archivo por SFTP es un trabajo aparte, no cubierto aquí.
/// </summary>
public sealed class ManejadorEventoArchivoInventarioKiSoft : IManejadorEventoKiSoft
{
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly ClsMatrixGate _matrixGate;
    private readonly IClienteWebhookSap _webhookClient;
    private readonly ILogger<ManejadorEventoArchivoInventarioKiSoft> _logger;

    public ManejadorEventoArchivoInventarioKiSoft(
        ICanalEventoKiSoft eventChannel, ClsMatrixGate matrixGate, IClienteWebhookSap webhookClient,
        ILogger<ManejadorEventoArchivoInventarioKiSoft> logger)
    {
        _eventChannel = eventChannel;
        _matrixGate = matrixGate;
        _webhookClient = webhookClient;
        _logger = logger;
    }

    public bool PuedeManejar(string data) => MapeadorTelegramaEventoArchivoInventario.IsInventoryFileReadyEvent(data);

    public async Task ManejarAsync(string data, CancellationToken cancellationToken)
    {
        EventoArchivoInventarioDto? fileReadyEvent = null;
        try
        {
            fileReadyEvent = MapeadorTelegramaEventoArchivoInventario.Decode(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo decodificar el aviso de archivo de inventario 3RR: '{Data}'.", data);
        }

        await _eventChannel.AcknowledgeAsync(MapeadorTelegramaEventoArchivoInventario.AckOk, cancellationToken);

        if (fileReadyEvent is null)
        {
            return;
        }

        _ = DispatchToSapAsync(fileReadyEvent, cancellationToken);
    }

    private async Task DispatchToSapAsync(EventoArchivoInventarioDto fileReadyEvent, CancellationToken cancellationToken)
    {
        try
        {
            var accion = _matrixGate.Resolve("*", MapeadorTelegramaEventoArchivoInventario.RecordId, fileReadyEvent.Station);

            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Aviso de archivo de inventario descartado: estación {Estacion} deshabilitada por matriz.", fileReadyEvent.Station);
                return;
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Aviso de archivo de inventario ignorado por matriz (estación {Estacion}).", fileReadyEvent.Station);
                return;
            }

            await _webhookClient.NotifyInventoryFileReadyAsync(fileReadyEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado despachando a SAP el aviso de archivo de inventario (estación {Estacion}).", fileReadyEvent.Station);
        }
    }
}
