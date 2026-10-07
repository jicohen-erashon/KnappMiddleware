-- Filas de PRUEBA para poder ejercitar los 8 flujos SAP->Middleware->KiSoft implementados con los
-- JSON de muestra reales (JSON-SAP/*.json). NO son reglas de negocio reales — son solo lo necesario
-- para que ClsMatrixGate.Resolve (lookup EXACTO emisor+tipo+estación, sin comodín — ver nota aparte)
-- deje pasar cada telegrama de prueba. Reemplazar/completar antes de ir a producción.
--
-- emisor 'A1301' = mandante AJISA (el único que aparece en las muestras, mandtk="A1301").
-- estación tomada del campo real de cada JSON de muestra; '*' donde el controller la pasa fija así.
INSERT INTO matriz (emisor, tipo_telegrama, estacion, accion) VALUES
    ('A1301', '14N', '061', 'Procesar'),  -- 14N_articulo.json: station=061 (CBS001)
    ('A1301', '15N', '*',   'Procesar'),  -- 15N_socio_comercial.json: BusinessPartnerController usa '*' fijo
    ('A1301', '16N', '*',   'Procesar'),  -- 16N_ruta.json: RouteController usa '*' fijo
    ('A1301', '12N', '065', 'Procesar'),  -- 12N_orden.json: items[0].station=065
    ('A1301', '1IA', '065', 'Procesar'),  -- 1IA_solicitud_de_inventario.json: items[0].station=065
    ('*',     '1RR', '065', 'Procesar'),  -- 1RR_visualicion_inventario.json: InventoryController usa emisor '*' fijo; station=065
    ('A1301', '1UU', '065', 'Procesar'),  -- 1UU_modificar_unidad_de_carga.json: sin 'station' en la muestra (gap conocido) — placeholder, ajustar cuando se confirme
    ('A1301', '1UN', '065', 'Procesar')   -- 1UN_Unidad_de_carga_disponible.json: mismo gap que 1UU — placeholder
ON CONFLICT (emisor, tipo_telegrama, estacion) DO UPDATE SET accion = EXCLUDED.accion;
