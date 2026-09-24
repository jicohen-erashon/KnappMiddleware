-- Auditoría opcional (activable por flag) de mensajes entrantes: SAP -> Middleware y KiSoft -> Middleware.
CREATE TABLE IF NOT EXISTS buzon_entrada (
    id BIGSERIAL PRIMARY KEY,
    id_correlacion UUID NOT NULL,
    tipo_telegrama VARCHAR(16) NOT NULL,
    origen VARCHAR(32) NOT NULL CONSTRAINT chk_buzon_entrada_origen CHECK (origen IN ('SAP', 'KiSoft', 'Middleware')),
    destino VARCHAR(32) NOT NULL CONSTRAINT chk_buzon_entrada_destino CHECK (destino IN ('SAP', 'KiSoft', 'Middleware')),
    estado VARCHAR(32) NOT NULL CONSTRAINT chk_buzon_entrada_estado CHECK (estado IN
        ('Recibido', 'Validado', 'Traducido', 'Gate', 'Enviado', 'Encolado', 'Emitido', 'Entregado', 'Perdido', 'Error')),
    contenido TEXT,
    error_detalle TEXT,
    duracion_ms INTEGER,
    http_status INTEGER,
    usuario VARCHAR(64),
    ruta VARCHAR(200),
    ip_origen VARCHAR(45),
    id_objeto VARCHAR(64),
    creado_por_sap VARCHAR(64),
    creado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala'),
    actualizado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala')
);

CREATE INDEX IF NOT EXISTS idx_buzon_entrada_correlation ON buzon_entrada (id_correlacion);
CREATE INDEX IF NOT EXISTS idx_buzon_entrada_created ON buzon_entrada (creado_en DESC);
