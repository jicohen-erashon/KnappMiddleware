using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>Traduce el payload SAP de ruta teórica al registro de datos maestros 16N (HIS §3.1.3.2), envuelto en el bracket 161/169.</summary>
public static class MapeadorTelegramaRuta
{
    public const string OpenUpsert = "161";
    public const string Close = "169";

    public static string BuildNew(SolicitudRutaDto dto)
    {
        // HIS pág. 35 (§3.1.3.2.1) declara este campo con ancho fijo 8 (cita literal del PDF, no una
        // suposición). SAP consistentemente envía 10 caracteres (p. ej. "RUTA000001") — mismatch real
        // escalado a KNAPP (ver JSON-SAP/PENDIENTE-1XR.md). Mientras se confirma, se mantiene fiel al
        // spec: un valor de más de 8 caracteres se rechaza en SolicitudRutaDto (StringLength(8)) antes
        // de llegar acá, en vez de truncarse silenciosamente.
        var writer = new EscritorTelegrama();
        writer.Raw("16N");
        writer.LengthPrefix(2, 16);
        writer.LengthPrefix(2, 8);
        writer.RawValue(16, TipoCampo.Alfanumerico, dto.Mandante);
        writer.RawValue(8, TipoCampo.Alfanumerico, dto.RouteNumber);

        var hasSchedule = dto.DepartureTime is not null || dto.AvailableTime is not null;
        writer.Block('Z', hasSchedule, w =>
        {
            w.LengthPrefix(2, 6);
            w.LengthPrefix(2, 6);
            w.Reserved(2); // día de semana: no aplica en esta instalación (HIS §3.1.3.2.1, celda deprecada)
            w.RawValue(6, TipoCampo.Numerico, UtilTextoTelegrama.DigitsOnly(dto.DepartureTime));
            w.RawValue(6, TipoCampo.Numerico, UtilTextoTelegrama.DigitsOnly(dto.AvailableTime));
        });

        writer.Block('R', dto.RampNumber is not null, w =>
        {
            w.LengthPrefix(2, 1); // cantidad de rampas
            w.LengthPrefix(2, 5); // longitud de número de rampa de expedición, declarada una vez
            w.RawValue(5, TipoCampo.Numerico, dto.RampNumber?.ToString());
        });

        return writer.Build();
    }
}
