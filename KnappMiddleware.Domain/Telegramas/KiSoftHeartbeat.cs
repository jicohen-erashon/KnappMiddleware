namespace KnappMiddleware.Domain.Telegramas;

/// <summary>Identificadores del protocolo de heartbeat KiSoft (sección 4.2 del GS).</summary>
public static class KiSoftHeartbeat
{
    /// <summary>Middleware → KiSoft: heartbeat saliente tras N segundos de silencio.</summary>
    public const string OutgoingPing = "1HR";

    /// <summary>KiSoft → Middleware: confirmación del heartbeat saliente.</summary>
    public const string OutgoingPingAck = "2HR";

    /// <summary>KiSoft → Middleware: heartbeat entrante de KiSoft.</summary>
    public const string IncomingPing = "3HR";

    /// <summary>Middleware → KiSoft: respuesta al heartbeat entrante.</summary>
    public const string IncomingPingAck = "4HR";
}
