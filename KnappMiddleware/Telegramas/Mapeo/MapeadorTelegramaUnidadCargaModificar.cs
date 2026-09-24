using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>
/// Traduce el payload SAP de modificación de unidad de carga al registro 1UU (HIS §3.6.2.1). A
/// diferencia de los bloques "b"/"Y" (anchura de columna declarada una vez para todo el LOOP), aquí
/// cada línea del LOOP es un mini-registro con sus propios tags (N/L/C/E) y termina en "*". Cantidad
/// y calidad de stock no se transmiten en 1UU (celdas tachadas/deprecadas en el HIS, a diferencia de
/// 1UN); ver <see cref="MapeadorTelegramaUnidadCargaDisponible"/>.
/// </summary>
public static class MapeadorTelegramaUnidadCargaModificar
{
    public static string BuildRequest(SolicitudUnidadCargaModificarDto dto)
    {
        var writer = new EscritorTelegrama();
        writer.Raw("1UU");
        writer.LengthPrefix(2, 6);
        writer.LengthPrefix(2, 3);
        writer.LengthPrefix(2, 12);
        writer.RawValue(6, TipoCampo.Alfanumerico, dto.LoadUnitCode);
        writer.RawValue(3, TipoCampo.Numerico, dto.Station);
        writer.RawValue(12, TipoCampo.Alfanumerico, dto.GeoCode);

        writer.Tag('X');
        writer.LengthPrefix(2, dto.Items.Count);

        foreach (var item in dto.Items)
        {
            writer.Tag('N').Reserved(2); // número de slot: no provisto por SAP en la muestra

            writer.Tag('L');
            writer.LengthPrefix(2, 16);
            writer.LengthPrefix(2, 12);
            writer.LengthPrefix(2, 4);
            writer.LengthPrefix(2, 8);
            writer.RawValue(16, TipoCampo.Alfanumerico, dto.Mandante);
            writer.RawValue(12, TipoCampo.Alfanumerico, item.ProductNumber);
            writer.RawValue(4, TipoCampo.Numerico, item.PackSize);
            writer.RawValue(8, TipoCampo.Alfanumerico, item.StockType);

            writer.Tag('C').Field(2, 20, TipoCampo.Alfanumerico, item.BatchNumber);
            writer.Tag('E').Field(2, 8, TipoCampo.Fecha, UtilTextoTelegrama.DigitsOnly(item.ExpirationDate));
            writer.Raw("*");
        }

        return writer.Build();
    }
}
