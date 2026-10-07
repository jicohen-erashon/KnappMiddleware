using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>Decodifica el aviso de unidad de carga vacía (3UE, HIS V3 §4.4.2.1).</summary>
public static class MapeadorTelegramaEventoUnidadCargaVacia
{
    public const string RecordId = "3UE";
    public const string AckOk = "4UE00";
    public const string AckError = "4UE99";

    public static bool IsLoadUnitEmptyEvent(string data) => data.StartsWith(RecordId, StringComparison.Ordinal);

    public static EventoUnidadCargaVaciaDto Decode(string data)
    {
        var reader = new LectorTelegrama(data);
        reader.Raw(3); // "3UE"

        var codeLen = reader.LengthPrefix(2);
        var loadUnitCode = reader.RawValue(codeLen, TipoCampo.Alfanumerico) ?? string.Empty;
        reader.ExpectEnd();

        return new EventoUnidadCargaVaciaDto { LoadUnitCode = loadUnitCode };
    }
}
