namespace KnappMiddleware.Domain.Configuration;

/// <summary>Origen de los flags/ajustes en caliente (tabla configuracion en Postgres).</summary>
public interface IConfigRepository
{
    Task<IReadOnlyList<ConfigEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Upsert de un valor. Quien llame debe recargar el <see cref="IConfigGate"/> para que el cambio sea visible.</summary>
    Task SetValueAsync(string clave, string valor, CancellationToken cancellationToken = default);
}
