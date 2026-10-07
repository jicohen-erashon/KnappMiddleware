-- fn_matriz_listar() nunca expuso creado_en/actualizado_en (agregadas a la tabla matriz en
-- 0011_matriz_usuarios_buzones_consistencia.sql), aunque la columna sí existe desde entonces. El
-- panel admin esperaba una fecha "Modificado" por fila que la API nunca pudo devolver.
--
-- DROP previo obligatorio: Postgres no permite cambiar el RETURNS TABLE de una función vía
-- CREATE OR REPLACE (mismo motivo que los DROP de 0013_estandarizar_campos_espanol.sql).
DROP FUNCTION IF EXISTS fn_matriz_listar();

CREATE OR REPLACE FUNCTION fn_matriz_listar()
RETURNS TABLE("Emisor" VARCHAR, "TipoTelegrama" VARCHAR, "Estacion" VARCHAR, "Accion" VARCHAR, "CreadoEn" TIMESTAMP, "ActualizadoEn" TIMESTAMP)
LANGUAGE sql
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT emisor, tipo_telegrama, estacion, accion, creado_en, actualizado_en
    FROM matriz;
$$;

GRANT EXECUTE ON FUNCTION fn_matriz_listar() TO iadministrator;
