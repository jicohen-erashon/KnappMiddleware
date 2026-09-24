using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>
/// Traduce el payload SAP de artículo al registro de datos maestros 14N (HIS §3.1.1.2), envuelto en
/// el bracket de apertura/cierre de datos maestros (§3.1.1.1: 141 = crear/borrar/crear nuevamente,
/// 149 = fin de transmisión). Estación CBS001 (bloque/canal/nivel); "sistema de líneas de
/// estanterías" y "estantería" no se transmiten para esta estación (HIS §3.1.1.4.2) y SAP no los
/// envía en el payload de muestra, así que se omiten siempre.
/// </summary>
public static class MapeadorTelegramaArticulo
{
    /// <summary>140/141: crear todos (descarta existentes) / crear-borrar-crear nuevamente (upsert). Se usa 141 (upsert) por defecto.</summary>
    public const string OpenUpsert = "141";

    public const string Close = "149";

    public static string BuildNew(SolicitudArticuloDto dto)
    {
        // Encabezado: HIS §3.1.1.4.2 declara las 5 longitudes agrupadas (sistema/estantería/módulo/
        // canal/nivel) ANTES que sus valores; "número de estación" es de ancho fijo (03) y no lleva
        // prefijo propio, por eso se escribe entre el grupo de prefijos y el grupo de valores.
        var rackBlockLen = dto.RackBlock is null ? 0 : 3;
        var rackChannelLen = dto.RackChannel is null ? 0 : 3;
        var rackLevelLen = dto.RackLevel is null ? 0 : 3;

        var writer = new EscritorTelegrama();
        writer.Raw("14N");
        writer.Reserved(2); // sistema de líneas de estanterías (no aplica a CBS001)
        writer.Reserved(2); // estantería (no aplica a CBS001)
        writer.LengthPrefix(2, rackBlockLen);
        writer.LengthPrefix(2, rackChannelLen);
        writer.LengthPrefix(2, rackLevelLen);
        writer.RawValue(3, TipoCampo.Numerico, dto.Station);
        writer.RawValue(rackBlockLen, TipoCampo.Numerico, dto.RackBlock);
        writer.RawValue(rackChannelLen, TipoCampo.Numerico, dto.RackChannel);
        writer.RawValue(rackLevelLen, TipoCampo.Numerico, dto.RackLevel);

        // L: número de artículo / tamaño de embalaje — obligatorio para todas las estaciones (HIS §3.1.1.4.1).
        writer.Tag('L');
        writer.LengthPrefix(2, 16);
        writer.LengthPrefix(2, 12);
        writer.LengthPrefix(2, 4);
        writer.Reserved(2);
        writer.RawValue(16, TipoCampo.Alfanumerico, dto.Mandante);
        writer.RawValue(12, TipoCampo.Alfanumerico, dto.ProductNumber);
        writer.RawValue(4, TipoCampo.Numerico, dto.PackSize);

        writer.Block('Y', dto.EjectionNumber is not null, w => w.Field(2, 2, TipoCampo.Numerico, dto.EjectionNumber));

        writer.Block('M', dto.MaxAutomatedQuantity is not null,
            w => w.Field(2, 4, TipoCampo.Numerico, dto.MaxAutomatedQuantity?.ToString()));

        var hasDimensions = dto.LengthMm is not null || dto.WidthMm is not null || dto.HeightMm is not null;
        writer.Block('D', hasDimensions, w =>
        {
            w.LengthPrefix(2, 4);
            w.LengthPrefix(2, 4);
            w.LengthPrefix(2, 4);
            w.Reserved(2); // anchura de la bolsa (no usado)
            w.RawValue(4, TipoCampo.Numerico, FormatMillimeters(dto.LengthMm, dto.DimensionUnit));
            w.RawValue(4, TipoCampo.Numerico, FormatMillimeters(dto.WidthMm, dto.DimensionUnit));
            w.RawValue(4, TipoCampo.Numerico, FormatMillimeters(dto.HeightMm, dto.DimensionUnit));
        });

        // G: peso en 1/10 gramos. El HIS solo admite un único campo de peso; se usa el peso bruto
        // (el que efectivamente mueve el eyector/robot), aunque SAP también envíe el neto.
        writer.Block('G', dto.GrossWeightKg is not null,
            w => w.Field(2, 6, TipoCampo.Numerico, FormatTenthsOfGram(dto.GrossWeightKg, dto.WeightUnit)));

        var barcodes = dto.Barcodes?.Where(b => !string.IsNullOrWhiteSpace(b.EanCode)).ToList();
        writer.Block('B', barcodes is { Count: > 0 }, w =>
        {
            w.LengthPrefix(2, barcodes!.Count);
            w.LengthPrefix(2, 20); // longitud de código de artículo, declarada una vez para todo el bloque
            foreach (var barcode in barcodes)
            {
                w.RawValue(20, TipoCampo.Alfanumerico, barcode.EanCode);
            }
        });

        writer.Block('K', dto.Description is not null || dto.GeoCode is not null, w =>
        {
            w.LengthPrefix(2, 40);
            w.LengthPrefix(2, 12);
            w.RawValue(40, TipoCampo.Alfanumerico, dto.Description);
            w.RawValue(12, TipoCampo.Alfanumerico, dto.GeoCode);
        });

        writer.Block('S', dto.ReplenishmentMinQty is not null || dto.ReplenishmentMaxQty is not null, w =>
        {
            w.LengthPrefix(2, 4);
            w.LengthPrefix(2, 4);
            w.RawValue(4, TipoCampo.Numerico, dto.ReplenishmentMinQty?.ToString());
            w.RawValue(4, TipoCampo.Numerico, dto.ReplenishmentMaxQty?.ToString());
        });

        var properties = dto.Properties?.Where(p => !string.IsNullOrWhiteSpace(p.Property)).ToList();
        writer.Block('E', properties is { Count: > 0 }, w =>
        {
            w.LengthPrefix(2, properties!.Count);
            w.LengthPrefix(2, 2); // longitud de propiedad de artículo, declarada una vez para todo el bloque
            foreach (var property in properties)
            {
                w.RawValue(2, TipoCampo.Numerico, property.Property);
            }
        });

        writer.Block('T', dto.ReplenishmentStation is not null || dto.ReplenishmentGeoCode is not null, w =>
        {
            w.LengthPrefix(2, 3);
            w.LengthPrefix(2, 12);
            w.RawValue(3, TipoCampo.Numerico, dto.ReplenishmentStation);
            w.RawValue(12, TipoCampo.Alfanumerico, dto.ReplenishmentGeoCode);
        });

        return writer.Build();
    }

    // dto.LengthMm/WidthMm/HeightMm traen el valor tal cual lo envía SAP (no necesariamente en mm) —
    // "umlwh" declara la unidad real (MM/CM/M, default MM si SAP no la envía). El HIS solo admite mm
    // entero (HIS §3.1.1.4.2).
    private static string? FormatMillimeters(decimal? value, string? unit)
    {
        if (value is null)
        {
            return null;
        }

        var mm = NormalizeUnit(unit) switch
        {
            "" or "MM" => value.Value,
            "CM" => value.Value * 10m,
            "M" => value.Value * 1000m,
            var otro => throw new ExcepcionFormatoTelegrama($"Unidad de dimensión '{otro}' no reconocida (esperado MM/CM/M, campo 'umlwh').")
        };

        return Math.Round(mm, MidpointRounding.AwayFromZero).ToString("F0");
    }

    // dto.NetWeightKg/GrossWeightKg traen el valor tal cual lo envía SAP (no necesariamente en kg) —
    // "umweigth" declara la unidad real (KG/G/LB/OZ, default KG si SAP no la envía). El HIS solo admite
    // 1/10 de gramo entero (HIS §3.1.1.4.2, bloque G).
    private static string? FormatTenthsOfGram(decimal? value, string? unit)
    {
        if (value is null)
        {
            return null;
        }

        var kg = NormalizeUnit(unit) switch
        {
            "" or "KG" => value.Value,
            "G" => value.Value / 1000m,
            "LB" => value.Value * 0.45359237m,
            "OZ" => value.Value * 0.0283495231m,
            var otro => throw new ExcepcionFormatoTelegrama($"Unidad de peso '{otro}' no reconocida (esperado KG/G/LB/OZ, campo 'umweigth').")
        };

        return Math.Round(kg * 10000m, MidpointRounding.AwayFromZero).ToString("F0");
    }

    private static string NormalizeUnit(string? unit) => unit?.Trim().ToUpperInvariant() ?? "";
}
