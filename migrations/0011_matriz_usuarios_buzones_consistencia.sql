-- Revisión de toda la base: alinea matriz, usuarios, buzon_entrada y buzon_salida con lo que 0001-
-- 0007 pretendían pero nunca quedó aplicado tal cual en algunos entornos (drift detectado: sin CHECK
-- de dominio, sin auditoría de fechas), y con el mismo estándar ya usado en configuracion (0009/0010):
-- fechas creado_en/actualizado_en como TIMESTAMP fijado en hora Guatemala (America/Guatemala),
-- independiente de la zona horaria del servidor/sesión de Postgres. Todo idempotente y seguro tanto
-- para un entorno con drift (columnas/constraints en inglés, de una versión vieja de 0001-0007) como
-- para una instalación nueva (que ya sale de 0001-0007 con nombres en español) — cada paso se guarda
-- contra la condición exacta que necesita, no contra "si ya se corrió esta migración".

-- ============================================================
-- matriz
-- ============================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_matriz_accion') THEN
        ALTER TABLE matriz ADD CONSTRAINT chk_matriz_accion CHECK (accion IN ('Procesar', 'Ignorar', 'Deshabilitado'));
    END IF;
END $$;

ALTER TABLE matriz ADD COLUMN IF NOT EXISTS creado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala');
ALTER TABLE matriz ADD COLUMN IF NOT EXISTS actualizado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala');

-- ============================================================
-- usuarios
-- ============================================================
-- Solo aplica si la tabla todavía tiene la columna en inglés (drift); una instalación nueva ya sale de
-- 0005_usuarios.sql con 'rol' + chk_usuarios_rol y no necesita nada de esto.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'usuarios' AND column_name = 'role')
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_usuarios_role') THEN
        ALTER TABLE usuarios ADD CONSTRAINT chk_usuarios_role CHECK (role IN ('SuperUsuario', 'Sap'));
    END IF;
END $$;

ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS creado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala');
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS actualizado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala');

-- ============================================================
-- buzon_entrada / buzon_salida (misma estructura en ambas)
-- ============================================================
DO $$
DECLARE
    tabla TEXT;
BEGIN
    FOREACH tabla IN ARRAY ARRAY['buzon_entrada', 'buzon_salida']
    LOOP
        -- Renombrar created_at_utc -> creado_en (Postgres actualiza índices/dependencias solo).
        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_name = tabla AND column_name = 'created_at_utc'
        ) THEN
            EXECUTE format('ALTER TABLE %I RENAME COLUMN created_at_utc TO creado_en', tabla);
        END IF;

        -- Convertir de TIMESTAMPTZ a TIMESTAMP fijado en hora Guatemala (idempotente).
        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_name = tabla AND column_name = 'creado_en' AND data_type <> 'timestamp without time zone'
        ) THEN
            EXECUTE format(
                'ALTER TABLE %I ALTER COLUMN creado_en TYPE TIMESTAMP USING (creado_en AT TIME ZONE ''America/Guatemala'')', tabla);
            EXECUTE format(
                'ALTER TABLE %I ALTER COLUMN creado_en SET DEFAULT (now() AT TIME ZONE ''America/Guatemala'')', tabla);
        END IF;

        EXECUTE format(
            'ALTER TABLE %I ADD COLUMN IF NOT EXISTS actualizado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE ''America/Guatemala'')', tabla);

        -- CHECK de dominio para source/target/estado — solo si la tabla todavía tiene las columnas en
        -- inglés (drift); una instalación nueva ya sale de 0002/0003 con origen/destino/estado y sus
        -- chk_*_origen/chk_*_destino/chk_*_estado correspondientes.
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = tabla AND column_name = 'source')
           AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_' || tabla || '_source') THEN
            EXECUTE format(
                'ALTER TABLE %I ADD CONSTRAINT %I CHECK (source IN (''SAP'', ''KiSoft'', ''Middleware''))',
                tabla, 'chk_' || tabla || '_source');
        END IF;

        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = tabla AND column_name = 'target')
           AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_' || tabla || '_target') THEN
            EXECUTE format(
                'ALTER TABLE %I ADD CONSTRAINT %I CHECK (target IN (''SAP'', ''KiSoft'', ''Middleware''))',
                tabla, 'chk_' || tabla || '_target');
        END IF;

        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_' || tabla || '_estado') THEN
            EXECUTE format(
                'ALTER TABLE %I ADD CONSTRAINT %I CHECK (estado IN (''Recibido'', ''Validado'', ''Traducido'', ''Gate'', ''Enviado'', ''Encolado'', ''Emitido'', ''Entregado'', ''Perdido'', ''Error''))',
                tabla, 'chk_' || tabla || '_estado');
        END IF;
    END LOOP;
END $$;
