-- Fila de PRUEBA para poder ejercitar el 1XR (consulta de stock, HIS V3 §3.5.2) recién implementado
-- con la muestra real de SAP (JSON-SAP/1XR_consulta_de_stock_articulo.json). NO es una regla de
-- negocio real — ver nota general en 0014_matriz_prueba.sql.
--
-- emisor 'A1301' = mandante AJISA (mandtk="A1301" en la muestra); estación 065 (station en la muestra).
INSERT INTO matriz (emisor, tipo_telegrama, estacion, accion) VALUES
    ('A1301', '1XR', '065', 'Procesar')
ON CONFLICT (emisor, tipo_telegrama, estacion) DO UPDATE SET accion = EXCLUDED.accion;
