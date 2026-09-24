namespace KnappMiddleware.Telegramas;

/// <summary>Identificadores del protocolo de heartbeat KiSoft (sección 4.2 del GS).</summary>
public static class LatidoKiSoft
{
    /// <summary>Middleware → KiSoft: heartbeat saliente tras N segundos de silencio.</summary>
    public const string PingSaliente = "1HR";

    /// <summary>KiSoft → Middleware: confirmación del heartbeat saliente.</summary>
    public const string AcusePingSaliente = "2HR";

    /// <summary>KiSoft → Middleware: heartbeat entrante de KiSoft.</summary>
    public const string PingEntrante = "3HR";

    /// <summary>Middleware → KiSoft: respuesta al heartbeat entrante.</summary>
    public const string AcusePingEntrante = "4HR";
}
