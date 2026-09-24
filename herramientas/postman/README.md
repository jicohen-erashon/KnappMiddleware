# Colección Postman — KnappMiddleware SAP ↔ KiSoft One

## Importar

1. Postman → **Import** → arrastra `KnappMiddleware.postman_collection.json`.
2. Postman → **Import** → arrastra `KnappMiddleware.postman_environment.json`.
3. Selecciona la environment **"KnappMiddleware - Local"** (arriba a la derecha).

Variables de la environment:

| Variable | Valor por defecto | Nota |
|---|---|---|
| `baseUrl` | `http://localhost:5231` | Ajusta si la Api corre en otro host/puerto |
| `sapUser` | `sap` | Usuario de prueba creado en esta sesión |
| `sapPassword` | `sap-test-2026` | Contraseña del usuario de prueba |

La colección usa Basic Auth a nivel de colección (`{{sapUser}}`/`{{sapPassword}}`), así que no hace
falta configurar auth en cada request.

## Contenido

- **Utilidades**: `GET /health` (sin autenticación).
- **SAP -> Middleware -> KiSoft**: los 8 telegramas implementados, cada uno con el JSON real de
  muestra que envió SAP (`JSON-SAP/*.json`, sin modificar).
- **Bloqueados / pendientes**: `1XR` — layout ya confirmado (HIS V3 §3.5.2) pero el endpoint aún no
  existe; al no haber controller/route registrado cae en la fallback policy y responde `403
  Forbidden` (no un `501` real); la ruta del request es tentativa.

## Para probar de verdad (round-trip completo)

Sin un servidor KiSoft real conectado, los requests exitosos van a dar `502 "canal no está
conectado"` — es el comportamiento correcto, no un error de la colección. Para ver un `200` real:

1. `dotnet run --project herramientas/StubKiSoft -- 9801` (stub de pruebas, ver
   `herramientas/StubKiSoft/Program.cs`).
2. Confirma que `configuracion.kisoft.orderChannel.host/port` apunte a `localhost:9801` (ya es el
   valor por defecto).
3. Corre la Api normalmente y dispara los requests desde Postman.

## Gaps conocidos (no son bugs de la colección)

Documentados en detalle en `JSON-SAP/PENDIENTE-1XR.md`:

- **16N Ruta**: el JSON real de SAP trae `route` de 10 caracteres; el HIS spec (V2 y V3) declara
  ancho fijo 8 → el `[StringLength(8)]` del DTO dispara la validación automática de `[ApiController]`
  antes de llegar al controller, dando `400` a propósito (no `422`).
- **1UU / 1UN**: el JSON real de SAP trae `loadunit` de 8 caracteres; el HIS spec declara ancho fijo
  6 → dan `400` (validación) a propósito.

Ambos están pendientes de confirmar con KNAPP/SAP — el middleware se mantiene fiel al spec mientras
tanto, en vez de aceptar en silencio un ancho no confirmado.
