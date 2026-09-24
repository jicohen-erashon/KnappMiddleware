# Cohen.Knapp — Middleware SAP EWM ↔ KiSoft One

Pasarela de integración **sin lógica de negocio**. Su única función es traducir
entre HTTP/JSON (lado SAP) y TCP/trama (lado KiSoft) y reenviar en ambos sentidos.
KiSoft es la fuente de verdad. Proyecto COHEN Guatemala · C100-006334.

## Stack

- C# / .NET 8 (LTS) — un único **Worker Service** que hospeda todo en el mismo proceso. *(Ajustar a la versión real del repo.)*
- API HTTP: **Minimal API** sobre Kestrel.
- Datos: **PostgreSQL** (solo configuración y auditoría; sin estado de negocio).
- Buffer FIFO: `System.Threading.Channels` en memoria, o RabbitMQ con mensajes **transitorios** (no durables).
- Logging: **Serilog** con sink asíncrono a archivo rotativo.
- SFTP: cliente tipo SSH.NET (solo para el inventario en tiempo real).

## Arquitectura (componentes)

- **API mínima**: recibe comandos de SAP, expone `/authenticate` y el CRUD de la matriz.
- **Traductor**: codec de trama + serializador de campos, bidireccional (JSON ↔ dominio ↔ trama).
- **Gate de matriz**: filtro consultado en ambos sentidos.
- **Canal TCP**: 2 sockets persistentes (9801 cliente, 9802), heartbeat, reconexión, envío serializado FIFO y correlación por `TaskCompletionSource`.
- **Cliente webhook**: POST a SAP de los eventos entrantes (fire-and-forget).
- **Cliente SFTP**: recoge `InventorySnapshot`.
- **Logging no bloqueante** + auditoría opcional por flag.

## Protocolo KiSoft (reglas críticas — no improvisar)

- **Trama**: `<LF>` + longitud (5 chars, `00006`–`99999`, **incluye sus propios 5 bytes**) + datos + `<CR>`. Codificación **UTF-8**.
- **Campos**: longitud por delante, luego valor. Relleno: alfanumérico→espacios a la derecha, numérico→ceros a la izquierda, fecha vacía→`0`.
- **Longitud de campos `TEXT` = nº de caracteres, NO de bytes** (cuidado con UTF-8 multibyte).
- **Dos canales**: `9801` Host→KiSoft (somos cliente) con estado de vuelta; `9802` KiSoft→Host, nosotros enviamos el acuse.
- **FIFO estricto (§2.6)**: comunicación sincronizada, **un solo telegrama en vuelo**; hay que leer el estado de cada registro antes de mandar el siguiente. Serializar con `SemaphoreSlim(1)`. Timeout del RPC ≈ ventana de estado de KiSoft (~10 s); pasado eso, cortar y reconectar.
- **Los estados (`22N`, `24N`…) no llevan clave de correlación** → emparejamiento posicional.
- **Heartbeat (§2.7)**: `1HR/2HR` (Host) y `3HR/4HR` (KiSoft) cada 60 s de silencio; doble timeout 120 s; **reconectar es responsabilidad del cliente**.
- **Datos maestros en bloque**: abrir (`140/141`) → `14N`* → cerrar (`149`). Sin abrir/cerrar, KiSoft responde estado `93`.
- Códigos de estado clave: `00` ok · `11` formato · `21` artículo desconocido · `23` socio desconocido · `93/94` datos maestros · `99` interno.

## Flujos

- **Comando síncrono (9801)**: `POST` → valida → traduce a trama → gate matriz → encola → envío FIFO → lee `22N` → completa el TCS → responde a SAP (`200` / `4xx` si la matriz rechaza / `504` si vence la ventana).
- **Evento por webhook (9802)**: KiSoft empuja → decodifica → **acusa a KiSoft en ≤10 s SIEMPRE** → gate matriz → si procede, POST a SAP (fire-and-forget). **Sin buffer de entrega**: si SAP está caído, el evento se pierde (riesgo aceptado).
- **Inventario en tiempo real**: aviso `3RR` → pickup por **SFTP** del `InventorySnapshot` → traduce → webhook.

## Matriz de mensajes / estaciones

- Clave: **`mandante × tipo de telegrama × estación` → acción**.
- Acciones: `PROCESAR` · `IGNORAR` (en entrada se acusa a KiSoft pero no se reenvía a SAP; en salida no se transmite) · `DESHABILITADO` (estación fuera de servicio: en salida rechaza con error a SAP).
- Comodín `*` en cualquier dimensión; **gana la regla más específica**.
- **Snapshot inmutable en memoria**, recargado en caliente al guardar desde el admin. El gate es O(1); **no se lee la BD por telegrama**.

## Endpoints REST (nombres en inglés)

- `POST /authenticate`
- Datos maestros (lote): `POST /article/batch`, `DELETE /article/{id}` — igual para `/businesspartner`, `/route`.
- Pedidos: `POST /order` (`12N`), `PUT /order/{id}` (`12U`), `DELETE /order/{id}` (`12D`).
- Inventario: `POST /inventory/request`.
- Eventos hacia SAP: por **webhook** (POST que hace el middleware a SAP), no por polling.
- Admin matriz: `GET/PUT /matrix` (+ por celda), dispara recarga del snapshot.

## Logging y datos

- Log de **todas** las transiciones: `recibido → validado → traducido → gate → enviado/encolado → acusado → entregado/perdido → error`.
- No bloqueante: la ruta crítica solo encola; un escritor en segundo plano vuelca a disco.
- Tablas Postgres: `Matriz` (config), `BuzonEntrada` / `BuzonSalida` (auditoría, **activable por flag**). Nada de negocio.

## Convenciones

- **Endpoints REST en inglés**; **dominio, tablas y términos en español** (`EstadoTelegrama`, `BuzonEntrada`, `BuzonSalida`, `EnviadorTcp`, `id_correlacion`, `contenido_json`…).
- Comentarios y mensajes de log en español.

## Reglas que NO romper

- **Cero lógica de negocio**: solo traducir, correlacionar, reenviar.
- **Nada de I/O en la ruta crítica**: ni lectura a BD, ni esperar al disco (log) ni al webhook de SAP.
- **Acusar a KiSoft SIEMPRE en ≤10 s**, pase lo que pase aguas abajo.
- **Un solo telegrama en vuelo por canal** (FIFO).
- El estado entre peticiones es **transitorio** (el TCS); no persistir estado de negocio.

## Pendientes (TODO)

- Definir mecanismo de **autenticación** (clave de API / OAuth client-credentials / mTLS contra SAP EWM).
- Pinear la versión de .NET del repo.
- **Mapeo campo a campo** de cada telegrama por entidad (sub-entregable; empezar por `14N` artículo).

## Comandos (ajustar al repo)

- `dotnet build` · `dotnet test` · `dotnet run`
- `docker compose up -d` (Postgres, y RabbitMQ si se usa)

## Referencias (consultar, no duplicar)

- Documento de diseño: `Plan-aplicacion-Middleware-Cohen-Knapp.docx`.
- Diagramas: `arquitectura-middleware.puml`, `flujo-sincrono-9801.puml`, `flujo-eventos-webhook-9802.puml`.
- Specs KNAPP (campo a campo): HIS (Host Interface Specification) V2, General Specification V2, Definiciones V3.
