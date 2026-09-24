using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>Línea de stock a modificar de una unidad de carga (bloque X del 1UU, HIS §3.6.2.1).</summary>
public sealed class LineaUnidadCargaModificarDto
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

    // quantity/stockquality/unit: el HIS §3.6.2.1 marca estas celdas como tachadas/deprecadas para
    // 1UU (a diferencia de 1UN) — MapeadorTelegramaUnidadCargaModificar no las transmite a propósito.
    // Se reciben para no romper el contrato, sin uso en la trama.
    [JsonPropertyName("quantity")]
    [Range(0, 9999)]
    public int? Quantity { get; init; }

    [JsonPropertyName("stockquality")]
    [StringLength(1)]
    public string? StockQuality { get; init; }

    [JsonPropertyName("unit")]
    [StringLength(4)]
    public string? Unit { get; init; }
}

/// <summary>Payload que SAP envía a POST /sap/loadunit/modify (modificar unidad de carga de almacenamiento, HIS §3.6.2 → 1UU/2UU).</summary>
public sealed class SolicitudUnidadCargaModificarDto : SobreTelegramaSapDto
{
    // lines: cantidad de líneas en items[], redundante con items.Count — no se usa en la trama.
    [JsonPropertyName("lines")]
    public int? Lines { get; init; }

    [JsonPropertyName("mandtk")]
    [StringLength(16)]
    public required string Mandante { get; init; }

    /// <summary>HIS §3.6.2.1 declara ancho fijo 6 — la muestra real de SAP trae 8 caracteres (mismo gap que en 1UN; ver JSON-SAP/PENDIENTE-1XR.md).</summary>
    [JsonPropertyName("loadunit")]
    [StringLength(6)]
    public required string LoadUnitCode { get; init; }

    /// <summary>Requerido por el HIS (§3.6.2.1), pero ausente en el JSON de muestra de SAP para 1UU — a confirmar con SAP/KNAPP.</summary>
    [JsonPropertyName("station")]
    [StringLength(3)]
    public required string Station { get; init; }

    [JsonPropertyName("geocode")]
    [StringLength(12)]
    public required string GeoCode { get; init; }

    [JsonPropertyName("items")]
    public required IReadOnlyList<LineaUnidadCargaModificarDto> Items { get; init; }
}
