using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>Decodifica el evento con los datos de stock en tiempo real de un artículo (3XR, HIS V3 §4.3.2.1).</summary>
public static class MapeadorTelegramaEventoStockArticulo
{
    public const string RecordId = "3XR";
    public const string AckOk = "4XR00";
    public const string AckError = "4XR99";

    public static bool IsStockArticleEvent(string data) => data.StartsWith(RecordId, StringComparison.Ordinal);

    public static EventoStockArticuloDto Decode(string data)
    {
        var reader = new LectorTelegrama(data);
        reader.Raw(3); // "3XR"

        var lines = new List<LineaEventoStockArticuloDto>();
        if (reader.TryConsumeTag('i'))
        {
            lines.AddRange(DecodeLines(reader));
        }

        reader.ExpectEnd();

        return new EventoStockArticuloDto { Lines = lines };
    }

    /// <summary>Bloque "i" (HIS §4.3.2.1): 11 anchuras de columna declaradas una vez, luego el LOOP de valores (una fila por unidad de carga o slot ocupado).</summary>
    private static List<LineaEventoStockArticuloDto> DecodeLines(LectorTelegrama reader)
    {
        var count = reader.LengthPrefix(3);
        var stationLen = reader.LengthPrefix(2);
        var mandanteLen = reader.LengthPrefix(2);
        var productNumberLen = reader.LengthPrefix(2);
        var packSizeLen = reader.LengthPrefix(2);
        var stockTypeLen = reader.LengthPrefix(2);
        var batchNumberLen = reader.LengthPrefix(2);
        var expirationDateLen = reader.LengthPrefix(2);
        var quantityLen = reader.LengthPrefix(2);
        var stockQualityLen = reader.LengthPrefix(2);
        var loadUnitLen = reader.LengthPrefix(2);
        var slotNumberLen = reader.LengthPrefix(2);

        var lines = new List<LineaEventoStockArticuloDto>(count);
        for (var i = 0; i < count; i++)
        {
            var station = reader.RawValue(stationLen, TipoCampo.Numerico);
            var mandante = reader.RawValue(mandanteLen, TipoCampo.Alfanumerico);
            var productNumber = reader.RawValue(productNumberLen, TipoCampo.Alfanumerico);
            var packSize = reader.RawValue(packSizeLen, TipoCampo.Numerico);
            var stockType = reader.RawValue(stockTypeLen, TipoCampo.Alfanumerico);
            var batchNumber = reader.RawValue(batchNumberLen, TipoCampo.Alfanumerico);
            var expirationDate = reader.RawValue(expirationDateLen, TipoCampo.Fecha);
            var quantity = reader.RawValue(quantityLen, TipoCampo.Numerico);
            var stockQuality = reader.RawValue(stockQualityLen, TipoCampo.Numerico);
            var loadUnitCode = reader.RawValue(loadUnitLen, TipoCampo.Alfanumerico);
            var slotNumber = reader.RawValue(slotNumberLen, TipoCampo.Numerico);

            lines.Add(new LineaEventoStockArticuloDto
            {
                Station = station,
                Mandante = mandante,
                ProductNumber = productNumber,
                PackSize = packSize,
                StockType = stockType,
                BatchNumber = batchNumber,
                ExpirationDate = expirationDate,
                Quantity = quantity,
                StockQuality = stockQuality,
                LoadUnitCode = loadUnitCode,
                SlotNumber = slotNumber
            });
        }

        return lines;
    }
}
