using KnappMiddleware.Telegramas;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// Cubre la guarda de overflow de <see cref="EscritorTelegrama.LengthPrefix"/>: si un contador de
/// LOOP (o cualquier prefijo de longitud) no cabe en los dígitos declarados, debe lanzar en vez de
/// emitir un dígito de más y desalinear en silencio el resto del registro.
/// </summary>
public class EscritorTelegramaTests
{
    [Fact]
    public void LengthPrefix_ValorQueNoCabeEnLosDigitos_Lanza()
    {
        var ex = Assert.Throws<ExcepcionFormatoTelegrama>(() => new EscritorTelegrama().LengthPrefix(2, 100));
        Assert.Equal("El prefijo de longitud '100' (3 dígitos) excede los dígitos declarados (2).", ex.Message);
    }

    [Fact]
    public void LengthPrefix_ValorEnElLimite_NoLanza()
    {
        Assert.Equal("99", new EscritorTelegrama().LengthPrefix(2, 99).Build());
        Assert.Equal("999", new EscritorTelegrama().LengthPrefix(3, 999).Build());
    }

    [Fact]
    public void LengthPrefix_RellenaConCerosALaIzquierda()
    {
        Assert.Equal("001", new EscritorTelegrama().LengthPrefix(3, 1).Build());
        Assert.Equal("00", new EscritorTelegrama().LengthPrefix(2, 0).Build());
    }

    [Fact]
    public void LengthPrefix_ValorNegativo_Lanza()
    {
        Assert.Throws<ExcepcionFormatoTelegrama>(() => new EscritorTelegrama().LengthPrefix(2, -1));
    }

    [Fact]
    public void Field_LongitudQueNoCabeEnElPrefijo_Lanza()
    {
        // Confirma que Field (tras delegar en LengthPrefix) hereda la misma guarda.
        Assert.Throws<ExcepcionFormatoTelegrama>(() => new EscritorTelegrama().Field(2, 100, TipoCampo.Alfanumerico, "x"));
    }

    [Fact]
    public void Reserved_EscribeCeros()
    {
        Assert.Equal("00", new EscritorTelegrama().Reserved(2).Build());
    }
}
