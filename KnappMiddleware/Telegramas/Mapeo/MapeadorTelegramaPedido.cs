using System.Globalization;
using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>
/// Traduce el payload SAP de pedido de entrada al registro 12N (HIS §3.2.1), para los tipos de
/// pedido 04 y 05 (los únicos con muestra JSON real; ver <see cref="SolicitudPedidoDto"/>). A
/// diferencia de los datos maestros (14N/15N/16N), el pedido NO lleva bracket de apertura/cierre:
/// es un único telegrama.
/// El bloque "b" (líneas de pedido) declara, para esta instalación, 19 anchuras de columna antes del
/// LOOP (HIS §3.2.1.1); solo referencia de línea, estación, artículo, embalaje, tipo de stock, lote,
/// fecha de duración, cantidad y calidad de stock están habilitadas (anchura &gt; 0) — el resto
/// (nota de procesamiento, medio de carga, código de UC por línea, slot, bloqueo, cantidades máximas)
/// están deshabilitadas (anchura "00") en esta instalación y varias aparecen tachadas en el spec.
/// </summary>
public static class MapeadorTelegramaPedido
{
    public static readonly IReadOnlyCollection<string> SupportedOrderTypes = ["04", "05"];

    public static string BuildNew(SolicitudPedidoDto dto)
    {
        if (!SupportedOrderTypes.Contains(dto.OrderType))
        {
            throw new NotSupportedException(
                $"Tipo de pedido '{dto.OrderType}' no soportado todavía (solo {string.Join("/", SupportedOrderTypes)}); " +
                "el tipo 02 (Transporte) y los tipos de salida (10/35/36) necesitan una muestra JSON real de SAP para mapear sus bloques.");
        }

        var writer = new EscritorTelegrama();
        writer.Raw("12N");
        writer.LengthPrefix(2, 16);
        writer.LengthPrefix(2, 12);
        writer.LengthPrefix(2, 4);
        writer.RawValue(16, TipoCampo.Alfanumerico, dto.Mandante);
        writer.RawValue(12, TipoCampo.Alfanumerico, dto.OrderNumber);
        writer.RawValue(4, TipoCampo.Numerico, dto.SheetNumber);

        writer.Tag('T').Field(2, 2, TipoCampo.Numerico, dto.OrderType);

        writer.Block('D', dto.LoadUnit is not null, w => w.Field(2, 8, TipoCampo.Alfanumerico, dto.LoadUnit));

        writer.Tag('b');
        writer.LengthPrefix(3, dto.Items.Count);
        writer.LengthPrefix(2, 20); // referencia de línea
        writer.LengthPrefix(2, 3);  // número de estación
        writer.LengthPrefix(2, 12); // número de artículo
        writer.LengthPrefix(2, 4);  // tamaño de embalaje
        writer.LengthPrefix(2, 8);  // tipo de stock
        writer.LengthPrefix(2, 20); // lote
        writer.LengthPrefix(2, 8);  // fecha de duración
        writer.Reserved(2);         // código de reservación (no usado)
        writer.LengthPrefix(2, 4);  // cantidad
        writer.LengthPrefix(2, 1);  // calidad de stock
        writer.Reserved(2);         // nota de procesamiento (no usado)
        writer.Reserved(2);         // medio de carga (no usado)
        writer.Reserved(2);         // código de unidad de carga por línea (no usado)
        writer.Reserved(2);         // número de slot (no usado)
        writer.Reserved(2);         // estado de bloqueo (no usado)
        writer.Reserved(2);         // cantidad máxima UC 1/1 (no usado)
        writer.Reserved(2);         // cantidad máxima UC 2/2 (no usado)
        writer.Reserved(2);         // cantidad máxima UC 4/4 (no usado)
        writer.Reserved(2);         // cantidad máxima UC 8/8 (no usado)

        foreach (var item in dto.Items)
        {
            writer.RawValue(20, TipoCampo.Alfanumerico, item.LineReference);
            writer.RawValue(3, TipoCampo.Numerico, item.Station);
            writer.RawValue(12, TipoCampo.Alfanumerico, item.ProductNumber);
            writer.RawValue(4, TipoCampo.Numerico, item.PackSize);
            writer.RawValue(8, TipoCampo.Alfanumerico, item.StockType);
            writer.RawValue(20, TipoCampo.Alfanumerico, item.BatchNumber);
            writer.RawValue(8, TipoCampo.Fecha, UtilTextoTelegrama.DigitsOnly(item.ExpirationDate));
            writer.RawValue(4, TipoCampo.Numerico, item.Quantity.ToString(CultureInfo.InvariantCulture));
            writer.RawValue(1, TipoCampo.Numerico, item.StockQuality);
        }

        return writer.Build();
    }
}
