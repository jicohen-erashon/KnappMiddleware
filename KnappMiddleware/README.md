# KnappMiddleware

Pasarela de integración entre **SAP EWM** y **KiSoft One** (KNAPP) para el proyecto COHEN
Guatemala (C100-006334). Traduce entre HTTP/JSON (lado SAP) y TCP/trama de ancho fijo (lado
KiSoft) y reenvía en ambos sentidos. KiSoft es la fuente de verdad; el middleware no contiene
lógica de negocio propia, solo traduce, valida formato, filtra por matriz de mensajes y audita.

## Stack

- **.NET 10** / ASP.NET Core (Web API con Controllers, no Minimal API).
- **PostgreSQL** (vía Npgsql + Dapper) para configuración, matriz de mensajes, usuarios y
  auditoría — sin estado de negocio.
- **RabbitMQ** y **SFTP** (SSH.NET) como canales adicionales hacia KiSoft (inventario en tiempo
  real).
- **NLog** con sink a archivo rotativo + Loki (Grafana).
- **Scalar** para la documentación OpenAPI interactiva (`/scalar`).

## Estructura del repositorio

```
KnappMiddleware/              Proyecto único (todo el código de la aplicación)
  Controllers/Sap/            Los 6 controllers que exponen los telegramas SAP -> KiSoft
  Controllers/Knapp/Admin/    Endpoints administrativos (auth, matriz, config, auditoría, status)
  Controllers/Knapp/Channels/ Endpoints de operación de canales (TCP, SFTP, cola)
  Contratos/Sap/              DTOs de los payloads JSON que envía SAP
  Telegramas/                 Codec TLV de trama fija + mapeadores por telegrama
  Auditing/                   Auditoría no bloqueante (buzon_entrada / buzon_salida)
  Auth/                       Basic Auth + autorización por rol (SuperUsuario / Sap)
  Matrix/                     Gate de matriz de mensajes (emisor x telegrama x estación -> acción)
  Tcp/ · Sftp/ · RabbitMq/     Canales de comunicación hacia KiSoft
  Eventos/                    Despachador de eventos KiSoft -> SAP (webhook)
  Postgres/                   Repositorios y servicios de arranque contra Postgres
  db/migrations/              Migraciones SQL numeradas (base de datos completa)
KnappMiddleware.Tests/        Pruebas unitarias (xUnit) de los mapeadores de telegramas
```

## Telegramas SAP ↔ KiSoft soportados

| Endpoint (SAP -> Middleware)                     | Telegrama(s) | Descripción                              |
|---------------------------------------------------|--------------|-------------------------------------------|
| `POST /api/v1/sap/article/14N`                     | 14N          | Alta/actualización de artículo            |
| `POST /api/v1/sap/businesspartner/15N`             | 15N          | Alta/actualización de socio comercial     |
| `POST /api/v1/sap/route/16N`                       | 16N          | Alta/actualización de ruta                |
| `POST /api/v1/sap/order/12N`                       | 12N          | Pedido nuevo                              |
| `POST /api/v1/sap/inventory/request/1IA`           | 1IA          | Solicitud de inventario                   |
| `POST /api/v1/sap/inventory/realtime/1RR`          | 1RR          | Visualización de inventario en tiempo real|
| `POST /api/v1/sap/loadunit/modify/1UU`             | 1UU          | Modificar unidad de carga                 |
| `POST /api/v1/sap/loadunit/available/1UN`          | 1UN          | Unidad de carga disponible                |
| `POST /api/v1/sap/inventory/stock/1XR`             | 1XR          | Consulta de stock de un artículo en tiempo real |
| *(evento, no HTTP entrante)*                       | 32R          | KiSoft empuja evento de pedido; el middleware acusa en ≤10s y hace POST fire-and-forget a SAP vía webhook |

`1XR` es un add-on de pago (HIS V3 §3.5.2, "CR16 – 1.1 – Segundo punto") no incluido en el precio
base — confirmar con KNAPP que esté contratado antes de habilitarlo en producción. A diferencia del
resto de telegramas, mandante y tipo de stock tienen presencia opcional en la trama (longitud `00`
si SAP no los envía).

Cada telegrama sigue el mismo patrón: valida contra la matriz de mensajes → traduce el DTO JSON a
la trama TLV de ancho fijo → la envía por el canal TCP correspondiente → interpreta el estado que
responde KiSoft → traduce a un código HTTP (`200` éxito, `409` estación deshabilitada por matriz,
`422`/`400` error de formato, `502` KiSoft rechazó o el canal no está disponible, `504` timeout).

## Autenticación y autorización

- **HTTP Basic Auth** sobre todo el canal SAP-facing (`Auth/BasicAuthenticationHandler`), respaldado
  por la tabla `usuarios` (contraseñas con bcrypt).
- Dos roles: `SuperUsuario` (acceso completo, endpoints de administración/operación) y `Sap` (solo
  los endpoints SAP-facing, vía la policy `SapOrSuperUsuario`).
- Por defecto **todo endpoint requiere autenticación y rol `SuperUsuario`** (fallback policy); los
  endpoints de telegramas y `/sftp-file` relajan esto a `SapOrSuperUsuario`.
- `/health`, `/scalar`, `/openapi` y `/` quedan anónimos.

## Endpoints administrativos

| Endpoint                          | Uso                                                       |
|------------------------------------|------------------------------------------------------------|
| `GET /health`                      | Health check (sin auth)                                    |
| `GET /api/v1/matrix` · `POST .../reload` | Consultar la matriz de mensajes / recargar el snapshot en memoria |
| `GET /api/v1/config` · `POST .../reload` | Consultar configuración clave/valor / recargarla sin reiniciar |
| `POST /api/v1/auth/reload`         | Recarga usuarios sin reiniciar                              |
| `GET /api/v1/audit` · `PUT .../toggle`   | Consultar auditoría / activar-desactivar el flag           |
| `GET /api/v1/status`               | Estado general del middleware                              |
| `GET .../connections` · `POST .../reconnect` · `GET .../queue` (`/api/v1/tcp`) | Estado de las conexiones TCP hacia KiSoft, reconexión manual y estado de la cola |
| `POST /api/v1/queue/clear`         | Vaciar cola/mantenimiento                                   |
| `POST /api/v1/sftp-file`           | Disparo manual del pickup SFTP de inventario                |

## Configuración

Dos niveles, por diseño:

- **appsettings / variables de entorno** — solo lo imprescindible para levantar el proceso. Se lee
  una vez al arrancar; cambiarlo exige reiniciar.
- **Tabla `configuracion` de Postgres** — todo lo demás (RabbitMQ, SFTP, canales TCP de KiSoft,
  webhook de SAP, auditoría). Se recarga en caliente con `POST /api/v1/config/reload`, sin reiniciar.

### Variables de entorno / `appsettings.json`

Precedencia estándar de ASP.NET Core: variable de entorno → `appsettings.{Environment}.json` →
`appsettings.json`. En variable de entorno los dos puntos se escriben como doble guion bajo
(`Postgres:ConnectionString` → `Postgres__ConnectionString`).

| Llave | Obligatoria | Descripción |
|---|---|---|
| `Postgres:ConnectionString` | Sí | Cadena Npgsql hacia la base `Middleware`. `ValidateOnStart` solo comprueba que **exista**, no que conecte: con Postgres caído la app arranca degradada (ver "Arranque y dependencias"). |
| `FileLogging:BasePath` | Sí | Raíz de todo lo que la app escribe en disco. Es `required`: **si falta, la app no arranca**. El valor del repo (`E:/logs/KnappMiddleware`) es una ruta Windows — en contenedor Linux hay que sobreescribirla. |
| `Loki:Endpoint` | No | Endpoint de push de Grafana Loki. Default `http://loki:3100`. |
| `__GRAFANA_LOKI_URL__` | No | Variable de entorno pura, sin equivalente en appsettings. **Pisa a `Loki:Endpoint`**; pensada para inyectar el endpoint desde el entorno del servidor sin tocar archivos. |
| `ASPNETCORE_ENVIRONMENT` | No | Solo con `Development` se mapean `/`, `/openapi` y `/scalar`; en cualquier otro valor devuelven 404. |
| `ASPNETCORE_URLS` | En contenedor | No hay sección `Kestrel` ni `UseUrls` en el código: fuera de `launchSettings.json` el binding depende enteramente de esta variable (p. ej. `http://+:8080`). |

`Postgres:ConnectionString` viene en el repo con el placeholder `CAMBIAR_EN_SERVIDOR`. Localmente,
configúrala con user secrets (el `.csproj` ya trae `UserSecretsId`) en vez de editar el archivo:

```bash
dotnet user-secrets set "Postgres:ConnectionString" "Host=localhost;Port=5432;Database=Middleware;Username=iadministrator;Password=..."
```

Nota de HTTPS: la cookie del panel admin es `Secure`+`SameSite=Strict` y `UseHttpsRedirection` está
activo. Servido por HTTP plano sin TLS por delante (proxy inverso o el perfil `https`), el login de
`/admin` no funciona porque el navegador descarta la cookie.

### Tabla `configuracion` (recarga en caliente)

Claves leídas vía `ClsConfigGate`. Si falta una obligatoria, falla el servicio que la necesita al
intentar conectar — no el arranque de la app. Se editan desde el panel (`/admin` → Configuración) o
en la tabla; los valores por defecto se siembran en `0008_sap_webhook_config.sql` y
`0009_infra_config.sql` del repositorio de base de datos.

| Prefijo | Claves (default entre paréntesis) | Obligatorias |
|---|---|---|
| `rabbitmq.` | `hostName`, `port` (5672), `userName`, `password`, `virtualHost` (`/`), `inboundExchange`, `inboundQueue`, `outboundExchange`, `outboundRoutingKey` | todas salvo `port` y `virtualHost` |
| `sftp.inventory.` | `host`, `port` (22), `username`, `password`, `privateKeyPath`, `privateKeyPassphrase`, `inboundDirectory`, `outboundDirectory` | `host`, `username`, `inboundDirectory`, `outboundDirectory` |
| `sftp.print.` | las mismas de conexión + `outboundDirectory` | `host`, `username`, `outboundDirectory` |
| `kisoft.orderChannel.` | `host`, `port` (9801), `connectTimeoutSeconds` (10), `responseTimeoutSeconds` (20), `heartbeatIdleSeconds` (60), `heartbeatTimeoutSeconds` (120), `reconnectDelaySeconds` (5) | `host`, `port` |
| `kisoft.eventChannel.` | las mismas, `port` 9802 | `host`, `port` |
| `sap.webhook.` | `baseUrl`, `orderEventPath` (`/kisoft/order-events`), `timeoutSeconds` (10) | ninguna — sin `baseUrl` el evento se descarta con warning |
| `audit.` | `enabled` (`false`), `queueCapacity` (`10000`) | ninguna |

`audit.enabled` se consulta en vivo en cada escritura; `audit.queueCapacity` **solo se lee al
construir el canal**, así que cambiarla exige reiniciar la app.

Las credenciales de login (panel admin y Basic Auth del canal SAP) no viven aquí sino en la tabla
`usuarios`; se recargan con `POST /api/v1/auth/reload`.

Los dos `sftp.*.privateKeyPath` apuntan a un archivo del sistema de archivos **del proceso**, no de
la base de datos: al contenerizar, la clave debe montarse dentro del contenedor y el valor en la
tabla debe ser esa ruta interna.

## Arranque y dependencias

Ninguna dependencia externa bloquea el arranque: Postgres se resuelve perezosamente, RabbitMQ
conecta en el primer uso, SFTP abre y cierra por operación, los canales TCP reconectan en bucle y
los fallos de Loki se descartan. Los tres servicios de arranque (`configuracion`, `matriz`,
`usuarios`) capturan su excepción y solo registran un warning: con Postgres caído la app levanta
pero sin matriz (todo deshabilitado) y sin usuarios (toda autenticación falla) hasta recargar.

La excepción que sí importa: si al arrancar faltan `kisoft.*.host`/`.port` en `configuracion`
(típicamente porque Postgres aún no estaba listo), el bucle de reconexión del canal TCP muere de
forma permanente y silenciosa — ni `/config/reload` lo revive, solo reiniciar el proceso. Por eso el
orden de arranque importa: Postgres sembrado y accesible **antes** que la app.

## Logs y archivos en disco

Todo cuelga de `FileLogging:BasePath`. Tres escritores distintos:

| Ruta | Contenido |
|---|---|
| `{BasePath}/log-yyyy-MM-dd.txt` + `archives/` | NLog, rotación diaria, retención de 30 archivos. |
| `{BasePath}/JSON/yyyy/MM/dd/` | Un archivo JSON **por request y otro por response**, siempre activo, sin flag para apagarlo. Es el consumidor de disco dominante. El header `Authorization` se enmascara. |
| `{BasePath}/Exceptions/yyyy/MM/dd/` | Volcado de excepciones no controladas. |

Además, todo evento de log se envía a Loki (labels `app=KnappMiddleware`, `env`, `level`, `logger`).
Si Loki no responde, el lote se pierde y la app sigue.

## Base de datos

Las migraciones **no viven en este repositorio** — los archivos SQL/de base de datos se versionan
en un repositorio git separado (política del proyecto: nunca mezclar SQL/credenciales de
infraestructura con el código de la app). Viven en `../db/migrations/` (hermano de este repo, al
nivel del workspace), son archivos SQL numerados pensados para aplicarse en orden y de forma
idempotente (usan `IF NOT EXISTS` / `CREATE OR REPLACE` donde aplica) contra una base Postgres
vacía o ya existente. No hay un runner automático; se aplican a mano, por ejemplo:

```bash
for f in ../db/migrations/*.sql; do
  psql "postgresql://usuario:password@localhost:5432/Middleware" -f "$f"
done
```

Algunas migraciones (p. ej. `0009_infra_config.sql`) siembran valores de configuración con un
placeholder (`CAMBIAR_EN_SERVIDOR`) en vez de la contraseña real — configúrala aparte antes de
aplicar la migración en un entorno real.

Tablas principales: `matriz` (config de la matriz de mensajes), `usuarios` (login + rol),
`configuracion` (clave/valor de infraestructura), `buzon_entrada` / `buzon_salida` (auditoría de
cada telegrama en ambas direcciones, incluyendo la trama de red completa en hex).

## Cómo correr localmente

```bash
dotnet build
dotnet run --project KnappMiddleware
```

La API queda en `http://localhost:5231` (perfil `http` de `launchSettings.json`) y abre
`/scalar` para explorar los endpoints. Necesita una instancia de Postgres con las migraciones
aplicadas; sin un servidor KiSoft real conectado, los telegramas responden `502` ("canal no
conectado"), que es el comportamiento esperado.

## Pruebas

```bash
dotnet test
```

`KnappMiddleware.Tests` cubre los mapeadores de telegramas (codificación/decodificación de la
trama TLV) con xUnit.

## Auditoría y trazabilidad

Cada request SAP y cada respuesta de KiSoft se audita de forma no bloqueante en `buzon_entrada` /
`buzon_salida`, incluyendo: código HTTP devuelto, usuario/ruta/IP de origen, `objectid` /
`tecreatedby` del sobre SAP para poder rastrear quién originó el dato en SAP, y la trama completa
de red (con los delimitadores `<LF>`/`<CR>`) codificada en hex, ya que contiene caracteres no
imprimibles. Los fallos de validación automática de `[ApiController]` (antes de llegar al
controller) también se auditan, vía un `InvalidModelStateResponseFactory` dedicado.

## Pendientes conocidos

- **[URGENTE]** `route` (16N): el HIS (V2 y V3, sin cambios) declara ancho fijo 8, pero SAP envía
  consistentemente 10 caracteres — el middleware se mantiene fiel al HIS y rechaza con 400 cualquier
  `route` de SAP de más de 8 caracteres, por lo que el 16N real de SAP no puede procesarse hoy. Ver
  `JSON-SAP/PENDIENTE-1XR.md`.
- `1XR`: confirmar con KNAPP si el add-on de pago está contratado antes de habilitarlo en producción.
- Definir mecanismo de autenticación definitivo del lado SAP→Middleware (hoy Basic Auth).
