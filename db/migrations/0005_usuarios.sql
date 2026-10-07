-- Cuentas de acceso para el canal HTTP entrante (Basic Auth, spec sección 10). No hay endpoint de alta:
-- igual que la matriz, operaciones administra esta tabla directamente en Postgres y luego llama a
-- POST /auth/reload para que el Gate en memoria recoja los cambios sin reiniciar la Api.
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS usuarios (
    nombre_usuario VARCHAR(64) PRIMARY KEY,
    hash_contrasena VARCHAR(256) NOT NULL,
    rol VARCHAR(32) NOT NULL CONSTRAINT chk_usuarios_rol CHECK (rol IN ('SuperUsuario', 'Sap')),
    habilitado BOOLEAN NOT NULL DEFAULT true,
    creado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala'),
    actualizado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala')
);

-- Ejemplo para dar de alta un usuario manualmente (bcrypt vía pgcrypto, compatible con BCrypt.Net-Next).
-- SuperUsuario: acceso a toda la Api. Sap: solo los endpoints SAP-facing (p. ej. /sftp-file, /sap/*).
-- INSERT INTO usuarios (nombre_usuario, hash_contrasena, rol) VALUES ('admin', crypt('changeme', gen_salt('bf')), 'SuperUsuario');
-- INSERT INTO usuarios (nombre_usuario, hash_contrasena, rol) VALUES ('sap', crypt('changeme', gen_salt('bf')), 'Sap');
