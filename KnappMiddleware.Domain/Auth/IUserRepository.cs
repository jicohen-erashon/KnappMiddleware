namespace KnappMiddleware.Domain.Auth;

/// <summary>Origen de las cuentas de acceso (tabla usuarios en Postgres). El Gate la usa solo para recargar su snapshot en memoria.</summary>
public interface IUserRepository
{
    Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default);
}
