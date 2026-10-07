-- Tabla genérica de configuración/flags en caliente (p. ej. audit.enabled, audit.queueCapacity), única
-- fuente de verdad para estos valores: no viven en appsettings. Hot-reloaded en memoria vía ConfigGate,
-- igual que la matriz; si una clave no existe acá, cae al default fail-safe hardcodeado en el código
-- (ver ClsAuditToggle/ClsAuditWriter). audit.queueCapacity solo toma efecto al reiniciar la Api (el
-- canal en memoria tiene tamaño fijo al crearse); audit.enabled sí es una verificación en vivo en cada
-- escritura. Fechas siempre en hora Guatemala (America/Guatemala), fijadas explícitamente en la
-- consulta/escritura — no dependen de la zona horaria del servidor/sesión de Postgres.
CREATE TABLE IF NOT EXISTS configuracion (
    clave VARCHAR(100) PRIMARY KEY,
    valor VARCHAR(500) NOT NULL,
    descripcion VARCHAR(255),
    creado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala'),
    actualizado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala')
);

INSERT INTO configuracion (clave, valor, descripcion) VALUES
    ('audit.enabled', 'false', 'Habilita/deshabilita la auditoría de telegramas (BuzonEntrada/BuzonSalida). Verificación en vivo en cada escritura.'),
    ('audit.queueCapacity', '10000', 'Tamaño del canal en memoria de auditoría. Solo toma efecto al reiniciar la Api.')
ON CONFLICT (clave) DO NOTHING;
