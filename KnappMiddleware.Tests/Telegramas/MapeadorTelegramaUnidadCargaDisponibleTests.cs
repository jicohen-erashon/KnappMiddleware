using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// 1UN (HIS §3.6.1.1): estructura casi idéntica a 1UU (mini-registros por línea con tags N/L/C/E),
/// pero SÍ transmite cantidad (S) y calidad de stock (F), y no lleva el terminador "*" que 1UU sí
/// tiene. Sin cobertura hasta ahora — mismo gap de <c>loadunit</c>/<c>station</c> que 1UU (ver esa
/// clase de test), mismas sustituciones aplicadas acá.
/// </summary>
public class MapeadorTelegramaUnidadCargaDisponibleTests
{
    private static string Pad(string value, int width) => value + new string(' ', width - value.Length);

    [Fact]
    public void BuildRequest_ValoresCortos_TransmiteCantidadYCalidadSinAsterisco()
    {
        // Valores basados en JSON-SAP/1UN_Unidad_de_carga_disponible.json.
        var dto = new SolicitudUnidadCargaDisponibleDto
        {
            Mandante = "A1301",
            LoadUnitCode = "1234",
            Station = "65",
            GeoCode = "001002034001",
            Items =
            [
                new LineaUnidadCargaDisponibleDto
                {
                    ProductNumber = "ASP500TAB001",
                    PackSize = "0040",
                    StockType = "STANDARD",
                    BatchNumber = "LOT20260901",
                    ExpirationDate = "2028-09-30",
                    Quantity = 40,
                    StockQuality = "1"
                }
            ]
        };

        var expected =
            "1UN" +
            "06" + "03" + "12" +
            Pad("1234", 6) +
            "065" +
            "001002034001" +                                        // geocódigo (12, exacto)
            "X" + "01" +
            "N" + "00" +
            "L" + "16" + "12" + "04" + "08" +
            Pad("A1301", 16) +
            "ASP500TAB001" +
            "0040" +
            "STANDARD" +
            "C" + "20" + Pad("LOT20260901", 20) +
            "E" + "08" + "20280930" +
            "S" + "04" + "0040" +                                    // cantidad 40 -> 4 numérico
            "F" + "01" + "1";
            // (sin "*": diferencia estructural con 1UU)

        var actual = MapeadorTelegramaUnidadCargaDisponible.BuildRequest(dto);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildRequest_CantidadCorta_RellenaConCerosPrecedentes()
    {
        var dto = new SolicitudUnidadCargaDisponibleDto
        {
            Mandante = "A1301",
            LoadUnitCode = "1234",
            Station = "65",
            GeoCode = "001002034001",
            Items =
            [
                new LineaUnidadCargaDisponibleDto
                {
                    ProductNumber = "ASP500TAB001",
                    PackSize = "0040",
                    StockType = "STANDARD",
                    BatchNumber = null,
                    ExpirationDate = null,
                    Quantity = 5,
                    StockQuality = "1"
                }
            ]
        };

        var actual = MapeadorTelegramaUnidadCargaDisponible.BuildRequest(dto);

        Assert.EndsWith("S" + "04" + "0005" + "F" + "01" + "1", actual);
        Assert.DoesNotContain("*", actual);
    }
}
