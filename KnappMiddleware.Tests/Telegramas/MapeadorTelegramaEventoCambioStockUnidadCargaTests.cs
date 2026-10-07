using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

public class MapeadorTelegramaEventoCambioStockUnidadCargaTests
{
    [Fact]
    public void Decode_ParsesHeaderAndStatesBlock()
    {
        var data =
            "3UU" + "06" + "03" + "12" +
            "UC0001" + "065" + "GEO" + new string('0', 8) + "1" + // geocódigo (12 exactos)
            "T" + "02" + "02" + "02" +
            "01" + "01" +
            "02" + "02";

        var result = MapeadorTelegramaEventoCambioStockUnidadCarga.Decode(data);

        Assert.Equal("UC0001", result.LoadUnitCode);
        Assert.Equal("65", result.Station); // TipoCampo.Numerico recorta ceros a la izquierda
        Assert.Equal("GEO" + new string('0', 8) + "1", result.GeoCode);
        Assert.Equal(2, result.States.Count);
        Assert.Equal("1", result.States[0].SlotNumber);
        Assert.Equal("1", result.States[0].State);
        Assert.Equal("2", result.States[1].SlotNumber);
        Assert.Equal("2", result.States[1].State);
    }

    [Fact]
    public void IsLoadUnitStockChangeEvent_OnlyMatchesRecordId3UU()
    {
        Assert.True(MapeadorTelegramaEventoCambioStockUnidadCarga.IsLoadUnitStockChangeEvent("3UU06UC0001..."));
        Assert.False(MapeadorTelegramaEventoCambioStockUnidadCarga.IsLoadUnitStockChangeEvent("3AU"));
    }
}
