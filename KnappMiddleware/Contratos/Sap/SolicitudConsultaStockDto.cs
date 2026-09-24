using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>
/// Payload que SAP envía a POST /sap/inventory/stock (consulta de stock de un artículo en tiempo
/// real, HIS V3 §3.5.2 → 1XR/2XR). Add-on de pago ("CR16 – 1.1 – Segundo punto"), no incluido en el
/// precio base — confirmar con KNAPP que esté contratado antes de habilitarlo en producción.
/// </summary>
public sealed class SolicitudConsultaStockDto : SobreTelegramaSapDto
{
    [JsonPropertyName("mandtk")]
    [StringLength(16)]
    public required string Mandante { get; init; }

    [JsonPropertyName("station")]
    [StringLength(3)]
    public required string Station { get; init; }

    [JsonPropertyName("productnumber")]
    [StringLength(12)]
    public required string ProductNumber { get; init; }

    [JsonPropertyName("stocktype")]
    [StringLength(8)]
    public string? StockType { get; init; }

    [JsonPropertyName("batchnumber")]
    [StringLength(20)]
    public string? BatchNumber { get; init; }
}
