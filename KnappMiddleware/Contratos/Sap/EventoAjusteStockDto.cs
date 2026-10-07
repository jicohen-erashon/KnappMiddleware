using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>
/// Ajuste de stock que KiSoft empuja al Host (canal 9802, HIS V3 §4.4.1 → 3SC/4SC) cada vez que el
/// operario de almacén (o el propio sistema) corrige, bloquea o desbloquea stock. Todos los campos
/// salvo <see cref="CorrectionNumber"/> son opcionales: qué bloques vienen poblados depende del
/// sistema de almacenamiento que generó el ajuste (HIS §4.4.1.3) — el OSR es el único que puede
/// generar los tres tipos de mensaje (41 bloqueo añadido, 42 bloqueo retirado, 43 stock corregido);
/// las estaciones manuales solo generan bloqueo/desbloqueo (41/42).
/// </summary>
public sealed class EventoAjusteStockDto
{
    /// <summary>Rango especial reservado por KiSoft para estos ajustes (HIS §4.4.1.4.1): "SC00000001"-"SC99999999".</summary>
    [JsonPropertyName("correctionnumber")]
    public required string CorrectionNumber { get; init; }

    [JsonPropertyName("station")]
    public string? Station { get; init; }

    /// <summary>HIS §4.4.1.4.3: 41 bloqueo de stock añadido, 42 bloqueo de stock retirado, 43 stock corregido.</summary>
    [JsonPropertyName("messagetype")]
    public string? MessageType { get; init; }

    [JsonPropertyName("loadunit")]
    public string? LoadUnitCode { get; init; }

    [JsonPropertyName("slotnumber")]
    public string? SlotNumber { get; init; }

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

    [JsonPropertyName("stockquality")]
    public string? StockQuality { get; init; }

    /// <summary>Motivo de bloqueo que se añadió o retiró en este ajuste (HIS §4.4.1.4.4).</summary>
    [JsonPropertyName("blockreasonchanged")]
    public string? BlockReasonChanged { get; init; }

    /// <summary>Todos los motivos de bloqueo vigentes DESPUÉS de este ajuste (HIS §4.4.1.4.5).</summary>
    [JsonPropertyName("blockreasons")]
    public IReadOnlyList<string> BlockReasons { get; init; } = [];

    /// <summary>HIS §4.4.1.4.6, solo en inglés: FOUND/LOST/HOST/SISTEMA/SUBSISTEMA, o (solo OSR) not_empty/under_pick/inventory/damaged_tray.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [JsonPropertyName("occurredat")]
    public string? OccurredAt { get; init; }

    [JsonPropertyName("warehouseoperator")]
    public string? WarehouseOperator { get; init; }

    /// <summary>Signo + valor absoluto tal como lo transmite KiSoft (HIS §4.4.1.1: "05 = 01+04"), p. ej. "+0012" o "-0005" — sin partir, para no asumir cuál lado interpreta SAP.</summary>
    [JsonPropertyName("difference")]
    public string? Difference { get; init; }
}
