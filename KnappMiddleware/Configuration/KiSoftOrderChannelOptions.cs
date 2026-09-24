namespace KnappMiddleware.Configuration;

/// <summary>Canal 9801: comandos síncronos SAP → KiSoft (alta/baja de pedidos, datos maestros).</summary>
public sealed class KiSoftOrderChannelOptions : KiSoftTcpChannelOptions
{
    public static KiSoftOrderChannelOptions ReadFrom(ClsConfigGate configGate)
    {
        var options = new KiSoftOrderChannelOptions();
        PopulateFrom(options, configGate, "kisoft.orderChannel");
        return options;
    }
}
