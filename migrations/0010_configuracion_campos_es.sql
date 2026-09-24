-- Ajustes al modelo de la tabla configuracion (a pedido): nombres de campos de fecha en español,
-- campo de creación (antes solo existía la fecha de actualización), campo de descripción, con
-- backfill de ambos para las filas ya existentes, y las fechas SIEMPRE en horario Guatemala
-- (America/Guatemala, UTC-6 fijo, sin horario de verano) independientemente de la zona horaria
-- configurada en el servidor/sesión de Postgres: se guardan como TIMESTAMP (sin tz) ya convertidas
-- explícitamente, en vez de depender de la config `timezone` de la sesión.

-- 1) Renombrar el campo de fecha a español (idempotente: solo si la columna vieja todavía existe).
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'configuracion' AND column_name = 'updated_at_utc'
    ) THEN
        ALTER TABLE configuracion RENAME COLUMN updated_at_utc TO actualizado_en;
    END IF;
END $$;

-- 2) Convertir actualizado_en de TIMESTAMPTZ a TIMESTAMP fijado en hora Guatemala (idempotente: solo
-- si todavía no se hizo la conversión).
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'configuracion' AND column_name = 'actualizado_en' AND data_type <> 'timestamp without time zone'
    ) THEN
        ALTER TABLE configuracion
            ALTER COLUMN actualizado_en TYPE TIMESTAMP USING (actualizado_en AT TIME ZONE 'America/Guatemala');
        ALTER TABLE configuracion
            ALTER COLUMN actualizado_en SET DEFAULT (now() AT TIME ZONE 'America/Guatemala');
    END IF;
END $$;

-- 3) Nuevo campo: fecha de creación, ya en hora Guatemala (naive TIMESTAMP).
ALTER TABLE configuracion ADD COLUMN IF NOT EXISTS creado_en TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'America/Guatemala');

-- 4) Nuevo campo: descripción.
ALTER TABLE configuracion ADD COLUMN IF NOT EXISTS descripcion VARCHAR(255);

-- 5) Backfill de creado_en para filas preexistentes: no hay fecha de creación real registrada, se
-- aproxima con la fecha de actualización (mejor dato disponible; ambas ya en hora Guatemala tras el
-- paso 2). Ejecutar una sola vez.
UPDATE configuracion SET creado_en = actualizado_en;

-- 6) Backfill de descripcion para las claves ya conocidas.
UPDATE configuracion SET descripcion = CASE clave
    WHEN 'audit.enabled' THEN 'Habilita/deshabilita la auditoría de telegramas (BuzonEntrada/BuzonSalida). Verificación en vivo en cada escritura.'
    WHEN 'audit.queueCapacity' THEN 'Tamaño del canal en memoria de auditoría. Solo toma efecto al reiniciar la Api.'

    WHEN 'sap.webhook.baseUrl' THEN 'URL base del endpoint de SAP que recibe los eventos de pedido (32R) por webhook. Vacío = el evento se descarta con warning.'
    WHEN 'sap.webhook.orderEventPath' THEN 'Ruta relativa (bajo la URL base) para el POST de eventos de pedido.'
    WHEN 'sap.webhook.timeoutSeconds' THEN 'Timeout del POST a SAP; si vence, el evento se descarta sin reintento.'

    WHEN 'rabbitmq.hostName' THEN 'Host del servidor RabbitMQ.'
    WHEN 'rabbitmq.port' THEN 'Puerto AMQP del servidor RabbitMQ.'
    WHEN 'rabbitmq.userName' THEN 'Usuario de conexión a RabbitMQ.'
    WHEN 'rabbitmq.password' THEN 'Contraseña de conexión a RabbitMQ.'
    WHEN 'rabbitmq.virtualHost' THEN 'Virtual host de RabbitMQ.'
    WHEN 'rabbitmq.inboundExchange' THEN 'Exchange de entrada (mensajes hacia el middleware).'
    WHEN 'rabbitmq.inboundQueue' THEN 'Cola de entrada consumida por el middleware.'
    WHEN 'rabbitmq.outboundExchange' THEN 'Exchange de salida (mensajes publicados por el middleware).'
    WHEN 'rabbitmq.outboundRoutingKey' THEN 'Routing key usada al publicar en el exchange de salida.'

    WHEN 'sftp.inventory.host' THEN 'Host SFTP para recoger el InventorySnapshot (aviso 3RR).'
    WHEN 'sftp.inventory.port' THEN 'Puerto SFTP del canal de inventario.'
    WHEN 'sftp.inventory.username' THEN 'Usuario SFTP del canal de inventario (típico "customer_osr").'
    WHEN 'sftp.inventory.password' THEN 'Contraseña SFTP del canal de inventario.'
    WHEN 'sftp.inventory.inboundDirectory' THEN 'Directorio remoto de entrada del canal SFTP de inventario.'
    WHEN 'sftp.inventory.outboundDirectory' THEN 'Directorio remoto de salida del canal SFTP de inventario.'

    WHEN 'sftp.print.host' THEN 'Host SFTP para el push de datos de impresión (albarán/etiqueta).'
    WHEN 'sftp.print.port' THEN 'Puerto SFTP del canal de impresión.'
    WHEN 'sftp.print.username' THEN 'Usuario SFTP del canal de impresión (típico "sftpuser").'
    WHEN 'sftp.print.password' THEN 'Contraseña SFTP del canal de impresión.'
    WHEN 'sftp.print.outboundDirectory' THEN 'Directorio remoto de salida del canal SFTP de impresión.'

    WHEN 'kisoft.eventChannel.host' THEN 'Host del canal 9802 (KiSoft -> Host, eventos empujados por KiSoft).'
    WHEN 'kisoft.eventChannel.port' THEN 'Puerto del canal 9802.'
    WHEN 'kisoft.eventChannel.connectTimeoutSeconds' THEN 'Timeout de conexión TCP del canal 9802.'
    WHEN 'kisoft.eventChannel.responseTimeoutSeconds' THEN 'Ventana de espera de respuesta (heartbeat) del canal 9802.'
    WHEN 'kisoft.eventChannel.heartbeatIdleSeconds' THEN 'Segundos de silencio antes de emitir heartbeat en el canal 9802.'
    WHEN 'kisoft.eventChannel.heartbeatTimeoutSeconds' THEN 'Doble timeout sin heartbeat ni tráfico antes de reconectar el canal 9802.'
    WHEN 'kisoft.eventChannel.reconnectDelaySeconds' THEN 'Espera entre reintentos de conexión del canal 9802.'

    WHEN 'kisoft.orderChannel.host' THEN 'Host del canal 9801 (Host -> KiSoft, comandos síncronos: pedidos, datos maestros).'
    WHEN 'kisoft.orderChannel.port' THEN 'Puerto del canal 9801.'
    WHEN 'kisoft.orderChannel.connectTimeoutSeconds' THEN 'Timeout de conexión TCP del canal 9801.'
    WHEN 'kisoft.orderChannel.responseTimeoutSeconds' THEN 'Ventana de espera de respuesta de KiSoft en el canal 9801 (10-30s por diseño).'
    WHEN 'kisoft.orderChannel.heartbeatIdleSeconds' THEN 'Segundos de silencio antes de emitir heartbeat en el canal 9801.'
    WHEN 'kisoft.orderChannel.heartbeatTimeoutSeconds' THEN 'Doble timeout sin heartbeat ni tráfico antes de reconectar el canal 9801.'
    WHEN 'kisoft.orderChannel.reconnectDelaySeconds' THEN 'Espera entre reintentos de conexión del canal 9801.'

    ELSE descripcion
END;
