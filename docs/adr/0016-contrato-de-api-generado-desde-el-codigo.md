# ADR-0016 — El contrato de API se genera desde el código y los clientes TypeScript desde el contrato

**Fecha:** 19/08/2026 · **Estado:** Aceptada

## Contexto

La sección 9.1 del documento de partes a desarrollar dejó abierta una pregunta: *"decidir si se
sigue el enfoque contract-first del repo anterior (OpenAPI escrito a mano, frontend generado
desde ahí)"*. Llegó el momento de contestarla porque los dos frontends ya corren contra la API
real y el costo de no tener contrato se volvió visible.

Estado al momento de decidir:

- La Api **no expone ningún documento OpenAPI**: no hay `Microsoft.AspNetCore.OpenApi` ni
  Swashbuckle en `ClubSpot.Api.csproj`, ni `AddOpenApi()` en `Program.cs`.
- Los 31 endpoints (13 GET, 14 POST, 3 PUT, 1 DELETE, repartidos en 13 archivos de
  `Endpoints/`) devuelven `Task<IResult>` con `Results.Ok(...)`. Eso **no deja metadata de
  tipo**: aunque se generara el documento hoy, saldría sin un solo schema de respuesta.
- Los DTO son `private sealed record` anidados en la clase de endpoints, y tres respuestas son
  objetos anónimos (`PaymentEndpoints.cs:52`, `PortalEndpoints.cs:111` y `:139`).
- Del otro lado, los dos frontends **reescriben a mano** las formas del backend: `apiHttp.ts`
  (346 líneas), `personasHttp.ts` (251) y `portalApi.ts` (396) declaran sus propias
  `interface CourtResponse`, `ScheduleResponse`, etc. Nada garantiza que sigan siendo ciertas
  después de un cambio en la Api; el error aparece en runtime, en el navegador.

## Decisión

**El contrato de API es un documento OpenAPI generado desde el código de la Api, versionado en
el repo, y los clientes TypeScript de los dos frontends se generan desde ese documento con
Orval.** El documento es un artefacto derivado: se regenera con herramienta y **no se edita a
mano**.

1. **La fuente de verdad son los endpoints .NET** (code-first). El documento vive versionado en
   `docs/api/clubspot.openapi.json`.
2. **Un endpoint declara su contrato o es un bug.** Se devuelve `TypedResults` y uniones
   `Results<Ok<T>, NotFound, Conflict>` en vez de `IResult`; los DTO son tipos nombrados y
   accesibles, no anónimos ni privados; cada ruta lleva `WithName` y `WithTags`. `WithName` fija
   el nombre de la función generada en TypeScript, así que es parte del contrato, no decoración.
3. **El documento se extrae del host de la Api**, no del build. Un test de integración lo pide
   al host de prueba, lo compara con el archivo versionado y **falla si difieren**: el contrato
   no puede quedar desactualizado sin que los tests lo digan.
4. **Lo generado es capa de cable y vive por debajo de los adaptadores.** Reemplaza las
   interfaces escritas a mano y las llamadas `api<T>('/api/...')`. Los tipos de dominio en
   español, el formateo de fechas y las claves de React Query siguen escritos a mano: la regla
   de la sección 10 —los componentes no saben que existe HTTP— no cambia.
5. **Se genera por aplicación**, cada una con su propio *mutator*: el backoffice reusa el
   `api()` de `http.ts` (sesión JWT, reintento en 401, `ApiError`) y el portal el suyo. El
   código generado **se commitea**, para que un clon fresco compile sin correr la generación y
   para que el diff del contrato se lea en la revisión.
6. **El contrato es código, así que va en inglés** (ADR-0006): rutas, DTO, nombres de operación
   y tags. Los textos en español siguen siendo cosa del frontend.

## Consecuencias

- Un cambio de forma en la Api aparece como diff en `clubspot.openapi.json` y, tras regenerar,
  rompe el `typecheck` del frontend afectado. Hoy ese mismo cambio no rompe nada hasta que un
  operador lo encuentra usando el sistema.
- Se paga una vez el costo de declarar los 31 endpoints, y después cada endpoint nuevo nace
  declarado. Es trabajo mecánico pero no trivial.
- Desaparecen ~30 interfaces duplicadas del backend en los frontends.
- Un tercer consumidor (portal del socio, app, integración) arranca con cliente tipado gratis.
- El documento OpenAPI **se expone siempre**, no sólo en Development: describe formas, no datos,
  y todos los endpoints siguen protegidos por su propia autorización. Si alguna vez hay que
  esconderlo, se resuelve en el borde, como el gating de módulos.
- Quedan fuera del documento las rutas que no son API: `/` , `/dev/checkout` (HTML de
  desarrollo) y `/api/payments/return` (redirección de navegador).

## Alternativas descartadas

- **Contract-first: escribir el OpenAPI a mano y generar desde ahí**, como el repo anterior.
  Con dos consumidores que viven en este mismo repo y ningún tercero externo, agrega una
  segunda fuente de verdad que se desincroniza sin que nadie se entere. Si aparece un
  consumidor externo que necesite el contrato antes que la implementación, se revisa con un
  ADR nuevo.
- **Generar el documento en el build** (`Microsoft.Extensions.ApiDescription.Server`): ejecuta
  el host durante la compilación, y `Program.cs` lanza a propósito si faltan la connection
  string o la configuración de JWT — que sólo están en `appsettings.Development.json`, que **no
  se versiona** (decisión del 17/08/2026). Un clon fresco o un CI limpio no compilarían.
- **Sólo tipos, con `openapi-typescript`**: da los tipos pero no las funciones, así que las
  llamadas se seguirían escribiendo a mano. La diferencia con Orval es chica, pero el trabajo
  caro —declarar el contrato en el backend— es el mismo, y Orval además cubre el día que se
  quieran hooks o validación con Zod.
- **Generar hooks de React Query con Orval**: chocarían con `queries.ts`, que ya tiene sus
  claves e invalidaciones, y saltearían los adaptadores. Se generan funciones, no hooks.
- **Seguir escribiendo los tipos a mano**: es el estado actual, y es lo que motivó la decisión.
