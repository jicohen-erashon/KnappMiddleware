namespace KnappMiddleware.Telegramas.Mapeo;

/// <summary>
/// Mensaje de estado genérico de KiSoft: identificador de registro (3) + estado (2) (HIS §2.6 y
/// análogos por telegrama: 24N, 22N, 25N, 26N, 2IA, 2RR, 2UU, 2UN, 240/241/249, etc.).
/// </summary>
public sealed record MensajeEstadoKiSoft(string RecordId, string Estado)
{
    public bool IsOk => Estado == "00";

    /// <summary>
    /// Decodifica un mensaje de estado. Si se indica <paramref name="expectedRecordId"/>, valida que
    /// el identificador de registro recibido coincida — sin esto, una respuesta desfasada o de otro
    /// telegrama (p. ej. por una correlación posicional rota) se aceptaría en silencio como si fuera
    /// la esperada.
    /// </summary>
    public static MensajeEstadoKiSoft Parse(string data, string? expectedRecordId = null)
    {
        var reader = new LectorTelegrama(data);
        var recordId = reader.Raw(3);
        var estado = reader.Raw(2);
        reader.ExpectEnd();

        if (expectedRecordId is not null && recordId != expectedRecordId)
        {
            throw new ExcepcionFormatoTelegrama(
                $"Respuesta de KiSoft con identificador de registro inesperado: se esperaba '{expectedRecordId}', se recibió '{recordId}'.");
        }

        return new MensajeEstadoKiSoft(recordId, estado);
    }
}
