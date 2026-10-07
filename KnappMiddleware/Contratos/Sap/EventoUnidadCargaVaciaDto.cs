using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>
/// Aviso de unidad de carga vacía que KiSoft empuja al Host (canal 9802, HIS V3 §4.4.2 → 3UE/4UE),
/// disparado manual o automáticamente por el operario de almacén. Permite al Host reponer de
/// inmediato (p. ej. puestos de preparación desde pallet).
/// </summary>
public sealed class EventoUnidadCargaVaciaDto
{
    [JsonPropertyName("loadunit")]
    public required string LoadUnitCode { get; init; }
}
