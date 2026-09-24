namespace KnappMiddleware.Auditing;

/// <summary>Estados del ciclo de vida de un telegrama en la auditoría (BuzonEntrada/BuzonSalida).</summary>
public enum AuditEstado
{
    Recibido,
    Validado,
    Traducido,
    Gate,
    Enviado,
    Encolado,
    Emitido,
    Entregado,
    Perdido,
    Error
}
