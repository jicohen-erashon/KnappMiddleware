using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// 1IA (HIS §3.4.1.1): LOOP de 10 columnas con 3 deshabilitadas ("00") intercaladas entre las
/// habilitadas (posiciones 2, 7 y 8, no agrupadas al final) — la interleaving es la parte propensa
/// a error y sin cobertura hasta ahora.
/// </summary>
public class MapeadorTelegramaInventarioTests
{
    [Fact]
    public void BuildRequest_MuestraRealDeSap_DeclaraLasAnchurasIntercaladas()
    {
        // Valores de JSON-SAP/1IA_solicitud_de_inventario.json.
        var dto = new SolicitudInventarioDto
        {
            Mandante = "A1301",
            InventoryRequestNumber = "INV0001",
            Items =
            [
                new LineaFiltroInventarioDto
                {
                    Station = "065",
                    ProductNumber = "ASP500TAB001",
                    PackSize = "0001",
                    StockType = "STANDARD",
                    BatchNumber = "LOT20260901",
                    LoadUnit = "00001234",
                    SlotNumber = "01"
                }
            ]
        };

        var expected =
            "1IA" +
            "16" + "07" + "00" +                                    // mandante, solicitud, reservado de cabecera
            "A1301           " +                                    // mandante (16): 5 + 11 espacios
            "INV0001" +                                              // solicitud (7, exacto)
            "Y" + "001" +                                            // contador de LOOP (3 dígitos)
            "03" + "00" + "12" + "04" + "08" + "20" + "00" + "00" + "08" + "02" +  // 10 anchuras, 3 deshabilitadas intercaladas
            "065" +                                                  // estación
            "ASP500TAB001" +                                         // artículo (12, exacto)
            "0001" +                                                 // embalaje
            "STANDARD" +                                             // tipo de stock (8, exacto)
            "LOT20260901" + new string(' ', 9) +                    // lote (20): 11 + 9 espacios
            "00001234" +                                             // unidad de carga (8, exacto)
            "01";                                                    // slot

        var actual = MapeadorTelegramaInventario.BuildRequest(dto);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildRequest_LineaDeFiltroVacia_RellenaCadaColumnaSegunSuTipo()
    {
        var dto = new SolicitudInventarioDto
        {
            Mandante = "A1301",
            InventoryRequestNumber = "INV1",
            Items =
            [
                new LineaFiltroInventarioDto
                {
                    Station = null,
                    ProductNumber = null,
                    PackSize = null,
                    StockType = null,
                    BatchNumber = null,
                    LoadUnit = null,
                    SlotNumber = null
                }
            ]
        };

        var expected =
            "1IA" +
            "16" + "07" + "00" +
            "A1301           " +
            "INV1   " +                                              // solicitud corta (4): + 3 espacios
            "Y" + "001" +
            "03" + "00" + "12" + "04" + "08" + "20" + "00" + "00" + "08" + "02" +
            "000" +                                                  // estación ausente: numérico -> "0" -> "000"
            new string(' ', 12) +                                   // artículo ausente: alfanumérico -> espacios
            "0000" +                                                 // embalaje ausente: numérico -> ceros
            new string(' ', 8) +                                    // tipo de stock ausente: espacios
            new string(' ', 20) +                                   // lote ausente: espacios
            new string(' ', 8) +                                    // unidad de carga ausente: espacios
            "00";                                                    // slot ausente: numérico -> ceros

        var actual = MapeadorTelegramaInventario.BuildRequest(dto);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildRequest_UnidadDeCargaCorta_RellenaConEspaciosNoConCeros()
    {
        // Trampa: loadunit "parece" numérico pero el campo es alfanumérico (HIS) -> espacios a la
        // derecha, nunca ceros a la izquierda como pasaría si fuera TipoCampo.Numerico.
        var dto = new SolicitudInventarioDto
        {
            Mandante = "A1301",
            InventoryRequestNumber = "INV0001",
            Items =
            [
                new LineaFiltroInventarioDto
                {
                    Station = "065",
                    ProductNumber = "ASP500TAB001",
                    PackSize = "0001",
                    StockType = "STANDARD",
                    BatchNumber = "LOT20260901",
                    LoadUnit = "1234",
                    SlotNumber = "01"
                }
            ]
        };

        var actual = MapeadorTelegramaInventario.BuildRequest(dto);

        Assert.EndsWith("1234    01", actual);
    }
}
