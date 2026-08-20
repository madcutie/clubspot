# Bitácora — Plan contrato de API y clientes TypeScript

Registro de avance del [plan](plan-contrato-api.md). La entrada más nueva arriba.

## 19/08/2026 — Corrección del usuario: regenerar en el build y prohibir los servicios a mano

Dos correcciones sobre el plan escrito hace un rato, las dos del usuario:

- **La regeneración va en el build de la Api**, no en un test que alguien corre cuando se
  acuerda. La objeción que había llevado a descartarla —`Program.cs` lanza sin connection string
  ni JWT, y en Development el arranque migra y siembra— se resuelve con un entorno propio
  `OpenApi`: `appsettings.OpenApi.json` versionado y sin secretos, una rama `--export-openapi` en
  `Program.cs` y un target de MSBuild que reescribe el documento sólo si cambió. Se descarta
  `Microsoft.Extensions.ApiDescription.Server` porque no deja elegir el entorno del host. Del
  lado del frontend, `api:gen` queda enganchado a `predev`/`prebuild`.
- **Hay que decir en alguna parte que siempre se usa lo generado**, porque si no van a aparecer
  servicios escritos a mano y los generados por Orval van a quedar huérfanos. Es ADR-0016
  punto 7, la sección 5 del diseño del plan (con las señales concretas a mirar en revisión) y
  dos viñetas nuevas en AGENTS.md §6, más la aclaración en §10.

Sigue sin arrancar la implementación.

## 19/08/2026 — ADR y plan escritos; implementación no arrancada

- **Cómo salió el tema:** el usuario preguntó qué haría falta para integrar Orval y generar los
  servicios TypeScript. El relevamiento del repo mostró que Orval es la parte barata y que el
  bloqueante está en el backend: **no hay documento OpenAPI**, y aunque se agregara hoy saldría
  vacío de schemas porque los 31 endpoints devuelven `Task<IResult>`.
- Con eso quedó contestada la pregunta que AGENTS.md §9.1 tenía abierta desde el arranque
  ("¿contract-first como el repo anterior?"): se va **code-first**, el documento se genera desde
  el código y los clientes desde el documento. Escrito en
  [ADR-0016](adr/0016-contrato-de-api-generado-desde-el-codigo.md).
- Números del relevamiento, medidos, no estimados: 31 endpoints (13 GET, 14 POST, 3 PUT,
  1 DELETE) en 13 archivos; ~31 DTO **sin colisiones de nombre**; 3 respuestas anónimas
  (`PaymentEndpoints.cs:52`, `PortalEndpoints.cs:111` y `:139`); 993 líneas de adaptadores en
  los dos frontends con las formas del backend reescritas a mano.
- **Descartada la generación del documento en el build** (`Microsoft.Extensions.ApiDescription.Server`),
  que era la opción obvia: ejecuta el host durante la compilación y `Program.cs` lanza a
  propósito si faltan la connection string o el JWT, que sólo viven en
  `appsettings.Development.json` — no versionado. Un clon fresco no compilaría. En su lugar, el
  documento se exporta desde el host de prueba y un test falla si el archivo versionado quedó
  desactualizado.
- Lo que **no** se decidió acá y sigue abierto: si en algún momento conviene generar hooks de
  React Query o validación con Zod. Hoy no, por los motivos del ADR.
- **F1–F5 pendientes. No se implementa sin pedido explícito del usuario.**
