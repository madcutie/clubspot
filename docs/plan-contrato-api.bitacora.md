# Bitácora — Plan contrato de API y clientes TypeScript

Registro de avance del [plan](plan-contrato-api.md). La entrada más nueva arriba.

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
