using KnappMiddleware.Contratos.Sap;
using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// 15N (HIS §3.1.2.2): 9 bloques opcionales alfanuméricos, la mayoría de ancho 30, con dos casos
/// especiales de ancho 6/2. Sin cobertura de padding hasta ahora. Los literales largos se rellenan
/// con <c>value + new string(' ', ancho - value.Length)</c> en vez de contar espacios a mano, para
/// que el test no dependa de una cuenta manual correcta.
/// </summary>
public class MapeadorTelegramaSocioTests
{
    private static string Pad(string value, int width) => value + new string(' ', width - value.Length);

    [Fact]
    public void BuildNew_MuestraRealDeSap_RellenaTodosLosBloques()
    {
        // Valores basados en JSON-SAP/15N_socio_comercial.json.
        var mandante = "A1301";
        var partner = "0000100001";
        var company = "FARMACIA CENTRAL GUATEMALA";
        var title = "SEÑOR";
        var street = "10 AVENIDA 12-34 ZONA 10";
        var city = "GUATEMALA";
        var postalCode = "01010";
        var region = "GUATEMALA";
        var countryCode = "GT";
        var email = "recepcion@farmaciacentral.gt";
        var telephone = "+502 2222-3344";

        var dto = new SolicitudSocioDto
        {
            Mandante = mandante,
            PartnerNumber = partner,
            Company = company,
            Title = title,
            Street = street,
            City = city,
            PostalCode = postalCode,
            Region = region,
            CountryCode = countryCode,
            Email = email,
            Telephone = telephone
        };

        var expected =
            "15N" +
            "16" + "12" +
            Pad(mandante, 16) +
            Pad(partner, 12) +
            "C" + "30" + Pad(company, 30) +
            "A" + "30" + Pad(title, 30) +
            "S" + "30" + Pad(street, 30) +
            "P" + "30" + Pad(city, 30) +
            "Z" + "06" + Pad(postalCode, 6) +      // trampa: CP de 5 dígitos en campo ALFANUMÉRICO -> 1 espacio, no "0"
            "R" + "30" + Pad(region, 30) +
            "O" + "02" + countryCode +               // ancho exacto (2), sin relleno
            "E" + "30" + Pad(email, 30) +
            "L" + "30" + Pad(telephone, 30);

        var actual = MapeadorTelegramaSocio.BuildNew(dto);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildNew_SoloCamposObligatorios_OmiteLosBloquesOpcionales()
    {
        var dto = new SolicitudSocioDto
        {
            Mandante = "A1301",
            PartnerNumber = "0000100001"
        };

        // Ningún bloque opcional presente: Block(...) omite el tag por completo (no emite "00"),
        // a diferencia del esquema de longitud opcional que usa MapeadorTelegramaConsultaStock.
        var expected =
            "15N" +
            "16" + "12" +
            Pad("A1301", 16) +
            Pad("0000100001", 12);

        var actual = MapeadorTelegramaSocio.BuildNew(dto);

        Assert.Equal(expected, actual);
    }
}
