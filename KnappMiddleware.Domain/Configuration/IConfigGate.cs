namespace KnappMiddleware.Domain.Configuration;

/// <summary>Snapshot en memoria de la tabla configuracion, consultado por claves conocidas (p. ej. audit.enabled).</summary>
public interface IConfigGate
{
    /// <summary>Valor crudo asociado a la clave, o null si no existe en el snapshot actual.</summary>
    string? GetValue(string clave);

    /// <summary>Valor booleano asociado a la clave; si no existe o no parsea, retorna <paramref name="defaultValue"/>.</summary>
    bool GetBool(string clave, bool defaultValue);

    /// <summary>Recarga el snapshot en memoria desde <see cref="IConfigRepository"/>. Seguro de llamar mientras hay lecturas en curso.</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
