using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>Línea de un pedido de entrada (bloque "b" del 12N).</summary>
public sealed class LineaPedidoDto
{
    [JsonPropertyName("linereference")]
    [StringLength(20)]
    public string? LineReference { get; init; }

    [JsonPropertyName("station")]
    [StringLength(3)]
    public required string Station { get; init; }

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
    // 8 dígitos del HIS. No se anota StringLength acá por la misma razón que en Ruta.
    [JsonPropertyName("expirationdate")]
    public string? ExpirationDate { get; init; }

    [JsonPropertyName("quantity")]
    [Range(0, 9999)]
    public required int Quantity { get; init; }

    [JsonPropertyName("stockquality")]
    [StringLength(1)]
    public required string StockQuality { get; init; }

    // note/loadtype/loadunit (por línea)/unit: HIS §3.2.1.1 los declara pero con anchura "00"
    // (deshabilitados) en esta instalación — el mapper ya los reserva sin transmitir (ver
    // MapeadorTelegramaPedido.BuildNew). Se reciben para no romper el contrato, sin uso en la trama.
    [JsonPropertyName("note")]
    [StringLength(99)]
    public string? Note { get; init; }

    [JsonPropertyName("loadtype")]
    [StringLength(10)]
    public string? LoadType { get; init; }

    [JsonPropertyName("loadunit")]
    [StringLength(8)]
    public string? LoadUnit { get; init; }

    [JsonPropertyName("unit")]
    [StringLength(4)]
    public string? Unit { get; init; }
}

/// <summary>Texto libre asociado al pedido (bloque "texts" del 12N, hasta 9 líneas por HIS).</summary>
public sealed class TextoPedidoDto
{
    [JsonPropertyName("text")]
    [StringLength(99)]
    public string? Text { get; init; }
}

/// <summary>Parámetro/estado del pedido (bloque "parameters" del 12N).</summary>
public sealed class ParametroPedidoDto
{
    [JsonPropertyName("stateqty")]
    public int? StateQty { get; init; }

    [JsonPropertyName("state")]
    [StringLength(4)]
    public string? State { get; init; }
}

/// <summary>
/// Payload que SAP envía a POST /sap/order (HIS §3.2 → 12N/22N). Cubre únicamente los pedidos de
/// ENTRADA de tipo 04 (almacenaje en el sistema mediante unidad de carga / decanting) y 05
/// (almacenaje abierto / open goods-in), que son los únicos para los que SAP ha enviado una muestra
/// real. El tipo 02 (Transporte, requiere el bloque "K" de estaciones de destino) y los tipos de
/// SALIDA (10/35/36, HIS §3.3, con sus propios bloques C/E/F/S/U/O/Z) quedan sin mapear hasta contar
/// con un JSON de referencia — ver <see cref="Telegramas.Mapeo.MapeadorTelegramaPedido"/>.
/// </summary>
public sealed class SolicitudPedidoDto : SobreTelegramaSapDto
{
    [JsonPropertyName("mandtk")]
    [StringLength(16)]
    public required string Mandante { get; init; }

    [JsonPropertyName("ordernumber")]
    [StringLength(12)]
    public required string OrderNumber { get; init; }

    [JsonPropertyName("sheetnumber")]
    [StringLength(4)]
    public required string SheetNumber { get; init; }

    [JsonPropertyName("ordertype")]
    [StringLength(2)]
    public required string OrderType { get; init; }

    // loadtype (a nivel de encabezado): HIS declara "loadmedium" pero no está entre los tipos 04/05
    // soportados por MapeadorTelegramaPedido; se recibe sin uso en la trama.
    [JsonPropertyName("loadtype")]
    [StringLength(10)]
    public string? LoadType { get; init; }

    [JsonPropertyName("loadunit")]
    [StringLength(8)]
    public string? LoadUnit { get; init; }

    // businesspartner/texts/priority/parameters: SAP los envía en la muestra (HIS §3.2 los documenta),
    // pero MapeadorTelegramaPedido.BuildNew no los transmite hoy — no están en la lista de campos
    // "deshabilitados a propósito" que documenta el mapper, así que esto es un gap real pendiente de
    // revisar (¿KiSoft los necesita?), no solo un campo ignorado por diseño.
    [JsonPropertyName("businesspartner")]
    [StringLength(12)]
    public string? BusinessPartner { get; init; }

    [JsonPropertyName("texts")]
    public IReadOnlyList<TextoPedidoDto>? Texts { get; init; }

    [JsonPropertyName("priority")]
    [StringLength(3)]
    public string? Priority { get; init; }

    [JsonPropertyName("parameters")]
    public IReadOnlyList<ParametroPedidoDto>? Parameters { get; init; }

    /// <summary>El contador de líneas del LOOP (bloque "b") se transmite en 3 dígitos (ver
    /// MapeadorTelegramaPedido), de ahí el tope de 999.</summary>
    [JsonPropertyName("items")]
    [MaxLength(999)]
    public required IReadOnlyList<LineaPedidoDto> Items { get; init; }
}
