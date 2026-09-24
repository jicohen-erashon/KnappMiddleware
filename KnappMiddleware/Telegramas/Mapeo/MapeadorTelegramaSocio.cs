using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>Traduce el payload SAP de socio al registro de datos maestros 15N (HIS §3.1.2.2), envuelto en el bracket 151/159.</summary>
public static class MapeadorTelegramaSocio
{
    public const string OpenUpsert = "151";
    public const string Close = "159";

    public static string BuildNew(SolicitudSocioDto dto)
    {
        var writer = new EscritorTelegrama();
        writer.Raw("15N");
        writer.LengthPrefix(2, 16);
        writer.LengthPrefix(2, 12);
        writer.RawValue(16, TipoCampo.Alfanumerico, dto.Mandante);
        writer.RawValue(12, TipoCampo.Alfanumerico, dto.PartnerNumber);

        writer.Block('C', dto.Company is not null, w => w.Field(2, 30, TipoCampo.Alfanumerico, dto.Company));
        writer.Block('A', dto.Title is not null, w => w.Field(2, 30, TipoCampo.Alfanumerico, dto.Title));
        writer.Block('S', dto.Street is not null, w => w.Field(2, 30, TipoCampo.Alfanumerico, dto.Street));
        writer.Block('P', dto.City is not null, w => w.Field(2, 30, TipoCampo.Alfanumerico, dto.City));
        writer.Block('Z', dto.PostalCode is not null, w => w.Field(2, 6, TipoCampo.Alfanumerico, dto.PostalCode));
        writer.Block('R', dto.Region is not null, w => w.Field(2, 30, TipoCampo.Alfanumerico, dto.Region));
        writer.Block('O', dto.CountryCode is not null, w => w.Field(2, 2, TipoCampo.Alfanumerico, dto.CountryCode));
        writer.Block('E', dto.Email is not null, w => w.Field(2, 30, TipoCampo.Alfanumerico, dto.Email));
        writer.Block('L', dto.Telephone is not null, w => w.Field(2, 30, TipoCampo.Alfanumerico, dto.Telephone));

        return writer.Build();
    }
}
