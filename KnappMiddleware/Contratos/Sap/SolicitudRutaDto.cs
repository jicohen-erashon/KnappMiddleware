using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>
/// Payload que SAP envía a POST /api/v1/sap/route/16N (datos maestros de ruta teórica, HIS §3.1.3.2
/// → 16N/26N). Clase plana con TODOS los campos que SAP realmente envía (ver JSON-SAP/16N_ruta.json),
/// en el mismo orden del JSON — los que no viajan a la trama TLV están marcados como tal, sin usarse
/// en <see cref="Telegramas.Mapeo.MapeadorTelegramaRuta"/>.
/// </summary>
public sealed class SolicitudRutaDto : ISobreTelegramaSap
{
    // ===== Sobre SAP (metadatos de enrutamiento/auditoría, no documentados en el HIS) =====
    [JsonPropertyName("telid")]
    public string? Telid { get; init; }

    [JsonPropertyName("idrecord")]
    public string? IdRecord { get; init; }

    [JsonPropertyName("idrecordstatus")]
    public string? IdRecordStatus { get; init; }

    [JsonPropertyName("direction")]
    public string? Direction { get; init; }

    [JsonPropertyName("objecttype")]
    public string? ObjectType { get; init; }

    [JsonPropertyName("objectid")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("tecreateddateon")]
    public string? TeCreatedDateOn { get; init; }

    [JsonPropertyName("tecreatedtimeon")]
    public string? TeCreatedTimeOn { get; init; }

    [JsonPropertyName("tecreatedby")]
    public string? TeCreatedBy { get; init; }

    [JsonPropertyName("temodifieddateon")]
    public string? TeModifiedDateOn { get; init; }

    [JsonPropertyName("temodifiedtimeon")]
    public string? TeModifiedTimeOn { get; init; }

    [JsonPropertyName("temodifiedby")]
    public string? TeModifiedBy { get; init; }

    [JsonPropertyName("sentdate")]
    public string? SentDate { get; init; }

    [JsonPropertyName("senttime")]
    public string? SentTime { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    // ===== Campos del registro 16N (HIS §3.1.3.2.1) =====

    /// <summary>HIS pág. 35: "Longitud de número de ruta teórica" = 08 (ancho fijo, confirmado
    /// literal en el PDF, no es una suposición). SAP consistentemente envía 10 caracteres (p. ej.
    /// "RUTA000001") — mismatch real, escalado a KNAPP en JSON-SAP/PENDIENTE-1XR.md. Mientras se
    /// confirma, el ancho de la trama se mantiene fiel al spec (8): un valor de 10 caracteres se
    /// rechaza con 422 en vez de truncarse silenciosamente.</summary>
    [JsonPropertyName("route")]
    [StringLength(8)]
    public required string RouteNumber { get; init; }

    [JsonPropertyName("warehousenumber")]
    public string? WarehouseNumber { get; init; }

    /// <summary>HIS: ancho 16, alfanumérico. Se transmite en el encabezado del 16N.</summary>
    [JsonPropertyName("mandtk")]
    [StringLength(16)]
    public required string Mandante { get; init; }

    // description: SAP lo envía pero el HIS §3.1.3.2 no tiene un campo de descripción de ruta en la
    // trama — no se usa en el mapeo TLV.
    [JsonPropertyName("description")]
    [StringLength(35)]
    public string? Description { get; init; }

    /// <summary>HIS: campo numérico ancho 6, formato HHmmss. SAP envía "HH:mm:ss" (8 chars); el
    /// mapper quita los ":" antes de escribir. No se anota StringLength porque limitaría el formato
    /// de ENTRADA (con separadores), no el de la trama ya transformada.</summary>
    [JsonPropertyName("departuretime")]
    public string? DepartureTime { get; init; }

    /// <summary>Igual que DepartureTime (HHmmss, SAP envía con separadores).</summary>
    [JsonPropertyName("availabletime")]
    public string? AvailableTime { get; init; }

    // ramp (código, p. ej. "DIS001"): SAP lo envía pero no se usa en el mapeo TLV — el HIS solo
    // transmite el NÚMERO de rampa (rampnumber, bloque R), no su código alfanumérico.
    [JsonPropertyName("ramp")]
    [StringLength(6)]
    public string? RampCode { get; init; }

    /// <summary>HIS pág. 35-36, bloque R: se transmite como LOOP de un solo valor, campo de 5 dígitos
    /// (00001-00010 DIS001, 00021-00025 DIS002) — el mapper declara "cantidad de rampas"=1 y escribe
    /// este valor con padding a 5 dígitos.</summary>
    [JsonPropertyName("rampnumber")]
    [Range(0, 99999)]
    public int? RampNumber { get; init; }

    // numberoframps: SAP lo envía pero es redundante con "cantidad de rampas" que el mapper ya
    // calcula solo (siempre 1, porque el DTO solo admite un rampnumber) — no se usa en el mapeo TLV.
    [JsonPropertyName("numberoframps")]
    public int? NumberOfRamps { get; init; }

    // ===== Auditoría propia de la ruta teórica ("rt"), no documentada en el HIS =====
    [JsonPropertyName("rtcreateddateon")]
    public string? RouteCreatedDateOn { get; init; }

    [JsonPropertyName("rtcreatedtimeon")]
    public string? RouteCreatedTimeOn { get; init; }

    [JsonPropertyName("rtcreatedby")]
    public string? RouteCreatedBy { get; init; }

    [JsonPropertyName("rtmodifieddateon")]
    public string? RouteModifiedDateOn { get; init; }

    [JsonPropertyName("rtmodifiedtimeon")]
    public string? RouteModifiedTimeOn { get; init; }
}
