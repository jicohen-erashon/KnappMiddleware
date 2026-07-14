namespace KnappMiddleware.Domain.Matrix;

/// <summary>
/// Snapshot inmutable en memoria de la matriz, reemplazado atómicamente en cada <see cref="ReloadAsync"/>
/// para que las lecturas concurrentes nunca esperen ni vean un estado a medio construir. Combinaciones
/// sin entrada configurada resuelven a <see cref="MatrixAction.Deshabilitado"/> (fail-safe).
/// </summary>
public sealed class MatrixGate : IMatrixGate
{
    private const char KeySeparator = (char)1;

    private readonly IMatrixRepository _repository;
    private IReadOnlyDictionary<string, MatrixAction> _snapshot = new Dictionary<string, MatrixAction>();

    public MatrixGate(IMatrixRepository repository)
    {
        _repository = repository;
    }

    public MatrixAction Resolve(string emisor, string tipoTelegrama, string estacion)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(tipoTelegrama);
        ArgumentNullException.ThrowIfNull(estacion);

        var snapshot = Volatile.Read(ref _snapshot);
        return snapshot.TryGetValue(BuildKey(emisor, tipoTelegrama, estacion), out var accion)
            ? accion
            : MatrixAction.Deshabilitado;
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _repository.GetAllAsync(cancellationToken);

        var next = new Dictionary<string, MatrixAction>(entries.Count);
        foreach (var entry in entries)
        {
            next[BuildKey(entry.Emisor, entry.TipoTelegrama, entry.Estacion)] = entry.Accion;
        }

        Volatile.Write(ref _snapshot, next);
    }

    private static string BuildKey(string emisor, string tipoTelegrama, string estacion) =>
        string.Join(KeySeparator, emisor, tipoTelegrama, estacion).ToUpperInvariant();
}
