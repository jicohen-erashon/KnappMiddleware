using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>
/// Traduce la solicitud de visualización de inventario en tiempo real al registro 1RR (HIS §3.5.1.1).
/// La respuesta síncrona (2RR) solo confirma que la solicitud fue aceptada; el resultado real llega
/// más tarde como evento asíncrono 3RR (canal 9802) avisando que el archivo quedó listo por SFTP —
/// ese flujo de eventos es un componente aparte, no cubierto por este endpoint.
/// </summary>
public static class MapeadorTelegramaInventarioTiempoReal
{
    public static string BuildRequest(SolicitudInventarioTiempoRealDto dto) =>
        new EscritorTelegrama()
            .Raw("1RR")
            .Field(2, 3, TipoCampo.Numerico, dto.Station)
            .Tag('T').Field(2, 2, TipoCampo.Numerico, dto.RequestType)
            .Build();
}
