namespace KnappMiddleware.Auth;

/// <summary>Compara una contraseña en texto plano contra un hash almacenado. La implementación concreta vive en Infrastructure.</summary>
public interface IPasswordHasher
{
    bool Verify(string password, string passwordHash);
}
