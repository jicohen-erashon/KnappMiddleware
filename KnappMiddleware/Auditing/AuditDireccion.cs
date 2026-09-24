namespace KnappMiddleware.Auditing;

/// <summary>A qué buzón pertenece un registro de auditoría.</summary>
public enum AuditDireccion
{
    /// <summary>BuzonEntrada: SAP -> Middleware y KiSoft -> Middleware.</summary>
    Entrada,

    /// <summary>BuzonSalida: Middleware -> KiSoft y Middleware -> SAP.</summary>
    Salida
}
