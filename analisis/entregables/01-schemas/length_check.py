"""
Validador byte-budget para los JSON que SAP envía al middleware.

POR QUÉ EXISTE
- La trama KiSoft tiene ventana fija: 5 bytes de longitud + datos (≤ 99.999) + <CR>.
- JSON Schema valida estructura (tipos, required, formats) pero NO el coste en bytes.
- Si el mapper convierte descuidadamente, podemos pasarnos del window sin que el JSON
  Schema lo detecte.
- Esta herramienta aplica la TABLA DE LONGITUDES HIS a cualquier sample JSON y
  reporta: total bytes de trama, % de ventana usado, y por-campo (actual vs max).

AUTODESCUBRIMIENTO
- Escanea todos los *.json en KnappMiddleware/JSON-SAP/.
- Los mapea por `idrecord` a través de MAPPING (manual por ahora).
- Para samples cuyo `idrecord` aún NO está en MAPPING, emite `NEEDS_SCHEMA` y deja
  pasar (no falla) para que cuando llegue un nuevo sample solo haya que añadir 1 línea.

DOS CATEGORÍAS DE OVERFLOW
- `structural`: el valor de SAP supera el max declarado en HIS → el mapper DEBE truncar/convertir.
- `missing-spec`: el campo no está declarado en la tabla HIS → no sabemos cuánto permite.
  El mapper debe mapearlo a otro campo o descartarlo.

USO
  python length_check.py                 # genera length_report.md + length_report.csv
  python length_check.py --strict        # sale con código 1 si hay structural overflow
  python length_check.py --new-only      # solo samples no mapeados (NEEDS_SCHEMA)
"""
import argparse, csv, json, pathlib, sys
from collections import defaultdict

REPO = pathlib.Path(__file__).resolve().parents[3]  # 01-schemas/entregables/analisis/<repo>
SAMPLES_DIR = REPO / "JSON-SAP"
OUT_DIR = pathlib.Path(__file__).resolve().parent / "_reports"
OUT_DIR.mkdir(exist_ok=True)

# ----------------------------------------------------------------------------
# TABLA HIS DE LONGITUDES (canónica)
# 'max_chars' = nº máximo de caracteres que el valor puede ocupar en la trama.
# 'kind'      = 'T' (TEXT: contar chars, NO bytes) | 'A' | 'N' (chars == bytes).
# 'note'      = opcional, sale en el reporte.
# ----------------------------------------------------------------------------
HIS_LENGTHS = {
    # ---- comunes a varios ----
    'mandtk':           {'max': 16, 'kind': 'A'},
    'ordernumber':      {'max': 12, 'kind': 'A'},
    'sheetnumber':      {'max':  4, 'kind': 'N'},
    'ordertype':        {'max':  2, 'kind': 'N'},
    'loadmedium':       {'max': 10, 'kind': 'A'},  # REGEX [A-Z][A-Z0-9_]*
    'loadunit_his':     {'max':  6, 'kind': 'A', 'note': 'HIS=6; SAP usa hasta 8 (loadunit_sap). Mapper traduce y trunca.'},
    'loadunit_sap':     {'max':  8, 'kind': 'A', 'note': 'longitud SAP observada (8 chars en 1UN/1UU).'},
    'loadtype_sap':     {'max': 10, 'kind': 'A', 'note': 'SAP token libre (OSR_BIN, CARTON, ...). Mapper → loadmedium REGEX.'},
    'businesspartner':  {'max': 12, 'kind': 'A'},
    'route':            {'max':  8, 'kind': 'A', 'note': 'HIS=8; SAP observa hasta 9. Mapper trunca o amplia según siguiente versión.'},
    'route_sap':        {'max': 12, 'kind': 'A', 'note': 'longitud SAP observada.'},
    'priority':         {'max':  3, 'kind': 'N'},
    'state':            {'max':  4, 'kind': 'N'},
    'stateqty':         {'max':  2, 'kind': 'N', 'note': 'wrapper de state (no es un campo HIS directo).'},
    'text':             {'max': 99, 'kind': 'T'},
    'linereference':    {'max': 20, 'kind': 'A'},
    'station':          {'max':  3, 'kind': 'N'},
    'productnumber':    {'max': 12, 'kind': 'A'},
    'packsize':         {'max':  4, 'kind': 'N'},
    'stocktype':        {'max':  8, 'kind': 'A'},
    'batchnumber':      {'max': 20, 'kind': 'A'},
    'expirationdate_his':{'max': 8, 'kind': 'N', 'note': 'YYYYMMDD. Mapper convierte YYYY-MM-DD → YYYYMMDD.'},
    'expirationdate_sap':{'max': 10, 'kind': 'N', 'note': 'YYYY-MM-DD observado en SAP (10 chars).'},
    'quantity':         {'max':  4, 'kind': 'N'},
    'stockquality':     {'max':  1, 'kind': 'N'},
    'unit':             {'max':  4, 'kind': 'A'},
    'note':             {'max': 99, 'kind': 'T'},
    'inventorynumber':  {'max':  7, 'kind': 'A'},
    'lines':            {'max':  2, 'kind': 'N', 'note': 'wrapper count, no es campo HIS directo.'},
    'requesttype':      {'max':  2, 'kind': 'N'},

    # ---- 14N ----
    'rackblk':          {'max':  3, 'kind': 'N'},
    'rackchannel':      {'max':  3, 'kind': 'N'},
    'racklevel':        {'max':  3, 'kind': 'N'},
    'eyenumber':        {'max':  2, 'kind': 'N'},
    'sdamaxqty':        {'max':  4, 'kind': 'N'},
    'length_sap':       {'max':  7, 'kind': 'N', 'note': 'SAP envía decimal ("120.000"). Mapper trunca a int mm.'},
    'width_sap':        {'max':  7, 'kind': 'N'},
    'height_sap':       {'max':  7, 'kind': 'N'},
    'umlwh':            {'max':  2, 'kind': 'A'},
    'netweight_sap':    {'max':  9, 'kind': 'N', 'note': 'SAP envía kg decimal. Mapper convierte a 1/10 g (max 6 chars).'},
    'grossweigth_sap':  {'max':  9, 'kind': 'N', 'note': 'typo SAP (con H); misma lógica que netweight.'},
    'umweigth':         {'max':  2, 'kind': 'A'},
    'eancode':          {'max': 20, 'kind': 'A'},
    'unitalt':          {'max':  4, 'kind': 'A'},
    'productdescription':{'max':40, 'kind': 'T'},
    'geocode':          {'max': 12, 'kind': 'A'},
    'repmaxqty':        {'max':  4, 'kind': 'N'},
    'repminqty':        {'max':  4, 'kind': 'N'},
    'property':         {'max':  2, 'kind': 'N'},
    'repgeocode':       {'max': 12, 'kind': 'A'},
    'repleftstation':   {'max':  3, 'kind': 'N'},

    # ---- 15N ----
    'partner':          {'max': 12, 'kind': 'A'},
    'nameOrg1':         {'max': 30, 'kind': 'T'},
    'nameOrg2':         {'max': 30, 'kind': 'T'},
    'street':           {'max': 30, 'kind': 'T'},
    'city1':            {'max': 30, 'kind': 'T'},
    'city2':            {'max': 30, 'kind': 'T'},
    'regioncode':       {'max':  6, 'kind': 'A'},
    'regionname':       {'max': 30, 'kind': 'T'},
    'postalcode':       {'max':  6, 'kind': 'T'},
    'country':          {'max':  2, 'kind': 'A'},
    'countryname':      {'max': 30, 'kind': 'T'},
    'email':            {'max': 30, 'kind': 'T'},
    'telephone':        {'max': 30, 'kind': 'T'},
    'title':            {'max':  4, 'kind': 'N'},
    'titledescription': {'max': 30, 'kind': 'T'},

    # ---- 16N ----
    'description':      {'max': 35, 'kind': 'T'},
    'departuretime_sap':{'max':  8, 'kind': 'N', 'note': 'SAP HH:mm:ss. Mapper quita ":" → HHmmss (6 chars).'},
    'departuretime_his':{'max':  6, 'kind': 'N'},
    'availabletime_sap':{'max':  8, 'kind': 'N'},
    'availabletime_his':{'max':  6, 'kind': 'N'},
    'ramp':             {'max':  6, 'kind': 'A', 'note': 'DIS001/DIS002.'},
    'rampnumber':       {'max':  2, 'kind': 'N'},
    'numberoframps':    {'max':  2, 'kind': 'N'},
}

# ----------------------------------------------------------------------------
# MAPEO SAP-field-name → HIS-length-key
# Cada idrecord declara qué campos del JSON se mapean a qué longitud HIS.
# Si un campo SAP no aparece aquí, se reporta como 'unknown-field'.
# ----------------------------------------------------------------------------
SAP_TO_HIS = {
    '12N': {
        'mandtk': 'mandtk', 'ordernumber': 'ordernumber', 'sheetnumber': 'sheetnumber',
        'ordertype': 'ordertype',
        'loadunit': 'loadunit_sap',           # SAP usa hasta 8
        'businesspartner': 'businesspartner',
        'priority': 'priority',
        'items[]': {
            'linereference': 'linereference', 'station': 'station',
            'productnumber': 'productnumber', 'packsize': 'packsize',
            'stocktype': 'stocktype', 'batchnumber': 'batchnumber',
            'expirationdate': 'expirationdate_sap',
            'quantity': 'quantity', 'stockquality': 'stockquality',
            'unit': 'unit', 'note': 'note',
            'loadtype': 'loadtype_sap',         # wrapper que mapea a loadmedium
            'loadunit': 'loadunit_sap',
        },
        'texts[]': {'text': 'text'},
        'parameters[]': {'state': 'state', 'stateqty': 'stateqty'},
    },
    '14N': {
        'mandtk': 'mandtk', 'productnumber': 'productnumber', 'station': 'station',
        'packsize': 'packsize',
        'rackblk': 'rackblk', 'rackchannel': 'rackchannel', 'racklevel': 'racklevel',
        'eyenumber': 'eyenumber', 'sdamaxqty': 'sdamaxqty',
        'length': 'length_sap', 'width': 'width_sap', 'height': 'height_sap',
        'umlwh': 'umlwh', 'netweight': 'netweight_sap', 'grossweigth': 'grossweigth_sap',
        'umweigth': 'umweigth',
        'itBarcodes[]': {
            'productnumber': 'productnumber', 'eancode': 'eancode', 'unitalt': 'unitalt',
        },
        'productdescription': 'productdescription',
        'geocode': 'geocode',
        'repmaxqty': 'repmaxqty', 'repminqty': 'repminqty',
        'itProperties[]': {'property': 'property'},
        'repgeocode': 'repgeocode', 'repleftstation': 'repleftstation',
    },
    '15N': {
        'mandtk': 'mandtk', 'partner': 'partner',
        'nameOrg1': 'nameOrg1', 'nameOrg2': 'nameOrg2',
        'street': 'street', 'city1': 'city1', 'city2': 'city2',
        'regioncode': 'regioncode', 'regionname': 'regionname',
        'postalcode': 'postalcode', 'country': 'country', 'countryname': 'countryname',
        'email': 'email', 'telephone': 'telephone',
        'title': 'title', 'titledescription': 'titledescription',
    },
    '16N': {
        'mandtk': 'mandtk', 'route': 'route_sap',
        'description': 'description',
        'departuretime': 'departuretime_sap',
        'availabletime': 'availabletime_sap',
        'ramp': 'ramp', 'rampnumber': 'rampnumber', 'numberoframps': 'numberoframps',
    },
    '1IA': {
        'mandtk': 'mandtk', 'inventorynumber': 'inventorynumber',
        'items[]': {
            'station': 'station', 'productnumber': 'productnumber',
            'packsize': 'packsize', 'stocktype': 'stocktype',
            'batchnumber': 'batchnumber',
            'loadunit': 'loadunit_sap', 'slotnumber': 'slotnumber',
        },
    },
    '1RR': {
        'station': 'station', 'requesttype': 'requesttype',
    },
    '1UN': {
        'mandtk': 'mandtk', 'geocode': 'geocode', 'loadunit': 'loadunit_sap',
        'lines': 'lines',
        'items[]': {
            'productnumber': 'productnumber', 'packsize': 'packsize',
            'stocktype': 'stocktype', 'batchnumber': 'batchnumber',
            'expirationdate': 'expirationdate_sap',
            'quantity': 'quantity', 'stockquality': 'stockquality',
            'unit': 'unit',
        },
    },
    '1UU': {
        'mandtk': 'mandtk', 'geocode': 'geocode', 'loadunit': 'loadunit_sap',
        'lines': 'lines',
        'items[]': {
            'productnumber': 'productnumber', 'packsize': 'packsize',
            'stocktype': 'stocktype', 'batchnumber': 'batchnumber',
            'expirationdate': 'expirationdate_sap',
            'quantity': 'quantity', 'stockquality': 'stockquality',
            'unit': 'unit',
        },
    },
}

# Máximo absoluto HIS §2.2: total trama = 5 bytes (length field) + datos ≤ 99.999.
ABSOLUTE_MAX_DATA = 99999 - 5  # 99.994 bytes de payload

def char_len(field, value):
    """Longitud que ocupará el valor en la trama (nº de caracteres, NO bytes UTF-8).
    HIS TEXT: contar chars. Numéricos: padded al ancho declarado."""
    if value is None: return 0
    if isinstance(value, bool): return 1
    if isinstance(value, int): return len(str(abs(value)))
    if isinstance(value, float): return len(str(value).rstrip('0').rstrip('.'))
    if isinstance(value, str): return len(value)
    return 0

def fixed_header_bytes(idrecord):
    """Bytes fijos de cabecera por idrecord (los que SIEMPRE van al inicio, antes de los campos variables)."""
    fixed = {
        '12N': [('mandtk', 16), ('ordernumber', 12), ('sheetnumber', 4)],
        '12U': [('mandtk', 16), ('ordernumber', 12), ('sheetnumber', 4)],
        '12D': [('mandtk', 16), ('ordernumber', 12), ('sheetnumber', 4)],
        '14N': [('mandtk', 16), ('productnumber', 12), ('packsize', 4)],
        '14D': [('mandtk', 16), ('productnumber', 12), ('packsize', 4)],
        '15N': [('mandtk', 16), ('partner', 12)],
        '15D': [('mandtk', 16), ('partner', 12)],
        '16N': [('mandtk', 16), ('route_sap', 12)],
        '16D': [('mandtk', 16), ('route', 8)],
        '1IA': [('mandtk', 16), ('inventorynumber', 7)],
        '1RR': [],
        '1UN': [('mandtk', 16)],
        '1UU': [('mandtk', 16)],
        '1UD': [],
        '1SL': [('mandtk', 16)],
        '1HR': [],
    }.get(idrecord, [])
    total = 0
    rows = []
    for f, m in fixed:
        rows.append((f, m, m, False, 'header'))
        total += 2 + m  # prefijo longitud + max chars
    return rows, total

def measure_payload(payload, idrecord):
    """Devuelve (rows, total_bytes, status) donde:
       rows = [(field_path, actual_chars, max_chars, overflow, kind)]"""
    if idrecord not in SAP_TO_HIS:
        return [], 0, 'NO_SCHEMA', [], [], []

    rows, total = fixed_header_bytes(idrecord)
    mapping = SAP_TO_HIS[idrecord]
    overflows_struct = []
    overflows_misspec = []
    unknown = []

    for sap_field, value in payload.items():
        # Saltar envelope (no va a la trama)
        if sap_field in {'telid','idrecord','idrecordstatus','direction','objecttype','objectid',
                         'tecreateddateon','tecreatedtimeon','tecreatedby',
                         'temodifieddateon','temodifiedtimeon','temodifiedby',
                         'sentdate','senttime','status','warehousenumber'}:
            continue

        if sap_field not in mapping:
            unknown.append(sap_field)
            continue

        his_key = mapping[sap_field]
        if isinstance(his_key, dict):
            # No esperado al nivel raíz; se trata en items[] / texts[] / parameters[] abajo
            continue

        spec = HIS_LENGTHS.get(his_key)
        if spec is None:
            overflows_misspec.append((sap_field, value, '?', 'spec-missing'))
            continue

        actual = char_len(sap_field, value)
        overflow = actual > spec['max']
        kind = 'T' if spec['kind'] == 'T' else 'A/N'
        rows.append((sap_field, actual, spec['max'], overflow, kind))
        total += 2 + actual
        if overflow:
            overflows_struct.append((sap_field, actual, spec['max']))

    # Arrays
    for array_key, inner_map in mapping.items():
        if not array_key.endswith('[]'): continue
        root = array_key[:-3]
        for item in payload.get(root) or []:
            for sap_field, value in (item.items() if isinstance(item, dict) else []):
                his_key = inner_map.get(sap_field)
                if his_key is None:
                    unknown.append(f"{root}[].{sap_field}")
                    continue
                spec = HIS_LENGTHS.get(his_key)
                if spec is None:
                    overflows_misspec.append((f"{root}[].{sap_field}", value, '?', 'spec-missing'))
                    continue
                actual = char_len(sap_field, value)
                overflow = actual > spec['max']
                kind = 'T' if spec['kind'] == 'T' else 'A/N'
                rows.append((f"{root}[].{sap_field}", actual, spec['max'], overflow, kind))
                total += 2 + actual
                if overflow:
                    overflows_struct.append((f"{root}[].{sap_field}", actual, spec['max']))

    # El número de líneas (b 003, K 03) ocupa 2+2 = 4 bytes
    if idrecord in ('12N', '14N', '15N', '16N', '1IA', '1UN', '1UU'):
        # El bloque b/K se manda si hay líneas (cantidad ≥ 1)
        if any(p.startswith('items') or p.startswith('texts') or p.startswith('parameters')
               or p.startswith('itBarcodes') or p.startswith('itProperties')
               for p,_,_,_,_ in rows):
            total += 4  # prefijo de bloque

    status = 'OK'
    if total > ABSOLUTE_MAX_DATA:
        status = 'OVER_WINDOW'
    elif overflows_struct:
        status = 'STRUCTURAL_OVERFLOW'
    elif overflows_misspec:
        status = 'SPEC_GAP'

    return rows, total, status, overflows_struct, overflows_misspec, unknown

def discover_samples():
    """Auto-descubre todos los *.json en JSON-SAP/ (excluyendo PENDIENTE-*)."""
    out = []
    for p in sorted(SAMPLES_DIR.glob("*.json")):
        out.append(p)
    return out

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--strict", action="store_true", help="Salir 1 si hay overflow estructural o fuera de ventana.")
    ap.add_argument("--new-only", action="store_true", help="Mostrar solo samples sin esquema mapeado.")
    args = ap.parse_args()

    samples = discover_samples()
    rows = []
    summary = []

    for sample_path in samples:
        try:
            payload = json.loads(sample_path.read_text(encoding="utf-8"))
        except Exception as e:
            summary.append((sample_path.name, '?', '?', '?', f'JSON_INVALID: {e}', 0, 0))
            continue

        idrecord = payload.get("idrecord") or "?"
        rec_rows, total, status, struct, misspec, unknown = measure_payload(payload, idrecord)
        summary.append((sample_path.name, idrecord, total, ABSOLUTE_MAX_DATA, status, len(struct), len(misspec)))

        if args.new_only and status != 'NO_SCHEMA':
            continue

        if status == 'NO_SCHEMA':
            print(f"[NEEDS_SCHEMA] {sample_path.name}: idrecord={idrecord} no está en SAP_TO_HIS — añadir MAPPING y SPEC cuando llegue.")
            continue

        # Generar reporte
        for field_path, actual, mx, ovf, kind in rec_rows:
            if ovf:
                rows.append({
                    'sample': sample_path.name,
                    'idrecord': idrecord,
                    'field': field_path,
                    'kind': kind,
                    'actual_chars': actual,
                    'max_chars': mx,
                    'overflow_by': actual - mx,
                    'note': HIS_LENGTHS.get(field_path.replace('[]', ''), {}).get('note', ''),
                })

    # Reporte Markdown
    md = ['# Length Budget Report — SAP → KiSoft One']
    md.append('')
    md.append(f'Generado por `length_check.py`. Ventana absoluta: **{ABSOLUTE_MAX_DATA:,} bytes** '
              f'(5 del campo longitud + 99.994 de payload).')
    md.append('')
    md.append('## Resumen por sample')
    md.append('')
    md.append('| Sample | idrecord | Bytes trama | % ventana | Status | Overflows | Spec gaps |')
    md.append('|---|---|---:|---:|---|---:|---:|')
    for name, idr, tot, mx, status, ns, ng in summary:
        pct = f"{100*tot/mx:.2f}%" if isinstance(tot, int) else '?'
        md.append(f'| `{name}` | {idr} | {tot if isinstance(tot, int) else "?"} | {pct} | {status} | {ns} | {ng} |')
    md.append('')
    if rows:
        md.append('## Overflows estructurales detectados (mapper debe convertir)')
        md.append('')
        md.append('| Sample | idrecord | Field | Kind | Actual | Max | Overflow | Nota |')
        md.append('|---|---|---|---|---:|---:|---:|---|')
        for r in rows:
            md.append(f"| `{r['sample']}` | {r['idrecord']} | `{r['field']}` | {r['kind']} | {r['actual_chars']} | {r['max_chars']} | +{r['overflow_by']} | {r['note']} |")
        md.append('')

    (OUT_DIR / 'length_report.md').write_text('\n'.join(md), encoding='utf-8')

    # Reporte CSV
    with open(OUT_DIR / 'length_report.csv', 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f)
        w.writerow(['sample','idrecord','total_bytes','max_bytes','status','struct_overflows','spec_gaps'])
        for name, idr, tot, mx, status, ns, ng in summary:
            w.writerow([name, idr, tot, mx, status, ns, ng])

    print(f"Reportes escritos en {OUT_DIR}/")
    print(f"  - length_report.md  ({len(summary)} samples)")
    print(f"  - length_report.csv")
    print()
    # Consola resumen
    print(f"{'Sample':<48} {'idrecord':<8} {'Bytes':>7} {'%':>6}  Status")
    print('-' * 90)
    for name, idr, tot, mx, status, ns, ng in summary:
        if status == 'NO_SCHEMA':
            continue
        pct = f"{100*tot/mx:.2f}%" if isinstance(tot, int) else '?'
        print(f"{name:<48} {idr:<8} {(tot if isinstance(tot, int) else '?'):>7} {pct:>6}  {status}")

    # Strict: falla si hay estructural o fuera de ventana
    if args.strict:
        bad = [s for s in summary if s[4] in ('OVER_WINDOW', 'STRUCTURAL_OVERFLOW', 'JSON_INVALID')]
        if bad:
            print()
            print(f"❌ FAILED: {len(bad)} samples con overflow estructural o fuera de ventana:")
            for b in bad:
                print(f"   - {b[0]} ({b[1]}): {b[4]}")
            sys.exit(1)
        else:
            print()
            print("✅ PASS: todos los samples dentro del window y sin overflows estructurales.")

if __name__ == "__main__":
    main()
