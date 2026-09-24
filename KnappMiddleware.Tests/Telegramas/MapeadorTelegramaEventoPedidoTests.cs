using KnappMiddleware.Telegramas.Mapeo;

namespace KnappMiddleware.Tests.Telegramas;

/// <summary>
/// Decodifica una trama 32R construida a mano (byte a byte, según el orden documentado en HIS §4.1)
/// para validar la lógica interna de <see cref="MapeadorTelegramaEventoPedido.Decode"/> — no valida
/// contra tráfico real de KiSoft, pero atrapa errores de desplazamiento/orden de campos.
/// </summary>
public class MapeadorTelegramaEventoPedidoTests
{
    [Fact]
    public void Decode_ParsesHeaderAndSingleValueBlocks()
    {
        var data =
            "32R" +
            "16" + "12" + "04" +
            "A1301           " +   // mandante (16)
            "P1          " +       // número de pedido (12)
            "0001" +                // número de hoja (4)
            "T" + "02" + "01" +     // tipo de pedido = 01 (pedido de salida de mercancía)
            "A" + "04" + "00" + "0002" + // cantidad de hojas = 0002 (transmitido a KNAPP: longitud 0)
            "O" + "02" + "04" + "0000" + "0001"; // estados: creado, arrancado

        var result = MapeadorTelegramaEventoPedido.Decode(data);

        Assert.Equal("A1301", result.Mandante);
        Assert.Equal("P1", result.OrderNumber);
        Assert.Equal("1", result.SheetNumber); // TipoCampo.Numerico recorta ceros a la izquierda
        Assert.Equal("1", result.OrderType);
        Assert.Equal("2", result.SheetCount);
        Assert.Equal(["0", "1"], result.OrderStatusCodes); // TipoCampo.Numerico recorta ceros a la izquierda
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Decode_ParsesLinesBlock()
    {
        var data =
            "32R" +
            "16" + "12" + "04" +
            "A1301           " +
            "P1          " +
            "0001" +
            "Z" + "001" +
            "00" + "03" + "12" + "04" + "08" + "00" + "00" + "00" + "04" + "01" + "02" + "00" + "00" + "00" + "00" + "00" + "00" +
            "065" + "ASP500TAB001" + "0001" + "STANDARD" + "0010" + "1" + "30";

        var result = MapeadorTelegramaEventoPedido.Decode(data);

        var line = Assert.Single(result.Lines);
        Assert.Equal("65", line.Station); // TipoCampo.Numerico recorta ceros a la izquierda
        Assert.Equal("ASP500TAB001", line.ProductNumber);
        Assert.Equal("1", line.PackSize);
        Assert.Equal("STANDARD", line.StockType);
        Assert.Equal("10", line.Quantity);
        Assert.Equal("1", line.StockQuality);
        Assert.Equal("30", line.LineStatus);
        Assert.Null(line.BatchNumber);
        Assert.Null(line.LineReference);
    }

    [Fact]
    public void IsOrderEvent_OnlyMatchesRecordId32R()
    {
        Assert.True(MapeadorTelegramaEventoPedido.IsOrderEvent("32R1600120..."));
        Assert.False(MapeadorTelegramaEventoPedido.IsOrderEvent("3HR"));
    }
}
