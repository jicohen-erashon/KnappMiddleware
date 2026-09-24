using System.Globalization;

namespace KnappMiddleware.Telegramas;

/// <summary>
/// Codifica y decodifica la trama externa del protocolo KiSoft: &lt;LF&gt; + longitud (5) + datos + &lt;CR&gt;.
/// La longitud declarada incluye sus propios 5 bytes, pero no los delimitadores &lt;LF&gt;/&lt;CR&gt;.
/// </summary>
public static class CodecTramaTelegrama
{
    public const char StartDelimiter = '\n';
    public const char EndDelimiter = '\r';
    public const int LengthFieldWidth = 5;
    public const int MinTotalLength = LengthFieldWidth + 1;
    public const int MaxTotalLength = 99999;

    public static string Encode(string data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var totalLength = LengthFieldWidth + data.Length;
        if (totalLength is < MinTotalLength or > MaxTotalLength)
        {
            throw new ExcepcionFormatoTelegrama(
                $"Longitud total de trama fuera de rango ({MinTotalLength}-{MaxTotalLength}): {totalLength}.");
        }

        var lengthField = totalLength.ToString(CultureInfo.InvariantCulture).PadLeft(LengthFieldWidth, '0');
        return $"{StartDelimiter}{lengthField}{data}{EndDelimiter}";
    }

    public static string Decode(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (raw.Length < LengthFieldWidth + 2 || raw[0] != StartDelimiter)
        {
            throw new ExcepcionFormatoTelegrama("La trama no inicia con el delimitador <LF> esperado.");
        }

        if (raw[^1] != EndDelimiter)
        {
            throw new ExcepcionFormatoTelegrama("La trama no termina con el delimitador <CR> esperado.");
        }

        var lengthField = raw.Substring(1, LengthFieldWidth);
        if (!int.TryParse(lengthField, NumberStyles.None, CultureInfo.InvariantCulture, out var totalLength))
        {
            throw new ExcepcionFormatoTelegrama($"Campo de longitud inválido: '{lengthField}'.");
        }

        var expectedRawLength = totalLength + 2;
        if (raw.Length != expectedRawLength)
        {
            throw new ExcepcionFormatoTelegrama(
                $"Longitud declarada ({totalLength}) no coincide con la longitud real de la trama recibida.");
        }

        return raw.Substring(1 + LengthFieldWidth, totalLength - LengthFieldWidth);
    }
}
