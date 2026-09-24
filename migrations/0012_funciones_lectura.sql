-- Todo el acceso a datos desde el proyecto debe pasar por funciones/procedimientos (igual que ya
-- hacían sp_insert_buzon_entrada/sp_insert_buzon_salida para escritura) — nada de SELECT/INSERT/UPDATE
-- con nombres de tabla o columnas embebidos en el C#. Las funciones declaran su RETURNS TABLE con los
-- nombres exactos que Dapper necesita (PascalCase, entre comillas) para que el C# invoque sin alias.
--
-- SECURITY DEFINER: en producción el rol de la aplicación solo tendrá GRANT EXECUTE sobre estas
-- funciones/procedimientos, NUNCA privilegios directos (SELECT/INSERT/UPDATE) sobre las tablas. Para
-- que eso funcione, la rutina debe correr con los privilegios de quien la creó (el owner, que sí tiene
-- acceso a las tablas), no con los del rol que la invoca — de ahí SECURITY DEFINER. Se fija
-- search_path para que una rutina SECURITY DEFINER no pueda ser engañada por un search_path hostil
-- (recomendación estándar de la documentación de Postgres para este patrón).

-- ============================================================
-- configuracion
-- ============================================================
CREATE OR REPLACE FUNCTION fn_configuracion_listar()
RETURNS TABLE("Clave" VARCHAR, "Valor" TEXT, "Descripcion" VARCHAR, "CreadoEn" TIMESTAMP, "ActualizadoEn" TIMESTAMP)
LANGUAGE sql
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT clave, valor, descripcion, creado_en, actualizado_en
    FROM configuracion;
$$;

CREATE OR REPLACE PROCEDURE sp_upsert_configuracion(p_clave VARCHAR, p_valor TEXT)
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

-- ============================================================
-- matriz
-- ============================================================
CREATE OR REPLACE FUNCTION fn_matriz_listar()
RETURNS TABLE("Emisor" VARCHAR, "TipoTelegrama" VARCHAR, "Estacion" VARCHAR, "Accion" VARCHAR)
LANGUAGE sql
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT emisor, tipo_telegrama, estacion, accion
    FROM matriz;
$$;

-- ============================================================
-- usuarios
-- ============================================================
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
-- buzon_entrada / buzon_salida (consulta con filtro opcional por id_correlacion)
-- ============================================================
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

-- Nota: sp_upsert_configuracion (creado arriba) se renombra a sp_guardar_configuracion en la
-- siguiente migración (0013_estandarizar_campos_espanol.sql), que también le agrega SECURITY
-- DEFINER — no se toca acá para no crear un nombre que la migración siguiente vuelve a eliminar.
-- sp_insertar_buzon_entrada/salida ya se crean directamente con nombre y columnas en español desde
-- 0004_stored_procedures.sql.
