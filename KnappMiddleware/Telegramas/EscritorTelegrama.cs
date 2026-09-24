using System.Globalization;
using System.Text;

namespace KnappMiddleware.Telegramas;

/// <summary>
/// Construye el cuerpo (datos) de un registro KiSoft: identificador de registro + campos con
/// prefijo de longitud + bloques opcionales identificados por una letra ("tag") + grupos repetibles
/// (LOOP). Cada primitiva corresponde 1:1 a una fila de las tablas del HIS spec:
/// <list type="bullet">
/// <item><see cref="Field"/>: campo de longitud fija, siempre presente (p. ej. mandante).</item>
/// <item><see cref="OptionalField"/>: campo cuyo prefijo de longitud es "00" (ausente) o la longitud
/// nominal (presente) — una sola ocurrencia, no repetible.</item>
/// <item><see cref="LengthPrefix"/> + <see cref="RawValue"/>: en un bloque con LOOP, la anchura de
/// cada columna se declara UNA sola vez antes de "LOOP START" y luego cada fila del loop transmite
/// solo el valor ya formateado a esa anchura, sin volver a declarar la longitud (HIS §3.2.3.9 y
/// análogos). Usa <c>LengthPrefix(digits, 0)</c> para deshabilitar una columna en todo el bloque.</item>
/// </list>
/// </summary>
public sealed class EscritorTelegrama
{
    private readonly StringBuilder _sb = new();

    public EscritorTelegrama Raw(string text)
    {
        _sb.Append(text);
        return this;
    }

    public EscritorTelegrama Tag(char tag)
    {
        _sb.Append(tag);
        return this;
    }

    /// <summary>Campo de una sola ocurrencia: prefijo de longitud fijo (siempre <paramref name="length"/>) + valor.</summary>
    public EscritorTelegrama Field(int lengthDigits, int length, TipoCampo kind, string? value)
    {
        _sb.Append(length.ToString(CultureInfo.InvariantCulture).PadLeft(lengthDigits, '0'));
        _sb.Append(CodecValorTelegrama.Encode(kind, length, value));
        return this;
    }

    /// <summary>Campo de una sola ocurrencia y presencia opcional: prefijo "00" si <paramref name="value"/> es nulo/vacío.</summary>
    public EscritorTelegrama OptionalField(int lengthDigits, int length, TipoCampo kind, string? value) =>
        string.IsNullOrEmpty(value) ? Field(lengthDigits, 0, kind, null) : Field(lengthDigits, length, kind, value);

    /// <summary>Escribe solo el prefijo de longitud (para contadores de LOOP o anchuras de columna declaradas antes del loop).</summary>
    public EscritorTelegrama LengthPrefix(int lengthDigits, int value)
    {
        _sb.Append(value.ToString(CultureInfo.InvariantCulture).PadLeft(lengthDigits, '0'));
        return this;
    }

    /// <summary>Valor de una fila de LOOP, ya con la anchura de columna declarada antes del loop (sin prefijo propio).</summary>
    public EscritorTelegrama RawValue(int length, TipoCampo kind, string? value)
    {
        _sb.Append(CodecValorTelegrama.Encode(kind, length, value));
        return this;
    }

    /// <summary>Campo reservado: solo el prefijo de longitud "00", nunca lleva valor.</summary>
    public EscritorTelegrama Reserved(int lengthDigits) => LengthPrefix(lengthDigits, 0);

    /// <summary>Bloque opcional identificado por <paramref name="tag"/>: se omite por completo si <paramref name="present"/> es falso.</summary>
    public EscritorTelegrama Block(char tag, bool present, Action<EscritorTelegrama> build)
    {
        if (!present)
        {
            return this;
        }

        Tag(tag);
        build(this);
        return this;
    }

    public string Build() => _sb.ToString();
}
