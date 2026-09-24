using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>
/// Identificador de negocio (objectid) y autor SAP (tecreatedby) del sobre — ambos opcionales (SAP
/// puede no enviarlos), pero mucho más útiles para trazabilidad/debugging en buzon_entrada/salida que
/// el usuario fijo de Basic Auth (siempre "sap"). Implementada tanto por <see cref="SobreTelegramaSapDto"/>
/// (herencia, 7 de los 9 DTOs) como directamente por los dos DTOs aplanados (14N, 16N).
/// </summary>
public interface ISobreTelegramaSap
{
    string? ObjectId { get; }
    string? TeCreatedBy { get; }
}

/// <summary>
/// Campos del "sobre" que SAP incluye en TODOS los telegramas de la muestra (ver JSON-SAP/*.json):
/// metadatos de enrutamiento y auditoría propios de la capa SAP, ninguno documentado en el HIS spec
/// (que solo define el contenido del registro KiSoft, no este sobre). Se declaran acá — opcionales,
/// sin usarse en ningún mapeo TLV — únicamente para que el contrato expuesto en Scalar refleje la
/// estructura real que SAP envía, no solo el subconjunto que el middleware traduce a la trama.
/// </summary>
public abstract class SobreTelegramaSapDto : ISobreTelegramaSap
{
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

    [JsonPropertyName("warehousenumber")]
    public string? WarehouseNumber { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("sentdate")]
    public string? SentDate { get; init; }

    [JsonPropertyName("senttime")]
    public string? SentTime { get; init; }

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
}
