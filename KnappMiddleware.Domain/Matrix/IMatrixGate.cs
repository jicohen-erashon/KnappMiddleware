namespace KnappMiddleware.Domain.Matrix;

/// <summary>Filtro único consultado en ambos sentidos antes de procesar cualquier telegrama.</summary>
public interface IMatrixGate
{
    /// <summary>Resuelve la acción para una combinación dada. Lectura O(1) contra el snapshot en memoria.</summary>
    MatrixAction Resolve(string emisor, string tipoTelegrama, string estacion);

    /// <summary>Recarga el snapshot en memoria desde <see cref="IMatrixRepository"/>. Seguro de llamar mientras hay lecturas en curso.</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
