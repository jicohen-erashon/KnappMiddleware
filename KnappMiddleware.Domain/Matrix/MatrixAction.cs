namespace KnappMiddleware.Domain.Matrix;

/// <summary>Acción resuelta por el Gate de matriz para una combinación emisor×tipo-telegrama×estación.</summary>
public enum MatrixAction
{
    /// <summary>El telegrama sigue su flujo normal.</summary>
    Procesar,

    /// <summary>En entrada se acusa a KiSoft pero no se reenvía a SAP; en salida no se transmite.</summary>
    Ignorar,

    /// <summary>Estación fuera de servicio; en salida rechaza con error a SAP.</summary>
    Deshabilitado
}
