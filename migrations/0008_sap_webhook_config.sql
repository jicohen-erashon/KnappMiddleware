-- Configuración del cliente webhook hacia SAP (eventos de pedido que KiSoft empuja, ver
-- Events/KiSoftOrderEventDispatcher y Sap/SapWebhookClient). Vive en configuracion, no en
-- appsettings, para poder cambiarla en caliente (URL/ruta reales de SAP aún no confirmadas).
INSERT INTO configuracion (clave, valor) VALUES
    ('sap.webhook.baseUrl', ''),
    ('sap.webhook.orderEventPath', '/kisoft/order-events'),
    ('sap.webhook.timeoutSeconds', '10')
ON CONFLICT (clave) DO NOTHING;
