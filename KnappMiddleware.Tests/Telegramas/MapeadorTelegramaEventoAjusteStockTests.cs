using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// Decodifica una trama 3SC construida a mano con TODOS los bloques opcionales presentes (K,T,B,L,C,
/// E,F,v,V,r,t,U,X), aunque en tráfico real rara vez vengan todos a la vez (HIS §4.4.1.3: depende del
/// sistema de almacenamiento que generó el ajuste).
/// </summary>
public class MapeadorTelegramaEventoAjusteStockTests
{
    [Fact]
    public void Decode_ParsesAllOptionalBlocks()
    {
        var data =
            "3SC" + "14" + "SC00000001".PadRight(14) +
            "K" + "03" + "065" +
            "T" + "02" + "43" +
            "B" + "06" + "02" + "UC0001" + "03" +
            "L" + "16" + "12" + "04" + "08" + "A1301".PadRight(16) + "ASP500TAB001" + "0001" + "STANDARD" +
            "C" + "20" + "LOT20260901".PadRight(20) +
            "E" + "08" + "20270101" +
            "F" + "01" + "1" +
            "v" + "20" + "DAMAGED".PadRight(20) +
            "V" + "02" + "20" + "DAMAGED".PadRight(20) + "EXPIRED".PadRight(20) +
            "r" + "15" + "00" + "LOST".PadRight(15) +
            "t" + "14" + "20260930153045" +
            "U" + "08" + "OP001".PadRight(8) +
            "X" + "05" + "+0012";

        var result = MapeadorTelegramaEventoAjusteStock.Decode(data);

        Assert.Equal("SC00000001", result.CorrectionNumber);
        Assert.Equal("65", result.Station); // TipoCampo.Numerico recorta ceros a la izquierda
        Assert.Equal("43", result.MessageType);
        Assert.Equal("UC0001", result.LoadUnitCode);
        Assert.Equal("3", result.SlotNumber);
        Assert.Equal("A1301", result.Mandante);
        Assert.Equal("ASP500TAB001", result.ProductNumber);
        Assert.Equal("1", result.PackSize);
        Assert.Equal("STANDARD", result.StockType);
        Assert.Equal("LOT20260901", result.BatchNumber);
        Assert.Equal("20270101", result.ExpirationDate);
        Assert.Equal("1", result.StockQuality);
        Assert.Equal("DAMAGED", result.BlockReasonChanged);
        Assert.Equal(["DAMAGED", "EXPIRED"], result.BlockReasons);
        Assert.Equal("LOST", result.Reason);
        Assert.Equal("20260930153045", result.OccurredAt);
        Assert.Equal("OP001", result.WarehouseOperator);
        Assert.Equal("+0012", result.Difference);
    }

    [Fact]
    public void Decode_HeaderOnly_LeavesOptionalBlocksNull()
    {
        var data = "3SC" + "14" + "SC00000002".PadRight(14);

        var result = MapeadorTelegramaEventoAjusteStock.Decode(data);

        Assert.Equal("SC00000002", result.CorrectionNumber);
        Assert.Null(result.Station);
        Assert.Null(result.MessageType);
        Assert.Empty(result.BlockReasons);
    }

    [Fact]
    public void IsStockAdjustmentEvent_OnlyMatchesRecordId3SC()
    {
        Assert.True(MapeadorTelegramaEventoAjusteStock.IsStockAdjustmentEvent("3SC14SC00000001..."));
        Assert.False(MapeadorTelegramaEventoAjusteStock.IsStockAdjustmentEvent("3UE"));
    }
}
