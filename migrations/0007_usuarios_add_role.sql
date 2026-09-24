-- Históricamente agregaba la columna role (drift entre entornos donde 0005_usuarios.sql se había
-- aplicado antes del commit cecef01 que introdujo autorización basada en roles). Ya no hace falta:
-- 0005_usuarios.sql crea la columna 'rol' (español) desde el inicio. Se deja como no-op guardado por
-- si algún entorno todavía tiene la tabla sin ninguna de las dos variantes.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'usuarios' AND column_name IN ('rol', 'role')
    ) THEN
        ALTER TABLE usuarios ADD COLUMN rol VARCHAR(32) NOT NULL DEFAULT 'Sap'
            CONSTRAINT chk_usuarios_rol CHECK (rol IN ('SuperUsuario', 'Sap'));
    END IF;
END $$;
