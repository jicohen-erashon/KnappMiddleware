# Tabla canónica HIS de longitudes — para el validador byte-budget

Esta tabla es **la fuente de verdad** que el `TelegramLengthValidator` (en `02-middleware/`)
aplica contra cualquier JSON recibido del API REST de SAP, antes de serializarlo a la
trama TCP/IP de KiSoft.

> El JSON Schema valida **estructura**. Esta tabla valida **coste en bytes**. Ambas son necesarias.

## Convenciones

- **`max_chars`** = nº máximo de **caracteres** que el valor puede ocupar en la trama (no bytes UTF-8).
  Para campos `TEXT` (HIS §5.4) es nº de chars; para numéricos y alfanuméricos, chars == bytes.
- **Prefijo de longitud** = **siempre 2 bytes** (HIS §2.3: campo + valor; longitud va primero).
  Excepción: HIS §2.2 dice longitud de **5 bytes** para el campo `<LF>+NNNNN`.
- **Ventana absoluta**: `99.999` bytes totales (5 del campo longitud + 99.994 de payload).
  Trama mínima útil = 6 bytes (`00006` + al menos 1 byte de datos).
- **TEXT fields**: contar chars, NO bytes UTF-8 (HIS §2.3 nota). Si el SAP envía
  `"ASPIRINA 500 MG TABLETA"` (24 chars) y el HIS permite 40, la trama ocupará
  prefijo 2 + valor 40 (rellenado con espacios) = 42 bytes; no se cuentan los
  bytes UTF-8 (que podrían ser más por acentos / emojis).

## Tabla por idrecord

Cada fila: `(sap_field_name, his_field_name, max_chars, kind, nota_mapeo)`.

### `12N` — Nuevo pedido (Host → KiSoft)

| SAP field | HIS field | max | kind | Nota |
|---|---|---:|---|---|
| `mandtk` | `mandtk` | 16 | A | común a todos |
| `ordernumber` | `ordernumber` | 12 | A | |
| `sheetnumber` | `sheetnumber` | 4 | N | 0000=entrega salida, 0001=transporte |
| `ordertype` | `ordertype` | 2 | N | 01..36 (ver §3.2.3.3) |
| `loadunit` | `loadunit` | 8 | A | HIS=6; SAP observa hasta 8 |
| `businesspartner` | `businesspartner` | 12 | A | |
| `priority` | `priority` | 3 | N | 000..999 |
| `loadtype` | `loadmedium` | 10 | A | SAP token libre (`OSR_BIN`, `CARTON`...). Mapper traduce a REGEX `[A-Z][A-Z0-9_]*` |
| `items[].linereference` | `linereference` | 20 | A | |
| `items[].station` | `station` | 3 | N | 065/017/041 etc. |
| `items[].productnumber` | `productnumber` | 12 | A | |
| `items[].packsize` | `packsize` | 4 | N | 0001..9999 |
| `items[].stocktype` | `stocktype` | 8 | A | STANDARD/B6/QQ/2F/1E/1X/1Z |
| `items[].batchnumber` | `lot` | 20 | A | |
| `items[].expirationdate` | `date` | **8** | N | **HIS=YYYYMMDD; SAP=YYYY-MM-DD (10 chars). Mapper debe quitar guiones** |
| `items[].quantity` | `quantity` | 4 | N | 0001..9999 |
| `items[].stockquality` | `stockquality` | 1 | N | 1/2 |
| `items[].unit` | `unit` | 4 | A | EA/CS/PAL |
| `items[].note` | `note` | 99 | T | |
| `items[].loadtype` | — | 10 | A | wrapper; el mapper descarta si no se usa |
| `items[].loadunit` | — | 8 | A | wrapper (a nivel de línea puede sobrescribir) |
| `texts[].text` | `text` | 99 | T | hasta 9 líneas |
| `parameters[].state` | `state` | 4 | N | 0001/0012/9005/9006/9007 |
| `parameters[].stateqty` | — | 2 | N | wrapper count |

### `14N` — Maestro de artículo (Host → KiSoft)

| SAP field | HIS field | max | kind | Nota |
|---|---|---:|---|---|
| `mandtk` | `mandtk` | 16 | A | |
| `productnumber` | `productnumber` | 12 | A | |
| `packsize` | `packsize` | 4 | N | |
| `station` | `station` | 3 | N | 001..199 |
| `rackblk` | `rackblk` | 3 | N | HIS=aisle |
| `rackchannel` | `rackchannel` | 3 | N | HIS=rackline |
| `racklevel` | `racklevel` | 3 | N | |
| `eyenumber` | `ejectionnumber` | 2 | N | **HIS=ejectionnumber; SAP=eyenumber.** Mapper renombra |
| `sdamaxqty` | `maxquantity` | 4 | N | |
| `length`/`width`/`height` | `dimensions.*` | 5 | N | **HIS=mm entero (0001..9999); SAP=decimal `"120.000"` (7 chars). Mapper trunca** |
| `umlwh` | — | 2 | A | MM/CM/M (mapper aplica) |
| `netweight` | `weight` | 6 | N | **HIS=1/10 g; SAP=kg decimal. Mapper convierte** |
| `grossweigth` | `weight` | 6 | N | typo SAP (con H); misma lógica |
| `umweigth` | — | 2 | A | KG/G/LB/OZ |
| `itBarcodes[].productnumber` | `productcode` | 20 | A | |
| `itBarcodes[].eancode` | `productcode` | 20 | A | |
| `itBarcodes[].unitalt` | — | 4 | A | wrapper |
| `productdescription` | `productname` | 40 | T | **HIS=productname; SAP=productdescription** |
| `geocode` | `geocode` | 12 | A | |
| `repmaxqty`/`repminqty` | `stocklimits.*` | 4 | N | |
| `itProperties[].property` | `productproperty` | 2 | N | 01=lote, 02=fecha, 03=serie |
| `repgeocode` | `geocode` (replenishment) | 12 | A | |
| `repleftstation` | `station` (replenishment) | 3 | N | 244/245 |

### `15N` — Socio comercial

| SAP field | HIS field | max | kind | Nota |
|---|---|---:|---|---|
| `mandtk` | `mandtk` | 16 | A | |
| `partner` | `partnernumber` | 12 | A | |
| `nameOrg1` | `company` | 30 | T | |
| `nameOrg2` | `company` | 30 | T | SAP envía sucursal en línea 2 |
| `street` | `street` | 30 | T | |
| `city1` | `city` | 30 | T | |
| `city2` | `region` | 30 | T | SAP provincia/departamento |
| `regioncode` | — | 6 | A | wrapper |
| `regionname` | `region` | 30 | T | |
| `postalcode` | `zip` | 6 | T | HIS lo marca TEXT (puede tener guiones) |
| `country` | `countrycode` | 2 | A | ISO 3166 ALPHA-2 |
| `countryname` | — | 30 | T | wrapper |
| `email` | `email` | 30 | T | |
| `telephone` | `telephone` | 30 | T | |
| `title` | — | 4 | N | wrapper tratamiento |
| `titledescription` | — | 30 | T | wrapper |

### `16N` — Ruta teórica

| SAP field | HIS field | max | kind | Nota |
|---|---|---:|---|---|
| `mandtk` | `mandtk` | 16 | A | |
| `route` | `route` | 12 | A | **HIS=8; SAP=RUTA000001 (9). Mapper debe truncar o ampliar** |
| `description` | — | 35 | T | |
| `departuretime` | `departuretime` | 6 | N | **HIS=HHmmss; SAP=HH:mm:ss (8 chars). Mapper quita `:`** |
| `availabletime` | `availabletime` | 6 | N | idem |
| `ramp` | `ramp` | 6 | A | DIS001/DIS002 |
| `rampnumber` | — | 2 | N | wrapper |
| `numberoframps` | — | 2 | N | wrapper |

### `1IA` — Solicitud de inventario

| SAP field | HIS field | max | kind | Nota |
|---|---|---:|---|---|
| `mandtk` | `mandtk` | 16 | A | |
| `inventorynumber` | — | 7 | A | wrapper |
| `items[].station` | `station` | 3 | N | |
| `items[].productnumber` | `productnumber` | 12 | A | |
| `items[].packsize` | `packsize` | 4 | N | |
| `items[].stocktype` | `stocktype` | 8 | A | |
| `items[].batchnumber` | `lot` | 20 | A | |
| `items[].loadunit` | `loadunit` | 8 | A | HIS=6; SAP=8 |
| `items[].slotnumber` | `slot` | 2 | N | 01..08 |

### `1RR` — Visualización inventario en tiempo real

| SAP field | HIS field | max | kind | Nota |
|---|---|---:|---|---|
| `station` | `station` | 3 | N | const 065 |
| `requesttype` | `requesttype` | 2 | N | const 31 |

### `1UN` / `1UU` — Unidad de carga disponible / modificar

| SAP field | HIS field | max | kind | Nota |
|---|---|---:|---|---|
| `mandtk` | `mandtk` | 16 | A | |
| `geocode` | `geocode` | 12 | A | |
| `loadunit` | `loadunit` | 8 | A | HIS=6; SAP=8 |
| `lines` | — | 2 | N | wrapper count |
| `items[].productnumber` | `productnumber` | 12 | A | |
| `items[].packsize` | `packsize` | 4 | N | |
| `items[].stocktype` | `stocktype` | 8 | A | |
| `items[].batchnumber` | `lot` | 20 | A | |
| `items[].expirationdate` | `date` | 8 | N | HIS=YYYYMMDD; SAP=YYYY-MM-DD |
| `items[].quantity` | `quantity` | 4 | N | |
| `items[].stockquality` | `stockquality` | 1 | N | |
| `items[].unit` | `unit` | 4 | A | |

### Cómo añadir un idrecord nuevo

```csharp
// en HisLengthCatalog.For(...)
"1XX" => Build(
    header: new[] { Field("mandtk", 16), Field("...campo fijo...", max) },
    vars:   new (string, FieldSpec)[] { ("campo_opcional", FieldSpec.Text(35)) },
    loops:  new (string, Dictionary<string, FieldSpec>)[]
            { ("items", new Dictionary<string, FieldSpec> { ["x"] = FieldSpec.Int(4) }) }),
```

Si SAP envía un campo no declarado en HIS:
1. **No** añadir max arbitrario — eso oculta el overflow.
2. Mejor: añadirlo al mapper como **campo descartado** (no va a la trama).
3. Si el campo debe viajar a KiSoft, abrir issue con KNAPP para confirmar max en próxima revisión del HIS.
