using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Telegramas;
using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// Cobertura del mapper de 14N: encabezado mínimo (solo campos obligatorios) y la conversión de
/// unidad de dimensión/peso (`umlwh`/`umweigth`) agregada para que el bloque D/G no asuma siempre
/// mm/kg cuando SAP declara una unidad distinta.
/// </summary>
public class MapeadorTelegramaArticuloTests
{
    [Fact]
    public void BuildNew_MinimalDto_WritesHeaderAndMandatoryLBlockOnly()
    {
        var dto = new SolicitudArticuloDto
        {
            Mandante = "A1301",
            Station = "061",
            ProductNumber = "ASP500TAB001",
            PackSize = "0001"
        };

        var expected =
            "14N" +
            "00" + "00" +                  // reservado: sistema de líneas de estanterías, estantería
            "00" + "00" + "00" +            // longitudes agrupadas (0): bloque/canal/nivel de estantería
            "061" +                         // número de estación
            // (bloque/canal/nivel de estantería no se transmiten: longitud 0)
            "L" + "16" + "12" + "04" + "00" + // longitudes agrupadas: mandante, artículo, embalaje, reservado
            "A1301           " +            // mandante (16, padded)
            "ASP500TAB001" +                // número de artículo (12, exacto)
            "0001";                         // tamaño de embalaje (4, exacto)

        var actual = MapeadorTelegramaArticulo.BuildNew(dto);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildNew_DefaultUnits_MatchesPreConversionBehavior()
    {
        // Sin umlwh/umweigth (null): debe comportarse igual que antes de agregar la conversión de
        // unidades, asumiendo MM/KG — mismos valores que trae el JSON de muestra real de SAP.
        var dto = new SolicitudArticuloDto
        {
            Mandante = "A1301",
            Station = "061",
            ProductNumber = "ASP500TAB001",
            PackSize = "0001",
            LengthMm = 120m,
            WidthMm = 55m,
            HeightMm = 25m,
            GrossWeightKg = 0.065m
        };

        var expected =
            "14N" +
            "00" + "00" +
            "00" + "00" + "00" +
            "061" +
            "L" + "16" + "12" + "04" + "00" +
            "A1301           " +
            "ASP500TAB001" +
            "0001" +
            "D" + "04" + "04" + "04" + "00" +   // longitudes agrupadas: longitud, ancho, altura, reservado (bolsa)
            "0120" + "0055" + "0025" +          // 120mm, 55mm, 25mm sin conversión (unidad por defecto MM)
            "G" + "06" +                        // longitud de peso
            "000650";                           // 0.065 kg * 10000 = 650 (1/10 de gramo), unidad por defecto KG

        var actual = MapeadorTelegramaArticulo.BuildNew(dto);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildNew_NonDefaultUnits_ConvertsToMillimetersAndTenthsOfGram()
    {
        // umlwh="CM" y umweigth="LB": fija la conversión real agregada al mapper (antes de este fix,
        // el mapper ignoraba estos campos y asumía siempre MM/KG, un gap silencioso para cualquier
        // artículo cuyo umlwh/umweigth real no fuera MM/KG).
        var dto = new SolicitudArticuloDto
        {
            Mandante = "A1301",
            Station = "061",
            ProductNumber = "ASP500TAB001",
            PackSize = "0001",
            LengthMm = 12m,     // 12 cm -> 120 mm
            WidthMm = 5.5m,     // 5.5 cm -> 55 mm
            HeightMm = 2.5m,    // 2.5 cm -> 25 mm
            DimensionUnit = "CM",
            GrossWeightKg = 1m, // 1 lb -> 0.45359237 kg -> 4535.9237 (1/10 g) -> redondeado 4536
            WeightUnit = "LB"
        };

        var expected =
            "14N" +
            "00" + "00" +
            "00" + "00" + "00" +
            "061" +
            "L" + "16" + "12" + "04" + "00" +
            "A1301           " +
            "ASP500TAB001" +
            "0001" +
            "D" + "04" + "04" + "04" + "00" +
            "0120" + "0055" + "0025" +
            "G" + "06" +
            "004536";

        var actual = MapeadorTelegramaArticulo.BuildNew(dto);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildNew_UnrecognizedDimensionUnit_ThrowsExcepcionFormatoTelegrama()
    {
        var dto = new SolicitudArticuloDto
        {
            Mandante = "A1301",
            Station = "061",
            ProductNumber = "ASP500TAB001",
            PackSize = "0001",
            LengthMm = 120m,
            DimensionUnit = "PULGADAS"
        };

        Assert.Throws<ExcepcionFormatoTelegrama>(() => MapeadorTelegramaArticulo.BuildNew(dto));
    }
}
