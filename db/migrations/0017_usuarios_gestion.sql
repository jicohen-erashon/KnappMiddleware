-- Gestión completa de la tabla usuarios desde el panel admin (alta, cambio de rol/habilitado, reset
-- de contraseña, baja). Hasta ahora solo existía fn_usuarios_listar() (0013_estandarizar_campos_espanol.sql),
-- de solo lectura y usada exclusivamente por ClsUserGate para el hot-path de autenticación (Basic +
-- Cookie). fn_usuarios_administrar_listar() es una función separada para no tocar el binding de Dapper
-- de ese hot-path: no selecciona hash_contrasena (la Api nunca expone credenciales, mismo criterio que
-- ConfigController con connection strings/URLs de webhook).
--
-- El hasheo de contraseña se hace acá con pgcrypto (crypt(p_contrasena, gen_salt('bf'))), ya instalado
-- desde 0005_usuarios.sql — BCryptPasswordHasher (C#) ya documenta que es compatible con hashes
-- generados así. Evita agregar un método Hash() nuevo en IPasswordHasher solo para esto.
--
-- SECURITY DEFINER + SET search_path = public + GRANT EXECUTE a iadministrator: mismo patrón que el
-- resto de 0012_funciones_lectura.sql / 0013_estandarizar_campos_espanol.sql (el rol de la app en
-- producción solo tiene EXECUTE sobre rutinas, nunca privilegios directos sobre las tablas).

CREATE OR REPLACE FUNCTION fn_usuarios_administrar_listar()
RETURNS TABLE("NombreUsuario" VARCHAR, "Rol" VARCHAR, "Habilitado" BOOLEAN, "CreadoEn" TIMESTAMP, "ActualizadoEn" TIMESTAMP)
LANGUAGE sql
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT nombre_usuario, rol, habilitado, creado_en, actualizado_en
    FROM usuarios;
$$;

CREATE OR REPLACE PROCEDURE sp_crear_usuario(p_nombre_usuario VARCHAR, p_contrasena VARCHAR, p_rol VARCHAR)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    INSERT INTO usuarios (nombre_usuario, hash_contrasena, rol, habilitado, creado_en, actualizado_en)
    VALUES (
        p_nombre_usuario,
        crypt(p_contrasena, gen_salt('bf')),
        p_rol,
        true,
        (now() AT TIME ZONE 'America/Guatemala'),
        (now() AT TIME ZONE 'America/Guatemala')
    );
END;
$$;

-- Rol + habilitado juntos (un solo formulario de edición en el panel, un solo guardar).
CREATE OR REPLACE PROCEDURE sp_actualizar_usuario(p_nombre_usuario VARCHAR, p_rol VARCHAR, p_habilitado BOOLEAN)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    UPDATE usuarios
    SET rol = p_rol,
        habilitado = p_habilitado,
        actualizado_en = (now() AT TIME ZONE 'America/Guatemala')
    WHERE nombre_usuario = p_nombre_usuario;
END;
$$;

CREATE OR REPLACE PROCEDURE sp_restablecer_contrasena_usuario(p_nombre_usuario VARCHAR, p_contrasena_nueva VARCHAR)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    UPDATE usuarios
    SET hash_contrasena = crypt(p_contrasena_nueva, gen_salt('bf')),
        actualizado_en = (now() AT TIME ZONE 'America/Guatemala')
    WHERE nombre_usuario = p_nombre_usuario;
END;
$$;

-- A diferencia de sp_eliminar_configuracion (procedure sin retorno, el C# hace un GET previo para
-- distinguir 404 vs 204), acá se usa una función que devuelve FOUND directamente — evita esa segunda
-- consulta y la deuda técnica de asumir éxito incondicional.
CREATE OR REPLACE FUNCTION fn_eliminar_usuario(p_nombre_usuario VARCHAR)
RETURNS BOOLEAN
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    DELETE FROM usuarios WHERE nombre_usuario = p_nombre_usuario;
    RETURN FOUND;
END;
$$;

GRANT EXECUTE ON FUNCTION fn_usuarios_administrar_listar() TO iadministrator;
GRANT EXECUTE ON PROCEDURE sp_crear_usuario(VARCHAR, VARCHAR, VARCHAR) TO iadministrator;
GRANT EXECUTE ON PROCEDURE sp_actualizar_usuario(VARCHAR, VARCHAR, BOOLEAN) TO iadministrator;
GRANT EXECUTE ON PROCEDURE sp_restablecer_contrasena_usuario(VARCHAR, VARCHAR) TO iadministrator;
GRANT EXECUTE ON FUNCTION fn_eliminar_usuario(VARCHAR) TO iadministrator;
