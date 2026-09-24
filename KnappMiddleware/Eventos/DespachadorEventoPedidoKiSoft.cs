using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Matrix;
using KnappMiddleware.Sap;
using KnappMiddleware.Tcp;
using KnappMiddleware.Telegramas.Mapeo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnappMiddleware.Eventos;

/// <summary>
/// Se suscribe a <see cref="ICanalEventoKiSoft.TelegramReceived"/> y traduce los eventos de pedido
/// (32R) que KiSoft empuja al Host: decodifica → acusa SIEMPRE en ≤10s (HIS §2.6, pase lo que pase
/// aguas abajo) → gate de matriz → si procede, POST fire-and-forget a SAP. Sin buffer de entrega: si
/// SAP está caído el evento se pierde (riesgo aceptado, ver CONTEXT.md).
/// </summary>
public sealed class DespachadorEventoPedidoKiSoft : IHostedService
{
    private readonly ICanalEventoKiSoft _eventChannel;
    private readonly ClsMatrixGate _matrixGate;
    private readonly IClienteWebhookSap _webhookClient;
    private readonly ILogger<DespachadorEventoPedidoKiSoft> _logger;

    public DespachadorEventoPedidoKiSoft(
        ICanalEventoKiSoft eventChannel, ClsMatrixGate matrixGate, IClienteWebhookSap webhookClient,
        ILogger<DespachadorEventoPedidoKiSoft> logger)
    {
        _eventChannel = eventChannel;
        _matrixGate = matrixGate;
        _webhookClient = webhookClient;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _eventChannel.TelegramReceived += HandleAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _eventChannel.TelegramReceived -= HandleAsync;
        return Task.CompletedTask;
    }

    private async Task HandleAsync(string data, CancellationToken cancellationToken)
    {
        if (!MapeadorTelegramaEventoPedido.IsOrderEvent(data))
        {
            await AcknowledgeUnknownAsync(data, cancellationToken);
            return;
        }

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

    /// <summary>HIS §2.5: identificador de registro no válido → "2" + 2º/3er dígito del recibido + estado "91".</summary>
    private async Task AcknowledgeUnknownAsync(string data, CancellationToken cancellationToken)
    {
        var recordId = data.Length >= 3 ? data[..3] : "000";
        var ack = recordId.Length == 3 ? $"2{recordId[1..3]}91" : "00091";
        _logger.LogWarning("Evento KiSoft con identificador de registro no soportado: '{RecordId}'.", recordId);
        await _eventChannel.AcknowledgeAsync(ack, cancellationToken);
    }
}
