using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Matrix;
using KnappMiddleware.Sap;
using KnappMiddleware.Tcp;
using KnappMiddleware.Telegramas.Mapeo;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Eventos;

/// <summary>
/// Traduce los eventos de pedido (32R) que KiSoft empuja al Host: decodifica → acusa SIEMPRE en ≤10s
/// (HIS §2.6, pase lo que pase aguas abajo) → gate de matriz → si procede, POST fire-and-forget a SAP.
/// Sin buffer de entrega: si SAP está caído el evento se pierde (riesgo aceptado, ver CONTEXT.md).
/// </summary>
public sealed class ManejadorEventoPedidoKiSoft : IManejadorEventoKiSoft
{
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly ClsMatrixGate _matrixGate;
    private readonly IClienteWebhookSap _webhookClient;
    private readonly ILogger<ManejadorEventoPedidoKiSoft> _logger;

    public ManejadorEventoPedidoKiSoft(
        ICanalEventoKiSoft eventChannel, ClsMatrixGate matrixGate, IClienteWebhookSap webhookClient,
        ILogger<ManejadorEventoPedidoKiSoft> logger)
    {
        _eventChannel = eventChannel;
        _matrixGate = matrixGate;
        _webhookClient = webhookClient;
        _logger = logger;
    }

    public bool PuedeManejar(string data) => MapeadorTelegramaEventoPedido.IsOrderEvent(data);

    public async Task ManejarAsync(string data, CancellationToken cancellationToken)
    {
        EventoPedidoDto? orderEvent = null;
        try
        {
            orderEvent = MapeadorTelegramaEventoPedido.Decode(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo decodificar el evento de pedido 32R: '{Data}'.", data);
        }

        // Acusar SIEMPRE en ≤10s, sin importar si la decodificación o lo que sigue falla.
        await _eventChannel.AcknowledgeAsync(MapeadorTelegramaEventoPedido.AckOk, cancellationToken);

        if (orderEvent is null)
        {
            return;
        }

        // Gate de matriz + webhook a SAP: fire-and-forget, no debe bloquear el bucle de lectura del canal.
        _ = DispatchToSapAsync(orderEvent, cancellationToken);
    }

    private async Task DispatchToSapAsync(EventoPedidoDto orderEvent, CancellationToken cancellationToken)
    {
        try
        {
            var estacion = orderEvent.StartStation ?? orderEvent.LastReadStation ?? "*";
            var accion = _matrixGate.Resolve(orderEvent.Mandante, MapeadorTelegramaEventoPedido.RecordId, estacion);

            if (accion == MatrixAction.Deshabilitado)
            {
                _logger.LogWarning("Evento de pedido {Pedido}/{Hoja} descartado: estación {Estacion} deshabilitada por matriz.",
                    orderEvent.OrderNumber, orderEvent.SheetNumber, estacion);
                return;
            }

            if (accion == MatrixAction.Ignorar)
            {
                _logger.LogInformation("Evento de pedido {Pedido}/{Hoja} ignorado por matriz (no se reenvía a SAP).",
                    orderEvent.OrderNumber, orderEvent.SheetNumber);
                return;
            }

            await _webhookClient.NotifyOrderEventAsync(orderEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado despachando a SAP el evento de pedido {Pedido}/{Hoja}.",
                orderEvent.OrderNumber, orderEvent.SheetNumber);
        }
    }
}
