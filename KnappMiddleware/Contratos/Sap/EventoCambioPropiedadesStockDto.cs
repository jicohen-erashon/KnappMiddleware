using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>
/// Respuesta de un cambio de propiedades de stock por artículo/lote que KiSoft empuja al Host (canal
/// 9802, HIS V3 §4.4.4 → 3AU/4AU) — la otra forma de respuesta de la solicitud "modificar propiedades
/// de stock por artículo/lote" (HIS §3.6.5, add-on CR16 – 1.2), cuando el artículo está en el OSR: se
/// le envía la solicitud y KiSoft informa el resultado por esta vía (ver también
/// <see cref="EventoCambioStockUnidadCargaDto"/> para la respuesta por unidad de carga). Add-on de
/// pago: confirmar con KNAPP que esté contratado antes de habilitarlo.
/// </summary>
public sealed class EventoCambioPropiedadesStockDto
{
    [JsonPropertyName("station")]
    public required string Station { get; init; }

    [JsonPropertyName("productnumber")]
    public required string ProductNumber { get; init; }

    [JsonPropertyName("batchnumber")]
    public string? BatchNumber { get; init; }

    [JsonPropertyName("mandtk")]
    public string? Mandante { get; init; }

    [JsonPropertyName("stocktype")]
    public string? StockType { get; init; }

    /// <summary>HIS §4.4.4.1: 01 cambios realizados con éxito, 02 no se pudieron realizar.</summary>
    [JsonPropertyName("state")]
    public string? State { get; init; }
}
