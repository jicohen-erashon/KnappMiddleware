using KnappMiddleware.Domain.Auth;

namespace KnappMiddleware.Tests.Auth;

public class UserGateTests
{
    private sealed class FakeUserRepository : IUserRepository
    {
        public List<UserAccount> Accounts { get; } = [];

        public Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserAccount>>(Accounts);
    }

    // Hash sencillo (no bcrypt real) para no acoplar estos tests unitarios a una implementación concreta:
    // simula "coincide si la contraseña es igual al hash, en mayúsculas".
    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public bool Verify(string password, string passwordHash) => passwordHash == password.ToUpperInvariant();
    }

    [Fact]
    public void TryAuthenticate_WithoutReload_Fails()
    {
        var gate = new UserGate(new FakeUserRepository(), new FakePasswordHasher());

        Assert.False(gate.TryAuthenticate("sap", "secret"));
    }

    [Fact]
    public async Task TryAuthenticate_UnknownUser_Fails()
    {
        var repository = new FakeUserRepository();
        repository.Accounts.Add(new UserAccount("sap", "SECRET", Enabled: true));
        var gate = new UserGate(repository, new FakePasswordHasher());
        await gate.ReloadAsync();

        Assert.False(gate.TryAuthenticate("other", "secret"));
    }

    [Fact]
    public async Task TryAuthenticate_WrongPassword_Fails()
    {
        var repository = new FakeUserRepository();
        repository.Accounts.Add(new UserAccount("sap", "SECRET", Enabled: true));
        var gate = new UserGate(repository, new FakePasswordHasher());
        await gate.ReloadAsync();

        Assert.False(gate.TryAuthenticate("sap", "wrong"));
    }

    [Fact]
    public async Task TryAuthenticate_DisabledUser_Fails()
    {
        var repository = new FakeUserRepository();
        repository.Accounts.Add(new UserAccount("sap", "SECRET", Enabled: false));
        var gate = new UserGate(repository, new FakePasswordHasher());
        await gate.ReloadAsync();

        Assert.False(gate.TryAuthenticate("sap", "secret"));
    }

    [Fact]
    public async Task TryAuthenticate_EnabledUserWithMatchingPassword_Succeeds()
    {
        var repository = new FakeUserRepository();
        repository.Accounts.Add(new UserAccount("sap", "SECRET", Enabled: true));
        var gate = new UserGate(repository, new FakePasswordHasher());
        await gate.ReloadAsync();

        Assert.True(gate.TryAuthenticate("sap", "secret"));
    }

    [Fact]
    public async Task TryAuthenticate_UsernameIsCaseSensitive()
    {
        var repository = new FakeUserRepository();
        repository.Accounts.Add(new UserAccount("sap", "SECRET", Enabled: true));
        var gate = new UserGate(repository, new FakePasswordHasher());
        await gate.ReloadAsync();

        Assert.False(gate.TryAuthenticate("SAP", "secret"));
    }

    [Fact]
    public async Task ReloadAsync_ReplacesPreviousSnapshotEntirely()
    {
        var repository = new FakeUserRepository();
        repository.Accounts.Add(new UserAccount("sap", "SECRET", Enabled: true));
        var gate = new UserGate(repository, new FakePasswordHasher());
        await gate.ReloadAsync();

        repository.Accounts.Clear();
        await gate.ReloadAsync();

        Assert.False(gate.TryAuthenticate("sap", "secret"));
    }

    [Fact]
    public void TryAuthenticate_NullArgument_Throws()
    {
        var gate = new UserGate(new FakeUserRepository(), new FakePasswordHasher());

        Assert.Throws<ArgumentNullException>(() => gate.TryAuthenticate(null!, "secret"));
    }
}
