using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

public class MapeadorTelegramaEventoStockArticuloTests
{
    [Fact]
    public void Decode_ParsesLinesBlock()
    {
        var data =
            "3XR" +
            "i" + "001" +
            "03" + "16" + "12" + "04" + "10" + "20" + "08" + "04" + "01" + "06" + "02" +
            "065" +
            "A1301".PadRight(16) +
            "ASP500TAB001" +
            "0001" +
            "STANDARD".PadRight(10) +
            "LOT20260901".PadRight(20) +
            "20270101" +
            "0010" +
            "1" +
            "UC0001" +
            "03";

        var result = MapeadorTelegramaEventoStockArticulo.Decode(data);

        var line = Assert.Single(result.Lines);
        Assert.Equal("65", line.Station); // TipoCampo.Numerico recorta ceros a la izquierda
        Assert.Equal("A1301", line.Mandante);
        Assert.Equal("ASP500TAB001", line.ProductNumber);
        Assert.Equal("1", line.PackSize);
        Assert.Equal("STANDARD", line.StockType);
        Assert.Equal("LOT20260901", line.BatchNumber);
        Assert.Equal("20270101", line.ExpirationDate);
        Assert.Equal("10", line.Quantity);
        Assert.Equal("1", line.StockQuality);
        Assert.Equal("UC0001", line.LoadUnitCode);
        Assert.Equal("3", line.SlotNumber);
    }

    [Fact]
    public void IsStockArticleEvent_OnlyMatchesRecordId3XR()
    {
        Assert.True(MapeadorTelegramaEventoStockArticulo.IsStockArticleEvent("3XRi001..."));
        Assert.False(MapeadorTelegramaEventoStockArticulo.IsStockArticleEvent("3RR"));
    }
}
