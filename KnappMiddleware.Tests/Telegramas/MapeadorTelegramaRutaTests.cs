using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// Round-trip del encabezado 16N: verifica el fix del bug sistemático (longitudes agrupadas antes
/// que los valores, no intercaladas por campo) contra una trama construida byte a byte a mano.
/// </summary>
public class MapeadorTelegramaRutaTests
{
    [Fact]
    public void BuildNew_GroupsLengthPrefixesBeforeValues()
    {
        var dto = new SolicitudRutaDto
        {
            Mandante = "A1301",
            RouteNumber = "RUTA0001",
            DepartureTime = "10:34:17",
            AvailableTime = "10:30:00",
            RampNumber = 1
        };

        var expected =
            "16N" +
            "16" + "08" +                  // longitudes agrupadas: mandante, número de ruta (HIS pág. 35: ancho fijo 8)
            "A1301           " +           // mandante (16, padded)
            "RUTA0001" +                   // número de ruta (8, exacto)
            "Z" + "06" + "06" + "00" +      // longitudes agrupadas: salida, disposición, día (reservado)
            "103417" + "103000" +          // valores: salida, disposición
            "R" + "01" + "05" +            // cantidad de rampas, longitud de rampa
            "00001";                       // valor: número de rampa

        var actual = MapeadorTelegramaRuta.BuildNew(dto);

        Assert.Equal(expected, actual);
    }
}
