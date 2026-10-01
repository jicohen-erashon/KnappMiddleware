using System.Text.RegularExpressions;

namespace KnappMiddleware.Admin;

/// <summary>
/// Reglas de validación para los endpoints CRUD de la tabla configuracion.
/// Bloquea claves con formato inválido, valores que exceden límites, valores que rompen
/// un patrón tipado (bool / int / uri) y protege del borrado a claves críticas que el
/// código lee de forma tipada al arrancar.
/// </summary>
public static partial class AdminGuardrails
{
    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9._\-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex ClaveRegex();

    private static readonly HashSet<string> ClavesReservadasSoloLectura = new(StringComparer.OrdinalIgnoreCase)
    {
        "audit.queueCapacity",
    };

    private static readonly HashSet<string> ClavesNoBorrables = new(StringComparer.OrdinalIgnoreCase)
    {
        "audit.enabled",
    };

    private static readonly (string Prefix, ValueKind Kind, int MaxLen)[] PatronesTipados =
    {
        ("audit.enabled",       ValueKind.Bool,  5),
        ("audit.queueCapacity", ValueKind.Int,  10),
        ("kiSoft.",             ValueKind.Uri, 512),
        ("sap.webhook.baseUrl", ValueKind.Uri, 512),
        ("sftp.",               ValueKind.Uri, 512),
        ("rabbitmq.",           ValueKind.Uri, 512),
    };

    public const int MaxValueLength = 8192;
    public const int MaxClaveLength = 64;

    public static ValidationOutcome ValidateClave(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
            return ValidationOutcome.Fail("La clave no puede estar vacía.");
        if (clave.Length > MaxClaveLength)
            return ValidationOutcome.Fail($"La clave '{clave}' excede el máximo de {MaxClaveLength} caracteres.");
        if (!ClaveRegex().IsMatch(clave))
            return ValidationOutcome.Fail(
                $"La clave '{clave}' no cumple el patrón: debe iniciar con letra, luego letras/números/._-.");
        return ValidationOutcome.Ok();
    }

    public static ValidationOutcome ValidateValor(string clave, string valor)
    {
        if (valor is null)
            return ValidationOutcome.Fail("El valor no puede ser null. Use cadena vacía si quiere borrar el contenido.");
        if (valor.Length > MaxValueLength)
            return ValidationOutcome.Fail($"El valor excede el máximo de {MaxValueLength:N0} caracteres.");

        foreach (var (prefix, kind, maxLen) in PatronesTipados)
        {
            if (clave.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && valor.Length > maxLen)
                return ValidationOutcome.Fail($"El valor para '{clave}' excede el máximo permitido ({maxLen} caracteres).");

            if (clave.Equals(prefix, StringComparison.OrdinalIgnoreCase) && kind != ValueKind.Any)
                if (!TryValidateKind(valor, kind, out var kindErr))
                    return ValidationOutcome.Fail($"El valor '{valor}' para '{clave}' no es un {kind}: {kindErr}");
        }
        return ValidationOutcome.Ok();
    }

    public static bool CanDelete(string clave) =>
        !ClavesReservadasSoloLectura.Contains(clave) && !ClavesNoBorrables.Contains(clave);

    private static bool TryValidateKind(string valor, ValueKind kind, out string? error)
    {
        switch (kind)
        {
            case ValueKind.Bool:
                if (bool.TryParse(valor, out _)) { error = null; return true; }
                error = "se esperaba 'true' o 'false'."; return false;
            case ValueKind.Int:
                if (int.TryParse(valor, out _)) { error = null; return true; }
                error = "se esperaba un entero."; return false;
            case ValueKind.Uri:
                if (Uri.TryCreate(valor, UriKind.Absolute, out _)) { error = null; return true; }
                if (Uri.TryCreate(valor, UriKind.Relative, out _)) { error = null; return true; }
                error = "se esperaba una URL absoluta o relativa válida."; return false;
            default: error = null; return true;
        }
    }

    public readonly record struct ValidationOutcome(bool IsValid, string? Error)
    {
        public static ValidationOutcome Ok() => new(true, null);
        public static ValidationOutcome Fail(string error) => new(false, error);
    }

    private enum ValueKind { Bool, Int, Uri, Any }
}
