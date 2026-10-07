using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>
/// Aviso de archivo listo que KiSoft empuja al Host (canal 9802, HIS V3 §4.3.1 → 3RR/4RR) cuando
/// termina de recopilar la visualización de inventario en tiempo real (1RR) y deja el archivo
/// (formato interno 3IS) disponible por SFTP. Este mapper solo cubre el AVISO por TCP — la descarga
/// y el parseo del archivo por SFTP es un trabajo aparte, no implementado todavía.
/// </summary>
public sealed class EventoArchivoInventarioDto
{
    [JsonPropertyName("station")]
    public required string Station { get; init; }
}
