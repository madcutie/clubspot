# Bitácora — Plan contrato de API y clientes TypeScript

Registro de avance del [plan](plan-contrato-api.md). La entrada más nueva arriba.

## 19/08/2026 — F1 a F5 ejecutadas y verificadas

Se implementó el plan entero. Lo que se hizo, y lo que apareció en el camino:

**F1 — el documento lo escribe el build.** `Microsoft.AspNetCore.OpenApi`, `AddOpenApi()` y
`MapOpenApi()`; rama `--export-openapi` en `Program.cs`; `appsettings.OpenApi.json` versionado;
target `ExportOpenApiDocument` después del `Build`, que sólo reescribe si el contenido cambió.

- **Hizo falta arrancar el host.** El primer intento pidió el documento a `IOpenApiDocumentProvider`
  con el host construido pero sin arrancar, y salió **vacío**: las rutas se mueven a los
  *endpoint data sources* recién en el arranque. Se arranca, pero **sin abrir ningún socket**:
  cuando hay que exportar, `IServer` se reemplaza por uno que no escucha. Una compilación no
  puede quedarse con un puerto tomado.
- Verificado: en un clon **sin `appsettings.Development.json` y sin base**, `dotnet build` escribe
  el documento y queda verde; un segundo build seguido no vuelve a tocar el archivo.

**F2 — los 29 endpoints declaran su contrato.** `TypedResults` y uniones `Results<…>` en todos los
handlers, DTO `internal` y nombrados, `WithName` y `WithTags` en cada ruta, y las tres respuestas
anónimas convertidas en records (`PaymentApplyResponse`, `PortalCatalogResponse` y
`PortalSettleResponse` — el plan nombraba dos, pero eran tres formas distintas: la de settle es
`PaymentApplyOutcome?`, anulable, y meterla en el mismo record habría mentido sobre eso).

- **29 y no 31**: los 31 del relevamiento incluían `/api/payments/return` y `/dev/checkout`, que
  quedan fuera del documento por ser navegación, no API. Del otro lado se sumó
  `/api/people/{id}/bookings`, posterior al relevamiento.
- **Ninguna respuesta cambió**: 143 tests verdes (79 unitarios + 64 de integración), y los de
  integración ya ejercitan los cuerpos JSON.

**Tres defectos del documento generado, encontrados y cerrados.** Los tres son de la generación,
no del código, y se arreglan una vez en la Api en vez de dos veces en los frontends:

1. **Los enums salían sin `type`** y se generaban como `unknown`. Un schema transformer les pone
   `type: string` (y `null` si el enum admite nulo).
2. **Los números salían como `integer | string`**, porque los defaults web de System.Text.Json
   aceptan un número entre comillas al leer. Es cierto en la entrada, pero ninguna respuesta lo
   escribe así y ningún cliente nuestro lo manda: la unión sólo ensuciaba **todos** los campos
   numéricos de los dos frontends. El transformer se queda con el tipo numérico.
3. **`{clubSlug}` no estaba declarado como parámetro** en 7 rutas del portal y en los 2 webhooks,
   porque lo consume un *endpoint filter* y no el handler. Orval se negó a generar —con razón: un
   documento que nombra un placeholder que no declara es inválido—. Un operation transformer
   declara todo token de la ruta que el handler no tomó como argumento.

**Se cambió la versión del documento de 3.0 a 3.1**, contra lo que decía el plan. Motivo concreto:
en 3.0, una propiedad de enum anulable se degrada a `{"type":"string","oneOf":[$ref]}` y el
generador se queda con el `string`, perdiendo el enum — es decir, produce **exactamente la unión
escrita a mano que este plan vino a eliminar** (`ApiPaymentMode` en el portal). En 3.1 sale
`oneOf: [{type: null}, {$ref}]` y se genera `PaymentMode | null`. Orval 8 lee 3.1 sin problema.

**F3 y F4 — Orval en los dos frontends.** `orval` como devDependency de cada app, config propia,
salida en `src/api/generado` commiteada, y `api:gen` enganchado a `predev` y `prebuild`. Los
adaptadores siguen siendo la frontera: perdieron las interfaces del backend y las URLs, y se
quedaron con la traducción al dominio.

- El portal **no tenía mutator**: hacía `fetch` crudo en cinco lugares. Se creó
  `src/api/http.ts` con `api()` y `ApiError`, y ahora `fetch` aparece una sola vez en cada app.
- Verificación del backoffice: las 22 rutas que arma el cliente generado, ejercitadas contra la
  API real (contexto, horarios, canchas, excepciones, agenda de los dos deportes, los cuatro
  filtros de personas, alta, ficha, historial, nota, bloqueo individual y masivo, pago, venta de
  turno, cobro y cancelación). Todas responden lo esperado.
- Verificación del portal: reserva online completa con el gateway fake —catálogo, disponibilidad,
  hold con seña, lectura con y sin prueba de propiedad (200 / 404), webhook aprobado, conciliación
  a pedido, `confirmed` con 7000 de 14000 pagados— más liberación de hold idempotente (204, 204) y
  rechazada sin token (404).
- **Sin click-through visual**: la extensión de Chrome no estaba conectada, así que las cuatro
  pantallas no se probaron a mano. Lo que sí está probado es el contrato entero por HTTP y que las
  dos apps compilan con `typecheck` y `build` en verde.

**F5 — cierre.** AGENTS.md §2, §6, §9.1 y §10 actualizados. Las dos convenciones de §6 ya estaban
escritas desde que se redactó el plan; ahora dejan de describir una intención.

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
