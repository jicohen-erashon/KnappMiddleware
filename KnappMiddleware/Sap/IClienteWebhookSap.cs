using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Sap;

/// <summary>
/// Cliente HTTP fire-and-forget hacia SAP para los eventos que KiSoft empuja al middleware.
/// Sin buffer de entrega: si SAP está caído, el llamador decide si el evento se pierde (riesgo
/// aceptado, ver CONTEXT.md "Flujos → Evento por webhook").
/// </summary>
public interface IClienteWebhookSap
{
    Task NotifyOrderEventAsync(EventoPedidoDto orderEvent, CancellationToken cancellationToken = default);
}
