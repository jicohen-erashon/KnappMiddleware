using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>Resultado de un slot de la unidad de carga (bloque "T" del 3UU, HIS V3 §4.4.3.1): 01 cambios realizados con éxito, 02 no se pudieron realizar.</summary>
public sealed class LineaEstadoSlotDto
{
    [JsonPropertyName("slotnumber")]
    public string? SlotNumber { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }
}

/// <summary>
/// Respuesta de un cambio de stock por unidad de carga que KiSoft empuja al Host (canal 9802, HIS V3
/// §4.4.3 → 3UU/4UU) — una de las dos formas de respuesta de la solicitud "modificar propiedades de
/// stock por artículo/lote" (HIS §3.6.5, add-on CR16 – 1.2), cuando el cambio se resolvió a nivel de
/// unidad de carga/slot. La otra forma, cuando se resuelve a nivel de artículo/lote, es 3AU/4AU (ver
/// <see cref="EventoCambioPropiedadesStockDto"/>). No confundir con los telegramas 1UU/2UU (modificar
/// unidad de carga, base price) — comparten prefijo "UU" pero son flujos completamente distintos.
/// Igual que 3XR, es un add-on de pago: confirmar con KNAPP que esté contratado antes de habilitarlo.
/// </summary>
public sealed class EventoCambioStockUnidadCargaDto
{
    [JsonPropertyName("loadunit")]
    public required string LoadUnitCode { get; init; }

    [JsonPropertyName("station")]
    public string? Station { get; init; }

    [JsonPropertyName("geocode")]
    public string? GeoCode { get; init; }

    [JsonPropertyName("states")]
    public IReadOnlyList<LineaEstadoSlotDto> States { get; init; } = [];
}
