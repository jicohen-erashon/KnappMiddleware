using System.Collections.Frozen;

namespace KnappMiddleware.Telegramas.Length;

/// <summary>
/// Tabla canónica de longitudes por idrecord. Espejo 1:1 de HIS §5.4 (parámetros de longitud)
/// y §3.1.1.4.1 / §3.2.3.6 / §3.3.3.1 / §3.4.2.2 / §3.6.5.1 (matrices de uso).
///
/// Fuentes:
///  - HIS V2, capítulo 5.4 (parámetros de longitud).
///  - DS (Definiciones) y GS (General Specifications) para los nombres de campo.
///  - JSON-SAP/*_*.json para los campos reales que envía SAP (puede diferir del HIS en
///    prefijo, mayúsculas o nombre semántico: loadtype vs loadmedium, eyenumber vs
///    ejectionnumber, productdescription vs productname, grossweigth [typo]).
///
/// Cuando SAP añada un idrecord nuevo:
///  1) Crear la entrada aquí.
///  2) Si hay un nuevo campo SAP, añadirlo a <see cref="FieldSpec"/> o a un LoopObjectMap.
///  3) Si hay un campo no declarado por HIS, marcarlo con <see cref="MaxChars"/> alto
///     (>= valor típico) y un comentario — el validador lo aceptará pero marcará
///     overflow si se pasa de lo razonable.
/// </summary>
public static class HisLengthCatalog
{
    public static LengthCatalog? For(string idrecord) => idrecord switch
    {
        "12N" => Build(
            header: new[] { Field("mandtk", 16), Field("ordernumber", 12), Field("sheetnumber", 4) },
            vars: new (string, FieldSpec)[]
            {
                ("ordertype",       FieldSpec.Int(2)),
                ("loadunit",        FieldSpec.Alpha(8, note: "HIS=6; SAP=8 (loadunit_sap)")),
                ("businesspartner", FieldSpec.Alpha(12)),
                ("priority",        FieldSpec.Int(3)),
                ("loadtype",        FieldSpec.Alpha(10, note: "SAP token libre (OSR_BIN, CARTON, ...). Mapper→loadmedium.")),
            },
            loops: new (string, Dictionary<string, FieldSpec>)[]
            {
                ("items", new Dictionary<string, FieldSpec>
                {
                    ["linereference"]  = FieldSpec.Alpha(20),
                    ["station"]        = FieldSpec.Int(3),
                    ["productnumber"]  = FieldSpec.Alpha(12),
                    ["packsize"]       = FieldSpec.Int(4),
                    ["stocktype"]      = FieldSpec.Alpha(8),
                    ["batchnumber"]    = FieldSpec.Alpha(20),
                    ["expirationdate"] = FieldSpec.Int(10, note: "SAP=YYYY-MM-DD. Mapper→YYYYMMDD (8)."),
                    ["quantity"]       = FieldSpec.Int(4),
                    ["stockquality"]   = FieldSpec.Int(1),
                    ["unit"]           = FieldSpec.Alpha(4),
                    ["note"]           = FieldSpec.Text(99),
                    ["loadtype"]       = FieldSpec.Alpha(10),
                    ["loadunit"]       = FieldSpec.Alpha(8),
                }),
                ("texts", new Dictionary<string, FieldSpec>
                {
                    ["text"] = FieldSpec.Text(99),
                }),
                ("parameters", new Dictionary<string, FieldSpec>
                {
                    ["state"]    = FieldSpec.Int(4),
                    ["stateqty"] = FieldSpec.Int(2, note: "wrapper count de state, no es campo HIS directo."),
                }),
            }),

        "14N" => Build(
            header: new[] { Field("mandtk", 16), Field("productnumber", 12), Field("packsize", 4) },
            vars: new (string, FieldSpec)[]
            {
                ("station",        FieldSpec.Int(3)),
                ("rackblk",        FieldSpec.Int(3)),
                ("rackchannel",    FieldSpec.Int(3)),
                ("racklevel",      FieldSpec.Int(3)),
                ("eyenumber",      FieldSpec.Int(2, note: "HIS=ejectionnumber. SAP=eyenumber.")),
                ("sdamaxqty",      FieldSpec.Int(4)),
                ("length",         FieldSpec.Int(7, note: "SAP envía decimal \"120.000\". Mapper trunca a int mm.")),
                ("width",          FieldSpec.Int(7)),
                ("height",         FieldSpec.Int(7)),
                ("umlwh",          FieldSpec.Alpha(2)),
                ("netweight",      FieldSpec.Int(9, note: "SAP kg decimal. Mapper→1/10 g (max 6 chars).")),
                ("grossweigth",    FieldSpec.Int(9, note: "typo SAP (con H).")),
                ("umweigth",       FieldSpec.Alpha(2)),
                ("productdescription", FieldSpec.Text(40, note: "HIS=productname. SAP=productdescription.")),
                ("geocode",        FieldSpec.Alpha(12)),
                ("repmaxqty",      FieldSpec.Int(4)),
                ("repminqty",      FieldSpec.Int(4)),
                ("repgeocode",     FieldSpec.Alpha(12)),
                ("repleftstation", FieldSpec.Int(3, note: "244=CBS izq, 245=CBS der.")),
            },
            loops: new (string, Dictionary<string, FieldSpec>)[]
            {
                ("itBarcodes", new Dictionary<string, FieldSpec>
                {
                    ["productnumber"] = FieldSpec.Alpha(20),
                    ["eancode"]       = FieldSpec.Alpha(20),
                    ["unitalt"]       = FieldSpec.Alpha(4),
                }),
                ("itProperties", new Dictionary<string, FieldSpec>
                {
                    ["property"] = FieldSpec.Int(2, note: "01=lote, 02=fecha, 03=serie."),
                }),
            }),

        "15N" => Build(
            header: new[] { Field("mandtk", 16), Field("partner", 12) },
            vars: new (string, FieldSpec)[]
            {
                ("nameOrg1",         FieldSpec.Text(30)),
                ("nameOrg2",         FieldSpec.Text(30)),
                ("street",           FieldSpec.Text(30)),
                ("city1",            FieldSpec.Text(30)),
                ("city2",            FieldSpec.Text(30)),
                ("regioncode",       FieldSpec.Alpha(6)),
                ("regionname",       FieldSpec.Text(30)),
                ("postalcode",       FieldSpec.Text(6)),
                ("country",          FieldSpec.Alpha(2, note: "ISO 3166 ALPHA-2")),
                ("countryname",      FieldSpec.Text(30)),
                ("email",            FieldSpec.Text(30)),
                ("telephone",        FieldSpec.Text(30)),
                ("title",            FieldSpec.Int(4)),
                ("titledescription", FieldSpec.Text(30)),
            },
            loops: Array.Empty<(string, Dictionary<string, FieldSpec>)>()),

        "16N" => Build(
            header: new[] { Field("mandtk", 16), Field("route", 12, note: "HIS=8; SAP observa hasta 12 (RUTA000001).") },
            vars: new (string, FieldSpec)[]
            {
                ("description",      FieldSpec.Text(35)),
                ("departuretime",    FieldSpec.Int(8, note: "SAP HH:mm:ss. Mapper quita ':' → HHmmss (6).")),
                ("availabletime",    FieldSpec.Int(8, note: "idem departuretime.")),
                ("ramp",             FieldSpec.Alpha(6, note: "DIS001/DIS002.")),
                ("rampnumber",       FieldSpec.Int(2)),
                ("numberoframps",    FieldSpec.Int(2)),
            },
            loops: Array.Empty<(string, Dictionary<string, FieldSpec>)>()),

        "1IA" => Build(
            header: new[] { Field("mandtk", 16), Field("inventorynumber", 7) },
            vars: Array.Empty<(string, FieldSpec)>(),
            loops: new (string, Dictionary<string, FieldSpec>)[]
            {
                ("items", new Dictionary<string, FieldSpec>
                {
                    ["station"]       = FieldSpec.Int(3),
                    ["productnumber"] = FieldSpec.Alpha(12),
                    ["packsize"]      = FieldSpec.Int(4),
                    ["stocktype"]     = FieldSpec.Alpha(8),
                    ["batchnumber"]   = FieldSpec.Alpha(20),
                    ["loadunit"]      = FieldSpec.Alpha(8, note: "HIS=6; SAP=8."),
                    ["slotnumber"]    = FieldSpec.Int(2, note: "01..08."),
                }),
            }),

        "1RR" => Build(
            header: Array.Empty<HeaderField>(),
            vars: new (string, FieldSpec)[]
            {
                ("station",     FieldSpec.Int(3, note: "const 065.")),
                ("requesttype", FieldSpec.Int(2, note: "const 31.")),
            },
            loops: Array.Empty<(string, Dictionary<string, FieldSpec>)>()),

        "1UN" => Build(
            header: new[] { Field("mandtk", 16) },
            vars: new (string, FieldSpec)[]
            {
                ("geocode",  FieldSpec.Alpha(12)),
                ("loadunit", FieldSpec.Alpha(8, note: "HIS=6; SAP=8.")),
                ("lines",    FieldSpec.Int(2, note: "wrapper count, no es campo HIS directo.")),
            },
            loops: new (string, Dictionary<string, FieldSpec>)[]
            {
                ("items", new Dictionary<string, FieldSpec>
                {
                    ["productnumber"]  = FieldSpec.Alpha(12),
                    ["packsize"]       = FieldSpec.Int(4),
                    ["stocktype"]      = FieldSpec.Alpha(8),
                    ["batchnumber"]    = FieldSpec.Alpha(20),
                    ["expirationdate"] = FieldSpec.Int(10, note: "SAP=YYYY-MM-DD. Mapper→YYYYMMDD (8)."),
                    ["quantity"]       = FieldSpec.Int(4),
                    ["stockquality"]   = FieldSpec.Int(1),
                    ["unit"]           = FieldSpec.Alpha(4),
                }),
            }),

        "1UU" => Build(
            header: new[] { Field("mandtk", 16) },
            vars: new (string, FieldSpec)[]
            {
                ("geocode",  FieldSpec.Alpha(12)),
                ("loadunit", FieldSpec.Alpha(8)),
                ("lines",    FieldSpec.Int(2)),
            },
            loops: new (string, Dictionary<string, FieldSpec>)[]
            {
                ("items", new Dictionary<string, FieldSpec>
                {
                    ["productnumber"]  = FieldSpec.Alpha(12),
                    ["packsize"]       = FieldSpec.Int(4),
                    ["stocktype"]      = FieldSpec.Alpha(8),
                    ["batchnumber"]    = FieldSpec.Alpha(20),
                    ["expirationdate"] = FieldSpec.Int(10),
                    ["quantity"]       = FieldSpec.Int(4),
                    ["stockquality"]   = FieldSpec.Int(1),
                    ["unit"]           = FieldSpec.Alpha(4),
                }),
            }),

        _ => null
    };

    private static HeaderField Field(string sap, int max, string note = "") =>
        new(sap, max, note);

    private static LengthCatalog Build(
        IEnumerable<HeaderField> header,
        IEnumerable<(string, FieldSpec)> vars,
        IEnumerable<(string, Dictionary<string, FieldSpec>)> loops)
        => new(header.ToArray(),
               vars.ToFrozenDictionary(StringComparer.Ordinal),
               loops.ToDictionary(x => x.Item1, x => x.Item2, StringComparer.Ordinal));
}

public sealed record LengthCatalog(
    HeaderField[] FixedHeader,
    FrozenDictionary<string, FieldSpec> Variables,
    Dictionary<string, Dictionary<string, FieldSpec>> LoopObjects);

public sealed record HeaderField(string SapName, int MaxChars, string Note = "");

public sealed record FieldSpec(FieldKind Kind, int MaxChars, string Note = "")
{
    public static FieldSpec Text(int max, string note = "") => new(FieldKind.Text, max, note);
    public static FieldSpec Alpha(int max, string note = "") => new(FieldKind.Alpha, max, note);
    public static FieldSpec Int(int max, string note = "") => new(FieldKind.Int, max, note);
}

public enum FieldKind { Text, Alpha, Int }
