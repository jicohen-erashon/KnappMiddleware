#!/usr/bin/env python3
import sys, os
os.environ.setdefault("PYTHONIOENCODING", "utf-8")
try:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except Exception:
    pass
"""
Valida cada sample JSON-SAP/* contra su esquema JSON Schema oficial en _validated/.

Salida:
  OK          → valida sin errores
  FAIL (n)    → lista los errores (instancePath + message)

Uso:
  python validate.py                          # todos los samples
  python validate.py --idrecord 12N           # solo el de 12N
  python validate.py --strict                # falla con código 1 si hay algún error
"""
import argparse, json, pathlib, sys
from jsonschema import Draft202012Validator

REPO = pathlib.Path(__file__).resolve().parents[3]  # 01-schemas/entregables/analisis/<repo>
SAMPLES = REPO / "JSON-SAP"
SCHEMAS = pathlib.Path(__file__).resolve().parent / "_validated"
ENVELOPE = pathlib.Path(__file__).resolve().parent / "envelope.schema.json"
print(f"Buscando samples en: {SAMPLES}")
SCHEMAS = pathlib.Path(__file__).resolve().parent / "_validated"
ENVELOPE = pathlib.Path(__file__).resolve().parent / "envelope.schema.json"

# Mapeo nombre-archivo-sample -> esquema
MAPPING = {
    "12N_orden.json":                  "order-new.schema.json",
    "14N_articulo.json":               "article-new.schema.json",
    "15N_socio_comercial.json":        "partner-new.schema.json",
    "16N_ruta.json":                   "route-new.schema.json",
    "1IA_solicitud_de_inventario.json": "inventory-request.schema.json",
    "1RR_visualicion_inventario.json":  "realtime-inventory.schema.json",
    "1UN_Unidad_de_carga_disponible.json": "loadunit-available.schema.json",
    "1UU_modificar_unidad_de_carga.json":  "loadunit-modify.schema.json",
    # 1XR_consulta_de_stock_articulo.json NO tiene schema — bloqueado por PENDIENTE-1XR.md
}

def load_validator(schema_path):
    # Inyectar el envelope como archivo resolvable de $ref
    base_uri = schema_path.resolve().as_uri()
    with open(schema_path, "r", encoding="utf-8") as f:
        schema = json.load(f)
    # Stub para el ref externo al envelope
    store = {base_uri: schema}
    # Cargar envelope por separado y exponerlo en el store usando el path relativo
    if base_uri.endswith("/_validated/"):
        envelope_uri = base_uri.replace("/_validated/", "/")
        # el archivo envelope.schema.json vive un nivel arriba
        with open(ENVELOPE, "r", encoding="utf-8") as f:
            env = json.load(f)
        # Necesitamos resolver "envelope.schema.json#/$defs/Envelope" relativo a la base
        envelope_path_uri = base_uri.replace(str(SCHEMAS.as_posix()).replace("/", "/"), "").rsplit("/", 1)[0] + "/envelope.schema.json"
    return schema, store

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--idrecord", help="Filtrar por idrecord (12N, 14N, ...)")
    ap.add_argument("--strict", action="store_true", help="Salir con código 1 si hay errores")
    args = ap.parse_args()

    samples = sorted(SAMPLES.glob("*.json"))
    total_ok = 0
    total_fail = 0
    skipped = []

    for sample in samples:
        if sample.name not in MAPPING:
            skipped.append(sample.name)
            continue
        schema_name = MAPPING[sample.name]
        schema_path = SCHEMAS / schema_name
        if not schema_path.exists():
            print(f"[SKIP] {sample.name} → schema {schema_name} no existe")
            continue

        with open(sample, "r", encoding="utf-8") as f:
            instance = json.load(f)
        with open(schema_path, "r", encoding="utf-8") as f:
            schema = json.load(f)

        # Filtrar por idrecord si se pasó
        if args.idrecord and instance.get("idrecord") != args.idrecord:
            continue

        # Resolver el $ref de envelope cargando manualmente el esquema completo
        # La forma rápida: aplanar allOf reemplazando el $ref por la definición real
        envelope_def = json.loads(json.dumps(_load_definition(ENVELOPE, "$defs", "Envelope")))
        schema_resolved = _resolve_allof(schema, envelope_def)

        v = Draft202012Validator(schema_resolved)
        errors = sorted(v.iter_errors(instance), key=lambda e: list(e.absolute_path))

        if not errors:
            print(f"[OK]   {sample.name} ({instance.get('idrecord')})  →  {schema_name}")
            total_ok += 1
        else:
            print(f"[FAIL] {sample.name} ({instance.get('idrecord')})  →  {schema_name}")
            for e in errors:
                path = "/".join(str(p) for p in e.absolute_path) or "<root>"
                print(f"        · {path}: {e.message}")
            total_fail += 1

    print()
    print(f"Resumen: {total_ok} OK · {total_fail} FAIL · {len(skipped)} omitidos")
    if skipped:
        print(f"Omitidos (sin esquema): {', '.join(skipped)}")
        print("  → 1XR está bloqueado: ver PENDIENTE-1XR.md en JSON-SAP/")

    if args.strict and total_fail:
        sys.exit(1)

def _load_definition(schema_path, *keys):
    with open(schema_path, "r", encoding="utf-8") as f:
        node = json.load(f)
    for k in keys:
        node = node[k]
    return node

def _resolve_allof(schema, envelope_def):
    """Reemplaza el $ref 'envelope.schema.json#/$defs/Envelope' por la definición concreta,
    manteniendo el resto del esquema intacto. Soporta allOf anidado."""
    if isinstance(schema, dict):
        if "$ref" in schema and "envelope.schema.json" in schema["$ref"]:
            return envelope_def
        if "allOf" in schema:
            merged = {"type": "object", "properties": {}, "required": []}
            for sub in schema["allOf"]:
                r = _resolve_allof(sub, envelope_def)
                if r.get("type") == "object":
                    merged["properties"].update(r.get("properties", {}))
                    merged["required"] = list(set(merged["required"]) | set(r.get("required", [])))
            # Mantener claves top-level distintas de allOf/$ref (p. ej. $schema, $id)
            for k, v in schema.items():
                if k not in ("allOf", "$ref"):
                    merged[k] = v
            return merged
        return {k: _resolve_allof(v, envelope_def) for k, v in schema.items()}
    if isinstance(schema, list):
        return [_resolve_allof(x, envelope_def) for x in schema]
    return schema

if __name__ == "__main__":
    main()
