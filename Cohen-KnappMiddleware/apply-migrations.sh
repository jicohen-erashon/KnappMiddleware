#!/bin/sh
# Aplica, en orden y UNA SOLA VEZ cada una, las migraciones SQL montadas en /migrations (repo
# separado db/migrations, ver README "Base de datos" — nunca viven en este repo). Corre como el
# servicio "migrate" del docker-compose, contra "postgres" ya healthy, antes de admin-seed y del
# middleware (ver depends_on de ambos).
#
# Lleva registro en schema_migrations (nombre de archivo = clave): cada migracion corre EXACTAMENTE
# una vez, nunca se reaplica. Esto es obligatorio, no cosmetico: migraciones como
# 0019_fn_matriz_listar_fechas.sql hacen DROP FUNCTION antes de recrear con una firma distinta
# (Postgres exige DROP para cambiar el RETURNS TABLE de una funcion via CREATE OR REPLACE).
# Reaplicar TODO el historial desde cero contra una base que ya paso por 0019 rompe en 0012 (crea
# la firma vieja) porque esa firma ya no coincide con la que quedo instalada — schema_migrations
# evita el choque corriendo cada archivo una sola vez, igual que cualquier runner de migraciones
# real (Flyway, EF Core, etc.), y sigue cubriendo tanto el primer despliegue (tabla vacia, se
# aplican todas) como un redeploy (solo se aplican los .sql nuevos desde el ultimo deploy).
#
# No usar "set -e": si el glob no encuentra nada, "for f in /migrations/*.sql" deja $f con el
# patron sin expandir (sh/dash sin nullglob) — se detecta explicitamente en vez de fallar con un
# error de archivo no encontrado.
#
# La sustitucion de variables :'var' de psql NO aplica con "psql -c" (solo via stdin/heredoc) —
# por eso las consultas parametrizadas van en heredoc, no en -c.

psql -v ON_ERROR_STOP=1 -c "CREATE TABLE IF NOT EXISTS schema_migrations (nombre_archivo TEXT PRIMARY KEY, aplicado_en TIMESTAMPTZ NOT NULL DEFAULT now());" >/dev/null

set -- /migrations/*.sql
if [ ! -e "$1" ]; then
  echo "[migrate] /migrations esta vacio (MIGRATIONS_PATH sin definir o sin .sql): no hay nada que aplicar."
  exit 0
fi

applied=0
skipped=0
for f in "$@"; do
  name=$(basename "$f")

  already=$(psql -tA -v ON_ERROR_STOP=1 -v name="$name" <<-'EOSQL'
SELECT 1 FROM schema_migrations WHERE nombre_archivo = :'name';
EOSQL
)
  if [ -n "$already" ]; then
    skipped=$((skipped + 1))
    continue
  fi

  echo "[migrate] Aplicando $name"
  if ! psql -v ON_ERROR_STOP=1 -f "$f"; then
    echo "[migrate] FALLÓ $name — se detiene aqui, no se marca aplicada ni se sigue con el resto." >&2
    exit 1
  fi

  psql -v ON_ERROR_STOP=1 -v name="$name" <<-'EOSQL' >/dev/null
INSERT INTO schema_migrations (nombre_archivo) VALUES (:'name');
EOSQL
  applied=$((applied + 1))
done

echo "[migrate] $applied migracion(es) nueva(s) aplicada(s), $skipped ya estaban al dia."
