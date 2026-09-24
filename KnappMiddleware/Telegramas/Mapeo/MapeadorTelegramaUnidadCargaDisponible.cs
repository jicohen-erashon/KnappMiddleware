using System.Globalization;
using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>
/// Traduce el payload SAP de unidad de carga puesta a disposición al registro 1UN (HIS §3.6.1.1).
/// Cada línea del LOOP es un mini-registro con sus propios tags (N/L/C/E/S/F); a diferencia de 1UU,
/// aquí sí se transmiten cantidad (S) y calidad de stock (F), y no hay carácter separador "*".
/// El número de slot (N) está deshabilitado ("00") para esta instalación.
/// </summary>
public static class MapeadorTelegramaUnidadCargaDisponible
{
    public static string BuildRequest(SolicitudUnidadCargaDisponibleDto dto)
    {
        var writer = new EscritorTelegrama();
        writer.Raw("1UN");
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
            writer.Tag('N').Reserved(2); // número de slot: no aplica en esta instalación (HIS §3.6.1.1)

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
            writer.Tag('S').Field(2, 4, TipoCampo.Numerico, item.Quantity.ToString(CultureInfo.InvariantCulture));
            writer.Tag('F').Field(2, 1, TipoCampo.Numerico, item.StockQuality);
        }

        return writer.Build();
    }
}
