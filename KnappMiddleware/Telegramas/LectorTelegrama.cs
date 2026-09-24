using System.Globalization;

namespace KnappMiddleware.Telegramas;

/// <summary>
/// Lee secuencialmente el cuerpo de un registro KiSoft, en espejo de <see cref="EscritorTelegrama"/>.
/// </summary>
public sealed class LectorTelegrama
{
    private readonly string _data;
    private int _pos;

    public LectorTelegrama(string data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public bool AtEnd => _pos >= _data.Length;

    /// <summary>Consume el tag si el siguiente carácter coincide; si no, no avanza y devuelve false (bloque ausente).</summary>
    public bool TryConsumeTag(char tag)
    {
        if (AtEnd || _data[_pos] != tag)
        {
            return false;
        }

        _pos++;
        return true;
    }

    public string Raw(int length) => Take(length);

    public int LengthPrefix(int lengthDigits)
    {
        var raw = Take(lengthDigits);
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var length))
        {
            throw new ExcepcionFormatoTelegrama($"Prefijo de longitud inválido: '{raw}'.");
        }

        return length;
    }

    public string Field(int lengthDigits, TipoCampo kind)
    {
        var length = LengthPrefix(lengthDigits);
        return CodecValorTelegrama.Decode(kind, Take(length));
    }

    public string? OptionalField(int lengthDigits, TipoCampo kind)
    {
        var length = LengthPrefix(lengthDigits);
        return length == 0 ? null : CodecValorTelegrama.Decode(kind, Take(length));
    }

    /// <summary>Valor de una fila de LOOP cuya anchura de columna ya se conoce (declarada antes del loop).</summary>
    public string? RawValue(int length, TipoCampo kind) =>
        length == 0 ? null : CodecValorTelegrama.Decode(kind, Take(length));

    public void ExpectEnd()
    {
        if (!AtEnd)
        {
            throw new ExcepcionFormatoTelegrama(
                $"Datos sobrantes tras decodificar el registro (posición {_pos} de {_data.Length}).");
        }
    }

    private string Take(int length)
    {
        if (length == 0)
        {
            return string.Empty;
        }

        if (_pos + length > _data.Length)
        {
            throw new ExcepcionFormatoTelegrama(
                $"Datos insuficientes: se esperaban {length} caracteres a partir de la posición {_pos} (longitud total {_data.Length}).");
        }

        var value = _data.Substring(_pos, length);
        _pos += length;
        return value;
    }
}
