using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>Payload que SAP envía a POST /sap/inventory/realtime (visualización de inventario en tiempo real, HIS §3.5.1 → 1RR/2RR).</summary>
public sealed class SolicitudInventarioTiempoRealDto : SobreTelegramaSapDto
{
    [JsonPropertyName("station")]
    [StringLength(3)]
    public required string Station { get; init; }

    [JsonPropertyName("requesttype")]
    [StringLength(2)]
    public required string RequestType { get; init; }
}
