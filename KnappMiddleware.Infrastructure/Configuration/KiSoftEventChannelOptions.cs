namespace KnappMiddleware.Infrastructure.Configuration;

/// <summary>Canal 9802: eventos empujados por KiSoft (confirmaciones, inventario) hacia el middleware.</summary>
public sealed class KiSoftEventChannelOptions : KiSoftTcpChannelOptions
{
    public const string SectionName = "KiSoft:EventChannel";
}
