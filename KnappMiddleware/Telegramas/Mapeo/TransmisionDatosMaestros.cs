using KnappMiddleware.Tcp;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>Resultado de un bracket abrir/registro/cerrar de datos maestros (HIS §3.1.x).</summary>
public sealed record TransmisionDatosMaestros(MensajeEstadoKiSoft Open, MensajeEstadoKiSoft Data, MensajeEstadoKiSoft Close)
{
    /// <summary>Envía abrir → registro → cerrar como una única secuencia atómica en el canal 9801 (sin intercalar otros telegramas).</summary>
    public static async Task<TransmisionDatosMaestros> SendAsync(
        ICanalPedidoKiSoft channel, string openPayload, string dataPayload, string closePayload,
        CancellationToken cancellationToken)
    {
        var responses = await channel.SendSequenceAsync([openPayload, dataPayload, closePayload], cancellationToken);
        return new TransmisionDatosMaestros(
            MensajeEstadoKiSoft.Parse(responses[0], EstadoEsperado(openPayload)),
            MensajeEstadoKiSoft.Parse(responses[1], EstadoEsperado(dataPayload[..3])),
            MensajeEstadoKiSoft.Parse(responses[2], EstadoEsperado(closePayload)));
    }

    /// <summary>
    /// Deriva el identificador de registro esperado del mensaje de estado a partir del de la
    /// petición: en todos los brackets de datos maestros documentados en el HIS (§3.1.1.1/§3.1.1.2,
    /// §3.1.2.1/§3.1.2.2, §3.1.3.1/§3.1.3.2 — confirmado en el PDF para artículo/socio/ruta) el
    /// identificador de la petición empieza siempre en "1" y el de su ack en "2" (140→240, 141→241,
    /// 149→249, 14N→24N; mismo patrón para 150/151/159/15N y 160/161/169/16N).
    /// </summary>
    private static string EstadoEsperado(string requestRecordId)
    {
        if (requestRecordId.Length < 3 || requestRecordId[0] != '1')
        {
            throw new ExcepcionFormatoTelegrama(
                $"No se pudo derivar el identificador de ack esperado desde '{requestRecordId}'.");
        }

        return "2" + requestRecordId[1..3];
    }
}
