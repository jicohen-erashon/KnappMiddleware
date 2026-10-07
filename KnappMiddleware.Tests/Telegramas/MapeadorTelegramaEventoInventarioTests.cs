using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// Decodifica una trama 3IR construida a mano (byte a byte, según el orden documentado en HIS V3
/// §4.2.1) para validar la lógica interna de <see cref="MapeadorTelegramaEventoInventario.Decode"/> —
/// no valida contra tráfico real de KiSoft, pero atrapa errores de desplazamiento/orden de campos.
/// </summary>
public class MapeadorTelegramaEventoInventarioTests
{
    [Fact]
    public void Decode_ParsesHeaderAndStatesBlock()
    {
        var data =
            "3IR" +
            "16" + "07" + "00" +
            "A1301           " +   // mandante (16)
            "SOL0001" +             // número de solicitud de inventario (7)
            "O" + "01" + "04" + "0002"; // un estado: finalizado normalmente

        var result = MapeadorTelegramaEventoInventario.Decode(data);

        Assert.Equal("A1301", result.Mandante);
        Assert.Equal("SOL0001", result.InventoryRequestNumber);
        Assert.Equal(["2"], result.States); // TipoCampo.Numerico recorta ceros a la izquierda
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Decode_ParsesLinesBlock()
    {
        var data =
            "3IR" +
            "16" + "07" + "00" +
            "A1301           " +
            "SOL0001" +
            "b" + "001" +
            "00" + "03" + "12" + "04" + "08" + "20" + "08" + "00" + "04" + "01" + "00" + "10" + "06" + "02" + "30" + "02" + "08" + "14" +
            "065" + "ASP500TAB001" + "0001" + "STANDARD" + "LOT20260901         " + "20270101" +
            "0010" + "1" + "PALLET    " + "UC0001" + "03" + "BLOQUEO_MANUAL                " + "01" + "OP001   " + "20260930153045";

        var result = MapeadorTelegramaEventoInventario.Decode(data);

        Assert.Empty(result.States);
        var line = Assert.Single(result.Lines);
        Assert.Equal("65", line.Station); // TipoCampo.Numerico recorta ceros a la izquierda
        Assert.Equal("ASP500TAB001", line.ProductNumber);
        Assert.Equal("1", line.PackSize);
        Assert.Equal("STANDARD", line.StockType);
        Assert.Equal("LOT20260901", line.BatchNumber);
        Assert.Equal("20270101", line.ExpirationDate);
        Assert.Equal("10", line.Quantity);
        Assert.Equal("1", line.StockQuality);
        Assert.Equal("PALLET", line.LoadMedium);
        Assert.Equal("UC0001", line.LoadUnitCode);
        Assert.Equal("3", line.SlotNumber);
        Assert.Equal("BLOQUEO_MANUAL", line.BlockStatus);
        Assert.Equal("1", line.LineStatus);
        Assert.Equal("OP001", line.WarehouseOperator);
        Assert.Equal("20260930153045", line.ProcessedAt);
    }

    [Fact]
    public void IsInventoryEvent_OnlyMatchesRecordId3IR()
    {
        Assert.True(MapeadorTelegramaEventoInventario.IsInventoryEvent("3IR1600120..."));
        Assert.False(MapeadorTelegramaEventoInventario.IsInventoryEvent("32R"));
    }
}
