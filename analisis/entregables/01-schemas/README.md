# 01-schemas — JSON Schema formal validado contra los samples reales de SAP

Los esquemas aquí describen **el JSON exacto que SAP envía hoy** al middleware por la API REST.
Fueron extraídos campo a campo de los 9 archivos en `KnappMiddleware/JSON-SAP/` y ajustados
hasta pasar `validate.py` al 100%.

## Estructura

```
01-schemas/
├── envelope.schema.json                 ← sobre (envelope) compartido en $defs/Envelope
├── LENGTHS.md                           ← tabla canónica HIS de longitudes por campo
├── _validated/                          ← esquemas que pasan validate.py contra los samples SAP
│   ├── order-new.schema.json            ← 12N
│   ├── article-new.schema.json          ← 14N
│   ├── partner-new.schema.json          ← 15N
│   ├── route-new.schema.json            ← 16N
│   ├── inventory-request.schema.json    ← 1IA
│   ├── realtime-inventory.schema.json  ← 1RR
│   ├── loadunit-available.schema.json   ← 1UN
│   └── loadunit-modify.schema.json      ← 1UU
├── _drafts/                             ← esquemas sin sample SAP (planes a futuro)
│   ├── host-to-kisoft/  (12U, 12D, 14D, 15D, 16D, 1UD, 1SL, 1HR, master-control)
│   └── kisoft-to-host/   (32R, 3IR, 3RR, 3SC, 3UE, 3UU + líneas)
├── common/                              ← sub-esquemas reusados (ack, station, stock-type, …)
├── print/filename.schema.json           ← nombre SFTP (canal de impresión)
├── validate.py                          ← validador estructural JSON Schema
├── length_check.py                      ← validador byte-budget por sample
└── _reports/
    ├── length_report.md                 ← reporte humano del byte-budget
    └── length_report.csv                ← mismo, en CSV para CI/dashboards
```

## Cómo se usan en el middleware

```csharp
// Validación en MinimalAPI antes de traducir a trama KiSoft
public sealed class SchemaValidator
{
    private readonly Dictionary<string, JsonSchema> _cache = new();
    public ValidationResult Validate(string idrecord, JsonElement payload)
    {
        var schemaPath = idrecord switch
        {
            "12N" => "_validated/order-new.schema.json",
            "14N" => "_validated/article-new.schema.json",
            "15N" => "_validated/partner-new.schema.json",
            "16N" => "_validated/route-new.schema.json",
            "1IA" => "_validated/inventory-request.schema.json",
            "1RR" => "_validated/realtime-inventory.schema.json",
            "1UN" => "_validated/loadunit-available.schema.json",
            "1UU" => "_validated/loadunit-modify.schema.json",
            _ => null
        };
        if (schemaPath is null) return ValidationResult.UnknownIdentifier(idrecord);
        var schema = LoadAndCache(schemaPath);  // pre-load al inicio
        return schema.Evaluate(payload, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        }).IsValid
            ? ValidationResult.Ok()
            : ValidationResult.Errors(...);
    }
}
```

> En .NET 8 usar **`JsonSchema.Net`** (Newtonsoft-equivalent) o **`Manatee.Json`** porque `System.Text.Json.JsonSchemaExporter` aún no estaba en GA.

## Validación byte-budget (longitudes vs ventana de trama)

`validate.py` valida estructura/tipos. **`length_check.py`** valida **coste en bytes** —
relee el JSON-SAP/* y emite:

| Sample | idrecord | Bytes trama | % ventana | Status |
|---|---|---:|---:|---|
| `12N_orden.json` | 12N | 96 | 0.10% | OK |
| `14N_articulo.json` | 14N | 196 | 0.20% | OK |
| `15N_socio_comercial.json` | 15N | 242 | 0.24% | OK |
| `16N_ruta.json` | 16N | 121 | 0.12% | OK |
| `1IA_solicitud_de_inventario.json` | 1IA | 43 | 0.04% | OK |
| `1RR_visualicion_inventario.json` | 1RR | 9 | 0.01% | OK |
| `1UN_Unidad_de_carga_disponible.json` | 1UN | 52 | 0.05% | OK |
| `1UU_modificar_unidad_de_carga.json` | 1UU | 52 | 0.05% | OK |

Ventana absoluta: **99.994 bytes** (HIS §2.2: `99.999 − 5` del campo longitud).

```bash
python length_check.py                # genera _reports/length_report.{md,csv}
python length_check.py --strict       # CI: sale con código 1 si hay overflow estructural
python length_check.py --new-only     # solo samples cuyo idrecord aún no está mapeado
```

Implementación runtime en `02-middleware/TelegramLengthValidator.cs` (espejo 1:1 del script).

Cuando SAP envíe un sample nuevo:
1. Copiar el JSON a `KnappMiddleware/JSON-SAP/<idrecord>_*.json`.
2. `python length_check.py --new-only` → marca `NEEDS_SCHEMA`.
3. Editar `01-schemas/length_check.py`:
   - `MAPPING["<idrecord>"] = {...}` en `SAP_TO_HIS`.
   - Añadir entradas que falten en `HIS_LENGTHS`.
4. Repetir hasta que el reporte muestre `OK` o documentar el overflow real.

## Diferencias detectadas contra HIS V2 (documentadas)

| Sample SAP | HIS dice | Decisión |
|---|---|---|
| `loadunit` = `"00001234"` (8 chars) | HIS: 6 chars | Ampliado a **8** en 1UN/1UU |
| `route` = `"RUTA000001"` (9 chars) | HIS: 8 chars | Ampliado a **12** en 16N |
| `departuretime` = `"10:34:17"` (HH:mm:ss) | HIS: HHmmss (sin `:`) | Aceptado **ambos formatos** |
| `loadtype` en lugar de `loadmedium` | HIS: `loadmedium` (10 chars, REGEX) | SAP usa tokens libres (`OSR_BIN`, `CARTON`, …). Sin restricción |
| `eyenumber` en lugar de `ejectionnumber` | HIS: `ejectionnumber` | Mapeo en `ArticleTelegramMapper` |
| `productdescription` en lugar de `productname` | HIS: `productname` | Mapeo en mapper |
| `grossweigth` (typo) en lugar de `grossweight` | HIS: `grossweight` | Preservamos typo en el esquema; corregir en mapper |
| `rackblk`, `rackchannel`, `racklevel` planos | HIS: `aisle`, `rackline`, … | Mapeo en mapper |
| `itBarcodes[]`, `itProperties[]` | HIS: `productcodes[]`, `productproperties[]` | Mapeo en mapper |
| `lines: 1` (count) + `items[]` | HIS: longitud declarada al inicio de cada item | SAP envía un count + array; el mapper debe descartar `lines` |
| `parameters[]` con `{stateqty, state}` | HIS: solo `state` | Mantenido — es la versión SAP |
| `texts[]` con `{text}` | HIS: 9 longitudes separadas | Mapeo en mapper (mantener orden) |
| `1XR/2XR` no aparece | NO en HIS V2 | **No se crea esquema** — bloqueado en `JSON-SAP/PENDIENTE-1XR.md` |

## Hallazgo crítico sobre el sobre

SAP envía un objeto **plano**: el sobre (envelope) y el telegrama están **al mismo nivel JSON**, no anidados.
Por eso `envelope.schema.json` solo expone `$defs/Envelope` (sin propiedades top-level obligatorias).
Cada esquema de `_validated/*.schema.json` lo agrega via `allOf: [{$ref: envelope}, {propias}]`.

```jsonc
// Lo que SAP envía:
{
  // ----- bloque envelope (igual en TODOS los telegramas) -----
  "telid": "1000000000",          //  ← envelope
  "idrecord": "12N",              //  ← envelope (el mismo nombre que en HIS §3)
  "idrecordstatus": "22N",
  "direction": "HK",
  "objecttype": "ORDER",
  "objectid": "INB000000001",
  "tecreateddateon": "2026-09-23",
  "tecreatedtimeon": "10:32:02",
  "tecreatedby": "ERASHON",
  "temodifieddateon": "2026-09-23",
  "temodifiedtimeon": "10:32:02",
  "temodifiedby": "ERASHON",
  "sentdate": "2026-09-23",
  "senttime": "10:32:02",
  "status": "001",
  "warehousenumber": "1200",     //  ← envelope (NO en HIS, propio de SAP)
  // ----- bloque telegrama (varía por idrecord) -----
  "mandtk": "A1301",
  "ordernumber": "INB000000001",
  "sheetnumber": "0000",
  "ordertype": "04",
  "loadtype": "OSR_BIN",
  "loadunit": "00001234",
  "businesspartner": "0000100001",
  "texts": [{ "text": "..." }],
  "priority": "500",
  "parameters": [{ "stateqty": 1, "state": "0000" }],
  "items": [{ ... }]
}
```

## Uso del validador

```bash
$ python validate.py
[OK]   12N_orden.json (12N)  →  order-new.schema.json
[OK]   14N_articulo.json (14N)  →  article-new.schema.json
[OK]   15N_socio_comercial.json (15N)  →  partner-new.schema.json
[OK]   16N_ruta.json (16N)  →  route-new.schema.json
[OK]   1IA_solicitud_de_inventario.json (1IA)  →  inventory-request.schema.json
[OK]   1RR_visualicion_inventario.json (1RR)  →  realtime-inventory.schema.json
[OK]   1UN_Unidad_de_carga_disponible.json (1UN)  →  loadunit-available.schema.json
[OK]   1UU_modificar_unidad_de_carga.json (1UU)  →  loadunit-modify.schema.json

Resumen: 8 OK · 0 FAIL · 1 omitidos
Omitidos (sin esquema): 1XR_consulta_de_stock_articulo.json
  → 1XR está bloqueado: ver PENDIENTE-1XR.md en JSON-SAP/
```

Filtros útiles:

```bash
python validate.py --idrecord 12N          # solo ese
python validate.py --strict                # código de salida != 0 si hay error (CI)
python length_check.py                     # validador byte-budget (longitudes por campo)
python length_check.py --strict            # CI: exit 1 si algún sample desborda la ventana
python length_check.py --new-only          # solo samples cuyo idrecord aún no está mapeado
```

## Lo que sigue (cuando SAP envíe más telegramas)

Cada vez que SAP añada un nuevo `idrecord`:
1. Crear `KnappMiddleware/JSON-SAP/<idrecord>_*.json` con la muestra real.
2. Crear `01-schemas/_validated/<idrecord>-*.schema.json` con `allOf: [{$ref: envelope}, {...}]`.
3. Añadir la entrada al `MAPPING` de `validate.py`.
4. Ejecutar `validate.py --strict` hasta que pase.
5. Mover de `_validated` a producción (el `MinimalAPI` controller lo consume por `idrecord`).

Cuando SAP retire o cambie un sample:
- Actualizar el esquema en `_validated/` (nunca en `_drafts/`).
- Confirmar que el `TelegramMapper` correspondiente en `KnappMiddleware/Telegramas/Mapping/` siga la nueva forma.
