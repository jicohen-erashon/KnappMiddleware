-- Traslada a configuracion todo lo que antes vivía en appsettings.json salvo Postgres:ConnectionString
-- (y el logging de arranque, Loki/FileLogging, que se queda en appsettings por la razón de "huevo y
-- gallina": si Postgres está caído al arrancar, no hay logs para diagnosticarlo). Valores por defecto
-- = los que ya estaban en appsettings.json/appsettings.Development.json, para no cambiar el
-- comportamiento en el primer arranque tras aplicar esta migración.
INSERT INTO configuracion (clave, valor) VALUES
    -- RabbitMq
    ('rabbitmq.hostName', 'localhost'),
    ('rabbitmq.port', '5672'),
    ('rabbitmq.userName', 'iadministrator'),
    ('rabbitmq.password', 'CAMBIAR_EN_SERVIDOR'),
    ('rabbitmq.virtualHost', '/'),
    ('rabbitmq.inboundExchange', 'kisoft.inbound'),
    ('rabbitmq.inboundQueue', 'kisoft.inbound.queue'),
    ('rabbitmq.outboundExchange', 'kisoft.outbound'),
    ('rabbitmq.outboundRoutingKey', 'kisoft.outbound'),

    -- SFTP inventario (recogida de InventorySnapshot tras 3RR)
    ('sftp.inventory.host', 'localhost'),
    ('sftp.inventory.port', '22'),
    ('sftp.inventory.username', 'customer_osr'),
    ('sftp.inventory.password', 'CAMBIAR_EN_SERVIDOR'),
    ('sftp.inventory.inboundDirectory', '/inbound'),
    ('sftp.inventory.outboundDirectory', '/outbound'),

    -- SFTP impresión (albarán/etiqueta)
    ('sftp.print.host', 'localhost'),
    ('sftp.print.port', '22'),
    ('sftp.print.username', 'sftpuser'),
    ('sftp.print.password', 'CAMBIAR_EN_SERVIDOR'),
    ('sftp.print.outboundDirectory', '/print'),

    -- Canal 9802 (KiSoft -> Host, eventos)
    ('kisoft.eventChannel.host', 'localhost'),
    ('kisoft.eventChannel.port', '9802'),
    ('kisoft.eventChannel.connectTimeoutSeconds', '10'),
    ('kisoft.eventChannel.responseTimeoutSeconds', '20'),
    ('kisoft.eventChannel.heartbeatIdleSeconds', '60'),
    ('kisoft.eventChannel.heartbeatTimeoutSeconds', '120'),
    ('kisoft.eventChannel.reconnectDelaySeconds', '5'),

    -- Canal 9801 (Host -> KiSoft, comandos síncronos)
    ('kisoft.orderChannel.host', 'localhost'),
    ('kisoft.orderChannel.port', '9801'),
    ('kisoft.orderChannel.connectTimeoutSeconds', '10'),
    ('kisoft.orderChannel.responseTimeoutSeconds', '20'),
    ('kisoft.orderChannel.heartbeatIdleSeconds', '60'),
    ('kisoft.orderChannel.heartbeatTimeoutSeconds', '120'),
    ('kisoft.orderChannel.reconnectDelaySeconds', '5')
ON CONFLICT (clave) DO NOTHING;
