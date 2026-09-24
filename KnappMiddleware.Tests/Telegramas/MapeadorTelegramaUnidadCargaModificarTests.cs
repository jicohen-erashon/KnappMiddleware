using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// 1UU (HIS §3.6.2.1): a diferencia de los LOOP con anchuras declaradas una vez (12N/1IA), cada línea
/// es un mini-registro con sus propios tags (N/L/C/E) terminado en "*". Sin cobertura hasta ahora.
///
/// La muestra real de SAP (JSON-SAP/1UU_modificar_unidad_de_carga.json) trae <c>loadunit</c> de 8
/// caracteres contra un ancho de spec de 6, y no incluye <c>station</c> (requerido) — gap ya conocido
/// y fuera de alcance (ver JSON-SAP/PENDIENTE-1XR.md). Estos tests usan un <c>loadunit</c> corto y sí
/// incluyen <c>station</c>, para poder probar el padding sin tropezar con ese gap.
/// </summary>
public class MapeadorTelegramaUnidadCargaModificarTests
{
    private static string Pad(string value, int width) => value + new string(' ', width - value.Length);

    [Fact]
    public void BuildRequest_ValoresCortos_RellenaYDelimitaCadaLineaConAsterisco()
    {
        var dto = new SolicitudUnidadCargaModificarDto
        {
            Mandante = "A1301",
            LoadUnitCode = "1234",
            Station = "65",
            GeoCode = "OSR001002001",
            Items =
            [
                new LineaUnidadCargaModificarDto
                {
                    ProductNumber = "ASP500TAB001",
                    PackSize = "1",
                    StockType = "STANDARD",
                    BatchNumber = "LOT20260902",
                    ExpirationDate = "2028-10-31"
                }
            ]
        };

        var expected =
            "1UU" +
            "06" + "03" + "12" +
            Pad("1234", 6) +                                        // unidad de carga (6): alfanumérico -> espacios finales
            "065" +                                                  // estación "65" -> 3 numérico, cero precedente
            "OSR001002001" +                                        // geocódigo (12, exacto)
            "X" + "01" +                                             // contador de LOOP (2 dígitos)
            "N" + "00" +                                             // slot: reservado
            "L" + "16" + "12" + "04" + "08" +
            Pad("A1301", 16) +
            "ASP500TAB001" +
            "0001" +                                                // embalaje "1" -> 4 numérico
            "STANDARD" +
            "C" + "20" + Pad("LOT20260902", 20) +
            "E" + "08" + "20281031" +
            "*";

        var actual = MapeadorTelegramaUnidadCargaModificar.BuildRequest(dto);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildRequest_DosLineasSinLoteNiFecha_RellenaConEspaciosYCeros()
    {
        var dto = new SolicitudUnidadCargaModificarDto
        {
            Mandante = "A1301",
            LoadUnitCode = "AB",
            Station = "7",
            GeoCode = "X",
            Items =
            [
                new LineaUnidadCargaModificarDto
                {
                    ProductNumber = "P1",
                    PackSize = "2",
                    StockType = "A",
                    BatchNumber = null,
                    ExpirationDate = null
                },
                new LineaUnidadCargaModificarDto
                {
                    ProductNumber = "P2",
                    PackSize = "3",
                    StockType = "B",
                    BatchNumber = null,
                    ExpirationDate = null
                }
            ]
        };

        static string Linea(string producto, string embalaje, string tipo) =>
            "N" + "00" +
            "L" + "16" + "12" + "04" + "08" +
            Pad("A1301", 16) +
            Pad(producto, 12) +
            embalaje +
            Pad(tipo, 8) +
            "C" + "20" + new string(' ', 20) +                     // lote ausente: 20 espacios
            "E" + "08" + "00000000" +                                // fecha ausente: fecha vacía -> ceros
            "*";

        var expected =
            "1UU" +
            "06" + "03" + "12" +
            Pad("AB", 6) +
            "007" +
            Pad("X", 12) +
            "X" + "02" +
            Linea("P1", "0002", "A") +
            Linea("P2", "0003", "B");

        var actual = MapeadorTelegramaUnidadCargaModificar.BuildRequest(dto);

        Assert.Equal(expected, actual);
    }
}
