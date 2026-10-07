#!/bin/sh
# Seed opcional del stack Docker standalone: crea el primer SuperUsuario si ADMIN_USERNAME/
# ADMIN_PASSWORD vienen definidos en .env. No es una migracion de esquema (esa vive en el repo
# separado db/migrations) sino un bootstrap exclusivo de este compose, por eso vive aqui y no alla.
#
# Corre como el servicio "admin-seed" del docker-compose: contenedor de un solo uso que se conecta
# por red a "postgres" ya healthy (PGHOST/PGUSER/PGPASSWORD/PGDATABASE via entorno, estandar libpq).
# Requiere que sp_crear_usuario ya exista (migracion 0017_usuarios_gestion.sql aplicada). Idempotente:
# valida existencia antes del CALL, asi que se puede re-ejecutar en cada "up" sin duplicar el usuario.
#
# Dos gotchas de psql detras de esto: (1) la sustitucion de variables :'var' NO se interpola dentro
# de bloques DO $$ ... $$ (el cuerpo entre $$ es un literal para el cliente), por eso son dos sentencias
# top-level sueltas, sin DO; (2) esa misma sustitucion tampoco aplica con "psql -c" (el modo -c no pasa
# por el parser que la hace), asi que el SQL va por stdin (heredoc) en vez de -c.

if [ -n "${ADMIN_USERNAME:-}" ] && [ -n "${ADMIN_PASSWORD:-}" ]; then
  ADMIN_ROLE="${ADMIN_ROLE:-SuperUsuario}"

  EXISTS=$(psql -tA -v ON_ERROR_STOP=1 -v admin_username="$ADMIN_USERNAME" <<-'EOSQL'
SELECT 1 FROM usuarios WHERE nombre_usuario = :'admin_username';
EOSQL
)

  if [ -z "$EXISTS" ]; then
    psql -v ON_ERROR_STOP=1 \
      -v admin_username="$ADMIN_USERNAME" \
      -v admin_password="$ADMIN_PASSWORD" \
      -v admin_role="$ADMIN_ROLE" <<-'EOSQL'
CALL sp_crear_usuario(:'admin_username', :'admin_password', :'admin_role');
EOSQL
    echo "[seed-admin] Usuario '${ADMIN_USERNAME}' (${ADMIN_ROLE}) creado."
  else
    echo "[seed-admin] Usuario '${ADMIN_USERNAME}' ya existia, no se modifica."
  fi
else
  echo "[seed-admin] ADMIN_USERNAME/ADMIN_PASSWORD no definidos en .env: se omite la creacion del primer SuperUsuario."
fi
