using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>Decodifica la respuesta de cambio de propiedades de stock por artículo/lote (3AU, HIS V3 §4.4.4.1).</summary>
public static class MapeadorTelegramaEventoCambioPropiedadesStock
{
    public const string RecordId = "3AU";
    public const string AckOk = "4AU00";
    public const string AckError = "4AU99";

    public static bool IsArticleStockChangeEvent(string data) => data.StartsWith(RecordId, StringComparison.Ordinal);

    public static EventoCambioPropiedadesStockDto Decode(string data)
    {
        var reader = new LectorTelegrama(data);
        reader.Raw(3); // "3AU"

        var stationLen = reader.LengthPrefix(2);
        var productNumberLen = reader.LengthPrefix(2);
        var batchNumberLen = reader.LengthPrefix(2);
        var mandanteLen = reader.LengthPrefix(2);
        var stockTypeLen = reader.LengthPrefix(2);

        var station = reader.RawValue(stationLen, TipoCampo.Numerico) ?? string.Empty;
        var productNumber = reader.RawValue(productNumberLen, TipoCampo.Alfanumerico) ?? string.Empty;
        var batchNumber = reader.RawValue(batchNumberLen, TipoCampo.Alfanumerico);
        var mandante = reader.RawValue(mandanteLen, TipoCampo.Alfanumerico);
        var stockType = reader.RawValue(stockTypeLen, TipoCampo.Alfanumerico);

        string? state = null;
        if (reader.TryConsumeTag('T'))
        {
            state = reader.Field(2, TipoCampo.Numerico);
        }

        reader.ExpectEnd();

        return new EventoCambioPropiedadesStockDto
        {
            Station = station,
            ProductNumber = productNumber,
            BatchNumber = batchNumber,
            Mandante = mandante,
            StockType = stockType,
            State = state
        };
    }
}
