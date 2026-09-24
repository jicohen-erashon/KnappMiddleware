using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>Código de barras/EAN de un artículo (bloque B del 14N, HIS pág. 20: ancho 20, alfanumérico).</summary>
public sealed class CodigoBarraArticuloDto
{
    // productnumber/unitalt: SAP los envía pero el bloque B del HIS solo transmite el código
    // (eancode); no se usan en el mapeo TLV.
    [JsonPropertyName("productnumber")]
    public string? ProductNumber { get; init; }

    [JsonPropertyName("eancode")]
    [StringLength(20)]
    public string? EanCode { get; init; }

    [JsonPropertyName("unitalt")]
    public string? UnitAlt { get; init; }
}

/// <summary>Propiedad especial de un artículo (bloque E del 14N, HIS pág. 28): "01" obligación de lote, "02" fecha de duración, "03" número de serie.</summary>
public sealed class PropiedadArticuloDto
{
    [JsonPropertyName("property")]
    [StringLength(2)]
    public string? Property { get; init; }
}

/// <summary>
/// Payload que SAP envía a POST /api/v1/sap/article/14N (datos maestros de artículo, HIS §3.1.1 →
/// 14N/24N). Clase plana con TODOS los campos que SAP realmente envía (ver JSON-SAP/14N_articulo.json),
/// en el mismo orden del JSON — los que no viajan a la trama TLV están marcados como tal, sin usarse
/// en <see cref="Telegramas.Mapeo.MapeadorTelegramaArticulo"/>. Anchos confirmados contra el PDF
/// original (HIS págs. 19-22, §3.1.1.1 a §3.1.1.4), no solo contra el catálogo derivado LENGTHS.md.
/// </summary>
public sealed class SolicitudArticuloDto : ISobreTelegramaSap
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

    // ===== Campos del registro 14N (HIS §3.1.1.2 / §3.1.1.4) =====

    [JsonPropertyName("warehousenumber")]
    public string? WarehouseNumber { get; init; }

    /// <summary>HIS pág. 20: ancho 16, alfanumérico.</summary>
    [JsonPropertyName("mandtk")]
    [StringLength(16)]
    public required string Mandante { get; init; }

    /// <summary>HIS pág. 20: ancho 3, numérico (001-004, 010, 011, 061, 065, 199).</summary>
    [JsonPropertyName("station")]
    [StringLength(3)]
    public required string Station { get; init; }

    /// <summary>HIS pág. 20: "Bloque de estantería", ancho 0 o 3 según estación (00 si no aplica).</summary>
    [JsonPropertyName("rackblk")]
    [StringLength(3)]
    public string? RackBlock { get; init; }

    /// <summary>HIS pág. 20: "Canal de estantería", ancho 0 o 3 según estación.</summary>
    [JsonPropertyName("rackchannel")]
    [StringLength(3)]
    public string? RackChannel { get; init; }

    /// <summary>HIS pág. 20: "Nivel", ancho 0 o 3 según estación.</summary>
    [JsonPropertyName("racklevel")]
    [StringLength(3)]
    public string? RackLevel { get; init; }

    /// <summary>HIS pág. 20: ancho 12, alfanumérico.</summary>
    [JsonPropertyName("productnumber")]
    [StringLength(12)]
    public required string ProductNumber { get; init; }

    /// <summary>HIS pág. 20: ancho 4, numérico (0001-9999).</summary>
    [JsonPropertyName("packsize")]
    [StringLength(4)]
    public required string PackSize { get; init; }

    /// <summary>HIS pág. 20 ("Y", número de eyección): ancho 2, numérico (10-80). HIS=ejectionnumber; SAP=eyenumber.</summary>
    [JsonPropertyName("eyenumber")]
    [StringLength(2)]
    public string? EjectionNumber { get; init; }

    /// <summary>HIS pág. 20 ("M", cantidad máxima): ancho 4, numérico (0001-9999).</summary>
    [JsonPropertyName("sdamaxqty")]
    [Range(0, 9999)]
    public int? MaxAutomatedQuantity { get; init; }

    /// <summary>HIS pág. 21 ("D", longitud [mm]): ancho 4, numérico (0001-9999). SAP envía decimal
    /// (p. ej. "120.000"); el mapper redondea y convierte según <see cref="DimensionUnit"/>.</summary>
    [JsonPropertyName("length")]
    public decimal? LengthMm { get; init; }

    /// <summary>HIS pág. 21 ("D", ancho [mm]): ancho 4, numérico.</summary>
    [JsonPropertyName("width")]
    public decimal? WidthMm { get; init; }

    /// <summary>HIS pág. 21 ("D", altura [mm]): ancho 4, numérico.</summary>
    [JsonPropertyName("height")]
    public decimal? HeightMm { get; init; }

    // umlwh: unidad de dimensión que SAP declara (MM/CM/M) — no es un campo del HIS (que siempre
    // asume mm); el mapper la usa para convertir length/width/height antes de escribir a la trama.
    [JsonPropertyName("umlwh")]
    public string? DimensionUnit { get; init; }

    // netweight: HIS documenta un único campo de peso (grossweigth, abajo); el neto se recibe pero
    // no se usa en el mapeo TLV (ver comentario en MapeadorTelegramaArticulo).
    [JsonPropertyName("netweight")]
    public decimal? NetWeightKg { get; init; }

    /// <summary>HIS pág. 21 ("G", peso en 1/10 gramos): ancho 6, numérico (000001-300000). SAP envía
    /// decimal en la unidad de <see cref="WeightUnit"/> (típicamente kg); el mapper convierte.</summary>
    [JsonPropertyName("grossweigth")]
    public decimal? GrossWeightKg { get; init; }

    // umweigth: unidad de peso que SAP declara (KG/G/LB/OZ) — no es un campo del HIS (que siempre
    // asume 1/10 gramo); el mapper la usa para convertir grossweigth antes de escribir a la trama.
    [JsonPropertyName("umweigth")]
    public string? WeightUnit { get; init; }

    /// <summary>HIS pág. 21 ("B", códigos de artículo): LOOP de códigos, ancho 20 cada uno, alfanumérico.</summary>
    [JsonPropertyName("itBarcodes")]
    public IReadOnlyList<CodigoBarraArticuloDto>? Barcodes { get; init; }

    /// <summary>HIS pág. 21 ("K", nombre de artículo): ancho 40, texto libre. HIS=productname; SAP=productdescription.</summary>
    [JsonPropertyName("productdescription")]
    [StringLength(40)]
    public string? Description { get; init; }

    /// <summary>HIS pág. 21 ("K", geocódigo): ancho 12, alfanumérico.</summary>
    [JsonPropertyName("geocode")]
    [StringLength(12)]
    public string? GeoCode { get; init; }

    /// <summary>HIS pág. 22 ("S", stock máximo): ancho 4, numérico (0001-9999).</summary>
    [JsonPropertyName("repmaxqty")]
    [Range(0, 9999)]
    public int? ReplenishmentMaxQty { get; init; }

    /// <summary>HIS pág. 22 ("S", stock mínimo): ancho 4, numérico (0001-9999).</summary>
    [JsonPropertyName("repminqty")]
    [Range(0, 9999)]
    public int? ReplenishmentMinQty { get; init; }

    /// <summary>HIS pág. 22 ("E", propiedades de artículo): LOOP, ancho 2 cada uno, numérico (01-99).</summary>
    [JsonPropertyName("itProperties")]
    public IReadOnlyList<PropiedadArticuloDto>? Properties { get; init; }

    /// <summary>HIS pág. 22 ("T", geocódigo de stock de reposición): ancho 12, alfanumérico.</summary>
    [JsonPropertyName("repgeocode")]
    [StringLength(12)]
    public string? ReplenishmentGeoCode { get; init; }

    /// <summary>HIS pág. 22 ("T", número de estación de reposición): ancho 3, numérico (244/245).</summary>
    [JsonPropertyName("repleftstation")]
    [StringLength(3)]
    public string? ReplenishmentStation { get; init; }

    // ===== Auditoría propia del maestro de artículo ("a"), no documentada en el HIS =====
    [JsonPropertyName("acreateddateon")]
    public string? ArticleCreatedDateOn { get; init; }

    [JsonPropertyName("acreatedtimeon")]
    public string? ArticleCreatedTimeOn { get; init; }

    [JsonPropertyName("acreatedby")]
    public string? ArticleCreatedBy { get; init; }

    [JsonPropertyName("amodifieddateon")]
    public string? ArticleModifiedDateOn { get; init; }

    [JsonPropertyName("amodifiedtimeon")]
    public string? ArticleModifiedTimeOn { get; init; }

    [JsonPropertyName("amodifiedby")]
    public string? ArticleModifiedBy { get; init; }
}
