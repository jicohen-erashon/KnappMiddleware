using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>
/// Decodifica el evento de resultado de inventario que KiSoft empuja al Host (3IR, HIS V3 §4.2.1,
/// pág. 94-96 — sin cambios respecto a V2). Igual que en <see cref="MapeadorTelegramaEventoPedido"/>,
/// las anchuras de cada campo se leen de los propios prefijos de longitud que KiSoft transmite, no de
/// una tabla fija "para esta instalación". Tres sub-campos del bloque de líneas están marcados como
/// deprecados en el HIS V3 (referencia de línea, código de reservación, nota de procesamiento — su
/// longitud declarada es siempre 00): se consumen igual (por si alguna vez viniera longitud &gt; 0)
/// pero se descartan, sin exponerse en <see cref="LineaEventoInventarioDto"/>.
/// </summary>
public static class MapeadorTelegramaEventoInventario
{
    public const string RecordId = "3IR";
    public const string AckOk = "4IR00";
    public const string AckError = "4IR99";

    public static bool IsInventoryEvent(string data) => data.StartsWith(RecordId, StringComparison.Ordinal);

    public static EventoInventarioDto Decode(string data)
    {
        var reader = new LectorTelegrama(data);
        reader.Raw(3); // "3IR"

        var mandanteLen = reader.LengthPrefix(2);
        var requestLen = reader.LengthPrefix(2);
        var reservedLen = reader.LengthPrefix(2); // reservado (HIS §4.2.1), longitud declarada 00
        var mandante = reader.RawValue(mandanteLen, TipoCampo.Alfanumerico) ?? string.Empty;
        var requestNumber = reader.RawValue(requestLen, TipoCampo.Alfanumerico) ?? string.Empty;
        reader.RawValue(reservedLen, TipoCampo.Alfanumerico);

        var states = new List<string>();
        if (reader.TryConsumeTag('O'))
        {
            var count = reader.LengthPrefix(2);
            var stateLen = reader.LengthPrefix(2);
            for (var i = 0; i < count; i++)
            {
                var state = reader.RawValue(stateLen, TipoCampo.Numerico);
                if (state is not null)
                {
                    states.Add(state);
                }
            }
        }

        var lines = new List<LineaEventoInventarioDto>();
        if (reader.TryConsumeTag('b'))
        {
            lines.AddRange(DecodeLines(reader));
        }

        reader.ExpectEnd();

        return new EventoInventarioDto
        {
            Mandante = mandante,
            InventoryRequestNumber = requestNumber,
            States = states,
            Lines = lines
        };
    }

    /// <summary>Bloque de líneas de stock (HIS §4.2.1): 17 anchuras de columna declaradas una vez, luego el LOOP de valores.</summary>
    private static List<LineaEventoInventarioDto> DecodeLines(LectorTelegrama reader)
    {
        var count = reader.LengthPrefix(3);
        var lineReferenceLen = reader.LengthPrefix(2); // deprecado en V3, no se usa
        var stationLen = reader.LengthPrefix(2);
        var productNumberLen = reader.LengthPrefix(2);
        var packSizeLen = reader.LengthPrefix(2);
        var stockTypeLen = reader.LengthPrefix(2);
        var batchNumberLen = reader.LengthPrefix(2);
        var expirationDateLen = reader.LengthPrefix(2);
        var reservationCodeLen = reader.LengthPrefix(2); // deprecado en V3, no se usa
        var quantityLen = reader.LengthPrefix(2);
        var stockQualityLen = reader.LengthPrefix(2);
        var processingNoteLen = reader.LengthPrefix(2); // deprecado en V3, no se usa
        var loadMediumLen = reader.LengthPrefix(2);
        var loadUnitLen = reader.LengthPrefix(2);
        var slotNumberLen = reader.LengthPrefix(2);
        var blockStatusLen = reader.LengthPrefix(2);
        var lineStatusLen = reader.LengthPrefix(2);
        var warehouseOperatorLen = reader.LengthPrefix(2);
        var processedAtLen = reader.LengthPrefix(2);

        var lines = new List<LineaEventoInventarioDto>(count);
        for (var i = 0; i < count; i++)
        {
            reader.RawValue(lineReferenceLen, TipoCampo.Alfanumerico);
            var station = reader.RawValue(stationLen, TipoCampo.Numerico);
            var productNumber = reader.RawValue(productNumberLen, TipoCampo.Alfanumerico);
            var packSize = reader.RawValue(packSizeLen, TipoCampo.Numerico);
            var stockType = reader.RawValue(stockTypeLen, TipoCampo.Alfanumerico);
            var batchNumber = reader.RawValue(batchNumberLen, TipoCampo.Alfanumerico);
            var expirationDate = reader.RawValue(expirationDateLen, TipoCampo.Fecha);
            reader.RawValue(reservationCodeLen, TipoCampo.Alfanumerico);
            var quantity = reader.RawValue(quantityLen, TipoCampo.Numerico);
            var stockQuality = reader.RawValue(stockQualityLen, TipoCampo.Numerico);
            reader.RawValue(processingNoteLen, TipoCampo.Alfanumerico);
            var loadMedium = reader.RawValue(loadMediumLen, TipoCampo.Alfanumerico);
            var loadUnitCode = reader.RawValue(loadUnitLen, TipoCampo.Alfanumerico);
            var slotNumber = reader.RawValue(slotNumberLen, TipoCampo.Numerico);
            var blockStatus = reader.RawValue(blockStatusLen, TipoCampo.Alfanumerico);
            var lineStatus = reader.RawValue(lineStatusLen, TipoCampo.Numerico);
            var warehouseOperator = reader.RawValue(warehouseOperatorLen, TipoCampo.Alfanumerico);
            var processedAt = reader.RawValue(processedAtLen, TipoCampo.Numerico);

            lines.Add(new LineaEventoInventarioDto
            {
                Station = station,
                ProductNumber = productNumber,
                PackSize = packSize,
                StockType = stockType,
                BatchNumber = batchNumber,
                ExpirationDate = expirationDate,
                Quantity = quantity,
                StockQuality = stockQuality,
                LoadMedium = loadMedium,
                LoadUnitCode = loadUnitCode,
                SlotNumber = slotNumber,
                BlockStatus = blockStatus,
                LineStatus = lineStatus,
                WarehouseOperator = warehouseOperator,
                ProcessedAt = processedAt
            });
        }

        return lines;
    }
}
