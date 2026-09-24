using System.Text.Json;

namespace KnappMiddleware.Telegramas.Length;

/// <summary>
/// Validador byte-budget para los telegramas KiSoft (HIS §2.2).
///
/// OBJETIVO
///   Antes de serializar a trama, comprueba que el JSON recibido de SAP no desbordará la
///   ventana fija de la trama (5 bytes de longitud + 99.994 bytes de payload = 99.999).
///   Complementa al JSON Schema (estructura/tipos) y al ArticleTelegramMapper (mapeo
///   campo a campo). NO valida semántica de negocio — sólo cuenta chars por campo.
///
/// POR QUÉ ESTA SEPARACIÓN
///   La JSON Schema valida que SAP mande los campos correctos con tipos correctos.
///   El mapper convierte formatos SAP→HIS (YYYY-MM-DD→YYYYMMDD, "OSR_BIN"→"OSR_BIN" en
///   la posición de loadmedium, etc.). PERO: si una transformación queda mal hecha,
///   podríamos pasarnos del window sin que el JSON Schema lo detecte.
///   Este validador hace una estimación CONSERVADORA: usa los valores tal como llegan
///   del JSON (no transformados) y compara contra los max de HIS. Si pasa, hay holgura;
///   si falla, hay que revisar el mapper o renegociar HIS.
///
/// TABLA DE LONGITUDES
///   Las constantes están en <see cref="HisLengthCatalog"/>. Coinciden 1:1 con la tabla
///   del capítulo 5.4.1 del HIS (parámetros de longitud) y la matriz de uso por idrecord
///   (capítulos 3.1.1.4.1, 3.2.3.6, 3.3.3.1, 3.4.2.2, 3.6.5.1).
///
/// USO TÍPICO (en el controller MinimalAPI, justo antes del SendAsync):
///   var v = new TelegramLengthValidator();
///   var res = v.Validate("12N", payload);
///   if (!res.Ok)
///       return Results.Problem(detail: res.ToString(), statusCode: StatusCodes.Status413PayloadTooLarge);
/// </summary>
public sealed class TelegramLengthValidator
{
    /// <summary>Máximo absoluto de payload de la trama (HIS §2.2: 99.999 − 5 del campo longitud).</summary>
    public const int AbsoluteMaxPayloadBytes = 99_994;

    public LengthValidationResult Validate(string idrecord, JsonElement payload)
    {
        ArgumentException.ThrowIfNullOrEmpty(idrecord);
        if (idrecord is not { Length: 3 }) throw new ArgumentException("idrecord debe tener 3 caracteres", nameof(idrecord));

        var catalog = HisLengthCatalog.For(idrecord);
        if (catalog is null)
        {
            return new LengthValidationResult(
                Ok: false,
                IdRecord: idrecord,
                TotalBytes: 0,
                MaxBytes: AbsoluteMaxPayloadBytes,
                Status: LengthStatus.NoCatalog,
                Overflows: Array.Empty<FieldOverflow>(),
                UnknownFields: Array.Empty<string>(),
                Message: $"idrecord '{idrecord}' sin catálogo de longitudes. Añadirlo en HisLengthCatalog cuando llegue el sample SAP.");
        }

        var rows = new List<FieldRow>();
        var overflows = new List<FieldOverflow>();
        var unknown = new List<string>();
        var total = 0;

        // Cabecera fija
        foreach (var f in catalog.FixedHeader)
        {
            // Aún si el JSON no trae el valor (requerido a nivel JSON Schema), aquí sumamos
            // el worst-case para no aceptar un payload que se pase del window cuando falten
            // campos requeridos (la JSON Schema debería rechazar antes).
            var present = TryGetProperty(payload, f.SapName, out var je);
            var actual = present ? CountChars(f.SapName, je) : 0;
            var used = present ? actual : f.MaxChars;
            total += 2 + used; // 2 bytes prefijo + chars valor
            if (present)
            {
                rows.Add(new FieldRow(f.SapName, actual, f.MaxChars, actual > f.MaxChars));
                if (actual > f.MaxChars)
                    overflows.Add(new FieldOverflow(f.SapName, actual, f.MaxChars, LengthOverflowKind.Structural));
            }
        }

        // Campos variables
        foreach (var prop in payload.EnumerateObject())
        {
            if (IsEnvelopeField(prop.Name)) continue;
            if (catalog.FixedHeader.Any(f => f.SapName == prop.Name)) continue; // ya sumado
            if (catalog.Variables.TryGetValue(prop.Name, out var spec))
            {
                var actual = CountChars(prop.Name, prop.Value);
                total += 2 + actual;
                rows.Add(new FieldRow(prop.Name, actual, spec.MaxChars, actual > spec.MaxChars));
                if (actual > spec.MaxChars)
                    overflows.Add(new FieldOverflow(prop.Name, actual, spec.MaxChars, LengthOverflowKind.Structural));
            }
            else if (prop.Value.ValueKind == JsonValueKind.Object && catalog.LoopObjects.TryGetValue(prop.Name, out var innerMap))
            {
                // Loop con identificador (ej. itBarcodes) — se manejará abajo en arrays
                continue;
            }
            else
            {
                unknown.Add(prop.Name);
            }
        }

        // Arrays / Loops
        foreach (var (sapName, innerMap) in catalog.LoopObjects)
        {
            if (!TryGetProperty(payload, sapName, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                foreach (var innerProp in item.EnumerateObject())
                {
                    if (innerMap.TryGetValue(innerProp.Name, out var spec))
                    {
                        var path = $"{sapName}[].{innerProp.Name}";
                        var actual = CountChars(innerProp.Name, innerProp.Value);
                        total += 2 + actual;
                        rows.Add(new FieldRow(path, actual, spec.MaxChars, actual > spec.MaxChars));
                        if (actual > spec.MaxChars)
                            overflows.Add(new FieldOverflow(path, actual, spec.MaxChars, LengthOverflowKind.Structural));
                    }
                    else
                    {
                        unknown.Add($"{sapName}[].{innerProp.Name}");
                    }
                }
            }
        }

        var status = total > AbsoluteMaxPayloadBytes ? LengthStatus.OverWindow
                    : overflows.Count > 0 ? LengthStatus.StructuralOverflow
                    : LengthStatus.Ok;

        return new LengthValidationResult(
            Ok: status == LengthStatus.Ok,
            IdRecord: idrecord,
            TotalBytes: total,
            MaxBytes: AbsoluteMaxPayloadBytes,
            Status: status,
            Overflows: overflows.ToArray(),
            UnknownFields: unknown.ToArray(),
            Message: BuildMessage(status, total, overflows, unknown));
    }

    private static int CountChars(string fieldName, JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!.Length,   // TEXT en HIS = nº de chars, NO bytes
            JsonValueKind.Number => value.TryGetInt64(out var n)
                                    ? Math.Abs(n).ToString().Length
                                    : value.GetDouble().ToString("G15").TrimEnd('0').TrimEnd('.').TrimEnd(',').Length,
            JsonValueKind.True or JsonValueKind.False => 1,
            JsonValueKind.Null => 0,
            _ => 0
        };
    }

    private static bool TryGetProperty(JsonElement el, string name, out JsonElement found)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out found)) return true;
        found = default;
        return false;
    }

    private static bool IsEnvelopeField(string name) =>
        name is "telid" or "idrecord" or "idrecordstatus" or "direction" or "objecttype" or "objectid"
             or "tecreateddateon" or "tecreatedtimeon" or "tecreatedby"
             or "temodifieddateon" or "temodifiedtimeon" or "temodifiedby"
             or "sentdate" or "senttime" or "status" or "warehousenumber";

    private static string BuildMessage(LengthStatus s, int total, IReadOnlyList<FieldOverflow> of, IReadOnlyList<string> unk) => s switch
    {
        LengthStatus.Ok => $"OK · {total:N0} bytes ({100.0 * total / AbsoluteMaxPayloadBytes:F2}% del window)",
        LengthStatus.OverWindow => $"OVER_WINDOW · {total:N0} > {AbsoluteMaxPayloadBytes:N0}. Overflows: " + string.Join("; ", of.Select(o => $"{o.Field}+{o.Excess}")),
        LengthStatus.StructuralOverflow => $"STRUCTURAL_OVERFLOW · {of.Count} campos exceden HIS. Top: " + string.Join("; ", of.Take(3).Select(o => $"{o.Field}={o.Actual}/{o.Max}")),
        LengthStatus.NoCatalog => "NO_CATALOG · añadir HisLengthCatalog.For(this idrecord)",
        _ => "UNKNOWN"
    };
}

public enum LengthStatus { Ok, StructuralOverflow, OverWindow, NoCatalog }

public sealed record LengthValidationResult(
    bool Ok,
    string IdRecord,
    int TotalBytes,
    int MaxBytes,
    LengthStatus Status,
    IReadOnlyList<FieldOverflow> Overflows,
    IReadOnlyList<string> UnknownFields,
    string Message);

public sealed record FieldOverflow(string Field, int Actual, int Max, LengthOverflowKind Kind)
{
    public int Excess => Actual - Max;
}

public enum LengthOverflowKind { Structural }

internal sealed record FieldRow(string SapName, int Actual, int Max, bool Overflow);
