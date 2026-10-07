-- Borrado de una clave de configuracion desde el panel admin (ConfigurationCrudController). A
-- diferencia de fn_eliminar_usuario (0017_usuarios_gestion.sql), este es un procedure sin retorno:
-- el C# hace un GET previo contra la clave para distinguir 404 (no existia) de 204 (borrada), en vez
-- de depender de FOUND. DROP previo obligatorio porque antes de esta migracion no existia ninguna
-- rutina con este nombre y firma (VARCHAR) — mismo motivo de los DROP en 0013/0019.
DROP PROCEDURE IF EXISTS sp_eliminar_configuracion(text);

CREATE OR REPLACE PROCEDURE sp_eliminar_configuracion(p_clave VARCHAR)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    DELETE FROM configuracion WHERE clave = p_clave;
END;
$$;

GRANT EXECUTE ON PROCEDURE sp_eliminar_configuracion(VARCHAR) TO iadministrator;
