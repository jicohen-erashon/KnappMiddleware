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

`appsettings.json` / `appsettings.Development.json` solo traen lo mínimo para poder arrancar
(logging, `Postgres:ConnectionString`); **todo lo demás** (RabbitMQ, SFTP, canales TCP de KiSoft,
URL del webhook de SAP) vive en la tabla `configuracion` de Postgres para poder cambiarse en
caliente sin reiniciar. Ver `db/migrations/0008_sap_webhook_config.sql` y
`0009_infra_config.sql` para los valores por defecto que se insertan.

> `Postgres:ConnectionString` en el repo trae un placeholder (`CAMBIAR_EN_SERVIDOR`) en vez de la
> contraseña real — configúrala localmente (variable de entorno, user secrets, o un archivo no
> versionado) antes de correr la aplicación.

## Base de datos

Las migraciones en `db/migrations/` son archivos SQL numerados, pensados para aplicarse en orden y
de forma idempotente (usan `IF NOT EXISTS` / `CREATE OR REPLACE` donde aplica) contra una base
Postgres vacía o ya existente. No hay un runner automático incluido en el repo; se aplican a mano,
por ejemplo:

```bash
for f in db/migrations/*.sql; do
  psql "postgresql://usuario:password@localhost:5432/Middleware" -f "$f"
done
```

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

- `1XR`: confirmar con KNAPP si el add-on de pago está contratado antes de habilitarlo en producción.
- Definir mecanismo de autenticación definitivo del lado SAP→Middleware (hoy Basic Auth).
