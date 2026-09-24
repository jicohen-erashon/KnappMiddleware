# 03-print — Parser ZPL / PDF del canal de impresión SFTP

KNAPP recibe trabajos de impresión desde el Host COHEN por **SFTP** (usuario `sftpuser`, contraseña `customer`, capítulo §3.7 del HIS). El middleware ya tiene `IPrintSftpService` que descarga los bytes a disco y `PrintSftpService` que ejecuta el `SftpService` base; **falta el parser** del nombre y la validación de contenido.

## Convenciones que valida este paquete

- **Nombre**: `<pedido>.<hoja>.<tipoDoc>.<infoAdicional>.<hojaImpresa>` — o `.end` para señalizar fin de transmisión.
- **`tipoDoc`**: `001` = albarán (PDF, Carta/Letter) · `008` = etiqueta de dirección (ZPL, 170 mm o 120-180 mm).
- **Tamaño máximo**: PDF 40 KB · ZPL 2.5 KB (HISP §3.7.1.2).
- **Asignación por defecto a estación**:
  - `001` albarán → `ABB001` (insertadora automática de documentos).
  - `008` ZPL → `LBA001` (etiqueta dirección caja cartón, 170 mm, ZPL 2000T) **o** `ABA001` (etiqueta dirección caja plástico, 120-180 mm). Determinar por el ancho del rollo ZPL o por metadatos del sobre.
- **`.end`** = señal vacía; **NO** se valida como ZPL/PDF.

## Archivos

| Archivo | Función |
|---|---|
| `PrintFileName.cs` | Parsea el nombre del archivo → record inmutable (`ordernumber`, `sheetnumber`, `documenttype`, `printedSheetNumber`, `isEndMarker`, etc.). |
| `ZplLabelValidator.cs` | Verifica empiece con `^XA`, termine con `^XZ`, balance de directivas, sin truncamiento, ≤ 2.5 KB. |
| `PdfDocumentValidator.cs` | Verifica header `%PDF-`, EOF `%%EOF`, magic de xref (`startxref ... %%EOF`), ≤ 40 KB. |
| `PrintFileParser.cs` | Entry point que toma `(filename, bytes)` y devuelve un `ValidacionImpresion` (válido/razones). |
| `PrintFileWatcher.cs` | Integración con `IPrintSftpService`: poll → download → parse → enrutar a canal interno que las impresoras físicas consumen. |

## Cómo se integra

```
SFTP server (sftpuser@knapp-internal)
   │ (poll cada N segundos)
   ▼
IPrintSftpService.DownloadAsync(path, token)
   │
   │ bytes[] contents
   ▼
PrintFileParser.TryParse(fileName, contents)
   │   └─► ValidacionImpresion { Ok, Station, Format, Errors[] }
   │
   ├─ errores → NonBlockingAuditTrail.EnqueueIncoming + descartar (no se reintenta)
   │
   └─ ok     → Channel<PrintJob> → router
                                              ┌─ ABB001  (documento, picking list)
                                              ├─ LBA001  (etiqueta caja cartón, 170 mm)
                                              └─ ABA001  (etiqueta caja plástico, 120-180 mm)
```

> Los nombres `.end` se persisten en el log pero **no se reenvían**: son la señal de "ya no hay más hojas de este pedido". El watcher los usa para liberar la marca de "esperando más hojas del pedido X hoja Y".

