using KnappMiddleware.Domain.Auth;

namespace KnappMiddleware.Infrastructure.Auth;

/// <summary>
/// Verifica contraseñas contra hashes bcrypt. Compatible con hashes generados fuera de la aplicación
/// vía pgcrypto (crypt(password, gen_salt('bf'))), lo que permite a operaciones dar de alta usuarios
/// insertando directamente en la tabla usuarios sin pasar por la API.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
