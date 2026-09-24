namespace KnappMiddleware.Auth;

/// <summary>
/// Snapshot inmutable en memoria de las cuentas de acceso, reemplazado atómicamente en cada
/// <see cref="ReloadAsync"/> para que las lecturas concurrentes nunca esperen ni vean un estado a medio
/// construir. Usuarios inexistentes, deshabilitados o sin snapshot cargado (p. ej. Postgres caído al
/// arrancar) fallan la autenticación (fail-safe).
/// </summary>
public sealed class ClsUserGate
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _hasher;
    private IReadOnlyDictionary<string, UserAccount> _snapshot = new Dictionary<string, UserAccount>(StringComparer.Ordinal);

    public ClsUserGate(IUserRepository repository, IPasswordHasher hasher)
    {
        _repository = repository;
        _hasher = hasher;
    }

    public bool TryAuthenticate(string username, string password, out UserRole role)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot.TryGetValue(username, out var account)
            && account.Enabled
            && _hasher.Verify(password, account.PasswordHash))
        {
            role = account.Role;
            return true;
        }

        role = default;
        return false;
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _repository.GetAllAsync(cancellationToken);

        var next = new Dictionary<string, UserAccount>(accounts.Count, StringComparer.Ordinal);
        foreach (var account in accounts)
        {
            next[account.Username] = account;
        }

        Volatile.Write(ref _snapshot, next);
    }
}
