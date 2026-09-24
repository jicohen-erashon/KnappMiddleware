using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>Línea de filtro de una solicitud de inventario por artículos (bloque Y del 1IA, HIS §3.4.1.1).</summary>
public sealed class LineaFiltroInventarioDto
{
    [JsonPropertyName("station")]
    [StringLength(3)]
    public string? Station { get; init; }

    [JsonPropertyName("productnumber")]
    [StringLength(12)]
    public string? ProductNumber { get; init; }

    [JsonPropertyName("packsize")]
    [StringLength(4)]
    public string? PackSize { get; init; }

    [JsonPropertyName("stocktype")]
    [StringLength(8)]
    public string? StockType { get; init; }

    [JsonPropertyName("batchnumber")]
    [StringLength(20)]
    public string? BatchNumber { get; init; }

    [JsonPropertyName("loadunit")]
    [StringLength(8)]
    public string? LoadUnit { get; init; }

    [JsonPropertyName("slotnumber")]
    [StringLength(2)]
    public string? SlotNumber { get; init; }
}

/// <summary>Payload que SAP envía a POST /sap/inventory (solicitud de inventario por artículos, HIS §3.4.1 → 1IA/2IA).</summary>
public sealed class SolicitudInventarioDto : SobreTelegramaSapDto
{
    [JsonPropertyName("mandtk")]
    [StringLength(16)]
    public required string Mandante { get; init; }

    [JsonPropertyName("inventorynumber")]
    [StringLength(7)]
    public required string InventoryRequestNumber { get; init; }

    /// <summary>El contador de líneas del LOOP (bloque "Y") se transmite en 3 dígitos (ver
    /// MapeadorTelegramaInventario), de ahí el tope de 999.</summary>
    [JsonPropertyName("items")]
    [MaxLength(999)]
    public required IReadOnlyList<LineaFiltroInventarioDto> Items { get; init; }
}
