using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>
/// Traduce el payload SAP de solicitud de inventario al registro 1IA (HIS §3.4.1.1). El bloque "Y"
/// (líneas de filtro) declara las anchuras de columna una única vez antes del LOOP; geocódigo, fecha
/// de duración y código de reservación están deshabilitados ("00") en esta instalación.
/// </summary>
public static class MapeadorTelegramaInventario
{
    public static string BuildRequest(SolicitudInventarioDto dto)
    {
        var items = dto.Items;

        var writer = new EscritorTelegrama();
        writer.Raw("1IA");
        writer.LengthPrefix(2, 16);
        writer.LengthPrefix(2, 7);
        writer.Reserved(2); // reservado
        writer.RawValue(16, TipoCampo.Alfanumerico, dto.Mandante);
        writer.RawValue(7, TipoCampo.Alfanumerico, dto.InventoryRequestNumber);

        writer.Tag('Y');
        writer.LengthPrefix(3, items.Count);
        writer.LengthPrefix(2, 3);  // número de estación
        writer.Reserved(2);         // geocódigo (no aplica en esta instalación)
        writer.LengthPrefix(2, 12); // número de artículo
        writer.LengthPrefix(2, 4);  // tamaño de embalaje
        writer.LengthPrefix(2, 8);  // tipo de stock
        writer.LengthPrefix(2, 20); // lote
        writer.Reserved(2);         // fecha de duración (no aplica en esta instalación)
        writer.Reserved(2);         // código de reservación (no aplica en esta instalación)
        writer.LengthPrefix(2, 8);  // código de unidad de carga
        writer.LengthPrefix(2, 2);  // número de slot

        foreach (var item in items)
        {
            writer.RawValue(3, TipoCampo.Numerico, item.Station);
            writer.RawValue(12, TipoCampo.Alfanumerico, item.ProductNumber);
            writer.RawValue(4, TipoCampo.Numerico, item.PackSize);
            writer.RawValue(8, TipoCampo.Alfanumerico, item.StockType);
            writer.RawValue(20, TipoCampo.Alfanumerico, item.BatchNumber);
            writer.RawValue(8, TipoCampo.Alfanumerico, item.LoadUnit);
            writer.RawValue(2, TipoCampo.Numerico, item.SlotNumber);
        }

        return writer.Build();
    }
}
