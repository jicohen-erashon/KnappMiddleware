using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>Línea de stock de una unidad de carga puesta a disposición (bloque X del 1UN, HIS §3.6.1.1).</summary>
public sealed class LineaUnidadCargaDisponibleDto
{
    [JsonPropertyName("productnumber")]
    [StringLength(12)]
    public required string ProductNumber { get; init; }

    [JsonPropertyName("packsize")]
    [StringLength(4)]
    public required string PackSize { get; init; }

    [JsonPropertyName("stocktype")]
    [StringLength(8)]
    public required string StockType { get; init; }

    [JsonPropertyName("batchnumber")]
    [StringLength(20)]
    public string? BatchNumber { get; init; }

    // SAP envía "YYYY-MM-DD" (10 chars); el mapper quita los guiones antes de escribir el campo de
    // 8 dígitos del HIS. No se anota StringLength acá por la misma razón que en Ruta/Pedido.
    [JsonPropertyName("expirationdate")]
    public string? ExpirationDate { get; init; }

    [JsonPropertyName("quantity")]
    [Range(0, 9999)]
    public required int Quantity { get; init; }

    [JsonPropertyName("stockquality")]
    [StringLength(1)]
    public required string StockQuality { get; init; }

    // unit: SAP lo envía (p. ej. "EA") pero el HIS §3.6.1.1 no tiene un campo de unidad en el bloque
    // X del 1UN — no se usa en la trama.
    [JsonPropertyName("unit")]
    [StringLength(4)]
    public string? Unit { get; init; }
}

/// <summary>Payload que SAP envía a POST /sap/loadunit/available (unidad de carga de almacenamiento puesta a disposición, HIS §3.6.1 → 1UN/2UN).</summary>
public sealed class SolicitudUnidadCargaDisponibleDto : SobreTelegramaSapDto
{
    // lines: cantidad de líneas en items[], redundante con items.Count — no se usa en la trama.
    [JsonPropertyName("lines")]
    public int? Lines { get; init; }

    [JsonPropertyName("mandtk")]
    [StringLength(16)]
    public required string Mandante { get; init; }

    /// <summary>HIS §3.6.1.1 declara ancho fijo 6 — la muestra real de SAP trae 8 caracteres (mismo gap que en 1UU; ver JSON-SAP/PENDIENTE-1XR.md).</summary>
    [JsonPropertyName("loadunit")]
    [StringLength(6)]
    public required string LoadUnitCode { get; init; }

    /// <summary>Requerido por el HIS (§3.6.1.1), pero ausente en el JSON de muestra de SAP para 1UN — a confirmar con SAP/KNAPP.</summary>
    [JsonPropertyName("station")]
    [StringLength(3)]
    public required string Station { get; init; }

    [JsonPropertyName("geocode")]
    [StringLength(12)]
    public required string GeoCode { get; init; }

    [JsonPropertyName("items")]
    public required IReadOnlyList<LineaUnidadCargaDisponibleDto> Items { get; init; }
}
