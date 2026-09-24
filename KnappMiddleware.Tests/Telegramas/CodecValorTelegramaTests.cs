using KnappMiddleware.Telegramas;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// Cobertura directa de las 3 reglas de padding del HIS ("si un valor tiene menos caracteres que la
/// longitud de campo, deberán completarse"): alfanumérico -> espacios a la derecha, numérico ->
/// ceros a la izquierda, fecha vacía -> ceros. También cubre la asimetría corregida en decode
/// (KiSoft recorta espacios al principio Y al final; nuestro encode solo rellena a la derecha).
/// </summary>
public class CodecValorTelegramaTests
{
    // ===== Encode - alfanumérico =====

    [Fact]
    public void Encode_Alfanumerico_RellenaConEspaciosALaDerecha()
    {
        Assert.Equal("AB      ", CodecValorTelegrama.Encode(TipoCampo.Alfanumerico, 8, "AB"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Encode_AlfanumericoVacioONulo_RellenaTodoConEspacios(string? value)
    {
        Assert.Equal("    ", CodecValorTelegrama.Encode(TipoCampo.Alfanumerico, 4, value));
    }

    [Fact]
    public void Encode_AlfanumericoDelAnchoExacto_NoSobreRellena()
    {
        Assert.Equal("STANDARD", CodecValorTelegrama.Encode(TipoCampo.Alfanumerico, 8, "STANDARD"));
    }

    [Fact]
    public void Encode_DigitosEnCampoAlfanumerico_RellenaConEspacioNoConCero()
    {
        // Trampa: un código postal u otro campo "que parece numérico" pero es alfanumérico (HIS)
        // debe rellenarse con espacio a la derecha, nunca con cero a la izquierda.
        Assert.Equal("01010 ", CodecValorTelegrama.Encode(TipoCampo.Alfanumerico, 6, "01010"));
    }

    // ===== Encode - numérico =====

    [Fact]
    public void Encode_Numerico_RellenaConCerosPrecedentes()
    {
        Assert.Equal("0012", CodecValorTelegrama.Encode(TipoCampo.Numerico, 4, "12"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Encode_NumericoVacioONulo_TransmiteCeros(string? value)
    {
        Assert.Equal("0000", CodecValorTelegrama.Encode(TipoCampo.Numerico, 4, value));
    }

    [Fact]
    public void Encode_NumericoConCaracteresNoNumericos_Lanza()
    {
        var ex = Assert.Throws<ExcepcionFormatoTelegrama>(() => CodecValorTelegrama.Encode(TipoCampo.Numerico, 4, "12A"));
        Assert.Equal("Valor numérico con caracteres no numéricos: '12A'.", ex.Message);
    }

    [Fact]
    public void Encode_NumericoConEspacioColado_Lanza()
    {
        // Un espacio (p. ej. de un valor mal recortado) no es un dígito.
        Assert.Throws<ExcepcionFormatoTelegrama>(() => CodecValorTelegrama.Encode(TipoCampo.Numerico, 4, " 12"));
    }

    // ===== Encode - fecha =====

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Encode_FechaVacia_SeTransmiteConCeros(string? value)
    {
        Assert.Equal("00000000", CodecValorTelegrama.Encode(TipoCampo.Fecha, 8, value));
    }

    [Fact]
    public void Encode_FechaConValor_SeTransmiteTalCual()
    {
        Assert.Equal("20280930", CodecValorTelegrama.Encode(TipoCampo.Fecha, 8, "20280930"));
    }

    [Fact]
    public void Encode_FechaCorta_RellenaConCerosALaIzquierda()
    {
        Assert.Equal("00000930", CodecValorTelegrama.Encode(TipoCampo.Fecha, 8, "930"));
    }

    // ===== Encode - longitud declarada =====

    [Theory]
    [InlineData(TipoCampo.Alfanumerico)]
    [InlineData(TipoCampo.Numerico)]
    [InlineData(TipoCampo.Fecha)]
    public void Encode_LongitudCero_DevuelveVacioSinValidarElValor(TipoCampo kind)
    {
        // length == 0 es el mecanismo de "campo deshabilitado en esta instalación" / "campo opcional
        // ausente" (ver MapeadorTelegramaConsultaStock) — debe ganarle a EnsureWithinLength, no lanzar.
        Assert.Equal(string.Empty, CodecValorTelegrama.Encode(kind, 0, "X"));
    }

    [Fact]
    public void Encode_AlfanumericoMasLargoQueLoDeclarado_Lanza()
    {
        var ex = Assert.Throws<ExcepcionFormatoTelegrama>(() => CodecValorTelegrama.Encode(TipoCampo.Alfanumerico, 4, "ABCDE"));
        Assert.Equal("El valor 'ABCDE' (5 caracteres) excede la longitud declarada (4).", ex.Message);
    }

    [Fact]
    public void Encode_NumericoMasLargoQueLoDeclarado_Lanza()
    {
        var ex = Assert.Throws<ExcepcionFormatoTelegrama>(() => CodecValorTelegrama.Encode(TipoCampo.Numerico, 4, "12345"));
        Assert.Equal("El valor '12345' (5 caracteres) excede la longitud declarada (4).", ex.Message);
    }

    [Fact]
    public void Encode_FechaMasLargaQueLoDeclarado_Lanza()
    {
        Assert.Throws<ExcepcionFormatoTelegrama>(() => CodecValorTelegrama.Encode(TipoCampo.Fecha, 4, "20280930"));
    }

    // ===== Decode - alfanumérico (fix del recorte simétrico) =====

    [Fact]
    public void Decode_Alfanumerico_RecortaEspaciosEnAmbosExtremos()
    {
        // Regresión del fix: antes solo se recortaba a la derecha (TrimEnd), dejando colado un
        // espacio a la izquierda si KiSoft (u otro emisor) rellenaba por ese lado.
        Assert.Equal("AB", CodecValorTelegrama.Decode(TipoCampo.Alfanumerico, "  AB  "));
    }

    [Fact]
    public void Decode_AlfanumericoSoloConRellenoALaDerecha_SigueFuncionando()
    {
        Assert.Equal("AB", CodecValorTelegrama.Decode(TipoCampo.Alfanumerico, "AB      "));
    }

    [Fact]
    public void Decode_AlfanumericoTodoEspacios_DevuelveVacio()
    {
        Assert.Equal(string.Empty, CodecValorTelegrama.Decode(TipoCampo.Alfanumerico, "        "));
    }

    [Fact]
    public void Decode_AlfanumericoConEspaciosInternos_LosPreserva()
    {
        Assert.Equal("A B", CodecValorTelegrama.Decode(TipoCampo.Alfanumerico, " A B  "));
    }

    [Fact]
    public void Decode_AlfanumericoVacio_DevuelveVacio()
    {
        Assert.Equal(string.Empty, CodecValorTelegrama.Decode(TipoCampo.Alfanumerico, ""));
    }

    // ===== Decode - numérico =====

    [Fact]
    public void Decode_Numerico_QuitaCerosPrecedentes()
    {
        Assert.Equal("12", CodecValorTelegrama.Decode(TipoCampo.Numerico, "0012"));
    }

    [Fact]
    public void Decode_NumericoTodoCeros_DevuelveCero()
    {
        Assert.Equal("0", CodecValorTelegrama.Decode(TipoCampo.Numerico, "0000"));
    }

    [Fact]
    public void Decode_NumericoVacio_DevuelveVacio()
    {
        Assert.Equal(string.Empty, CodecValorTelegrama.Decode(TipoCampo.Numerico, ""));
    }

    // ===== Decode - fecha =====

    [Fact]
    public void Decode_FechaTodoCeros_DevuelveVacio()
    {
        Assert.Equal(string.Empty, CodecValorTelegrama.Decode(TipoCampo.Fecha, "00000000"));
    }

    [Fact]
    public void Decode_FechaConValor_SeDevuelveTalCual()
    {
        Assert.Equal("20280930", CodecValorTelegrama.Decode(TipoCampo.Fecha, "20280930"));
    }

    [Fact]
    public void Decode_FechaVacia_DevuelveVacio()
    {
        Assert.Equal(string.Empty, CodecValorTelegrama.Decode(TipoCampo.Fecha, ""));
    }

    // ===== Round-trip =====

    [Theory]
    [InlineData(TipoCampo.Alfanumerico, 16, "A1301")]
    [InlineData(TipoCampo.Numerico, 4, "120")]
    [InlineData(TipoCampo.Fecha, 8, "20280930")]
    public void RoundTrip_DecodeDeEncode_DevuelveElValorOriginal(TipoCampo kind, int length, string value)
    {
        var encoded = CodecValorTelegrama.Encode(kind, length, value);
        Assert.Equal(value, CodecValorTelegrama.Decode(kind, encoded));
    }

    [Fact]
    public void RoundTrip_AlfanumericoConEspacioALaIzquierda_EsIntencionalmenteConDatosDePerdida()
    {
        // Un espacio a la izquierda con significado nunca sobreviviría un ida-y-vuelta real por
        // KiSoft (que ya recorta ambos lados) -- documentamos que nuestro decode hace lo mismo.
        var encoded = CodecValorTelegrama.Encode(TipoCampo.Alfanumerico, 8, " AB");
        Assert.Equal("AB", CodecValorTelegrama.Decode(TipoCampo.Alfanumerico, encoded));
    }
}
