namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>
/// Los payloads de SAP traen fechas/horas con separadores (a veces con guiones Unicode no-ASCII,
/// p. ej. "2028‑09‑30") donde el HIS espera el formato compacto (YYYYMMDD / HHmmss). Esta utilidad
/// se limita a extraer los dígitos, sin interpretar ni validar el calendario (cero lógica de negocio).
/// </summary>
public static class UtilTextoTelegrama
{
    public static string? DigitsOnly(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}
