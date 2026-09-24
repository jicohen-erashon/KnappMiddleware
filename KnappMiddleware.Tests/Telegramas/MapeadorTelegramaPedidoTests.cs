using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// 12N (HIS §3.2.1.1): el LOOP más grande de los 9 telegramas (19 anchuras de columna declaradas
/// antes de los valores, con 10 columnas habilitadas y 9 reservadas/deshabilitadas intercaladas).
/// Sin cobertura de padding hasta ahora — es el mapper de mayor riesgo si una anchura se corre.
/// </summary>
public class MapeadorTelegramaPedidoTests
{
    [Fact]
    public void BuildNew_MuestraRealDeSap_CoincideByteAByte()
    {
        // Valores de JSON-SAP/12N_orden.json (fecha normalizada a guion ASCII).
        var dto = new SolicitudPedidoDto
        {
            Mandante = "A1301",
            OrderNumber = "INB000000001",
            SheetNumber = "0000",
            OrderType = "04",
            LoadUnit = "00001234",
            Items =
            [
                new LineaPedidoDto
                {
                    LineReference = "000010",
                    Station = "065",
                    ProductNumber = "ASP500TAB001",
                    PackSize = "0001",
                    StockType = "STANDARD",
                    BatchNumber = "LOT20260901",
                    ExpirationDate = "2028-09-30",
                    Quantity = 120,
                    StockQuality = "1"
                }
            ]
        };

        var expected =
            "12N" +
            "16" + "12" + "04" +                                   // longitudes agrupadas: mandante, pedido, hoja
            "A1301" + new string(' ', 11) +                        // mandante (16): 5 + 11 espacios
            "INB000000001" +                                       // pedido (12, exacto)
            "0000" +                                                // hoja (4 numérico)
            "T" + "02" + "04" +                                     // tipo de pedido
            "D" + "08" + "00001234" +                               // unidad de carga (bloque D)
            "b" + "001" +                                           // contador de LOOP (3 dígitos)
            "20" + "03" + "12" + "04" + "08" + "20" + "08" + "00" + "04" + "01" +
            "00" + "00" + "00" + "00" + "00" + "00" + "00" + "00" + "00" +      // 19 anchuras en total
            "000010" + new string(' ', 14) +                       // referencia de línea (20): 6 + 14 espacios
            "065" +                                                 // estación
            "ASP500TAB001" +                                        // artículo (12, exacto)
            "0001" +                                                // embalaje
            "STANDARD" +                                            // tipo de stock (8, exacto)
            "LOT20260901" + new string(' ', 9) +                    // lote (20): 11 + 9 espacios
            "20280930" +                                            // fecha: DigitsOnly quita los guiones
            "0120" +                                                // cantidad 120 -> 4 numérico
            "1";                                                    // calidad de stock

        var actual = MapeadorTelegramaPedido.BuildNew(dto);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildNew_ValoresCortosYOpcionalesAusentes_RellenaSegunElSpec()
    {
        var dto = new SolicitudPedidoDto
        {
            Mandante = "A1",
            OrderNumber = "P1",
            SheetNumber = "7",
            OrderType = "05",
            LoadUnit = null,
            Items =
            [
                new LineaPedidoDto
                {
                    LineReference = null,
                    Station = "65",
                    ProductNumber = "X",
                    PackSize = "1",
                    StockType = "STD",
                    BatchNumber = null,
                    ExpirationDate = null,
                    Quantity = 5,
                    StockQuality = "1"
                }
            ]
        };

        var expected =
            "12N" +
            "16" + "12" + "04" +
            "A1" + new string(' ', 14) +                           // mandante corto: 2 + 14 espacios
            "P1" + new string(' ', 10) +                           // pedido corto: 2 + 10 espacios
            "0007" +                                                // hoja "7" -> 4 numérico
            "T" + "02" + "05" +
            // sin bloque "D": LoadUnit es null, Block lo omite por completo (no emite "00")
            "b" + "001" +
            "20" + "03" + "12" + "04" + "08" + "20" + "08" + "00" + "04" + "01" +
            "00" + "00" + "00" + "00" + "00" + "00" + "00" + "00" + "00" +
            new string(' ', 20) +                                  // referencia de línea ausente: 20 espacios
            "065" +                                                 // estación "65" -> 3 numérico, cero precedente
            "X" + new string(' ', 11) +                            // artículo "X" -> 12: 1 + 11 espacios
            "0001" +                                                // embalaje "1" -> 4 numérico
            "STD" + new string(' ', 5) +                           // tipo de stock "STD" -> 8: 3 + 5 espacios
            new string(' ', 20) +                                  // lote ausente: 20 espacios
            "00000000" +                                            // fecha ausente: fecha vacía -> ceros
            "0005" +                                                // cantidad 5 -> 4 numérico
            "1";

        var actual = MapeadorTelegramaPedido.BuildNew(dto);

        Assert.Equal(expected, actual);
    }
}
