-- Agrega contexto de trazabilidad/debugging a buzon_entrada/buzon_salida:
-- - http_status/usuario/ruta/ip_origen: contexto HTTP (qué código se devolvió a SAP, quién autenticó
--   el request vía Basic Auth, qué endpoint exacto, desde qué IP).
-- - id_objeto/creado_por_sap: identificador de negocio (objectid) y autor SAP (tecreatedby) del
--   sobre del telegrama — más útil para debugging que el usuario fijo de Basic Auth (siempre "sap").
-- Idempotente: cubre tanto el entorno con la firma vieja de 8 parámetros (0004/0013 originales) como
-- el que ya tenía la firma intermedia de 12 (una corrida anterior de este mismo archivo); una
-- instalación nueva sale de 0004/0013 con la firma final de 14 y los DROP de abajo son no-op.

ALTER TABLE buzon_entrada ADD COLUMN IF NOT EXISTS http_status INTEGER;
ALTER TABLE buzon_entrada ADD COLUMN IF NOT EXISTS usuario VARCHAR(64);
ALTER TABLE buzon_entrada ADD COLUMN IF NOT EXISTS ruta VARCHAR(200);
ALTER TABLE buzon_entrada ADD COLUMN IF NOT EXISTS ip_origen VARCHAR(45);
ALTER TABLE buzon_entrada ADD COLUMN IF NOT EXISTS id_objeto VARCHAR(64);
ALTER TABLE buzon_entrada ADD COLUMN IF NOT EXISTS creado_por_sap VARCHAR(64);

ALTER TABLE buzon_salida ADD COLUMN IF NOT EXISTS http_status INTEGER;
ALTER TABLE buzon_salida ADD COLUMN IF NOT EXISTS usuario VARCHAR(64);
ALTER TABLE buzon_salida ADD COLUMN IF NOT EXISTS ruta VARCHAR(200);
ALTER TABLE buzon_salida ADD COLUMN IF NOT EXISTS ip_origen VARCHAR(45);
ALTER TABLE buzon_salida ADD COLUMN IF NOT EXISTS id_objeto VARCHAR(64);
ALTER TABLE buzon_salida ADD COLUMN IF NOT EXISTS creado_por_sap VARCHAR(64);

DROP PROCEDURE IF EXISTS sp_insertar_buzon_entrada(UUID, VARCHAR, VARCHAR, VARCHAR, VARCHAR, TEXT, TEXT, INTEGER);
DROP PROCEDURE IF EXISTS sp_insertar_buzon_salida(UUID, VARCHAR, VARCHAR, VARCHAR, VARCHAR, TEXT, TEXT, INTEGER);
DROP PROCEDURE IF EXISTS sp_insertar_buzon_entrada(UUID, VARCHAR, VARCHAR, VARCHAR, VARCHAR, TEXT, TEXT, INTEGER, INTEGER, VARCHAR, VARCHAR, VARCHAR);
DROP PROCEDURE IF EXISTS sp_insertar_buzon_salida(UUID, VARCHAR, VARCHAR, VARCHAR, VARCHAR, TEXT, TEXT, INTEGER, INTEGER, VARCHAR, VARCHAR, VARCHAR);

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
