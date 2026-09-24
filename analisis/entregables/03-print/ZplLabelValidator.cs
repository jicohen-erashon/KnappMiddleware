using System.Text;

namespace KnappMiddleware.Print;

/// <summary>
/// Validador ligero de etiquetas Zebra (ZPL II). Suficiente para detectar:
///  - Etiqueta truncada: falta ^XA al inicio.
///  - Etiqueta no cerrada: falta ^XZ al final.
///  - Directivas desbalanceadas: ^FD sin ^FS.
///  - Tamaño excesivo (> 2.5 KB en HIS §3.7.1.2).
///
/// NO es un parser ZPL completo (no evalúa plantillas, ni fuentes). Es un "health check"
/// para evitar enviar al sistema KiSoft archivos corruptos que luego se imprimirían mal.
/// </summary>
public static class ZplLabelValidator
{
    public const int MaxBytes = 2_560; // 2.5 KB — HIS §3.7.1.2

    public static ZplValidationResult Validate(ReadOnlySpan<byte> bytes)
    {
        var errors = new List<string>(4);

        if (bytes.IsEmpty)
        {
            errors.Add("etiqueta vacía");
            return new ZplValidationResult(false, errors);
        }

        if (bytes.Length > MaxBytes)
        {
            errors.Add($"tamaño {bytes.Length:N0} bytes excede el máximo {MaxBytes:N0} (HIS §3.7.1.2)");
        }

        var s = StripBom(bytes);

        // Inicio ^XA — toleramos BOM inicial.
        var idxXa = s.IndexOf("^XA", StringComparison.Ordinal);
        if (idxXa < 0)
        {
            errors.Add("falta directiva inicial ^XA");
        }
        else if (idxXa > 16)
        {
            errors.Add($"^XA aparece tras {idxXa} bytes (¿truncada al inicio?)");
        }

        // Cierre ^XZ
        var lastXz = s.LastIndexOf("^XZ", StringComparison.Ordinal);
        if (lastXz < 0)
        {
            errors.Add("falta directiva final ^XZ");
        }

        // Balance FD/FS (sencillo: contar; debe ser par)
        var fd = CountDirective(s, "^FD");
        var fs = CountDirective(s, "^FS");
        if (fd != fs)
        {
            errors.Add($"^FD={fd} ^FS={fs} desbalanceados");
        }

        return errors.Count == 0 ? new(true, Array.Empty<string>()) : new(false, errors);
    }

    /// <summary>Elimina BOM UTF-8 si está presente.</summary>
    private static string StripBom(ReadOnlySpan<byte> bytes)
    {
        var span = bytes;
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
        {
            span = span[3..];
        }
        return Encoding.UTF8.GetString(span);
    }

    private static int CountDirective(string s, string directive)
    {
        var count = 0;
        var i = 0;
        while ((i = s.IndexOf(directive, i, StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += directive.Length;
        }
        return count;
    }
}

public sealed record ZplValidationResult(bool IsValid, IReadOnlyList<string> Errors);
