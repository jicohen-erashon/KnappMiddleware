using KnappMiddleware.Contratos.Sap;

namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>
/// Traduce la consulta de stock de un artículo al registro 1XR (HIS V3 §3.5.2). A diferencia de la
/// mayoría de telegramas, mandante y tipo de stock tienen presencia opcional ("00" o su ancho
/// nominal) — el spec declara TODAS las longitudes antes que TODOS los valores, por eso se usa
/// LengthPrefix/RawValue en vez de Field (que escribiría prefijo+valor intercalados). La respuesta
/// (2XR) es un simple mensaje de estado, sin datos de stock en el cuerpo.
/// </summary>
public static class MapeadorTelegramaConsultaStock
{
    public static string BuildRequest(SolicitudConsultaStockDto dto)
    {
        var mandanteLength = string.IsNullOrEmpty(dto.Mandante) ? 0 : 16;
        var stockTypeLength = string.IsNullOrEmpty(dto.StockType) ? 0 : 8;

        var writer = new EscritorTelegrama();
        writer.Raw("1XR");
        writer.LengthPrefix(2, 3);              // longitud de número de estación (fija)
        writer.LengthPrefix(2, mandanteLength);  // longitud de mandante (00 ausente, 16 presente)
        writer.LengthPrefix(2, 12);              // longitud de número de artículo (fija)
        writer.LengthPrefix(2, stockTypeLength); // longitud de tipo de stock (00 ausente, 08 presente)
        writer.RawValue(3, TipoCampo.Numerico, dto.Station);
        writer.RawValue(mandanteLength, TipoCampo.Alfanumerico, dto.Mandante);
        writer.RawValue(12, TipoCampo.Alfanumerico, dto.ProductNumber);
        writer.RawValue(stockTypeLength, TipoCampo.Alfanumerico, dto.StockType);
        writer.Tag('C').Field(2, 20, TipoCampo.Alfanumerico, dto.BatchNumber);

        return writer.Build();
    }
}
