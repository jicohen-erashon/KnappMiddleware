namespace KnappMiddleware.Telegramas;

/// <summary>
/// Codifica/decodifica un único valor de campo a una longitud exacta (ya conocida por el llamador,
/// sea porque es constante para esta instalación o porque viene de un prefijo de longitud leído del
/// propio telegrama). Relleno: alfanumérico → espacios a la derecha; numérico → ceros a la izquierda;
/// fecha vacía → ceros. Las longitudes son número de caracteres, no de bytes (HIS §2.3, nota UTF-8).
/// </summary>
public static class CodecValorTelegrama
{
    public static string Encode(TipoCampo kind, int length, string? value)
    {
        if (length == 0)
        {
            return string.Empty;
        }

        switch (kind)
        {
            case TipoCampo.Alfanumerico:
                value ??= string.Empty;
                EnsureWithinLength(length, value);
                return value.PadRight(length, ' ');

            case TipoCampo.Numerico:
                value = string.IsNullOrEmpty(value) ? "0" : value;
                EnsureWithinLength(length, value);
                if (!value.All(char.IsAsciiDigit))
                {
                    throw new ExcepcionFormatoTelegrama($"Valor numérico con caracteres no numéricos: '{value}'.");
                }
                return value.PadLeft(length, '0');

            case TipoCampo.Fecha:
                if (string.IsNullOrEmpty(value))
                {
                    return new string('0', length);
                }
                EnsureWithinLength(length, value);
                return value.PadLeft(length, '0');

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de campo no soportado.");
        }
    }

    public static string Decode(TipoCampo kind, string raw)
    {
        return kind switch
        {
            TipoCampo.Alfanumerico => raw.TrimEnd(' '),
            TipoCampo.Numerico => DecodeNumeric(raw),
            TipoCampo.Fecha => raw.Length > 0 && raw.All(c => c == '0') ? string.Empty : raw,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de campo no soportado.")
        };
    }

    private static string DecodeNumeric(string raw)
    {
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        var trimmed = raw.TrimStart('0');
        return trimmed.Length > 0 ? trimmed : "0";
    }

    private static void EnsureWithinLength(int length, string value)
    {
        if (value.Length > length)
        {
            throw new ExcepcionFormatoTelegrama(
                $"El valor '{value}' ({value.Length} caracteres) excede la longitud declarada ({length}).");
        }
    }
}
