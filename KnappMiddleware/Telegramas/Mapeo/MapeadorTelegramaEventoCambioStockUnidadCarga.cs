using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>Decodifica la respuesta de cambio de stock por unidad de carga (3UU, HIS V3 §4.4.3.1).</summary>
public static class MapeadorTelegramaEventoCambioStockUnidadCarga
{
    public const string RecordId = "3UU";
    public const string AckOk = "4UU00";
    public const string AckError = "4UU99";

    public static bool IsLoadUnitStockChangeEvent(string data) => data.StartsWith(RecordId, StringComparison.Ordinal);

    public static EventoCambioStockUnidadCargaDto Decode(string data)
    {
        var reader = new LectorTelegrama(data);
        reader.Raw(3); // "3UU"

        var codeLen = reader.LengthPrefix(2);
        var stationLen = reader.LengthPrefix(2);
        var geoCodeLen = reader.LengthPrefix(2);
        var loadUnitCode = reader.RawValue(codeLen, TipoCampo.Alfanumerico) ?? string.Empty;
        var station = reader.RawValue(stationLen, TipoCampo.Numerico);
        var geoCode = reader.RawValue(geoCodeLen, TipoCampo.Alfanumerico);

        var states = new List<LineaEstadoSlotDto>();
        if (reader.TryConsumeTag('T'))
        {
            var count = reader.LengthPrefix(2);
            var slotLen = reader.LengthPrefix(2);
            var stateLen = reader.LengthPrefix(2);
            for (var i = 0; i < count; i++)
            {
                var slot = reader.RawValue(slotLen, TipoCampo.Numerico);
                var state = reader.RawValue(stateLen, TipoCampo.Numerico);
                states.Add(new LineaEstadoSlotDto { SlotNumber = slot, State = state });
            }
        }

        reader.ExpectEnd();

        return new EventoCambioStockUnidadCargaDto
        {
            LoadUnitCode = loadUnitCode,
            Station = station,
            GeoCode = geoCode,
            States = states
        };
    }
}
