# Entregables — Análisis Cohen ↔ KNAPP (C100-006334)

Tres paquetes de archivos físicos para que el equipo de desarrollo los integre al `KnappMiddleware/.NET 8 Worker Service` que ya existe en este repositorio.

## Restricciones del proyecto (del CONTEXT.md y del código)

- **Cero lógica de negocio**: el middleware solo traduce, correlaciona y reenvía.
- **FIFO estricto** sobre los dos sockets KiSoft (9801 / 9802): **un solo telegrama en vuelo** por canal. Se serializa con `SemaphoreSlim(1)`. Emparejamiento posicional con el ack (no hay correlation-id en el estado).
- **Auditoría no bloqueante**: la ruta crítica *solo encola*; un `BackgroundService` drena la cola a Postgres (procedimientos `sp_insert_buzon_entrada` / `sp_insert_buzon_salida`). Los fallos por ítem se **descartan** (no se reintentan; el siguiente item continúa).
- **Acuse a KiSoft en ≤10 s SIEMPRE** (HISP §2.6) — pase lo que pase aguas abajo. Por eso el ack nunca espera al webhook de SAP.
- **Cuello de botella: KNAPP** (no SAP). Por eso:
  - la lectura del canal 9802 entrega telegramas a un `Channel<T>` ilimitado (bounded) y el dispatcher los consume con `await foreach`.
  - el envío 9801 se serializa con `SemaphoreSlim(1)` y se acopla con `TaskCompletionSource` que solo se completa cuando llega el ack (timeout 10 s).
  - el `BackgroundService` de auditoría descarta en caso de error de BD (jamás bloquea la ruta crítica).
- **Endpoints REST en inglés**; **dominio, tablas, términos y comentarios en español**.

## Estado actual del repo (auditoría rápida)

| Ya existe | Falta / ahora entregado |
|---|---|
| `Telegramas/TelegramFrameCodec.cs` (LF + length + CR) | ✅ JSON Schema formal validado contra los 9 samples (`01-schemas/_validated/`) |
| `Telegramas/TelegramValueCodec.cs` (long-then-value, padding) | ✅ Validador byte-budget (`02-middleware/TelegramLengthValidator.cs` + `01-schemas/length_check.py`) |
| `Telegramas/TelegramReader.cs` / `TelegramWriter.cs` (con `SemaphoreSlim`) | ✅ `TelegramDispatcher` centralizado (clasificación `idrecord` → handler) |
| `Telegramas/KiSoftHeartbeat.cs` (constantes 1HR/2HR/3HR/4HR) | ✅ `HeartbeatScheduler` automático (1HR/2HR/3HR/4HR) |
| `Telegramas/Mapping/ArticleTelegramMapper` (14N), `RouteTelegramMapper` (16N), `BusinessPartnerTelegramMapper` (15N) | ✅ Mappers restantes: 12N/U/D, 1IA, 1RR, 1UN/UU/UD/SL (borradores en `_drafts/`) |
| `Auditing/AuditWriterBackgroundService.cs` (Drena cola sin bloquear) | ✅ Patrón plantilla reusable (`02-middleware/NonBlockingAuditTrail.cs`) |
| `Tcp/KiSoftTcpChannelBase.cs` + `KiSoftOrderChannel.cs` + `KiSoftEventChannel.cs` | ✅ Documentación de la correlación por `TaskCompletionSource` (`02-middleware/README.md`) |
| `Sftp/PrintSftpService.cs` (descarga PDFs/ZPLs a disco) | ✅ **Parser del nombre**, validación ZPL/PDF, enrutado a LBA001/ABA001/ABB001 (`03-print/`) |

## Paquetes entregados

| Carpeta | Contenido | Total |
|---|---|---|
| `01-schemas/` | JSON Schema formal (Draft 2020-12) por identificador de registro + `validate.py` (estructura) + `length_check.py` (byte-budget) + `LENGTHS.md` (tabla canónica HIS de longitudes) | ~36 archivos + 2 validadores |
| `02-middleware/` | C# sketches: `TelegramDispatcher`, `NonBlockingAuditTrail`, `HeartbeatScheduler`, `MasterDataState`, `KiSoftChannelOptions`, `TelegramLengthValidator`, `HisLengthCatalog` | 7 archivos + README |
| `03-print/` | Parser ZPL/PDF: `PrintFileName`, `ZplLabelValidator`, `PdfDocumentValidator`, `PrintFileParser`, `PrintFileWatcher` | 5 archivos + README |

## Cómo usar estos entregables

### 1. JSON Schema

```csharp
// .NET 8 — usar el componente oficial NJsonSchema o JsonSchema.Net
var validator = new JsonSchemaValidator();
var result = validator.Validate(payload, schema, out var errors);
if (!result.IsValid) return BadRequest(errors);
```

Para validar los cuerpos antes de traducir a telegrama KiSoft:

```
[SAP POST /order]
   │  (body)
   ▼
[MinimalApi Controller]
   │  1) SchemaValidator.Validate("12N", body)            ← estructural
   │  2) TelegramLengthValidator.Validate("12N", body)    ← byte-budget
   │  ↓ if invalid → 400 / 413 + audit "validation_error"
   │  TranslateToTelegram(12N body)
   ▼
[KiSoftOrderChannel.SendAsync] ── SemaphoreSlim(1) ──► [TCP socket 9801]
```

El validador de longitudes (`length_check.py` + `TelegramLengthValidator.cs`) detecta
situaciones que la JSON Schema NO detecta — p. ej. SAP puede enviar
`"2028-09-30"` (10 chars) cuando HIS espera `YYYYMMDD` (8): el schema pasa, pero la
trama podría pasarse del window si el mapper no convierte. La herramienta marca esos
casos y devuelve `STRUCTURAL_OVERFLOW`.

### 2. Middleware C#

Copiar los `.cs` de `02-middleware/` a `KnappMiddleware/Telegramas/` (o crear sub-espacio de nombres `KnappMiddleware.Telegramas.Dispatching` si prefieres orden). El dispatcher implementa el patrón **reader → mailbox → handler** ya alineado con `TelegramReader` y `TelegramWriter` existentes.

### 3. Parser ZPL/PDF

Integración en `Sftp/PrintSftpWatcher.cs` (pendiente). Reemplaza al actual `IPrintSftpService.DownloadAsync` por una canalización:

```
SFTP poll → PrintFileWatcher
   │ DownloadAsync(file)
   │ ParseFileName → PrintFileName record
   │ ValidateByExtension + ParseHeader
   │ Enqueue to Channel<ValidatedPrintJob>
   ▼
Background printer-router (KiSoft toma los archivos ya validados y los enruta a LBA/ABA/ABB)
```

> Los archivos `.end` (0 bytes) **no** se validan: son la señal de "ya no hay más hojas de este pedido".

