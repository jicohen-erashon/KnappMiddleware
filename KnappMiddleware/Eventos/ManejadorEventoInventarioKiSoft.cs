using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Matrix;
using KnappMiddleware.Sap;
using KnappMiddleware.Tcp;
using KnappMiddleware.Telegramas.Mapeo;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Eventos;

/// <summary>
/// Traduce el evento de resultado de inventario (3IR) que KiSoft empuja al Host: decodifica → acusa
/// SIEMPRE en ≤10s (HIS §2.6, pase lo que pase aguas abajo) → gate de matriz → si procede, POST
/// fire-and-forget a SAP. Mismo patrón que <see cref="ManejadorEventoPedidoKiSoft"/> para 32R. Sin
/// buffer de entrega: si SAP está caído el evento se pierde (riesgo aceptado, ver CONTEXT.md).
/// </summary>
public sealed class ManejadorEventoInventarioKiSoft : IManejadorEventoKiSoft
{
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly ClsMatrixGate _matrixGate;
    private readonly IClienteWebhookSap _webhookClient;
    private readonly ILogger<ManejadorEventoInventarioKiSoft> _logger;

    public ManejadorEventoInventarioKiSoft(
        ICanalEventoKiSoft eventChannel, ClsMatrixGate matrixGate, IClienteWebhookSap webhookClient,
        ILogger<ManejadorEventoInventarioKiSoft> logger)
    {
        _eventChannel = eventChannel;
        _matrixGate = matrixGate;
        _webhookClient = webhookClient;
        _logger = logger;
    }

    public bool PuedeManejar(string data) => MapeadorTelegramaEventoInventario.IsInventoryEvent(data);

    public async Task ManejarAsync(string data, CancellationToken cancellationToken)
    {
        EventoInventarioDto? inventoryEvent = null;
        try
        {
            inventoryEvent = MapeadorTelegramaEventoInventario.Decode(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo decodificar el evento de resultado de inventario 3IR: '{Data}'.", data);
        }

        // Acusar SIEMPRE en ≤10s, sin importar si la decodificación o lo que sigue falla.
        await _eventChannel.AcknowledgeAsync(MapeadorTelegramaEventoInventario.AckOk, cancellationToken);

        if (inventoryEvent is null)
        {
            return;
        }

        // Gate de matriz + webhook a SAP: fire-and-forget, no debe bloquear el bucle de lectura del canal.
        _ = DispatchToSapAsync(inventoryEvent, cancellationToken);
    }

    private async Task DispatchToSapAsync(EventoInventarioDto inventoryEvent, CancellationToken cancellationToken)
    {
        try
        {
            // Igual que en 1IA (SolicitudInventarioDto): se gatea con la estación de la primera línea,
            // porque el encabezado del 3IR no trae una estación propia — el inventario puede cubrir líneas
            // de varias estaciones a la vez.
            var estacion = inventoryEvent.Lines.FirstOrDefault()?.Station ?? "*";
            var accion = _matrixGate.Resolve(inventoryEvent.Mandante, MapeadorTelegramaEventoInventario.RecordId, estacion);

            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Evento de resultado de inventario {Solicitud} descartado: estación {Estacion} deshabilitada por matriz.",
                    inventoryEvent.InventoryRequestNumber, estacion);
                return;
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Evento de resultado de inventario {Solicitud} ignorado por matriz (no se reenvía a SAP).",
                    inventoryEvent.InventoryRequestNumber);
                return;
            }

            await _webhookClient.NotifyInventoryEventAsync(inventoryEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado despachando a SAP el evento de resultado de inventario {Solicitud}.",
                inventoryEvent.InventoryRequestNumber);
        }
    }
}
