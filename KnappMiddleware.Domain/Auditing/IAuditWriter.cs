namespace KnappMiddleware.Domain.Auditing;

/// <summary>
/// Punto de entrada para la auditoría opcional. Enqueue nunca bloquea ni lanza: si la auditoría está
/// deshabilitada (flag) o la cola interna está llena, el registro simplemente se descarta.
/// </summary>
public interface IAuditWriter
{
    void EnqueueEntrada(AuditRecord record);

    void EnqueueSalida(AuditRecord record);
}
