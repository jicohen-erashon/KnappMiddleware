using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

public class MapeadorTelegramaEventoArchivoInventarioTests
{
    [Fact]
    public void Decode_ParsesStation()
    {
        var data = "3RR" + "03" + "065";

        var result = MapeadorTelegramaEventoArchivoInventario.Decode(data);

        Assert.Equal("65", result.Station); // TipoCampo.Numerico recorta ceros a la izquierda
    }

    [Fact]
    public void IsInventoryFileReadyEvent_OnlyMatchesRecordId3RR()
    {
        Assert.True(MapeadorTelegramaEventoArchivoInventario.IsInventoryFileReadyEvent("3RR03065"));
        Assert.False(MapeadorTelegramaEventoArchivoInventario.IsInventoryFileReadyEvent("3XR"));
    }
}
