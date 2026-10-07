using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>
/// Decodifica el ajuste de stock que KiSoft empuja al Host (3SC, HIS V3 §4.4.1.1). Todos los bloques
/// tras el encabezado son etiquetados y opcionales (orden del spec: K,T,B,L,C,E,F,v,V,r,t,U,X); cuáles
/// vienen poblados depende del sistema de almacenamiento que generó el ajuste (§4.4.1.3). El bloque
/// "X" (diferencia) declara una longitud combinada signo+valor (HIS: "05 = 01+04") en vez de un
/// prefijo de longitud + valor normal, así que se lee crudo con <see cref="LectorTelegrama.Raw"/> en
/// vez de <see cref="TipoCampo"/>.
/// </summary>
public static class MapeadorTelegramaEventoAjusteStock
{
    public const string RecordId = "3SC";
    public const string AckOk = "4SC00";
    public const string AckError = "4SC99";

    public static bool IsStockAdjustmentEvent(string data) => data.StartsWith(RecordId, StringComparison.Ordinal);

    public static EventoAjusteStockDto Decode(string data)
    {
        var reader = new LectorTelegrama(data);
        reader.Raw(3); // "3SC"

        var correctionLen = reader.LengthPrefix(2);
        var correctionNumber = reader.RawValue(correctionLen, TipoCampo.Alfanumerico) ?? string.Empty;

        string? station = null;
        if (reader.TryConsumeTag('K'))
        {
            station = reader.Field(2, TipoCampo.Numerico);
        }

        string? messageType = null;
        if (reader.TryConsumeTag('T'))
        {
            messageType = reader.Field(2, TipoCampo.Numerico);
        }

        string? loadUnitCode = null;
        string? slotNumber = null;
        if (reader.TryConsumeTag('B'))
        {
            var codeLen = reader.LengthPrefix(2);
            var slotLen = reader.LengthPrefix(2);
            loadUnitCode = reader.RawValue(codeLen, TipoCampo.Alfanumerico);
            slotNumber = reader.RawValue(slotLen, TipoCampo.Numerico);
        }

        string? mandante = null;
        string? productNumber = null;
        string? packSize = null;
        string? stockType = null;
        if (reader.TryConsumeTag('L'))
        {
            var mandanteLen = reader.LengthPrefix(2);
            var productNumberLen = reader.LengthPrefix(2);
            var packSizeLen = reader.LengthPrefix(2);
            var stockTypeLen = reader.LengthPrefix(2);
            mandante = reader.RawValue(mandanteLen, TipoCampo.Alfanumerico);
            productNumber = reader.RawValue(productNumberLen, TipoCampo.Alfanumerico);
            packSize = reader.RawValue(packSizeLen, TipoCampo.Numerico);
            stockType = reader.RawValue(stockTypeLen, TipoCampo.Alfanumerico);
        }

        string? batchNumber = null;
        if (reader.TryConsumeTag('C'))
        {
            batchNumber = reader.Field(2, TipoCampo.Alfanumerico);
        }

        string? expirationDate = null;
        if (reader.TryConsumeTag('E'))
        {
            expirationDate = reader.Field(2, TipoCampo.Fecha);
        }

        string? stockQuality = null;
        if (reader.TryConsumeTag('F'))
        {
            stockQuality = reader.Field(2, TipoCampo.Numerico);
        }

        string? blockReasonChanged = null;
        if (reader.TryConsumeTag('v'))
        {
            blockReasonChanged = reader.Field(2, TipoCampo.Alfanumerico);
        }

        var blockReasons = new List<string>();
        if (reader.TryConsumeTag('V'))
        {
            var count = reader.LengthPrefix(2);
            var tokenLen = reader.LengthPrefix(2);
            for (var i = 0; i < count; i++)
            {
                var token = reader.RawValue(tokenLen, TipoCampo.Alfanumerico);
                if (token is not null)
                {
                    blockReasons.Add(token);
                }
            }
        }

        string? reason = null;
        if (reader.TryConsumeTag('r'))
        {
            var reasonLen = reader.LengthPrefix(2);
            var extraReasonLen = reader.LengthPrefix(2); // "motivo adicional", deprecado (HIS V3), siempre 00
            reason = reader.RawValue(reasonLen, TipoCampo.Alfanumerico);
            reader.RawValue(extraReasonLen, TipoCampo.Alfanumerico);
        }

        string? occurredAt = null;
        if (reader.TryConsumeTag('t'))
        {
            occurredAt = reader.Field(2, TipoCampo.Numerico);
        }

        string? warehouseOperator = null;
        if (reader.TryConsumeTag('U'))
        {
            warehouseOperator = reader.Field(2, TipoCampo.Alfanumerico);
        }

        string? difference = null;
        if (reader.TryConsumeTag('X'))
        {
            var totalLen = reader.LengthPrefix(2); // 05 = 01 (signo) + 04 (valor), ancho combinado
            difference = reader.Raw(totalLen);
        }

        reader.ExpectEnd();

        return new EventoAjusteStockDto
        {
            CorrectionNumber = correctionNumber,
            Station = station,
            MessageType = messageType,
            LoadUnitCode = loadUnitCode,
            SlotNumber = slotNumber,
            Mandante = mandante,
            ProductNumber = productNumber,
            PackSize = packSize,
            StockType = stockType,
            BatchNumber = batchNumber,
            ExpirationDate = expirationDate,
            StockQuality = stockQuality,
            BlockReasonChanged = blockReasonChanged,
            BlockReasons = blockReasons,
            Reason = reason,
            OccurredAt = occurredAt,
            WarehouseOperator = warehouseOperator,
            Difference = difference
        };
    }
}
