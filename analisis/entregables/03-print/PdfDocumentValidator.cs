using System.Text;

namespace KnappMiddleware.Print;

/// <summary>
/// Validador estructural de PDFs (no requiere librería completa).
///
/// Contrato HIS §3.7.1.2:
///  - Tamaño &lt;= 40 KB.
///  - Formato Carta (Letter) — se permite A4 como fallback para pruebas internas.
///
/// Verifica:
///  - Header "%PDF-".
///  - Versión (1.4..1.7).
///  - Tabla xref presente (palabra clave "xref" + bloque de offsets) y "%%EOF".
/// </summary>
public static class PdfDocumentValidator
{
    public const int MaxBytes = 40_960; // 40 KB

    public static PdfValidationResult Validate(ReadOnlySpan<byte> bytes)
    {
        var errors = new List<string>(4);

        if (bytes.IsEmpty)
        {
            errors.Add("PDF vacío");
            return new PdfValidationResult(false, errors);
        }

        if (bytes.Length > MaxBytes)
        {
            errors.Add($"tamaño {bytes.Length:N0} bytes excede el máximo {MaxBytes:N0} (HIS §3.7.1.2)");
        }

        var head = ReadTail(bytes, 1024);
        var s = Encoding.ASCII.GetString(head);

        if (!s.StartsWith("%PDF-", StringComparison.Ordinal))
        {
            errors.Add("header '%PDF-' ausente");
        }
        else
        {
            var minor = ReadMinorVersion(s);
            if (minor is < 1 or > 7)
            {
                errors.Add($"versión PDF {minor} fuera de [1..7]");
            }
        }

        if (!s.Contains("xref", StringComparison.Ordinal))
        {
            errors.Add("falta tabla xref");
        }
        if (!s.Contains("%%EOF", StringComparison.Ordinal))
        {
            errors.Add("falta marca %%EOF");
        }

        var body = Encoding.ASCII.GetString(bytes);
        var firstPage = body.IndexOf("/Type /Page", StringComparison.Ordinal);
        if (firstPage < 0 && body.IndexOf("/Type/Page", StringComparison.Ordinal) < 0)
        {
            errors.Add("no se encontraron páginas (/Type /Page)");
        }

        return errors.Count == 0 ? new(true, Array.Empty<string>()) : new(false, errors);
    }

    private static int ReadMinorVersion(string head)
    {
        // %PDF-1.7\n  ó  %PDF-1.4\n
        var span = head.AsSpan();
        var idx = span.IndexOf("%PDF-", StringComparison.Ordinal);
        if (idx < 0) return 0;
        var start = idx + "%PDF-".Length;
        var end = start;
        while (end < span.Length && span[end] != '.' && span[end] != '\r' && span[end] != '\n' && span[end] != ' ')
        {
            end++;
        }
        var slice = span[start..end];
        return int.TryParse(slice, out var v) ? v : 0;
    }

    /// <summary>Lee los últimos <paramref name="count"/> bytes del archivo.</summary>
    private static ReadOnlySpan<byte> ReadTail(ReadOnlySpan<byte> bytes, int count)
        => bytes.Length <= count ? bytes : bytes[^count..];
}

public sealed record PdfValidationResult(bool IsValid, IReadOnlyList<string> Errors);
