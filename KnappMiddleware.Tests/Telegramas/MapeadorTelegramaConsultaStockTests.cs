using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// 1XR (HIS V3 §3.5.2): a diferencia de la mayoría de telegramas, mandante y tipo de stock tienen
/// presencia opcional ("00" o su ancho nominal) — estos tests cubren ambos casos, con la muestra
/// real de SAP (JSON-SAP/1XR_consulta_de_stock_articulo.json) y con los campos opcionales ausentes.
/// </summary>
public class MapeadorTelegramaConsultaStockTests
{
    [Fact]
    public void BuildRequest_ConCamposOpcionalesPresentes_CoincideConMuestraReal()
    {
        var dto = new SolicitudConsultaStockDto
        {
            Mandante = "A1301",
            Station = "065",
            ProductNumber = "ASP500TAB001",
            StockType = "STANDARD",
            BatchNumber = "LOT20260901"
        };

        var expected =
            "1XR" +
            "03" + "16" + "12" + "08" +        // longitudes agrupadas: estación, mandante, artículo, tipo de stock
            "065" +                            // estación (3, exacto)
            "A1301           " +               // mandante (16, padded)
            "ASP500TAB001" +                   // número de artículo (12, exacto)
            "STANDARD" +                       // tipo de stock (8, exacto)
            "C" + "20" + "LOT20260901         "; // bloque C: lote (20, padded)

        var actual = MapeadorTelegramaConsultaStock.BuildRequest(dto);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildRequest_SinMandanteNiTipoDeStock_UsaLongitudCero()
    {
        var dto = new SolicitudConsultaStockDto
        {
            Mandante = "",
            Station = "065",
            ProductNumber = "ASP500TAB001",
            StockType = null,
            BatchNumber = null
        };

        var expected =
            "1XR" +
            "03" + "00" + "12" + "00" +        // mandante y tipo de stock ausentes -> longitud 00
            "065" +
            "ASP500TAB001" +
            "C" + "20" + "                    "; // bloque C: lote ausente, 20 espacios

        var actual = MapeadorTelegramaConsultaStock.BuildRequest(dto);

        Assert.Equal(expected, actual);
    }
}
