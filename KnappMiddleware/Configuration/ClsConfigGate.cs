namespace KnappMiddleware.Configuration;

/// <summary>
/// Snapshot inmutable en memoria de la tabla configuracion, reemplazado atómicamente en cada
/// <see cref="ReloadAsync"/> para que las lecturas concurrentes nunca esperen ni vean un estado a medio
/// construir. Claves ausentes (tabla vacía, Postgres caído al arrancar) resuelven a null/al default del caller.
/// </summary>
public sealed class ClsConfigGate
{
    private readonly IConfigRepository _repository;
    private IReadOnlyDictionary<string, string> _snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public ClsConfigGate(IConfigRepository repository)
    {
        _repository = repository;
    }

    public string? GetValue(string clave)
    {
        ArgumentNullException.ThrowIfNull(clave);

        var snapshot = Volatile.Read(ref _snapshot);
        return snapshot.TryGetValue(clave, out var valor) ? valor : null;
    }

    public bool GetBool(string clave, bool defaultValue)
    {
        var valor = GetValue(clave);
        return valor is not null && bool.TryParse(valor, out var parsed) ? parsed : defaultValue;
    }

    public int GetInt(string clave, int defaultValue)
    {
        var valor = GetValue(clave);
        return valor is not null && int.TryParse(valor, out var parsed) ? parsed : defaultValue;
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _repository.GetAllAsync(cancellationToken);

        var next = new Dictionary<string, string>(entries.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            next[entry.Clave] = entry.Valor;
        }

        Volatile.Write(ref _snapshot, next);
    }
}
