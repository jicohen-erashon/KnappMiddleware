-- Tabla de configuración del Gate de matriz: emisor x tipo_telegrama x estacion -> accion.
-- Aplicar una vez al levantar docker-compose.infra.yml (init de Postgres) o manualmente con psql.
CREATE TABLE IF NOT EXISTS matriz (
    emisor VARCHAR(64) NOT NULL,
    tipo_telegrama VARCHAR(16) NOT NULL,
    estacion VARCHAR(64) NOT NULL,
    accion VARCHAR(32) NOT NULL CONSTRAINT chk_matriz_accion CHECK (accion IN ('Procesar', 'Ignorar', 'Deshabilitado')),
    creado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala'),
    actualizado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala'),
    PRIMARY KEY (emisor, tipo_telegrama, estacion)
);
