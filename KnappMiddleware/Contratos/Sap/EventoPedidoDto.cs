using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>Línea de procesamiento de un evento de pedido (bloque "Z"/"b" del 32R, HIS §4.1.5.13).</summary>
public sealed class LineaEventoPedidoDto
{
    [JsonPropertyName("linereference")]
    public string? LineReference { get; init; }

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

    /// <summary>Estado de la línea (HIS §4.1.5.13): 01 aún no preparada, 30 procesada OK, 50-60 distintos errores.</summary>
    [JsonPropertyName("linestatus")]
    public string? LineStatus { get; init; }

    [JsonPropertyName("warehouseoperator")]
    public string? WarehouseOperator { get; init; }

    [JsonPropertyName("processedat")]
    public string? ProcessedAt { get; init; }

    [JsonPropertyName("serialnumber")]
    public string? SerialNumber { get; init; }

    [JsonPropertyName("geocode")]
    public string? GeoCode { get; init; }
}

/// <summary>
/// Evento de pedido que KiSoft empuja al Host (canal 9802, HIS §4.1 → 32R/42R). Es el payload que el
/// middleware reenvía a SAP por webhook. Todos los campos salvo el encabezado son opcionales: qué
/// bloques vienen poblados depende del "motivo de activación" (HIS §4.1.3/4.1.4) — creación,
/// arranque, paso por estación, finalización, cancelación, timeout, etc.
/// </summary>
public sealed class EventoPedidoDto
{
    [JsonPropertyName("mandtk")]
    public required string Mandante { get; init; }

    [JsonPropertyName("ordernumber")]
    public required string OrderNumber { get; init; }

    [JsonPropertyName("sheetnumber")]
    public required string SheetNumber { get; init; }

    [JsonPropertyName("ordertype")]
    public string? OrderType { get; init; }

    /// <summary>Solo presente en mensajes de cajas adicionales (hoja &gt; 0999).</summary>
    [JsonPropertyName("initialsheetnumber")]
    public string? InitialSheetNumber { get; init; }

    [JsonPropertyName("sheetcount")]
    public string? SheetCount { get; init; }

    [JsonPropertyName("startstation")]
    public string? StartStation { get; init; }

    [JsonPropertyName("loadmedium")]
    public string? LoadMedium { get; init; }

    [JsonPropertyName("loadunit")]
    public string? LoadUnitCode { get; init; }

    [JsonPropertyName("businesspartner")]
    public string? BusinessPartner { get; init; }

    [JsonPropertyName("dispatchramp")]
    public string? DispatchRamp { get; init; }

    [JsonPropertyName("starttime")]
    public string? StartTime { get; init; }

    [JsonPropertyName("endtime")]
    public string? EndTime { get; init; }

    [JsonPropertyName("laststation")]
    public string? LastReadStation { get; init; }

    [JsonPropertyName("laststationtime")]
    public string? LastReadTimestamp { get; init; }

    /// <summary>true = la unidad de carga se desvió/procesó en esta estación; false = pasó de largo.</summary>
    [JsonPropertyName("diverted")]
    public bool? LastReadDiverted { get; init; }

    /// <summary>Lista acumulativa de estados del pedido (HIS §4.1.5.12): 0000 creado, 0001 arrancado, 0002 finalizado, 0005 timeout, 0010 última UC.</summary>
    [JsonPropertyName("orderstatus")]
    public IReadOnlyList<string> OrderStatusCodes { get; init; } = [];

    [JsonPropertyName("lines")]
    public IReadOnlyList<LineaEventoPedidoDto> Lines { get; init; } = [];
}
