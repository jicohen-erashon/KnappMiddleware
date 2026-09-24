namespace KnappMiddleware.Print;

/// <summary>
/// Punto de entrada del parser de impresión. Combina el <see cref="PrintFileName"/>
/// y el validador de bytes (ZPL/PDF).
///
/// Devuelve un <see cref="PrintValidation"/> que la capa superior usa para:
///  - Enrutar a la estación correcta (ABB001 / LBA001 / ABA001).
///  - Auditar el error si la validación falla (vía <see cref="NonBlockingAuditTrail"/>).
///  - Persistir el archivo válido en el directorio que las impresoras físicas consumen.
/// </summary>
public static class PrintFileParser
{
    public static PrintValidation TryParse(string filename, ReadOnlyMemory<byte> contents, PrintParserOptions options)
    {
        var reasons = new List<string>();
        PrintFileNameInfo? name = null;

        // 1) nombre
        if (!PrintFileName.TryParse(filename, out name))
        {
            reasons.Add($"nombre '{filename}' no cumple la convención 12N.HH.DDD.AAA.PP|end");
        }

        // 2) cierre .end → válido sin validar bytes (es señal de control)
        if (name is { IsEndMarker: true })
        {
            return new PrintValidation(
                Ok: true,
                IsEndMarker: true,
                Filename: filename,
                Station: null,
                MimeType: null,
                Name: name,
                Reasons: Array.Empty<string>());
        }

        if (name is null)
        {
            return new PrintValidation(false, false, filename, null, null, null, reasons);
        }

        // 3) bytes según tipo
        var span = contents.Span;
        switch (name.DocumentType)
        {
            case PrintDocumentType.DeliveryNote:
                var pdf = PdfDocumentValidator.Validate(span);
                if (!pdf.IsValid) reasons.AddRange(pdf.Errors);
                return new PrintValidation(
                    Ok: pdf.IsValid,
                    IsEndMarker: false,
                    Filename: filename,
                    Station: name.DefaultStation,
                    MimeType: name.ExpectedMime,
                    Name: name,
                    Reasons: reasons);

            case PrintDocumentType.AddressLabel:
                var zpl = ZplLabelValidator.Validate(span);
                if (!zpl.IsValid) reasons.AddRange(zpl.Errors);
                return new PrintValidation(
                    Ok: zpl.IsValid,
                    IsEndMarker: false,
                    Filename: filename,
                    Station: ChooseLabelStation(name, span, options),
                    MimeType: name.ExpectedMime,
                    Name: name,
                    Reasons: reasons);

            default:
                reasons.Add($"documenttype no soportado");
                return new PrintValidation(false, false, filename, null, null, name, reasons);
        }
    }

    /// <summary>
    /// Decide ABA001 vs LBA001. Por defecto LBA001 (cartón, 170 mm, ZPL 2000T).
    /// Si el ancho del campo de etiqueta es ≤ 100 mm y los códigos ópticos están en una caja
    /// con identificador [A-Z] plástico (heurística: aparece 'PLASTIC' en el ZPL) → ABA001.
    /// </summary>
    private static string ChooseLabelStation(PrintFileNameInfo name, ReadOnlySpan<byte> zplBytes, PrintParserOptions options)
    {
        var s = System.Text.Encoding.UTF8.GetString(zplBytes);
        if (s.Contains("^PW80", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("^PW100", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("PLASTIC", StringComparison.OrdinalIgnoreCase))
        {
            return "ABA001";
        }
        // override explícito por configuración
        return options.AddressLabelStationOverride ?? name.DefaultStation;
    }
}

public sealed class PrintParserOptions
{
    /// <summary>Override opcional (config) para forzar LBA001 o ABA001 cuando la heurística no esté clara.</summary>
    public string? AddressLabelStationOverride { get; init; }
}

public sealed record PrintValidation(
    bool Ok,
    bool IsEndMarker,
    string Filename,
    string? Station,
    string? MimeType,
    PrintFileNameInfo? Name,
    IReadOnlyList<string> Reasons);
