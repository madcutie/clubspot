# Plan de remediación — modelado del backend

Fecha: 16/08/2026. Origen: revisión de modelado del 16/08/2026 y decisiones del usuario
registradas en [ADR-0008](adr/0008-deporte-como-configuracion-no-modulo.md) y
[ADR-0009](adr/0009-club-module-guarda-lo-contratado.md). Avance en la
[bitácora del plan](plan-backend-backoffice.bitacora.md).

**Qué es esto:** la lista exacta de cambios para alinear el código con las reglas de negocio
ya decididas. No hay decisiones abiertas acá: todo lo opinable ya está decidido en los ADRs,
en `AGENTS.md` y en este documento. Un ejecutor (humano o agente) aplica los paquetes en orden.

**La fase C del plan del backoffice no arranca hasta cerrar esta remediación.**

## Reglas para el ejecutor

1. **No hacer `git commit` ni `git push`.** Los commits los hace el usuario.
2. **No tocar `docs/`** (la documentación ya está actualizada) **ni `src/frontend/`** (sus
   pendientes quedaron registrados en `AGENTS.md` §10).
3. Código entero en inglés, casi cero comentarios (ADR-0006). Los únicos comentarios nuevos
   permitidos son los que este plan pide textualmente.
4. Los paquetes se aplican **en orden**. Cierre de cada paquete: `cd src/backend && dotnet
   build` sin errores ni warnings (`TreatWarningsAsErrors`) y los **tests unitarios** verdes
   (`dotnet test src/Tests/ClubSpot.UnitTests`). Los tests de **integración recién compilan y
   pasan al final del paquete 8** (necesitan las migraciones regeneradas); mientras tanto sólo
   deben compilar.
5. Si algo de este plan resulta imposible tal como está escrito, **parar y reportar**, no
   improvisar un diseño alternativo.

---

## Paquete 1 — Deporte y módulos (ADR-0008)

Elimina los módulos por deporte y el deporte de las personas. Un solo enum `Sport`.

### 1.1 Un solo enum

- **Borrar** `src/backend/src/Core/ClubSpot.SharedKernel/Primitives/Sport.cs`.
- Queda como único enum `ClubSpot.Domain.Bookings.Sport` (valores `Padel`, `Football` — sin
  renombres: "Fútbol 5" es presentación, no otro deporte).

### 1.2 `Person` pierde el deporte

- `Domain/Core/People/Person.cs`: eliminar la propiedad `PreferredSport` y el parámetro
  `preferredSport`/`sport` del constructor (y el `using` de Primitives si queda sin uso —
  ojo: `Money` sigue viniendo de ahí).
- `Application/Core/People/PeopleHandlers.cs`: `CreatePersonHandler.HandleAsync` pierde el
  parámetro `Sport sport`.
- `Application/Core/People/IPeopleQueries.cs`: `PersonListItem` pierde `PreferredSport`.
- `Infrastructure/Repositories/PeopleQueries.cs`: quitar `PreferredSport` de las proyecciones.
- `Api/Endpoints/PeopleEndpoints.cs`: `CreatePersonRequest` pierde `PreferredSport`;
  eliminar el bloque `Enum.TryParse<Sport>`; `PersonResponse` pierde el campo; ajustar los dos
  `From`.
- `Infrastructure/Persistence/Configurations/PersonConfiguration.cs`: eliminar el mapping de
  `preferredSport`.

### 1.3 Catálogo sin módulos por deporte

- `SharedKernel/Modularity/ModuleId.cs`: eliminar los estáticos `Padel` y `Football`.
- `Application/Modularity/ProductModules.cs`: eliminar las clases `PadelModule` y
  `FootballModule`.
- `Api/Program.cs`: el `ModuleCatalog` se registra con los cuatro manifiestos
  (`CoreModule`, `MembersModule`, `FinanceModule`, `BookingsModule`).
- `Api/Seed/DevSeeder.cs`: la línea del `Resolve` pasa a
  `moduleCatalog.Resolve([ModuleId.Members, ModuleId.Bookings])` (el `Resolve` se elimina
  después, en el paquete 4).

### 1.4 Tests

- `UnitTests/Modularity/ModuleCatalogTests.cs`: el catálogo de prueba queda con los cuatro
  módulos. Reescribir los tests que usaban `Padel`/`Football` sobre el grafo nuevo,
  conservando qué invariante prueba cada uno:
  - cierre transitivo: `Resolve([ModuleId.Members])` ⇒ exactamente
    `{Core, Finance, Members}`; agregar `Resolve([ModuleId.Bookings])` ⇒
    `{Core, Finance, Bookings}`.
  - `Resolve([])` ⇒ sólo los núcleo (sin cambios).
  - `DependentsOf`: `DependentsOf(ModuleId.Finance, Resolve([ModuleId.Members,
    ModuleId.Bookings]))` ⇒ `{Members, Bookings}`.
- `UnitTests/Core/People/PersonTests.cs` y `IntegrationTests/People/PeopleEndpointsTests.cs`:
  quitar el deporte del constructor y del JSON (`preferredSport`).
- Test de integración de `/api/context`: los módulos esperados pasan a ser cuatro
  (`bookings`, `core`, `finance`, `members`).
- `UnitTests/Bookings/CourtTests.cs` y `IntegrationTests/Bookings/SchedulePersistenceTests.cs`
  siguen usando `Sport.Padel` del enum de `Domain.Bookings` — sólo ajustar `using` si hiciera
  falta.

---

## Paquete 2 — Bookings pasa por Application y el agregado se endurece (ADR-0005)

Hoy `CourtEndpoints` y `ScheduleEndpoints` usan `BookingsDbContext` directo desde la Api.
Regla nueva de `AGENTS.md` §4: la Api no toca EF; los endpoints sólo traducen HTTP ↔
Application. Excepciones explícitas: el bootstrap de `Program.cs` (migraciones al arrancar) y
`DevSeeder` no son casos de uso y pueden seguir como están.

### 2.1 Puertos y handlers en `Application/Bookings/`

Crear la carpeta `Application/Bookings/` con:

```csharp
// SchedulesStore.cs (puerto + tipos)
public sealed record ScheduleSnapshot(Schedule Schedule, uint Version);
public enum ReplaceOutcome { Saved, VersionMissing, VersionUnexpected, UnknownSchedule, VersionConflict, ScheduleInUse }

public interface ISchedulesStore
{
    Task<IReadOnlyList<ScheduleSnapshot>> GetAllAsync(CancellationToken cancellationToken);
    Task<ReplaceOutcome> ReplaceAllAsync(IReadOnlyList<(Schedule Schedule, uint? Version)> items, CancellationToken cancellationToken);
}

// CourtsStore.cs — espejo exacto para Court
public sealed record CourtSnapshot(Court Court, uint Version);
public interface ICourtsStore
{
    Task<IReadOnlyList<CourtSnapshot>> GetAllAsync(CancellationToken cancellationToken);
    Task<ReplaceOutcome> ReplaceAllAsync(IReadOnlyList<(Court Court, uint? Version)> items, CancellationToken cancellationToken);
}
```

Handlers (`GetSchedulesHandler`, `ReplaceSchedulesHandler`, `GetCourtsHandler`,
`ReplaceCourtsHandler`), estilo de los de People (clases con primary constructor, un
`HandleAsync`):

- Los `Get` devuelven los snapshots del store.
- Los `Replace` reciben la lista de inputs crudos (records de Application con los mismos
  campos que hoy tienen los `CourtRequest`/`ScheduleRequest`, incluidos `Guid? Id` y
  `uint? Version`), validan **ids duplicados** (mismo chequeo que hoy: ids no nulos repetidos
  ⇒ resultado inválido), construyen los agregados (`request.Id ?? Guid.NewGuid()`, tenant de
  `ITenantContext`) y llaman al store. Devuelven algo mapeable a HTTP (el `ReplaceOutcome`
  más el caso "ids duplicados").

### 2.2 Implementaciones en Infrastructure

`Infrastructure/Repositories/SchedulesStore.cs` y `CourtsStore.cs`: mover ahí, tal cual, la
lógica EF que hoy vive en los endpoints — carga del diccionario existente, `RemoveRange` de
los ausentes, `xmin` como `OriginalValue`, `CurrentValues.SetValues`, detección previa de
canchas colgando de un horario eliminado (`ScheduleInUse`), validación de horario inexistente
en courts (`UnknownSchedule`), versión faltante en update (`VersionMissing`) o presente en
alta (`VersionUnexpected`), y `DbUpdateConcurrencyException` ⇒ `VersionConflict`.

Registrarlos en `ServiceCollectionExtensions` con un `AddClubSpotBookings()` espejo de
`AddClubSpotPeople()` (stores + handlers) y llamarlo desde `Program.cs`.

### 2.3 Endpoints delgados

`ScheduleEndpoints` y `CourtEndpoints` quedan sin `BookingsDbContext`, sin `EF.Property` y
sin `using` de EF/Infrastructure.Persistence: parsean el request, llaman al handler y mapean:
`Saved` ⇒ 204 · `VersionMissing`/`VersionUnexpected`/`UnknownSchedule`/ids duplicados ⇒ 422 ·
`VersionConflict`/`ScheduleInUse` ⇒ 409. Los DTOs wire (`…Request`/`…Response`) se quedan en
la Api con la misma forma que hoy (con `Version` de los snapshots).

### 2.4 El agregado no puede romperse después de construido

- `Domain/Bookings/TimeRange.cs`: la validación pasa al **constructor** (reescribir el record
  posicional como record con propiedades y constructor que valida); eliminar el método
  `Validate()`. `Schedule.ValidateRanges` deja de invocarlo y sólo verifica superposición.
- `Domain/Bookings/Schedule.cs`: `WeeklyRanges` pasa a
  `IReadOnlyDictionary<DayOfWeek, IReadOnlyList<TimeRange>>` y `SpecialDates` a
  `IReadOnlyList<SpecialDate>`; el constructor hace copia defensiva de dicionario y listas.
- `Domain/Bookings/SpecialDate.cs`: `TimeRanges` pasa a `IReadOnlyList<TimeRange>` (hoy es
  `IReadOnlyCollection`), por uniformidad.
- `Domain/Bookings/Schedule.cs`, propiedad `TimeZone`, agregar el comentario:
  `// Informative mirror of the mock's editor; booking rules use the club's time zone.`
- `Infrastructure/Persistence/BookingsDbContext.cs`: ajustar los tipos genéricos de los
  `ValueConverter`/`ValueComparer` a los tipos nuevos (deserializar a los tipos concretos y
  asignar; System.Text.Json serializa interfaces sin problema).
- Ajustar `ScheduleTests` y los DTOs que usaban los tipos mutables.

---

## Paquete 3 — Plata en `Money` y la moneda la define el club

Reglas (`AGENTS.md` §6): nunca `decimal` suelto para plata; `Money` sin moneda por defecto;
la moneda sale de `Club.Currency`.

### 3.1 `Money` sin default

- `SharedKernel/Primitives/Money.cs`: eliminar `DefaultCurrency` y los valores por defecto de
  los parámetros `currency` en `Zero` y `Of`. La moneda es siempre explícita.
- Arreglar todos los call sites que dependían del default (tests incluidos): reciben la
  moneda del contexto (ver 3.2) o, en tests unitarios, una constante local del test
  (por ejemplo `"ARS"` literal en el arrange).

### 3.2 Puerto de configuración del club

```csharp
// Application/Core/IClubSettings.cs
public sealed record ClubInfo(string Name, string? Venue, string Currency);

public interface IClubSettings
{
    Task<ClubInfo> GetAsync(CancellationToken cancellationToken);
}
```

Implementación `Infrastructure/Repositories/ClubSettings.cs`: consulta `db.Clubs` por
`Id == tenantContext.Current` y proyecta el record. Sin cache (tabla de una fila por tenant).
Registrar en `AddClubSpotPersistence` (o en una extensión nueva si queda más prolijo — pero
registrado queda).

- `CreatePersonHandler`: inyecta `IClubSettings` y crea la deuda con
  `Money.Zero(club.Currency)`.
- `PeopleQueries.SearchAsync`: obtiene la moneda del club (consulta directa a `db.Clubs`, ya
  vive en Infrastructure) y construye `TotalDebt` con ella.

### 3.3 Tarifas de `Court` en `Money`

- `Domain/Bookings/Court.cs`: `DayPrice` y `NightPrice` pasan a `Money`. Validación del
  constructor: ninguna negativa (`IsNegative`) y **misma moneda entre ambas** (si difieren,
  `ArgumentException`).
- `Infrastructure/Persistence/BookingsDbContext.cs`: mapear ambas con `ComplexProperty` como
  `Person.Debt` — columnas `dayPriceAmount` (precision 14,2) / `dayPriceCurrency` (char 3
  fijo) / `nightPriceAmount` / `nightPriceCurrency`.
  ⚠️ Si `CurrentValues.SetValues` no copiara los complex types en el update del store,
  asignar esas propiedades explícitamente en `CourtsStore`; verificarlo con el test de 3.4.
- **El wire no cambia**: `CourtRequest`/`CourtResponse` siguen llevando
  `decimal DayPrice`/`NightPrice` (importes en la moneda del club). `ReplaceCourtsHandler`
  inyecta `IClubSettings`, lee la moneda una vez y construye `Money.Of(amount, currency)`;
  el `Get` devuelve `.Amount`.
- `PeopleEndpoints` sigue exponiendo `decimal Debt`/`Paid` como hasta ahora.

### 3.4 Tests

- `CourtTests`: construir con `Money`; agregar caso de monedas distintas ⇒ excepción.
- `SchedulePersistenceTests` (courts): además del rename existente, modificar un precio en el
  primer PUT y verificar que el GET posterior lo devuelve (prueba el roundtrip del complex
  type en el update).

---

## Paquete 4 — `club_module` guarda lo contratado (ADR-0009)

- `Infrastructure/Modularity/TenantModulesProvider.cs`: inyectar `ModuleCatalog`; el conjunto
  cacheado pasa a ser `catalog.Resolve(persistido)`. La clave y el TTL del cache no cambian.
- `Api/Seed/DevSeeder.cs`: eliminar el `Resolve`; persiste **sólo lo contratado**:
  `[ModuleId.Members, ModuleId.Bookings]`.
- `Domain/Core/ClubModule.cs`: agregar el comentario
  `// Rows are what the club purchased; enablement is the resolved closure (ADR-0009).`
- Test de integración: con el seed persistiendo dos filas, `/api/context` devuelve los cuatro
  habilitados — el test del paquete 1 ya lo cubre; verificar que siga en verde en el paquete 6
  (es la prueba de que la expansión ocurre en lectura).

## Paquete 5 — Contexto completo, vocabulario wire y stubs señalizados

### 5.1 `GET /api/context` completo

El plan (§5) promete club + operador + roles + módulos; hoy sólo devuelve módulos. Forma
nueva (los nombres comerciales/es-AR los pone el frontend):

```json
{
  "club": { "name": "…", "venue": "…" },
  "operator": { "name": "…", "roles": ["administrator"] },
  "modules": ["bookings", "core", "finance", "members"]
}
```

- Club vía `IClubSettings` (paquete 3). Operador: `ClaimTypes.Name` del principal; roles:
  claims de rol parseados a `Role` y expuestos como enum en el record (el converter de 5.2
  los escribe camelCase). Sin tocar EF desde el endpoint.
- Actualizar el test de integración del contexto (módulos + operador + club).

### 5.2 Enums camelCase en el wire

- `Program.cs`: `builder.Services.ConfigureHttpJsonOptions(options =>
  options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));`
- `PeopleEndpoints.PersonResponse`: `Origin` pasa de `string` a `PersonOrigin` (el converter
  emite `"counter"`/`"app"`); quitar los `ToString()` manuales.
- `CourtResponse.Sport` ya es enum: pasa a emitirse `"padel"`/`"football"` solo.
- **No tocar** la serialización de claves de diccionario (`WeeklyRanges`) ni el JSONB
  persistido (usa sus propias opciones): el contrato fino del wire se cierra en fase C.
- Tests de integración que deserializan respuestas: registrar el mismo converter en las
  opciones del cliente de test (helper compartido en el proyecto de tests).

### 5.3 Stubs marcados (los únicos comentarios nuevos permitidos, además de los ya pedidos)

- `Person.cs`, sobre `Debt`:
  `// Provisional until the finance module owns the ledger (plan §8): debt is a plain balance and RegisterPayment wipes it with no counter-entry.`
- `PeopleQueries.cs`, sobre los contadores:
  `// Bookings counters are stubbed until bookings exist: zero bookings, no last date, and the 'without bookings' filter matches everyone.`

## Paquete 6 — Nombres de tabla en plural

Decisión del usuario del 16/08/2026, ya fijada en `AGENTS.md` §6: **los nombres de tabla van
en plural** (plural inglés real, incluidos los irregulares). Índices y constraints siguen al
nombre de su tabla, también en plural. **Las columnas no cambian**: siguen en singular.

Va antes de las migraciones a propósito, para regenerarlas una sola vez.

### 6.1 Renombres exactos

| Archivo | Actual | Nuevo |
|---|---|---|
| `Configurations/ClubConfiguration.cs` | tabla `club` | `clubs` |
| ídem | check `ckClubDepositPercent` | `ckClubsDepositPercent` |
| ídem | índice `uxClubSlug` | `uxClubsSlug` |
| `Configurations/UserConfiguration.cs` | tabla `user` | `users` |
| ídem | índice `uxUserTenantEmail` | `uxUsersTenantEmail` |
| ídem | tabla owned `userRole` | `userRoles` |
| `Configurations/ClubModuleConfiguration.cs` | tabla `clubModule` | `clubModules` |
| `Configurations/PersonConfiguration.cs` | tabla `person` | `people` |
| ídem | índice `ixPersonTenantSearchName` | `ixPeopleTenantSearchName` |
| ídem | índice `ixPersonTenantPhoneDigits` | `ixPeopleTenantPhoneDigits` |
| `Configurations/NoteConfiguration.cs` | tabla `personNote` | `personNotes` |
| `Persistence/BookingsDbContext.cs` | tabla `schedule` | `schedules` |
| ídem | tabla `court` | `courts` |
| ídem | índice `uxCourtTenantSportSortOrder` | `uxCourtsTenantSportSortOrder` |

`person` → **`people`**, no `persons`: es el plural real y coincide con el `DbSet` (`db.People`)
y con la ruta `/api/people`.

### 6.2 Qué NO se toca

- **Los nombres de columna**: siguen en singular (`tenantId`, `createdAt`, `dayPriceAmount`…).
- **Las tablas de historial de EF** `__EFMigrationsHistoryCore` y `__EFMigrationsHistoryBookings`:
  son infraestructura de EF, no tablas de dominio.
- Los índices sin nombre explícito (el de `personNotes`, y los de `tenantId` que agrega
  `ModuleDbContextBase`): EF los deriva del nombre de tabla nuevo solo.
- Nombres de clases, `DbSet`, endpoints y cualquier identificador de C#.

### 6.3 Verificación del paquete

Greppear en `src/backend` (incluye tests y cualquier SQL crudo) los nombres viejos como
literal de tabla: `"club"`, `"user"`, `"userRole"`, `"clubModule"`, `"person"`, `"personNote"`,
`"schedule"`, `"court"`. No debe quedar ninguno como nombre de tabla. Ojo con los falsos
positivos: `"user"` aparece en otros contextos y las columnas homónimas no se tocan.

## Paquete 7 — Un solo `DbContext` y una sola cadena de migraciones (ADR-0010)

Decisión del usuario del 16/08/2026: **una sola tabla de migraciones**. Como dos historiales
son la consecuencia obligada de tener dos contextos, se unifican los contextos.
Ver [ADR-0010](adr/0010-un-solo-dbcontext-y-una-sola-tabla-de-migraciones.md).

Va antes de regenerar las migraciones, a propósito.

### 7.1 El contexto único

- Crear `Persistence/ClubSpotDbContext.cs`: un único contexto que reemplaza a `CoreDbContext` y
  `BookingsDbContext`. Contiene **todos** los `DbSet` de ambos (`Clubs`, `Users`, `ClubModules`,
  `People`, `Notes`, `Schedules`, `Courts`) y aplica **todas** las configuraciones de entidad.
- Conserva `public const string Schema = "public"` y `modelBuilder.HasDefaultSchema(Schema)`.
  **Elimina** las constantes `MigrationsHistoryTable`: se usa el default `__EFMigrationsHistory`.
- La configuración inline de `Schedule` y `Court` que hoy vive dentro de `BookingsDbContext`
  (converters y comparers JSONB, índices, FK, `xmin`) se mueve a
  `Persistence/Configurations/ScheduleConfiguration.cs` y `CourtConfiguration.cs`, como clases
  `IEntityTypeConfiguration<>`, igual que las del módulo `core`. Las carpetas y archivos
  siguen separando los módulos: la frontera es de código (ADR-0005).
- `ModuleDbContextBase` deja de tener sentido con un solo contexto: **plegar** su contenido
  (convención de `TenantId`, filtro global sobre `ITenantOwned`, guardia de `SaveChanges`,
  `TenantMismatchException`) dentro de `ClubSpotDbContext` y **borrar el archivo**. El
  comentario de cabecera sobre la política de tenancy se conserva, adaptado.
- Borrar `CoreDbContext.cs`, `BookingsDbContext.cs`, `CoreDbContextFactory.cs` y
  `BookingsDbContextFactory.cs`; crear una única `ClubSpotDbContextFactory` (misma forma que
  las actuales).

### 7.2 Todos los consumidores

Reemplazar `CoreDbContext`/`BookingsDbContext` por `ClubSpotDbContext` en:
`Repositories/PersonRepository.cs` · `Repositories/PeopleQueries.cs` ·
`Repositories/UserRepository.cs` · `Repositories/SchedulesStore.cs` ·
`Repositories/CourtsStore.cs` · `Modularity/TenantModulesProvider.cs` ·
`DependencyInjection/ServiceCollectionExtensions.cs` (**un solo** `AddDbContext`, sin
`MigrationsHistoryTable`) · `Api/Seed/DevSeeder.cs` · `Api/Program.cs` (**un solo**
`MigrateAsync`) · y los tests de integración que lo usen.

### 7.3 `AuthEndpoints` deja de usar EF

`Api/Endpoints/AuthEndpoints.cs` consulta `db.Clubs` directo, lo que contradice la regla de
`AGENTS.md` §4 ("la Api no usa EF ni los DbContexts directamente"). Corregirlo acá:

- Agregar a `Application/Core/Users/IUserRepository.cs` —o a un puerto nuevo
  `IClubDirectory` en `Application/Core/`, a elección del ejecutor, pero **uno solo**— un
  método `Task<TenantId?> FindClubIdBySlugAsync(string slug, CancellationToken)`.
- Implementarlo en Infrastructure y usarlo en el endpoint. El comportamiento no cambia: club
  inexistente ⇒ 401 genérico, igual que hoy.

### 7.4 Fixture de tests

`IntegrationTests/Persistence/PostgresFixture.cs`: un solo `CreateDbContext(...)` que devuelve
`ClubSpotDbContext` (sin `MigrationsHistoryTable`) y un solo `MigrateAsync` en
`InitializeAsync`. Actualizar los tests que llamaban a `CreateCoreDbContext` /
`CreateBookingsDbContext`.

### 7.5 Verificación del paquete

Cero apariciones de `CoreDbContext`, `BookingsDbContext`, `ModuleDbContextBase`,
`MigrationsHistoryTable`, `__EFMigrationsHistoryCore` y `__EFMigrationsHistoryBookings` en
todo `src/backend` (incluidos tests; los `obj/` no cuentan).

## Paquete 8 — Migraciones y verificación total

1. Borrar **la carpeta `Persistence/Migrations/` entera** (las dos subcarpetas `Core/` y
   `Bookings/` con todas sus migraciones, Designer y snapshots). Con un solo contexto ya no
   corresponde ninguna de esas migraciones, cualquiera sea su timestamp.
2. Regenerar **una sola** migración inicial (desde `src/backend`, con `dotnet tool restore`
   hecho):

   ```bash
   dotnet ef migrations add Initial --project src/Infrastructure/ClubSpot.Infrastructure --startup-project src/Api/ClubSpot.Api --output-dir Persistence/Migrations
   ```

3. Verificar en el resultado: no existe columna `preferredSport`; `courts` tiene
   `dayPriceAmount`/`dayPriceCurrency`/`nightPriceAmount`/`nightPriceCurrency` y no
   `dayPrice`/`nightPrice`; **las ocho tablas están en plural** (`clubs`, `users`, `userRoles`,
   `clubModules`, `people`, `personNotes`, `schedules`, `courts`); todo en `public` y en
   camelCase. **Una sola migración** que crea las ocho tablas.
4. `cd src/backend && dotnet build` — cero errores, cero warnings.
5. `dotnet test` completo — unitarios e integración verdes (integración usa Testcontainers;
   Docker debe estar corriendo).

### Checklist final (todo debe dar cero, salvo lo indicado)

| Chequeo | Esperado |
|---|---|
| `PreferredSport` en `src/backend/src` | 0 apariciones |
| `ModuleId.Padel` / `ModuleId.Football` / `PadelModule` / `FootballModule` | 0 apariciones |
| `Sport` fuera de `Domain/Bookings`, `Application/Bookings`, Infrastructure, Api y tests de bookings | 0 apariciones |
| `DefaultCurrency` | 0 apariciones |
| `"ARS"` en `src/backend/src` | sólo en `DevSeeder` (dato del club semilla) y en arranges de tests |
| `BookingsDbContext` o `CoreDbContext` referenciados desde `ClubSpot.Api` | sólo `Program.cs` (bootstrap de migraciones) y `Seed/DevSeeder.cs` |
| `decimal` como tipo de una propiedad de dominio que representa plata | 0 (sólo dentro de `Money` y en DTOs wire) |
| Nombres de tabla en singular (`ToTable("club")`, `"person"`, `"court"`…) | 0 apariciones |
| Las 8 tablas de la migración regenerada | todas en plural |
| `CoreDbContext` · `BookingsDbContext` · `ModuleDbContextBase` · `MigrationsHistoryTable` | 0 apariciones |
| Carpetas bajo `Persistence/Migrations/` | ninguna: una sola cadena, sin subcarpetas por módulo |

### Nota para el usuario (no para el ejecutor)

La base local de desarrollo debe recrearse al terminar:
`docker compose down -v && docker compose up -d postgres`, y al arrancar la API en
Development aplica migraciones y seed desde cero.

## Fuera de alcance (registrado, no se hace acá)

- Frontend: quitar el deporte de la base de personas y del alta; adaptación del vocabulario
  wire al conectar (fase C). Registrado en `AGENTS.md` §10.
- El contrato fino de `WeeklyRanges` en el wire (claves de día) — fase C.
- La pantalla/endpoint de configuración de módulos por club (sigue pendiente en §9.1).
- Cualquier rediseño de canchas/deportes (catálogo administrable, formatos F5/F7/F11):
  decisión de diseño pendiente con el usuario (ADR-0008, `AGENTS.md` §9.6).
