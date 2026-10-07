using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

public class MapeadorTelegramaEventoUnidadCargaVaciaTests
{
    [Fact]
    public void Decode_ParsesLoadUnitCode()
    {
        var data = "3UE" + "06" + "UC0001";

        var result = MapeadorTelegramaEventoUnidadCargaVacia.Decode(data);

        Assert.Equal("UC0001", result.LoadUnitCode);
    }

    [Fact]
    public void IsLoadUnitEmptyEvent_OnlyMatchesRecordId3UE()
    {
        Assert.True(MapeadorTelegramaEventoUnidadCargaVacia.IsLoadUnitEmptyEvent("3UE06UC0001"));
        Assert.False(MapeadorTelegramaEventoUnidadCargaVacia.IsLoadUnitEmptyEvent("3UU"));
    }
}
