using System.Text;

namespace KnappMiddleware.Domain.Telegramas;

/// <summary>
/// Serializa/deserializa los campos de un telegrama según un esquema ordenado de <see cref="FieldDefinition"/>.
/// Relleno: alfanumérico → espacios a la derecha; numérico → ceros a la izquierda; fecha vacía → ceros.
/// Las longitudes de campo son número de caracteres, no de bytes.
/// </summary>
public static class TelegramFieldCodec
{
    public static string Encode(IReadOnlyList<FieldDefinition> fields, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(values);

        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            values.TryGetValue(field.Name, out var value);
            builder.Append(EncodeField(field, value));
        }

        return builder.ToString();
    }

    public static IReadOnlyDictionary<string, string> Decode(IReadOnlyList<FieldDefinition> fields, string data)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(data);

        var result = new Dictionary<string, string>(fields.Count);
        var offset = 0;
        foreach (var field in fields)
        {
            if (offset + field.Length > data.Length)
            {
                throw new TelegramFormatException(
                    $"Datos insuficientes para el campo '{field.Name}' (se esperaban {field.Length} caracteres a partir de la posición {offset}).");
            }

            var raw = data.Substring(offset, field.Length);
            result[field.Name] = DecodeField(field, raw);
            offset += field.Length;
        }

        if (offset != data.Length)
        {
            throw new TelegramFormatException(
                $"Longitud de datos ({data.Length}) no coincide con la suma de longitudes de campos definidos ({offset}).");
        }

        return result;
    }

    private static string EncodeField(FieldDefinition field, string? value)
    {
        switch (field.Kind)
        {
            case FieldKind.AlphaNumeric:
                value ??= string.Empty;
                EnsureWithinLength(field, value);
                return value.PadRight(field.Length, ' ');

            case FieldKind.Numeric:
                value = string.IsNullOrEmpty(value) ? "0" : value;
                EnsureWithinLength(field, value);
                if (!value.All(char.IsAsciiDigit))
                {
                    throw new TelegramFormatException(
                        $"El valor del campo numérico '{field.Name}' contiene caracteres no numéricos: '{value}'.");
                }
                return value.PadLeft(field.Length, '0');

            case FieldKind.Date:
                if (string.IsNullOrEmpty(value))
                {
                    return new string('0', field.Length);
                }
                EnsureWithinLength(field, value);
                return value.PadLeft(field.Length, '0');

            default:
                throw new ArgumentOutOfRangeException(nameof(field), field.Kind, "Tipo de campo no soportado.");
        }
    }

    private static string DecodeField(FieldDefinition field, string raw)
    {
        return field.Kind switch
        {
            FieldKind.AlphaNumeric => raw.TrimEnd(' '),
            FieldKind.Numeric => DecodeNumeric(raw),
            FieldKind.Date => raw.All(c => c == '0') ? string.Empty : raw,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field.Kind, "Tipo de campo no soportado.")
        };
    }

    private static string DecodeNumeric(string raw)
    {
        var trimmed = raw.TrimStart('0');
        return trimmed.Length > 0 ? trimmed : "0";
    }

    private static void EnsureWithinLength(FieldDefinition field, string value)
    {
        if (value.Length > field.Length)
        {
            throw new TelegramFormatException(
                $"El valor del campo '{field.Name}' ({value.Length} caracteres) excede la longitud definida ({field.Length}).");
        }
    }
}
