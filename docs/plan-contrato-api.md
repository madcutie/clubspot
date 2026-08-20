# Plan — Contrato de API y clientes TypeScript generados

**Estado:** aprobado y **ejecutado** el 19/08/2026 (F1–F5 cerradas y verificadas) · implementa
[ADR-0016](adr/0016-contrato-de-api-generado-desde-el-codigo.md) · avance en la
[bitácora](plan-contrato-api.bitacora.md).

## Objetivo

Que exista un **documento OpenAPI fiel** generado desde la Api y versionado en el repo, y que
las dos aplicaciones de frontend hablen con el backend a través de **código TypeScript generado
con Orval** desde ese documento, en vez de interfaces copiadas a mano.

Al terminar: cambiar la forma de una respuesta en un endpoint tiene que romper el `typecheck`
del frontend que la consume, no aparecer como pantalla en blanco en el mostrador.

Este plan **no** cambia ninguna ruta, ningún cuerpo JSON ni ningún comportamiento observable
por los frontends. Es contrato y tipos: si algún cliente se rompe, es porque el tipo escrito a
mano estaba mal.

## Estado de partida

Medido sobre el repo el 19/08/2026:

| | Dato |
|---|---|
| Documento OpenAPI | no existe: sin `Microsoft.AspNetCore.OpenApi` ni `AddOpenApi()` |
| Endpoints | 31 (13 GET, 14 POST, 3 PUT, 1 DELETE) en 13 archivos de `Endpoints/` |
| Firma de los handlers | todos `Task<IResult>` con `Results.Ok(...)` ⇒ cero metadata de respuesta |
| DTO | ~31 records, **sin colisiones de nombre**, casi todos `private sealed record` anidados |
| Respuestas anónimas | 3: `PaymentEndpoints.cs:52`, `PortalEndpoints.cs:111`, `PortalEndpoints.cs:139` |
| Nombres de operación | ninguno: no hay `WithName` ni `WithTags` |
| Tipos del backend escritos a mano en el frontend | `apiHttp.ts` (346), `personasHttp.ts` (251), `portalApi.ts` (396) |

Juegan a favor y no hay que tocarlos: los converters camelCase están registrados **por enum
concreto** en `Program.cs`, y el generador los lee desde `JsonSerializerOptions` (⇒ `Sport` sale
como `"padel" | "football"`); los DTO ya aplanan `Money` a `decimal`; `uint Version` (el `xmin`
de la concurrencia optimista) mapea a número; `AddProblemDetails` ya está registrado.

## Diseño

### 1. Grupos y nombres

Cada grupo de rutas se etiqueta con un tag, y ese tag define el archivo que genera Orval:

| Ruta | Tag |
|---|---|
| `/api/auth/session` | `auth` |
| `/api/context` | `context` |
| `/api/schedules` | `schedules` |
| `/api/courts` | `courts` |
| `/api/availability-overrides` | `availabilityOverrides` |
| `/api` (agenda y reservas de mostrador) | `bookings` |
| `/api/portal/{clubSlug}` | `portal` |
| `/api/payments` | `payments` |
| `/api/people` | `people` |

Fuera del documento (`ExcludeFromDescription`): `/`, `/dev/checkout` (HTML de desarrollo) y
`/api/payments/return` (redirección de navegador, no la consume código).

`WithName` en inglés y en imperativo del verbo HTTP (`GetCourts`, `ReplaceCourts`,
`SearchPeople`), porque es el nombre de la función que va a escribir el frontend.

### 2. Cómo se declara un endpoint

Hoy:

```csharp
group.MapGet("/", GetAsync);

private static async Task<IResult> GetAsync(GetCourtsHandler handler, CancellationToken ct)
{
    var snapshots = await handler.HandleAsync(ct);
    return Results.Ok(snapshots.Select(CourtResponse.From));
}
```

Después:

```csharp
group.MapGet("/", GetAsync).WithName("GetCourts");

private static async Task<Ok<IReadOnlyList<CourtResponse>>> GetAsync(GetCourtsHandler handler, CancellationToken ct)
{
    var snapshots = await handler.HandleAsync(ct);
    return TypedResults.Ok<IReadOnlyList<CourtResponse>>([.. snapshots.Select(CourtResponse.From)]);
}
```

Y donde hay más de un resultado posible, la unión los declara a todos:

```csharp
private static async Task<Results<NoContent, UnprocessableEntity, Conflict>> ReplaceAsync(...)
```

Reglas que quedan fijas:

- Los DTO pasan de `private` a `internal` y se quedan donde están, anidados en la clase de
  endpoints. Se mueven a archivo aparte sólo si el archivo se vuelve incómodo.
- Las tres respuestas anónimas pasan a records nombrados (`PaymentApplyResponse`,
  `PortalCatalogResponse`).
- Los códigos de error sin cuerpo (400, 404, 409, 422) se declaran igual: el cliente generado
  necesita saber que existen aunque no traigan JSON.
- **No se cambia ningún status code ni ninguna forma de respuesta.** Si al declarar aparece una
  discrepancia con lo que el frontend espera, se anota en la bitácora y se decide aparte.

### 3. El documento se regenera en el build de la Api

`AddOpenApi()` + `MapOpenApi()` en la Api, y **un target de MSBuild que reescribe el documento
después de cada `Build`** de `ClubSpot.Api`. Tres piezas:

- **`Program.cs` gana una rama corta**: si los argumentos traen `--export-openapi <ruta>`, arma
  el host, le pide el documento al proveedor de OpenAPI, lo escribe y sale sin `app.Run()`.
- **`appsettings.OpenApi.json`, versionado y sin ningún secreto**: connection string y JWT de
  relleno. El entorno se llama `OpenApi` justamente porque **no es Development**, así que el
  arranque no migra ni siembra, y EF no se conecta al registrarse. El export no necesita Docker
  ni base.
- **El target** corre `dotnet exec $(TargetPath) --export-openapi docs/api/clubspot.openapi.json`
  con `ASPNETCORE_ENVIRONMENT=OpenApi`, y **sólo reescribe el archivo si el contenido cambió**,
  para no disparar recompilaciones en cascada.

Como `dotnet test` compila la Api, el documento se regenera también al correr los tests. No hay
forma de trabajar contra un contrato viejo: o el archivo está al día, o hay un diff sin
commitear.

Se descarta `Microsoft.Extensions.ApiDescription.Server` (la vía oficial) por lo que dice
ADR-0016: no deja elegir el entorno con el que corre el host, y con `Development` la Api migra
y siembra al arrancar.

### 4. Orval en los frontends

`orval` como devDependency de cada app, con su propia config:

```ts
// src/frontend/backoffice/orval.config.ts
export default {
  clubspot: {
    input: '../../../docs/api/clubspot.openapi.json',
    output: {
      target: 'src/api/generado',
      mode: 'tags-split',
      client: 'fetch',
      override: { mutator: { path: './src/api/http.ts', name: 'api' } },
    },
  },
};
```

- El *mutator* es la pieza que evita que lo generado traiga su propio `fetch`: todo pasa por el
  `api()` que ya maneja sesión, reintento en 401 y `ApiError`. En el portal apunta a su
  equivalente.
- `npm run api:gen` en cada app, **enganchado a `predev` y `prebuild`**: arrancar el frontend o
  compilarlo regenera el cliente primero. El resultado se commitea igual, para que un clon
  fresco compile sin generar nada.
- Los adaptadores (`apiHttp.ts`, `personasHttp.ts`, `portalApi.ts`) **siguen existiendo y
  siguen siendo la frontera**: pierden las interfaces del backend y las llamadas crudas, y se
  quedan con lo suyo, que es traducir a los tipos de dominio en español y dejar las fechas ya
  escritas.

### 5. La regla de uso (lo que evita los huérfanos)

ADR-0016 punto 7, escrito acá porque es lo que hay que mirar en cada revisión:

**Todo acceso a la API pasa por el código generado.** Ni un `fetch`, ni una URL armada a mano,
ni una `interface` que repita un DTO del backend. Si falta un endpoint, se agrega en la Api y se
regenera — nunca se escribe un servicio al lado "por ahora".

Un servicio escrito a mano deja **huérfano** al generado: cuando el contrato cambia, ese camino
no lo acompaña y el compilador no avisa. Es el mismo agujero que este plan viene a tapar, pero
peor, porque conviven los dos y parece que está cubierto.

Señales para revisar: un `fetch(` fuera del mutator · un `import` de `api/http.ts` que no sea el
mutator · una `interface` en `src/` con la forma de un DTO del backend · un archivo bajo
`src/api/generado/` con ediciones a mano.

## Fases

### F1 — Documento OpenAPI regenerado por el build

- `Microsoft.AspNetCore.OpenApi` en `ClubSpot.Api.csproj`; `AddOpenApi()` y `MapOpenApi()`;
  `ExcludeFromDescription()` en las tres rutas que no son API.
- Rama `--export-openapi` en `Program.cs`, `appsettings.OpenApi.json` versionado y el target de
  MSBuild que reescribe `docs/api/clubspot.openapi.json` después del build.

**Verificación:** en un clon sin `appsettings.Development.json` y **con Docker apagado**,
`dotnet build` escribe el documento y queda verde con `TreatWarningsAsErrors`; el documento
lista los 31 endpoints y no incluye `/dev/checkout`; un segundo build seguido no vuelve a tocar
el archivo; tocar un endpoint y compilar deja el diff a la vista.

### F2 — Declarar el contrato en los endpoints

El grueso del trabajo. Se hace **por tag**, en este orden, para poder cortar en cualquier punto
con el repo compilando: `courts` · `schedules` · `availabilityOverrides` · `context` · `auth` ·
`people` · `bookings` · `payments` · `portal`.

Por cada tag: `TypedResults` y uniones en los handlers, DTO `internal` y nombrados,
`WithName`/`WithTags`, y regenerar el documento.

**Verificación:** build y los 111 tests verdes después de cada tag; el diff del documento
muestra schemas de respuesta donde antes no había nada; ningún cambio en las respuestas reales
—se comprueba con los tests de integración existentes, que ya ejercitan los cuerpos JSON—.

### F3 — Orval en el backoffice

- `orval` + `orval.config.ts` + `npm run api:gen` enganchado a `predev`/`prebuild`; salida en
  `src/api/generado`, commiteada.
- `apiHttp.ts` y `personasHttp.ts` pasan a consumir lo generado y se quedan sólo con la
  traducción al dominio. `http.ts` queda como mutator.

**Verificación:** `npm run typecheck` y `npm run build` verdes; las cuatro pantallas probadas
contra la API real (agenda del día, editor de cancha, editor de horario, base de personas), con
un alta de persona y una venta de turno de punta a punta.

### F4 — Orval en el portal de reservas

- Misma operación sobre `portalApi.ts`, con el mutator del portal.

**Verificación:** `typecheck` y `build` verdes; reserva online completa con el gateway fake
(disponibilidad → datos de la persona → hold → pago → confirmada).

### F5 — Cierre

- AGENTS.md: la sección 6 gana las dos convenciones —"todo endpoint declara su contrato" y
  "todo acceso a la API pasa por el cliente generado"—, la sección 9.1 marca la pregunta del
  contrato de API como contestada y la sección 10 aclara que lo generado vive debajo de los
  adaptadores y no se edita.
- `dev-up.ps1` no necesita cambios: cada `npm run dev` ya regenera su cliente por `predev`.

**Verificación:** clon limpio → `dotnet build` + `dotnet test` + `npm run typecheck` en las dos
apps, sin correr la generación.

## Riesgos y cómo se acotan

| Riesgo | Cómo se acota |
|---|---|
| Al declarar aparecen respuestas que el frontend leía distinto de lo que la Api manda | Los tests de integración ya ejercitan los cuerpos; cualquier discrepancia se anota en la bitácora y se decide, no se "arregla" de paso |
| El documento se desactualiza | Lo reescribe el build de la Api, y esa pieza llega en F1, **antes** que el trabajo de F2 justamente por eso |
| Alguien escribe un servicio a mano y deja huérfano al generado | Regla explícita (diseño §5 y ADR-0016 punto 7), llevada a AGENTS.md §6 en F5, con las señales concretas a mirar en revisión |
| El target de build tarda o molesta en cada compilación | Sólo reescribe si cambió; se mide en F1 y, si molesta, se condiciona a `Configuration=Debug` |
| `TreatWarningsAsErrors` con las uniones `Results<...>` | Se descubre en F2 sobre el primer tag (`courts`), que es el más chico y sirve de sonda |
| Lo generado tienta a usarse directo desde los componentes | Regla de ADR-0016 punto 4, repetida en AGENTS.md §10 en F5 |

## Fuera de alcance

- Contract-first (escribir el OpenAPI a mano): descartado en ADR-0016; si aparece un consumidor
  externo, se revisa con un ADR nuevo.
- Hooks de React Query generados: `queries.ts` sigue a mano.
- Validación en runtime con Zod: Orval la sabe generar; se evalúa cuando haya un motivo.
- Versionado del contrato (`/v1`, deprecaciones): no hay consumidor externo que lo justifique.
- Publicar una UI de documentación (Swagger UI, Scalar): el documento alcanza para generar.
