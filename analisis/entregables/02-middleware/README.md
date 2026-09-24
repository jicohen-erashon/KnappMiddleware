# 02-middleware — Componentes C# borrador

Sketches `.cs` que **complementan** (no sustituyen) lo que ya está en `KnappMiddleware/Telegramas/`, `KnappMiddleware/Auditing/` y `KnappMiddleware/Tcp/`.

| Archivo | Qué añade / cubre |
|---|---|
| `TelegramDispatcher.cs` | Punto único de despacho: **lee → clasifica idrecord → llama al handler**. Implementa la máquina de estado por socket. |
| `KiSoftChannelOptions.cs` | Opciones de los dos canales KiSoft (copia comentada del existente con énfasis en timeouts FIFO y backpressure). |
| `NonBlockingAuditTrail.cs` | **Plantilla reusable** del patrón non-blocking (espejo del `AuditWriterBackgroundService` real). Sirve para documentar y para usarlo en otros sitios (p. ej. log de impresión). |
| `HeartbeatScheduler.cs` | Emisor `1HR` cada 60 s en silencio. Vigila `3HR` entrante para devolver `4HR`. Dispara reconexión al doble timeout. |
| `MasterDataState.cs` | Trackea si la sesión de transmisión de maestros (140-149 / 150-159 / 160-169) está **abierta/cerrada** (necesario para responder a error 93/94). |

> Los **handlers individuales** (cómo convertir `12N body → 12N trama`) ya existen como `ArticleTelegramMapper` etc. y son los que el `TelegramDispatcher` invoca por `idrecord`.

## Patrón global

```
                 ┌──────────────────────────┐
SAP HTTP ──►│ MinimalAPI (Controllers) │──► envelope.schema.json validation
                 └────────────┬─────────────┘
                              ▼
                 ┌──────────────────────────┐
                 │  Channel<WireCommand>    │  ← bounded (backpressure KNAPP)
                 └────────────┬─────────────┘
                              ▼
                 ┌──────────────────────────┐
   ┌─ audit ─► │ TelemetryAuditTrail.enq  │  ← non-blocking
                 └────────────┬─────────────┘
                              ▼
   ┌──────────────────────────┐  1 sólo en vuelo por canal
   │  TelegramDispatcher.FIFO │  (SemaphoreSlim(1))
   │  (Estado: orden pendiente│
   │   → envía → await ack)   │──► TCP 9801 ──► KiSoft One
   └──────────────────────────┘
                 ▲                 │
   ┌──────────────────────────┐    │ (espera ≤ 10 s)
   │  TelegramDispatcher.Read │    │
   │  (idrecord-based router) │ ◄──┘ TCP 9802 ◄── KiSoft One
   └──────────────────────────┘
```

## Características garantizadas

- **Sin I/O bloqueante en ruta crítica**: SAP HTTP responde con 200/504 sin esperar la BD, el webhook o el log de auditoría.
- **Auditoría sin pérdida silenciosa**: cada `Enqueue` retorna `ValueTask` completa; la cola tiene back-pressure explícito (BoundedChannelFullMode = DropOldest) si por alguna razón el drenaje se atasca.
- **FIFO KiSoft**: un único `SemaphoreSlim(1)` por canal 9801; el segundo envío espera al ack del primero (≤ 10 s).
- **Backpressure KNAPP-cuello-botella**: el canal de entrada del Host (consumidor de 9802) se procesa con `await foreach` sobre un `Channel<TailTelegram>`, regulando naturalmente la producción.

