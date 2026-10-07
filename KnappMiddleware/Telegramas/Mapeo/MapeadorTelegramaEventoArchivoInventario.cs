using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>Decodifica el aviso de archivo listo para la visualización de inventario en tiempo real (3RR, HIS V3 §4.3.1.1).</summary>
public static class MapeadorTelegramaEventoArchivoInventario
{
    public const string RecordId = "3RR";
    public const string AckOk = "4RR00";
    public const string AckError = "4RR99";

    public static bool IsInventoryFileReadyEvent(string data) => data.StartsWith(RecordId, StringComparison.Ordinal);

    public static EventoArchivoInventarioDto Decode(string data)
    {
        var reader = new LectorTelegrama(data);
        reader.Raw(3); // "3RR"

        var stationLen = reader.LengthPrefix(2);
        var station = reader.RawValue(stationLen, TipoCampo.Numerico) ?? string.Empty;
        reader.ExpectEnd();

        return new EventoArchivoInventarioDto { Station = station };
    }
}
