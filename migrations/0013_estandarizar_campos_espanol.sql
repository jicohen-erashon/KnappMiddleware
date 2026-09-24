-- Estandariza a español los nombres de columna que quedaron en inglés (usuarios, buzon_entrada,
-- buzon_salida) y los nombres de procedimiento con prefijo sp_ que también tenían el verbo en inglés
-- ("insert", "upsert"). Los alias de salida de las funciones (RETURNS TABLE) se dejan tal cual
-- (Source/Target/Payload/DurationMs/Username/...) porque son el límite de traducción hacia el C#, que
-- no cambia en esta migración — solo cambian los nombres físicos de columna y de rutina en Postgres.
-- Idempotente: se puede correr más de una vez sin error.

-- ============================================================
-- usuarios: username, password_hash, role, enabled -> español
-- ============================================================
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'usuarios' AND column_name = 'username') THEN
        ALTER TABLE usuarios RENAME COLUMN username TO nombre_usuario;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'usuarios' AND column_name = 'password_hash') THEN
        ALTER TABLE usuarios RENAME COLUMN password_hash TO hash_contrasena;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'usuarios' AND column_name = 'role') THEN
        ALTER TABLE usuarios RENAME COLUMN role TO rol;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'usuarios' AND column_name = 'enabled') THEN
        ALTER TABLE usuarios RENAME COLUMN enabled TO habilitado;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_usuarios_role') THEN
        ALTER TABLE usuarios RENAME CONSTRAINT chk_usuarios_role TO chk_usuarios_rol;
    END IF;
END $$;

CREATE OR REPLACE FUNCTION fn_usuarios_listar()
RETURNS TABLE("Username" VARCHAR, "PasswordHash" VARCHAR, "Role" VARCHAR, "Enabled" BOOLEAN)
LANGUAGE sql
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT nombre_usuario, hash_contrasena, rol, habilitado
    FROM usuarios;
$$;

-- ============================================================
-- buzon_entrada / buzon_salida: correlation_id, source, target, payload, duration_ms -> español
-- ============================================================
DO $$
DECLARE
    tabla TEXT;
BEGIN
    FOREACH tabla IN ARRAY ARRAY['buzon_entrada', 'buzon_salida']
    LOOP
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = tabla AND column_name = 'correlation_id') THEN
            EXECUTE format('ALTER TABLE %I RENAME COLUMN correlation_id TO id_correlacion', tabla);
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = tabla AND column_name = 'source') THEN
            EXECUTE format('ALTER TABLE %I RENAME COLUMN source TO origen', tabla);
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = tabla AND column_name = 'target') THEN
            EXECUTE format('ALTER TABLE %I RENAME COLUMN target TO destino', tabla);
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = tabla AND column_name = 'payload') THEN
            EXECUTE format('ALTER TABLE %I RENAME COLUMN payload TO contenido', tabla);
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = tabla AND column_name = 'duration_ms') THEN
            EXECUTE format('ALTER TABLE %I RENAME COLUMN duration_ms TO duracion_ms', tabla);
        END IF;

        IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_' || tabla || '_source') THEN
            EXECUTE format('ALTER TABLE %I RENAME CONSTRAINT %I TO %I', tabla, 'chk_' || tabla || '_source', 'chk_' || tabla || '_origen');
        END IF;
        IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_' || tabla || '_target') THEN
            EXECUTE format('ALTER TABLE %I RENAME CONSTRAINT %I TO %I', tabla, 'chk_' || tabla || '_target', 'chk_' || tabla || '_destino');
        END IF;
    END LOOP;
END $$;

-- DROP previo obligatorio: Postgres no permite renombrar parámetros de entrada vía CREATE OR REPLACE.
DROP FUNCTION IF EXISTS fn_buzon_entrada_consultar(UUID, INTEGER);
DROP FUNCTION IF EXISTS fn_buzon_salida_consultar(UUID, INTEGER);

CREATE OR REPLACE FUNCTION fn_buzon_entrada_consultar(p_id_correlacion UUID, p_cantidad INT)
RETURNS TABLE(
    "Id" BIGINT, "CorrelationId" UUID, "TipoTelegrama" VARCHAR, "Source" VARCHAR, "Target" VARCHAR,
    "Estado" VARCHAR, "Payload" TEXT, "ErrorDetalle" TEXT, "DurationMs" INTEGER, "CreadoEn" TIMESTAMP
)
LANGUAGE sql
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT id, id_correlacion, tipo_telegrama, origen, destino, estado, contenido, error_detalle, duracion_ms, creado_en
    FROM buzon_entrada
    WHERE (p_id_correlacion IS NULL OR id_correlacion = p_id_correlacion)
    ORDER BY creado_en DESC
    LIMIT p_cantidad;
$$;

CREATE OR REPLACE FUNCTION fn_buzon_salida_consultar(p_id_correlacion UUID, p_cantidad INT)
RETURNS TABLE(
    "Id" BIGINT, "CorrelationId" UUID, "TipoTelegrama" VARCHAR, "Source" VARCHAR, "Target" VARCHAR,
    "Estado" VARCHAR, "Payload" TEXT, "ErrorDetalle" TEXT, "DurationMs" INTEGER, "CreadoEn" TIMESTAMP
)
LANGUAGE sql
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT id, id_correlacion, tipo_telegrama, origen, destino, estado, contenido, error_detalle, duracion_ms, creado_en
    FROM buzon_salida
    WHERE (p_id_correlacion IS NULL OR id_correlacion = p_id_correlacion)
    ORDER BY creado_en DESC
    LIMIT p_cantidad;
$$;

-- ============================================================
-- Procedimientos sp_*: el verbo también en español ("insert" -> "insertar", "upsert" -> "guardar").
-- Se elimina el nombre viejo (evita dejar dos procedimientos duplicados) y se crea el nuevo.
-- ============================================================
-- DROP defensivo: en algún momento de esta sesión sp_insert_buzon_entrada/salida quedaron creadas como
-- FUNCTION en vez de PROCEDURE (error de una sesión anterior) — se eliminan sin importar de qué tipo
-- de rutina se trate, para no fallar por "cannot change routine kind".
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN
        SELECT p.oid, p.prokind, pg_get_function_identity_arguments(p.oid) AS args
        FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'public' AND p.proname IN ('sp_insert_buzon_entrada', 'sp_insert_buzon_salida', 'sp_upsert_configuracion')
    LOOP
        IF r.prokind = 'p' THEN
            EXECUTE format('DROP PROCEDURE %s', r.oid::regprocedure);
        ELSE
            EXECUTE format('DROP FUNCTION %s', r.oid::regprocedure);
        END IF;
    END LOOP;
END $$;

-- Nota: http_status/usuario/ruta/ip_origen/id_objeto/creado_por_sap se agregaron después (ver
-- 0015_buzon_trazabilidad.sql); esta redefinición defensiva ya usa la firma final de 14 parámetros
-- para no dejar un overload viejo.
CREATE OR REPLACE PROCEDURE sp_insertar_buzon_entrada(
    p_id_correlacion UUID,
    p_tipo_telegrama VARCHAR,
    p_origen VARCHAR,
    p_destino VARCHAR,
    p_estado VARCHAR,
    p_contenido TEXT,
    p_error_detalle TEXT,
    p_duracion_ms INTEGER,
    p_http_status INTEGER,
    p_usuario VARCHAR,
    p_ruta VARCHAR,
    p_ip_origen VARCHAR,
    p_id_objeto VARCHAR,
    p_creado_por_sap VARCHAR
)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    INSERT INTO buzon_entrada (id_correlacion, tipo_telegrama, origen, destino, estado, contenido, error_detalle, duracion_ms, http_status, usuario, ruta, ip_origen, id_objeto, creado_por_sap)
    VALUES (p_id_correlacion, p_tipo_telegrama, p_origen, p_destino, p_estado, p_contenido, p_error_detalle, p_duracion_ms, p_http_status, p_usuario, p_ruta, p_ip_origen, p_id_objeto, p_creado_por_sap);
END;
$$;

CREATE OR REPLACE PROCEDURE sp_insertar_buzon_salida(
    p_id_correlacion UUID,
    p_tipo_telegrama VARCHAR,
    p_origen VARCHAR,
    p_destino VARCHAR,
    p_estado VARCHAR,
    p_contenido TEXT,
    p_error_detalle TEXT,
    p_duracion_ms INTEGER,
    p_http_status INTEGER,
    p_usuario VARCHAR,
    p_ruta VARCHAR,
    p_ip_origen VARCHAR,
    p_id_objeto VARCHAR,
    p_creado_por_sap VARCHAR
)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    INSERT INTO buzon_salida (id_correlacion, tipo_telegrama, origen, destino, estado, contenido, error_detalle, duracion_ms, http_status, usuario, ruta, ip_origen, id_objeto, creado_por_sap)
    VALUES (p_id_correlacion, p_tipo_telegrama, p_origen, p_destino, p_estado, p_contenido, p_error_detalle, p_duracion_ms, p_http_status, p_usuario, p_ruta, p_ip_origen, p_id_objeto, p_creado_por_sap);
END;
$$;

CREATE OR REPLACE PROCEDURE sp_guardar_configuracion(p_clave VARCHAR, p_valor TEXT)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    INSERT INTO configuracion (clave, valor, actualizado_en)
    VALUES (p_clave, p_valor, (now() AT TIME ZONE 'America/Guatemala'))
    ON CONFLICT (clave) DO UPDATE SET valor = EXCLUDED.valor, actualizado_en = (now() AT TIME ZONE 'America/Guatemala');
END;
$$;
