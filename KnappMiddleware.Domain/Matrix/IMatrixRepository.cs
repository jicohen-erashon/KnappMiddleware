namespace KnappMiddleware.Domain.Matrix;

/// <summary>Origen de la configuración de la matriz (tabla Matriz en Postgres). El Gate la usa solo para recargar su snapshot en memoria.</summary>
public interface IMatrixRepository
{
    Task<IReadOnlyList<MatrixEntry>> GetAllAsync(CancellationToken cancellationToken = default);
}
