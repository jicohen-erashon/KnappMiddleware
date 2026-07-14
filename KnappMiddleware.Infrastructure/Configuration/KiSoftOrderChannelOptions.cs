namespace KnappMiddleware.Infrastructure.Configuration;

/// <summary>Canal 9801: comandos síncronos SAP → KiSoft (alta/baja de pedidos, datos maestros).</summary>
public sealed class KiSoftOrderChannelOptions : KiSoftTcpChannelOptions
{
    public const string SectionName = "KiSoft:OrderChannel";
}
