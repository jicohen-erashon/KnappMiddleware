using System.Text.RegularExpressions;

namespace KnappMiddleware.Print;

/// <summary>
/// Parsea el nombre de archivo SFTP que entrega COHEN al canal de impresión.
/// HIS §3.7.1.1:
///
///   {ordernumber}.{sheetnumber}.{documenttype}.{additionalInfo}.{printedsheetnumber}
///   ó
///   {ordernumber}.{sheetnumber}.{documenttype}.{additionalInfo}.end        (señal de cierre)
///
/// documenttype: 001 = albarán (PDF), 008 = etiqueta de dirección (ZPL).
/// </summary>
public static class PrintFileName
{
    private static readonly Regex FormatoHoja = new(
        @"^(?<order>[A-Za-z0-9_\-]{1,12})\.(?<sheet>\d{4})\.(?<doctype>00[18])\.(?<info>\d{3})\.(?<printed>\d{2})$",
        RegexOptions.Compiled);

    private static readonly Regex FormatoCierre = new(
        @"^(?<order>[A-Za-z0-9_\-]{1,12})\.(?<sheet>\d{4})\.(?<doctype>00[18])\.(?<info>\d{3})\.end$",
        RegexOptions.Compiled);

    public static bool TryParse(string filename, out PrintFileNameInfo info)
    {
        info = default!;
        if (string.IsNullOrWhiteSpace(filename)) return false;

        var m = FormatoHoja.Match(filename);
        if (m.Success)
        {
            info = new PrintFileNameInfo(
                Raw: filename,
                IsEndMarker: false,
                OrderNumber: m.Groups["order"].Value,
                SheetNumber: m.Groups["sheet"].Value,
                DocumentType: MapDocumentType(m.Groups["doctype"].Value),
                AdditionalInfo: m.Groups["info"].Value,
                PrintedSheetNumber: m.Groups["printed"].Value);
            return true;
        }

        m = FormatoCierre.Match(filename);
        if (m.Success)
        {
            info = new PrintFileNameInfo(
                Raw: filename,
                IsEndMarker: true,
                OrderNumber: m.Groups["order"].Value,
                SheetNumber: m.Groups["sheet"].Value,
                DocumentType: MapDocumentType(m.Groups["doctype"].Value),
                AdditionalInfo: m.Groups["info"].Value,
                PrintedSheetNumber: null);
            return true;
        }

        return false;
    }

    private static PrintDocumentType MapDocumentType(string raw) => raw switch
    {
        "001" => PrintDocumentType.DeliveryNote,
        "008" => PrintDocumentType.AddressLabel,
        _ => PrintDocumentType.Unknown
    };
}

public enum PrintDocumentType { Unknown, DeliveryNote, AddressLabel }

public sealed record PrintFileNameInfo(
    string Raw,
    bool IsEndMarker,
    string OrderNumber,
    string SheetNumber,
    PrintDocumentType DocumentType,
    string AdditionalInfo,
    string? PrintedSheetNumber)
{
    /// <summary>MIME esperado para validación cruzada (PDF / ZPL).</summary>
    public string ExpectedMime => DocumentType switch
    {
        PrintDocumentType.DeliveryNote => "application/pdf",
        PrintDocumentType.AddressLabel => "application/zpl",
        _ => "application/octet-stream"
    };

    /// <summary>Estación KiSoft a la que se debe enrutar (mapping por defecto del HIS).</summary>
    public string DefaultStation => DocumentType switch
    {
        PrintDocumentType.DeliveryNote => "ABB001",
        PrintDocumentType.AddressLabel => "LBA001", // 008 → contenedor cartón por defecto; ABA001 si plástico
        _ => "UNKNOWN"
    };
}
