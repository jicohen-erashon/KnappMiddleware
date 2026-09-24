using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KnappMiddleware.Contratos.Sap;

/// <summary>Payload que SAP envía a POST /sap/businesspartner (datos maestros de socio, HIS §3.1.2 → 15N/25N).</summary>
public sealed class SolicitudSocioDto : SobreTelegramaSapDto
{
    [JsonPropertyName("mandtk")]
    [StringLength(16)]
    public required string Mandante { get; init; }

    [JsonPropertyName("partner")]
    [StringLength(12)]
    public required string PartnerNumber { get; init; }

    [JsonPropertyName("nameOrg1")]
    [StringLength(30)]
    public string? Company { get; init; }

    // nameOrg2: SAP envía la sucursal en una segunda línea de nombre; el HIS solo tiene un campo
    // "company" (30) — no hay dónde mapearlo sin combinar/truncar, así que hoy no se usa en la trama.
    [JsonPropertyName("nameOrg2")]
    [StringLength(30)]
    public string? CompanyLine2 { get; init; }

    // title: código numérico de tratamiento (p. ej. "0001"); distinto de "titledescription" (el que sí
    // se mapea abajo como Title). No se usa en la trama.
    [JsonPropertyName("title")]
    public string? TreatmentCode { get; init; }

    [JsonPropertyName("titledescription")]
    [StringLength(30)]
    public string? Title { get; init; }

    [JsonPropertyName("street")]
    [StringLength(30)]
    public string? Street { get; init; }

    [JsonPropertyName("city1")]
    [StringLength(30)]
    public string? City { get; init; }

    // city2: SAP envía provincia/departamento acá (además de regionname); no se usa en la trama.
    [JsonPropertyName("city2")]
    [StringLength(30)]
    public string? City2 { get; init; }

    [JsonPropertyName("postalcode")]
    [StringLength(6)]
    public string? PostalCode { get; init; }

    // regioncode: código corto de región (p. ej. "GUA"); el HIS solo tiene el campo de texto "region"
    // (mapeado desde regionname, abajo). No se usa en la trama.
    [JsonPropertyName("regioncode")]
    [StringLength(6)]
    public string? RegionCode { get; init; }

    [JsonPropertyName("regionname")]
    [StringLength(30)]
    public string? Region { get; init; }

    [JsonPropertyName("country")]
    [StringLength(2)]
    public string? CountryCode { get; init; }

    // countryname: nombre completo del país; el HIS solo tiene el código ISO (mapeado como CountryCode).
    [JsonPropertyName("countryname")]
    [StringLength(30)]
    public string? CountryName { get; init; }

    [JsonPropertyName("email")]
    [StringLength(30)]
    public string? Email { get; init; }

    [JsonPropertyName("telephone")]
    [StringLength(30)]
    public string? Telephone { get; init; }
}
