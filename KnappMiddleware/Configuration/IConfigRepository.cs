namespace KnappMiddleware.Configuration;

/// <summary>Origen de los flags/ajustes en caliente (tabla configuracion en Postgres).</summary>
public interface IConfigRepository
{
    Task<IReadOnlyList<ConfigEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Upsert de un valor. Quien llame debe recargar el <see cref="IConfigGate"/> para que el cambio sea visible.</summary>
    Task SetValueAsync(string clave, string valor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina una fila por clave. Devuelve true si la fila existía y fue eliminada, false si no
    /// existía. Requiere el SP <c>sp_eliminar_configuracion(@Clave)</c> en Postgres.
    /// </summary>
    Task<bool> DeleteValueAsync(string clave, CancellationToken cancellationToken = default);
}
