# Pendiente: 1XR / 2XR — Consulta de stock por artículo

## Estado (actualizado 2026-09-24)
**DESBLOQUEADO.** Apareció `C100-006334 COHEN Guatemala_HIS_Host Interface Specification_V3_ES.pdf`
(V3, 2026-04-17 — 125 páginas, más reciente que la V2 del 2026-02-10 que se usó hasta ahora como
fuente de verdad). **A partir de ahora, V3 es la fuente de verdad del proyecto** para cualquier
verificación de campo/ancho; V2 queda como referencia histórica.

`1XR`/`2XR` **sí es un telegrama real**, documentado completo en la V3 §3.5.2 "Solicitud de
visualización del stock de un artículo en tiempo real" (pág. 67-68 del PDF V3) — no existía en la
V2 porque es una función nueva ligada a "CR16 – 1.1 – Segundo punto" (junto con la estación `000`
"todo el almacén" que se agregó a 1RR en el mismo cambio, "CR16 – 1.1 – primer punto"). **Es un
add-on de pago, no incluido en el precio base** — confirmar con KNAPP si está contratado antes de
implementarlo, pero el layout ya no es una incógnita.

### Layout confirmado (HIS V3 §3.5.2, cita literal del PDF)

**Registro de datos [Host_TO_KiSoftOne]:**

| Longitud | Descripción | Contenido, rango |
|---|---|---|
| 03 caracteres | Identificador de registro | `1XR` |
| 02 caracteres | Longitud de número de estación | 03 |
| 02 caracteres | Longitud de mandante | 00, 16 |
| 02 caracteres | Longitud de número de artículo | 12 |
| 02 caracteres | Longitud de tipo de stock | 00, 08 |
| 03 caracteres | Número de estación | `000` (todo el almacén excl. CBS), `065` (OSR), `001`-`004` (manuales), `010`/`011` (controlados/refrigerados), `199` (full carton) |
| 00, 16 caracteres | Mandante | 0–9, A–Z, a–z, '-', '_' |
| 12 caracteres | Número de artículo | 0–9, A–Z, a–z, '-', '_' |
| 00, 08 caracteres | Tipo de stock | 0–9, A–Z, a–z |
| Bloque `C` (lote): 01 carácter identificador + 02 caracteres longitud (20) + 20 caracteres lote (TEXT) | | |

**Mensaje de estado [KiSoftOne_TO_Host]:** `2XR` (03) + Estado (02, 00-99) — `00` sin error, `11`
error de formato, `21` número de artículo desconocido, `99` con error.

Esto coincide con la muestra real de SAP (`JSON-SAP/1XR_consulta_de_stock_articulo.json`):
`station`→número de estación, `mandtk`→mandante, `productnumber`→número de artículo,
`stocktype`→tipo de stock, `batchnumber`→lote (bloque C). Listo para implementar cuando se
priorice — ver preguntas de confirmación (contratación del add-on) más abajo.

## Contenido de la muestra recibida (JSON-SAP/1XR_consulta_de_stock_articulo.json)
```json
{
 "telid":"1000000000",
 "idrecord":"1XR",
 "idrecordstatus":"2XR",
 "direction":"HK",
 "objecttype":"REQUEST",
 "objectid":"ASP500TAB001",
 "warehousenumber":"1200",
 "mandtk":"A1301",
 "station":"065",
 "productnumber":"ASP500TAB001",
 "stocktype":"STANDARD",
 "batchnumber":"LOT20260901"
}
```

Ver preguntas consolidadas (junto con los demás gaps encontrados) más abajo, en
"Preguntas a escalar a SAP/KNAPP".

## Resultado de la prueba end-to-end (2026-09-24)

Con la matriz de prueba cargada (`db/migrations/0014_matriz_prueba.sql`) y el fix de manejo de
errores aplicado (`InvalidOperationException` → 502, `ExcepcionFormatoTelegrama` → 422, antes ambos
caían en un 500 genérico), los 9 flujos llegan hasta el punto esperado: auth + matriz + mapeo TLV
funcionan; solo falla el envío TCP porque no hay servidor KiSoft real en este entorno.

Las rutas ahora incluyen versión + código de telegrama (`/api/v1/sap/<recurso>/<idrecord>`).

| Telegrama | Endpoint | Resultado | Estado | Nota |
|---|---|---|---|---|
| 14N Artículo | `/api/v1/sap/article/14N` | 502 "canal no conectado" | Esperado | — |
| 15N Socio | `/api/v1/sap/businesspartner/15N` | 502 | Esperado | — |
| 16N Ruta | `/api/v1/sap/route/16N` | 422 con el `route` real de SAP (10 chars) | Bloqueado a propósito | ver hallazgo confirmado abajo — con un `route` de 8 chars sí llega a 502 |
| 12N Orden | `/api/v1/sap/order/12N` | 502 | Esperado | — |
| 1IA Inventario (solicitud) | `/api/v1/sap/inventory/request/1IA` | 502 | Esperado | — |
| 1RR Inventario (tiempo real) | `/api/v1/sap/inventory/realtime/1RR` | 502 | Esperado | — |
| 1UU Unidad de carga (modificar) | `/api/v1/sap/loadunit/modify/1UU` | 400 (falta `station`) + 422 en `loadunit` | Gap de campo/contrato | ver tabla de anchos abajo |
| 1UN Unidad de carga (disponible) | `/api/v1/sap/loadunit/available/1UN` | 400 (falta `station`) + 422 en `loadunit` | Gap de campo/contrato | ver tabla de anchos abajo |

### Round-trip real confirmado para 14N y 16N (2026-09-24)

Se construyó `herramientas/StubKiSoft/` (TCP stub de pruebas, fuera de la app de producción) que
habla el protocolo real de trama (`<LF>+longitud(5)+datos+<CR>`) y responde acks correctos. Con el
canal 9801 apuntado a este stub (`configuracion`: `kisoft.orderChannel.host/port`, ya en
`localhost:9801`), se probó el round-trip COMPLETO (no solo la construcción de la petición):

- **Caso feliz**: `POST /api/v1/sap/article/14N` (JSON real) y `POST /api/v1/sap/route/16N` (con
  `route` de 8 chars) → ambos `200 OK` con `ok:true` — primera confirmación real de que
  `TransmisionDatosMaestros` interpreta correctamente una respuesta legítima de KiSoft (abrir+dato+
  cerrar, con validación de `RecordId` incluida).
- **Caso de rechazo**: forzando `estado="99"` en el ack del registro de datos → ambos devuelven
  `502` con el detalle de `open`/`data`/`close` (antes de esta sesión hubiera sido `200` con
  `ok:false` en el body).

## Tabla de anchos reales HIS vs. muestra SAP — para escalar a SAP/KNAPP

Fuente de los anchos HIS: **cita literal del PDF V3** (más reciente, ver "Estado" arriba —
`C100-006334 COHEN Guatemala_HIS_Host Interface Specification_V3_ES.pdf`, en la raíz del repo),
verificada independientemente de `analisis/entregables/01-schemas/LENGTHS.md`, que ya demostró tener
dos errores de transcripción (dimensiones de 14N, y `loadunit` de 12N — ver abajo). Donde hay duda,
manda el PDF, no el catálogo derivado.

| Telegrama(s) | Campo SAP | Campo HIS | Sección HIS | Ancho declarado HIS (V3) | Valor de muestra SAP | Ancho real de la muestra | Estado |
|---|---|---|---|---:|---|---:|---|
| 16N | `route` | `route` | §3.1.3.2.1 | 8 (A) | `"RUTA000001"` | 10 | 🚨 **confirmado en V2 y V3, no resuelto**: "Longitud de número de ruta teórica: 08" — sin cambios entre versiones. SAP consistentemente envía 10. **Decisión**: el código se mantiene fiel al spec (8) mientras se confirma con KNAPP — un `route` de más de 8 caracteres se rechaza con 422. |
| 1UU / 1UN | `loadunit` | código de unidad de carga de almacenamiento | §3.6.2.1 / §3.6.1.1 | 6 (A) | `"00001234"` | 8 | ❌ **confirmado en V3, celda sin marcas de cambio** — excede en 2, aún sin confirmar/ampliar. |
| 12N | `loadunit` | código de unidad de carga | §3.2.1.1 | **8 (A)** | `"00001234"` | 8 | ✅ **sin gap, `LENGTHS.md` estaba mal**: la V3 confirma ancho **8** (no 6 como decía el catálogo derivado) — el mapper de 12N ya usa 8 correctamente. Corregido este documento para no seguir citando el error. |
| 14N | `length`/`width`/`height` | dimensiones (bloque D) | §3.1.1.4.7 | 4 (N) | `120.000`/`55.000`/`25.000` | — | ✅ **verificado en V2 y V3, sin gap**: ancho 4 sin cambios entre versiones (el único cambio de §3.1.1.4.2 en V3 fue una nota de negocio, no de ancho). `LENGTHS.md` decía erróneamente "max=5". |
| 1RR | `station` | número de estación | §3.5.1.1 | 3 (N) | `"065"` | 3 | ✅ **sin gap** — V3 agregó el valor `000` ("todo el almacén") al rango permitido; el ancho de campo no cambia y el código ya acepta cualquier valor de 3 caracteres. |

**Nota interna sobre `loadunit` (no es parte de la escalación a SAP/KNAPP, es un hallazgo propio,
corregido tras verificar contra la V3):** el middleware es consistente consigo mismo en lo que
respecta al spec — 12N usa 8 porque el HIS **realmente declara 8** para ese campo (confirmado en
V3, no un error). El gap real está únicamente en 1UU/1UN, donde el HIS sí declara 6 pero SAP envía
8 — no hay ninguna ampliación previa "no aplicada de forma consistente"; son dos campos con anchos
HIS genuinamente distintos (8 en 12N, 6 en 1UU/1UN) que casualmente coinciden en la práctica con lo
que SAP envía en un caso (12N) y no en el otro (1UU/1UN).

## Preguntas a escalar a SAP/KNAPP (consolidado)

1. **`1XR`/`2XR` (consulta de stock por artículo)**: layout ya confirmado (HIS V3 §3.5.2, ver
   arriba) — es un add-on de pago ("CR16 – 1.1 – Segundo punto"), no incluido en el precio base.
   - ¿Está contratado este add-on? Si no, no tiene sentido implementarlo todavía.
   - La respuesta (`2XR`) es un simple mensaje de estado (`00`/`11`/`21`/`99`) — confirmado, no
     incluye datos de stock en el cuerpo (a diferencia de lo que se especuló antes de encontrar la
     V3).
2. **[URGENTE]** `route` (16N, HIS §3.1.3.2.1): el spec declara ancho fijo **8** caracteres
   alfanuméricos — cita literal, confirmada igual en V2 y V3, no una suposición. SAP consistentemente
   envía **10** (p. ej. `"RUTA000001"`). El middleware hoy se mantiene fiel al spec (8) y **rechaza
   con 422 cualquier `route` de SAP con más de 8 caracteres** — es decir, tal como está hoy, el 16N
   real de SAP NO puede procesarse hasta resolver esto. Dos salidas posibles: (a) SAP debe enviar un
   código de ruta de máximo 8 caracteres, o (b) KNAPP confirma que KiSoft realmente acepta 10 y el
   spec del PDF está desactualizado — en ese caso se vuelve a ampliar el ancho en el middleware (DTO
   + mapper + test, ya se hizo una vez, es reversible).
3. `loadunit` (1UU/1UN, HIS §3.6.1.1/§3.6.2.1): el spec declara ancho fijo 6 caracteres
   alfanuméricos (confirmado en V3), pero las muestras traen `"00001234"` (8). Distinto del caso de
   12N, donde el HIS sí declara 8 (no es la misma inconsistencia). ¿Se debe ampliar el ancho real de
   `loadunit` a 8 específicamente en 1UU/1UN?
4. `station` (1UU/1UN, HIS §3.6.1.1/§3.6.2.1): requerido por el spec para resolver la matriz, pero
   ausente en ambas muestras de SAP. ¿SAP no lo envía nunca para estos dos telegramas, o falta en la
   muestra por error de generación?

## Referencia (1XR)
Middleware: `Controllers/Sap/InventoryController.cs` sigue devolviendo `501 Not Implemented` para
este caso — layout ya confirmado (ver arriba), pendiente solo de decidir prioridad de implementación
y confirmar con KNAPP que el add-on esté contratado antes de construirlo.
