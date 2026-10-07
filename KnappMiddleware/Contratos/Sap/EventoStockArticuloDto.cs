using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>Línea de stock de una unidad de carga/slot (bloque "i" del 3XR, HIS V3 §4.3.2.1).</summary>
public sealed class LineaEventoStockArticuloDto
{
    [JsonPropertyName("station")]
    public string? Station { get; init; }

    [JsonPropertyName("mandtk")]
    public string? Mandante { get; init; }

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

    [JsonPropertyName("loadunit")]
    public string? LoadUnitCode { get; init; }

    [JsonPropertyName("slotnumber")]
    public string? SlotNumber { get; init; }
}

/// <summary>
/// Evento con los datos de stock en tiempo real de un artículo que KiSoft empuja al Host (canal 9802,
/// HIS V3 §4.3.2 → 3XR/4XR), contraparte asíncrona de la solicitud síncrona 1XR (su respuesta
/// síncrona 2XR es solo un mensaje de estado, sin datos — los datos llegan por esta vía separada, una
/// línea por unidad de carga o slot ocupado). Igual que 1XR, es un add-on de pago (HIS V3 §4.3.2,
/// "CR16 – 1.1 – Segundo punto") — confirmar con KNAPP que esté contratado antes de habilitarlo.
/// </summary>
public sealed class EventoStockArticuloDto
{
    [JsonPropertyName("lines")]
    public IReadOnlyList<LineaEventoStockArticuloDto> Lines { get; init; } = [];
}
