using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>
/// Decodifica el evento de pedido que KiSoft empuja al Host (32R, HIS §4.1). A diferencia de los
/// mappers de escritura, aquí las anchuras de cada campo NO se asumen: se leen del propio prefijo de
/// longitud que KiSoft acaba de transmitir (por eso no hace falta una tabla de anchuras "para esta
/// instalación" — el emisor ya las declaró). El orden de los bloques opcionales (T,f,A,B,C,D,E,G,s,e,
/// t,O,Z/b) es el del HIS spec; cada uno se intenta consumir por su tag antes de pasar al siguiente.
/// El bloque de líneas (Z/b) es la parte menos verificada de este mapper (17 sub-campos posibles,
/// varios de uso infrecuente) — su interpretación se derivó del spec pero no se ha validado todavía
/// contra tráfico real de KiSoft.
/// </summary>
public static class MapeadorTelegramaEventoPedido
{
    public const string RecordId = "32R";
    public const string AckOk = "42R00";
    public const string AckError = "42R99";

    public static bool IsOrderEvent(string data) => data.StartsWith(RecordId, StringComparison.Ordinal);

    public static EventoPedidoDto Decode(string data)
    {
        var reader = new LectorTelegrama(data);
        reader.Raw(3); // "32R"

        var mandanteLen = reader.LengthPrefix(2);
        var orderLen = reader.LengthPrefix(2);
        var sheetLen = reader.LengthPrefix(2);
        var mandante = reader.RawValue(mandanteLen, TipoCampo.Alfanumerico) ?? string.Empty;
        var orderNumber = reader.RawValue(orderLen, TipoCampo.Alfanumerico) ?? string.Empty;
        var sheetNumber = reader.RawValue(sheetLen, TipoCampo.Numerico) ?? string.Empty;

        string? orderType = null;
        if (reader.TryConsumeTag('T'))
        {
            orderType = reader.Field(2, TipoCampo.Numerico);
        }

        string? initialSheetNumber = null;
        if (reader.TryConsumeTag('f'))
        {
            initialSheetNumber = reader.Field(2, TipoCampo.Numerico);
        }

        string? sheetCount = null;
        if (reader.TryConsumeTag('A'))
        {
            var sheetCountLen = reader.LengthPrefix(2);
            var reservedLen = reader.LengthPrefix(2); // "transmitido a KNAPP", no se usa
            sheetCount = reader.RawValue(sheetCountLen, TipoCampo.Numerico);
            reader.RawValue(reservedLen, TipoCampo.Numerico);
        }

        string? startStation = null;
        if (reader.TryConsumeTag('B'))
        {
            startStation = reader.Field(2, TipoCampo.Numerico);
        }

        string? loadMedium = null;
        if (reader.TryConsumeTag('C'))
        {
            loadMedium = reader.Field(2, TipoCampo.Alfanumerico);
        }

        string? loadUnitCode = null;
        if (reader.TryConsumeTag('D'))
        {
            loadUnitCode = reader.Field(2, TipoCampo.Alfanumerico);
        }

        string? businessPartner = null;
        if (reader.TryConsumeTag('E'))
        {
            businessPartner = reader.Field(2, TipoCampo.Alfanumerico);
        }

        string? dispatchRamp = null;
        if (reader.TryConsumeTag('G'))
        {
            dispatchRamp = reader.Field(2, TipoCampo.Numerico);
        }

        string? startTime = null;
        if (reader.TryConsumeTag('s'))
        {
            startTime = reader.Field(2, TipoCampo.Numerico);
        }

        string? endTime = null;
        if (reader.TryConsumeTag('e'))
        {
            endTime = reader.Field(2, TipoCampo.Numerico);
        }

        string? lastReadStation = null;
        string? lastReadTimestamp = null;
        bool? lastReadDiverted = null;
        if (reader.TryConsumeTag('t'))
        {
            var stationLen = reader.LengthPrefix(2);
            var timeLen = reader.LengthPrefix(2);
            var stateLen = reader.LengthPrefix(2);
            lastReadStation = reader.RawValue(stationLen, TipoCampo.Numerico);
            lastReadTimestamp = reader.RawValue(timeLen, TipoCampo.Numerico);
            var stateRaw = reader.RawValue(stateLen, TipoCampo.Numerico);
            lastReadDiverted = stateRaw switch { "1" => true, "0" => false, _ => null };
        }

        var orderStatusCodes = new List<string>();
        if (reader.TryConsumeTag('O'))
        {
            var count = reader.LengthPrefix(2);
            var codeLen = reader.LengthPrefix(2);
            for (var i = 0; i < count; i++)
            {
                var code = reader.RawValue(codeLen, TipoCampo.Numerico);
                if (code is not null)
                {
                    orderStatusCodes.Add(code);
                }
            }
        }

        var lines = new List<LineaEventoPedidoDto>();
        if (reader.TryConsumeTag('Z') || reader.TryConsumeTag('b'))
        {
            lines.AddRange(DecodeLines(reader));
        }

        reader.ExpectEnd();

        return new EventoPedidoDto
        {
            Mandante = mandante,
            OrderNumber = orderNumber,
            SheetNumber = sheetNumber,
            OrderType = orderType,
            InitialSheetNumber = initialSheetNumber,
            SheetCount = sheetCount,
            StartStation = startStation,
            LoadMedium = loadMedium,
            LoadUnitCode = loadUnitCode,
            BusinessPartner = businessPartner,
            DispatchRamp = dispatchRamp,
            StartTime = startTime,
            EndTime = endTime,
            LastReadStation = lastReadStation,
            LastReadTimestamp = lastReadTimestamp,
            LastReadDiverted = lastReadDiverted,
            OrderStatusCodes = orderStatusCodes,
            Lines = lines
        };
    }

    /// <summary>Bloque de líneas (HIS §4.1.5.13): 17 anchuras de columna declaradas una única vez, luego el LOOP de valores.</summary>
    private static List<LineaEventoPedidoDto> DecodeLines(LectorTelegrama reader)
    {
        var count = reader.LengthPrefix(3);
        var lineReferenceLen = reader.LengthPrefix(2);
        var stationLen = reader.LengthPrefix(2);
        var productNumberLen = reader.LengthPrefix(2);
        var packSizeLen = reader.LengthPrefix(2);
        var stockTypeLen = reader.LengthPrefix(2);
        var batchNumberLen = reader.LengthPrefix(2);
        var expirationDateLen = reader.LengthPrefix(2);
        var reservationLen = reader.LengthPrefix(2); // código de reservación, no usado
        var quantityLen = reader.LengthPrefix(2);
        var stockQualityLen = reader.LengthPrefix(2);
        var lineStatusLen = reader.LengthPrefix(2);
        var warehouseOperatorLen = reader.LengthPrefix(2);
        var processedAtLen = reader.LengthPrefix(2);
        var loadUnitOriginLen = reader.LengthPrefix(2); // código de UC de origen/destino, no usado en esta instalación
        var slotOriginLen = reader.LengthPrefix(2); // número de slot en la UC de origen/destino, no usado
        var serialNumberLen = reader.LengthPrefix(2);
        var geoCodeLen = reader.LengthPrefix(2);

        var lines = new List<LineaEventoPedidoDto>(count);
        for (var i = 0; i < count; i++)
        {
            var lineReference = reader.RawValue(lineReferenceLen, TipoCampo.Alfanumerico);
            var station = reader.RawValue(stationLen, TipoCampo.Numerico);
            var productNumber = reader.RawValue(productNumberLen, TipoCampo.Alfanumerico);
            var packSize = reader.RawValue(packSizeLen, TipoCampo.Numerico);
            var stockType = reader.RawValue(stockTypeLen, TipoCampo.Alfanumerico);
            var batchNumber = reader.RawValue(batchNumberLen, TipoCampo.Alfanumerico);
            var expirationDate = reader.RawValue(expirationDateLen, TipoCampo.Fecha);
            reader.RawValue(reservationLen, TipoCampo.Alfanumerico);
            var quantity = reader.RawValue(quantityLen, TipoCampo.Numerico);
            var stockQuality = reader.RawValue(stockQualityLen, TipoCampo.Numerico);
            var lineStatus = reader.RawValue(lineStatusLen, TipoCampo.Numerico);
            var warehouseOperator = reader.RawValue(warehouseOperatorLen, TipoCampo.Alfanumerico);
            var processedAt = reader.RawValue(processedAtLen, TipoCampo.Numerico);
            reader.RawValue(loadUnitOriginLen, TipoCampo.Alfanumerico);
            reader.RawValue(slotOriginLen, TipoCampo.Numerico);
            var serialNumber = reader.RawValue(serialNumberLen, TipoCampo.Alfanumerico);
            var geoCode = reader.RawValue(geoCodeLen, TipoCampo.Alfanumerico);

            lines.Add(new LineaEventoPedidoDto
            {
                LineReference = lineReference,
                Station = station,
                ProductNumber = productNumber,
                PackSize = packSize,
                StockType = stockType,
                BatchNumber = batchNumber,
                ExpirationDate = expirationDate,
                Quantity = quantity,
                StockQuality = stockQuality,
                LineStatus = lineStatus,
                WarehouseOperator = warehouseOperator,
                ProcessedAt = processedAt,
                SerialNumber = serialNumber,
                GeoCode = geoCode
            });
        }

        return lines;
    }
}
