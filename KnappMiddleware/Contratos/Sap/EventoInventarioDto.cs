using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>Línea de stock de un artículo (bloque "b" del 3IR, HIS §4.2.1).</summary>
public sealed class LineaEventoInventarioDto
{
    [JsonPropertyName("station")]
    public string? Station { get; init; }

    [JsonPropertyName("productnumber")]
    public string? ProductNumber { get; init; }

    [JsonPropertyName("packsize")]
    public string? PackSize { get; init; }

    [JsonPropertyName("stocktype")]
    public string? StockType { get; init; }

    [JsonPropertyName("batchnumber")]
    public string? BatchNumber { get; init; }

    [JsonPropertyName("expirationdate")]
    public string? ExpirationDate { get; init; }

    [JsonPropertyName("quantity")]
    public string? Quantity { get; init; }

    [JsonPropertyName("stockquality")]
    public string? StockQuality { get; init; }

    [JsonPropertyName("loadmedium")]
    public string? LoadMedium { get; init; }

    [JsonPropertyName("loadunit")]
    public string? LoadUnitCode { get; init; }

    [JsonPropertyName("slotnumber")]
    public string? SlotNumber { get; init; }

    /// <summary>Estado de bloqueo del stock (HIS §4.2.1), texto libre (p. ej. motivo de un bloqueo manual).</summary>
    [JsonPropertyName("blockstatus")]
    public string? BlockStatus { get; init; }

    /// <summary>HIS declara un único valor posible ("01") para este campo en 3IR — a diferencia del 32R, no es un catálogo de estados de error.</summary>
    [JsonPropertyName("linestatus")]
    public string? LineStatus { get; init; }

    [JsonPropertyName("warehouseoperator")]
    public string? WarehouseOperator { get; init; }

    [JsonPropertyName("processedat")]
    public string? ProcessedAt { get; init; }
}

/// <summary>
/// Evento de resultado de inventario que KiSoft empuja al Host (canal 9802, HIS §4.2 → 3IR/4IR). Según
/// la nota del HIS, solo se genera si el Host solicitó el inventario a través de la interfaz (1IA) —
/// una solicitud hecha desde la GUI de KiSoft no activa este mensaje. Es el payload que el middleware
/// reenvía a SAP por webhook.
/// </summary>
public sealed class EventoInventarioDto
{
    [JsonPropertyName("mandtk")]
    public required string Mandante { get; init; }

    [JsonPropertyName("inventoryrequestnumber")]
    public required string InventoryRequestNumber { get; init; }

    /// <summary>HIS §4.2.3.2: "0002" finalizado normalmente, "0003" solicitud cancelada mediante la GUI.</summary>
    [JsonPropertyName("states")]
    public IReadOnlyList<string> States { get; init; } = [];

    [JsonPropertyName("lines")]
    public IReadOnlyList<LineaEventoInventarioDto> Lines { get; init; } = [];
}
