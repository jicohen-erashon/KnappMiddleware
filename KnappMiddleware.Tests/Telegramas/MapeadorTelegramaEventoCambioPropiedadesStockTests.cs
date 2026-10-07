using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

public class MapeadorTelegramaEventoCambioPropiedadesStockTests
{
    [Fact]
    public void Decode_ParsesHeaderAndStateTag()
    {
        var data =
            "3AU" + "03" + "12" + "20" + "16" + "10" +
            "065" +
            "ASP500TAB001" +
            "LOT20260901".PadRight(20) +
            "A1301".PadRight(16) +
            "STANDARD".PadRight(10) +
            "T" + "02" + "01";

        var result = MapeadorTelegramaEventoCambioPropiedadesStock.Decode(data);

        Assert.Equal("65", result.Station); // TipoCampo.Numerico recorta ceros a la izquierda
        Assert.Equal("ASP500TAB001", result.ProductNumber);
        Assert.Equal("LOT20260901", result.BatchNumber);
        Assert.Equal("A1301", result.Mandante);
        Assert.Equal("STANDARD", result.StockType);
        Assert.Equal("1", result.State);
    }

    [Fact]
    public void IsArticleStockChangeEvent_OnlyMatchesRecordId3AU()
    {
        Assert.True(MapeadorTelegramaEventoCambioPropiedadesStock.IsArticleStockChangeEvent("3AU0312..."));
        Assert.False(MapeadorTelegramaEventoCambioPropiedadesStock.IsArticleStockChangeEvent("3UU"));
    }
}
