namespace KnappMiddleware.Configuration;

/// <summary>Canal 9802: eventos empujados por KiSoft (confirmaciones, inventario) hacia el middleware.</summary>
public sealed class KiSoftEventChannelOptions : KiSoftTcpChannelOptions
{
    public static KiSoftEventChannelOptions ReadFrom(ClsConfigGate configGate)
    {
        var options = new KiSoftEventChannelOptions();
        PopulateFrom(options, configGate, "kisoft.eventChannel");
        return options;
    }
}
