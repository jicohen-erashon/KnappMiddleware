using System.Text.RegularExpressions;
using KnappMiddleware.Auth;

namespace KnappMiddleware.Admin;

/// <summary>
/// Reglas de validación para el CRUD de usuarios del panel admin (mismo espíritu que
/// <see cref="AdminGuardrails"/> para la tabla configuracion). Además de validar forma, protege contra
/// el auto-lockout: con solo 2 roles fijos (<see cref="UserRole"/>) y sin tabla de permisos, deshabilitar,
/// degradar o eliminar el último SuperUsuario habilitado dejaría el panel sin nadie que pueda entrar.
/// </summary>
public static partial class UserGuardrails
{
    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9._\-]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex NombreUsuarioRegex();

    public const int MinNombreUsuarioLength = 3;
    public const int MaxNombreUsuarioLength = 64;
    public const int MinContrasenaLength = 8;
    public const int MaxContrasenaLength = 128;

    public static AdminGuardrails.ValidationOutcome ValidateNombreUsuario(string nombreUsuario)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario))
            return AdminGuardrails.ValidationOutcome.Fail("El nombre de usuario no puede estar vacío.");
        if (nombreUsuario.Length > MaxNombreUsuarioLength)
            return AdminGuardrails.ValidationOutcome.Fail($"El nombre de usuario excede el máximo de {MaxNombreUsuarioLength} caracteres.");
        if (!NombreUsuarioRegex().IsMatch(nombreUsuario))
            return AdminGuardrails.ValidationOutcome.Fail(
                $"El nombre de usuario debe iniciar con letra y tener al menos {MinNombreUsuarioLength} caracteres (letras/números/._-).");
        return AdminGuardrails.ValidationOutcome.Ok();
    }

    public static AdminGuardrails.ValidationOutcome ValidateContrasena(string contrasena)
    {
        if (string.IsNullOrEmpty(contrasena))
            return AdminGuardrails.ValidationOutcome.Fail("La contraseña no puede estar vacía.");
        if (contrasena.Length < MinContrasenaLength)
            return AdminGuardrails.ValidationOutcome.Fail($"La contraseña debe tener al menos {MinContrasenaLength} caracteres.");
        if (contrasena.Length > MaxContrasenaLength)
            return AdminGuardrails.ValidationOutcome.Fail($"La contraseña excede el máximo de {MaxContrasenaLength} caracteres.");
        return AdminGuardrails.ValidationOutcome.Ok();
    }

    public static AdminGuardrails.ValidationOutcome ValidateRol(string rol)
    {
        if (rol != nameof(UserRole.SuperUsuario) && rol != nameof(UserRole.Sap))
            return AdminGuardrails.ValidationOutcome.Fail(
                $"Rol inválido: '{rol}'. Debe ser '{nameof(UserRole.SuperUsuario)}' o '{nameof(UserRole.Sap)}'.");
        return AdminGuardrails.ValidationOutcome.Ok();
    }

    /// <summary>
    /// Bloquea la acción si dejaría el sistema sin ningún SuperUsuario habilitado. Aplica a
    /// deshabilitar, degradar a Sap, o eliminar. <paramref name="permaneceSuperUsuarioHabilitado"/> es
    /// lo que quedaría DESPUÉS de aplicar la acción sobre <paramref name="nombreUsuario"/> (true si
    /// sigue siendo SuperUsuario y habilitado, false si la acción lo saca de esa condición).
    /// </summary>
    public static AdminGuardrails.ValidationOutcome CanChangeOrDelete(
        IReadOnlyList<UserAccountSummary> actuales,
        string nombreUsuario,
        bool permaneceSuperUsuarioHabilitado)
    {
        if (permaneceSuperUsuarioHabilitado)
            return AdminGuardrails.ValidationOutcome.Ok();

        var actual = actuales.FirstOrDefault(u => u.NombreUsuario.Equals(nombreUsuario, StringComparison.OrdinalIgnoreCase));
        if (actual is null || actual.Rol != UserRole.SuperUsuario || !actual.Habilitado)
            return AdminGuardrails.ValidationOutcome.Ok(); // no era SuperUsuario habilitado: no hay riesgo de lockout

        var quedanOtros = actuales.Any(u =>
            !u.NombreUsuario.Equals(nombreUsuario, StringComparison.OrdinalIgnoreCase)
            && u.Rol == UserRole.SuperUsuario
            && u.Habilitado);

        return quedanOtros
            ? AdminGuardrails.ValidationOutcome.Ok()
            : AdminGuardrails.ValidationOutcome.Fail(
                "No se puede completar: dejaría el panel sin ningún SuperUsuario habilitado. Cree o habilite otro SuperUsuario primero.");
    }
}
