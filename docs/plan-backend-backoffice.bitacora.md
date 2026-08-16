# Bitácora — Plan backend del backoffice

Registro de avance del plan [`plan-backend-backoffice.md`](plan-backend-backoffice.md).

**Regla de uso:** el agente que trabaje sobre el plan actualiza este archivo **al terminar cada
bloque de trabajo**, no al final de la sesión. Cada entrada va arriba de las anteriores, con
fecha, qué se hizo, qué decisiones se tomaron sobre la marcha, y un cierre explícito de
**"dónde quedó / próximo paso"**. La tabla de estado se mantiene al día.

## Estado por fase

Corte de fases del 15/08/2026: F0 se dividió en A1–A4; B = F1+F2; C = F3+F4 (ver la nota de
actualización en el plan).

| Fase | Contenido | Estado |
|---|---|---|
| Plan | Diseño del plan + documentos movidos + links arreglados | ✅ 14/08/2026 |
| A1 | Renombres a inglés (`Period`, ids de módulo, tests, docs) + reestructura por capas con Application (ADR-0005) | ✅ 15/08/2026 — build verde y 14 tests unitarios verdes |
| A2 | Persistencia (EF Core + PostgreSQL, `CoreDbContext`, tabla `club`) + tenancy (`AsyncLocal`, filtro global, guardia en `SaveChanges`) + infra Testcontainers | ✅ 15/08/2026 — PostgreSQL local, build y 17 tests verdes |
| A3 | Auth: tablas `user`/`user_role`, hash, `POST /api/auth/session` → JWT, roles y políticas | ✅ 15/08/2026 — build y 20 tests verdes |
| A4 | Módulos por club (`club_module`), `GET /api/context`, gating 404, ProblemDetails, CORS, seed | ✅ 15/08/2026 — migración, HTTP real y 22 tests verdes |
| B | Schedules, Courts y People: agregados, GET/PUT masivos con xmin, búsqueda y ficha, endpoints y tests | ⬜ |
| C | Agenda y Bookings (exclusion constraint, servicios de dominio, 6 endpoints) + conexión del frontend (`http.ts` reemplaza `mockApi.ts`, se borra `store.ts`, login mínimo) | ⬜ |

Leyenda: ⬜ pendiente · 🚧 en curso · ✅ terminada (build + tests verdes).

---

## Entradas

### 15/08/2026 (9) — A4: módulos por club y borde HTTP.

**Qué se hizo:**

- Agregada la entidad `ClubModule` y la migración `AddClubModules`. La tabla `core.club_module`
  guarda módulos contratados por club con PK `(club_id, module_id)` y FK al registro de tenants.
- Implementado `TenantModulesProvider`: consulta los módulos del tenant actual mediante el filtro
  global y los cachea 30 segundos. Registrado el catálogo explícito de los seis módulos.
- Agregado middleware de resolución de tenant desde el claim JWT, `GET /api/context` autenticado,
  extensión de gating `RequireModule` que responde 404 y handler central para
  `ModuleDisabledException`. Configurada CORS para el backoffice local (`:5184`) y
  ProblemDetails.
- Agregado seed idempotente de Development: club Chaco For Ever, cierre completo de los seis
  módulos y usuario administrador de prueba. Sólo usa datos inventados de desarrollo.
- Pruebas de integración cubren contexto autenticado con los módulos habilitados, 401 sin token
  y preflight CORS. `dotnet build` pasó sin advertencias y `dotnet test --no-build` pasó: 15
  unitarios + 7 de integración. Contra PostgreSQL local, la API aplicó migraciones, permitió
  login y devolvió los seis módulos desde `/api/context`.

**Dónde quedó / próximo paso:** A4 queda ✅ y F0 está terminada. Siguiente fase: B, empezando
por los agregados y endpoints de Schedules y Courts, seguidos por People.

### 15/08/2026 (8) — A3: autenticación propia con JWT.

**Qué se hizo:**

- Agregados el agregado `User`, el catálogo `Role` de siete roles y la tabla dependiente
  `user_role`; email único por tenant, hash de contraseña y usuario activo. Generada la
  migración `AddUsers` en `core`.
- Agregado `IPasswordHasher` en Application y la implementación con `PasswordHasher<T>` de
  ASP.NET Core Identity en Infrastructure, sin incorporar el esquema ni el modelo de Identity.
- Implementados `POST /api/auth/session`, emisión JWT HS256 con `sub`, `tenant`, nombre y un
  claim de rol por cada asignación; vida útil de 12 horas y respuesta 401 genérica para club,
  usuario, contraseña o estado inválidos. Las cuatro políticas quedan registradas para usarlas
  al mapear los endpoints de A4/B.
- Pruebas: una unitaria de normalización de usuario y dos de integración. La sesión válida usa
  PostgreSQL real, hash real y verifica claims de tenant y rol; credenciales inválidas devuelven
  401. `dotnet build` pasó sin advertencias y `dotnet test --no-build` pasó: 15 unitarios + 5
  de integración.

**Dónde quedó / próximo paso:** A3 queda ✅. Siguiente fase: A4, módulos contratados por club,
`GET /api/context`, gating 404, ProblemDetails, CORS y seed de Development.

### 15/08/2026 (7) — A2 verificada contra PostgreSQL.

**Qué se hizo:**

- Iniciado Docker Desktop y levantado `postgres` con Docker Compose. El contenedor quedó
  saludable en `localhost:5432` con el volumen persistente configurado.
- `dotnet build` pasó sin advertencias ni errores.
- Iniciada temporalmente la API en Development contra PostgreSQL local: aplicó la migración y
  `GET /` respondió `200 Hello World!`.
- `dotnet test --no-build` pasó completo: 14 tests unitarios y 3 de integración con PostgreSQL
  real mediante Testcontainers.

**Dónde quedó / próximo paso:** A1 y A2 quedan ✅. Siguiente fase: A3, autenticación propia
con usuarios, roles, hash de contraseña, `POST /api/auth/session` y JWT.

### 15/08/2026 (6) — Entorno PostgreSQL local y finalización pendiente de A2.

**Qué se hizo:**

- Agregado `compose.yaml` en la raíz: PostgreSQL 17 Alpine, base `clubspot`, puerto 5432,
  volumen persistente, healthcheck y contraseña configurable mediante `.env` (plantilla
  `.env.example`).
- La API registra tenancy y persistencia desde `Program.cs`; toma
  `ConnectionStrings:ClubSpot` y aplica las migraciones de `CoreDbContext` sólo al iniciar en
  Development. Se habilitó User Secrets para reemplazar la cadena local sin versionarla.
- Documentado el flujo de desarrollo y la diferencia entre la base persistente local y el
  PostgreSQL descartable de Testcontainers en `README.md`.
- `docker compose config` y `dotnet build` pasaron. No se pudo levantar el servicio ni correr
  integración porque Docker Desktop sigue detenido o no configurado (no existe el pipe
  `dockerDesktopLinuxEngine`).

**Dónde quedó / próximo paso:** iniciar Docker Desktop y ejecutar `docker compose up -d
postgres`; luego `cd src/backend && dotnet test --no-build` y arrancar la API para comprobar
que aplique `InitialCore`. Si los 3 tests de integración pasan, marcar A2 ✅ y continuar con A3.

### 15/08/2026 (5) — Verificación de A1/A2 y migración inicial.

**Qué se hizo:**

- Generada `InitialCore` para `CoreDbContext` en
  `Infrastructure/Persistence/Migrations/Core/`; crea el esquema `core`, la tabla `club`, el
  índice único de `slug` y el check de `deposit_percent`.
- Corregidas las dependencias que impedían verificar: EF Core/Npgsql/EF Design se actualizaron
  a versiones 10.0.x compatibles y Testcontainers PostgreSQL a 4.14.0. Se fijaron EF Core y
  Relational 10.0.11 en IntegrationTests para evitar un conflicto con la versión transitiva.
  El fixture declara explícitamente `postgres:17-alpine`, requerido por la API actual de
  Testcontainers. La herramienta local `dotnet-ef` quedó en 10.0.11, alineada con EF.
- `dotnet build` pasó sin advertencias ni errores. Los tests unitarios pasaron: 14/14.
- Se ejecutó `dotnet test --no-build`: los 3 tests de integración no alcanzaron a correr porque
  Docker Desktop no está iniciado/configurado (no existe el pipe `docker_engine`). No hubo
  fallos funcionales reportados por esos tests.

**Dónde quedó / próximo paso:** A1 queda ✅. A2 queda pendiente exclusivamente de iniciar
Docker y volver a ejecutar `cd src/backend && dotnet test --no-build`; con los 3 tests de
integración verdes, marcar A2 ✅ y continuar con A3 (auth JWT).

### 15/08/2026 (4) — ADR-0006: código entero en inglés, casi sin comentarios. Backend reescrito.

**Decisión del usuario:** casi cero comentarios (sólo lo muy importante) y el código siempre
en inglés, comentarios incluidos. Quedó como [ADR-0006], que **reemplaza al ADR-0004**;
mensajes de excepción y nombres de tests también pasan a inglés por ser código.

**Qué se hizo:** reescritos los 20 archivos `.cs` del backend: doc-comments eliminados (quedan
sólo notas de una o dos líneas en inglés sobre invariantes no obvias), mensajes de excepción en
inglés, nombres de tests en inglés (`The_product_catalog_is_valid`). Los asserts que dependían
de mensajes en español se ajustaron. `DisplayName`/`Description` de los manifiestos siguen en
español (texto de UI). Actualizados `AGENTS.md` (§3 y §6), el índice de ADRs y la nota del plan.

**Dónde quedó / próximo paso:** igual que la entrada (3) — todo sin compilar; en la
verificación: scaffold de la migración inicial + `dotnet build` + `dotnet test`. Después A3.

### 15/08/2026 (3) — A2: persistencia + tenancy, código completo sin verificar.

**Qué se hizo:**

- **SharedKernel/Tenancy**: `ITenantOwned` (marca de entidad por tenant) +
  `TenantMismatchException` · `ITenantScopeFactory` (apertura de ámbitos) ·
  `AsyncLocalTenantContext` (implementa contexto y factory sobre `AsyncLocal`; vive en
  SharedKernel y no en Infrastructure porque no depende de nada, igual que `SystemClock`).
- **Domain/Core**: agregado `Club` — primera carpeta de módulo en Domain. Id = TenantId,
  slug único, zona horaria, moneda, `DepositPercent` (la seña, default 50 se define al crear).
  No es `ITenantOwned` a propósito: es el registro de tenants (lista blanca de un elemento).
- **Infrastructure/Persistence**: `ModuleDbContextBase` (filtro global por tenant vía
  `HasQueryFilter` sobre toda entidad `ITenantOwned` + guardia en `SaveChanges` que estampa el
  tenant en altas y lanza `TenantMismatchException` ante tenant ajeno; fuera de ámbito lanza
  `MissingTenantException`) · `TenantIdConverter` como convención · `CoreDbContext` (esquema
  `core`, tabla `club` con unique de slug y check de seña 0–100) · `CoreDbContextFactory`
  (design-time para `dotnet ef`) · extensiones de DI `AddClubSpotTenancy` /
  `AddClubSpotPersistence`.
- **Paquetes**: Infrastructure → `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.0 +
  `Microsoft.EntityFrameworkCore.Design` · IntegrationTests → `Testcontainers.PostgreSql`
  4.4.0 · manifiesto de herramientas `.config/dotnet-tools.json` con `dotnet-ef`.
  ⚠️ Versiones elegidas sin restore (no se compiló): ajustar si el restore las rechaza.
- **Tests**: unidad `AsyncLocalTenantContextTests` (sin ámbito lanza, anidado restaura, doble
  Dispose inocuo, no se filtra entre flujos async) · integración `PostgresFixture`
  (Testcontainers, requiere Docker) + `ClubPersistenceTests` (roundtrip por slug, slug único,
  check de seña impuesto por la base).
- Decisión chica: `Respawn` no se agrega todavía (recién hace falta cuando varios tests
  compartan datos, fase B). `ITransactionRunner` tampoco: recién en fase C.

**Dónde quedó / próximo paso:** falta **scaffoldear la migración inicial** — sin ella,
`MigrateAsync` del fixture no crea nada y los tests de integración fallan. En la verificación:
`cd src/backend && dotnet tool restore && dotnet ef migrations add InitialCore --project
src/Infrastructure/ClubSpot.Infrastructure --startup-project src/Api/ClubSpot.Api --context
CoreDbContext --output-dir Persistence/Migrations/Core`, después `dotnet build` + `dotnet test`
(integración necesita Docker). En verde: A1 y A2 ✅. Siguiente fase: A3 (auth JWT). Pendiente
menor arrastrado: tests de primitivas de SharedKernel (Money, Period, ClubCalendar).

### 15/08/2026 (2) — Reestructura por capas (ADR-0005) y creación de los ADRs.

**Decisión del usuario:** la estructura tiene que tener una capa **Application** explícita,
como su proyecto `anubis` (`C:\Users\dario\source\repos\anubis`), con los módulos separados
por carpetas y no por proyectos. Además pidió que las decisiones de arquitectura queden
**escritas en piedra en ADRs**.

**Qué se hizo:**

- Creado `docs/adr/` con índice y 5 ADRs: 0001 monolito modular con modularidad comercial por
  tenant (no plugins) · 0002 agenda calculada en lectura + exclusion constraint · 0003 auth
  tablas propias + JWT · 0004 identificadores en inglés · 0005 capas con Application, módulos
  como carpetas.
- Reestructura espejo de anubis: `src/backend/src/{Core,Infrastructure,Api,Tests}/`. Nuevos
  proyectos `ClubSpot.Domain` (vacío aún) y `ClubSpot.Application`; **eliminados** los 5
  proyectos `ClubSpot.Modules.*` y `ClubSpot.Jobs` (vacío). Manifiestos del catálogo movidos a
  `Application/Modularity/ProductModules.cs`. Referencias: Api → Application + Infrastructure;
  Infrastructure → Application; Application → Domain; Domain → SharedKernel. `ClubSpot.slnx`
  con carpetas de solución por capa. `ModuleCatalogTests` apunta a
  `ClubSpot.Application.Modularity`.
- Actualizados `AGENTS.md` (§2 tabla de docs, §4 árbol y reglas de frontera, §8 estado) y
  `README.md` (árbol y tabla de docs); nota 3 agregada al recuadro de actualización del plan
  (cómo leer el plan con la estructura nueva).
- Nota operativa: hubo que cerrar VS Code para mover las carpetas (el C# Dev Kit retiene
  handles sobre los proyectos).

**Dónde quedó / próximo paso:** A1 + reestructura con el código completo y **sin verificar**
(sigue en pie el pedido de no compilar). Cuando el usuario pida la verificación: `dotnet build`
+ `dotnet test` en `src/backend`; en verde, marcar A1 ✅ y continuar con A2 (persistencia +
tenancy), creando las carpetas de módulo `Core/` y `Bookings/` dentro de Domain y Application
según el plan §3 y la nota 3.

### 15/08/2026 — Arranca la implementación. A1 (renombres a inglés) con el código listo, sin verificar.

**Decisiones tomadas (por el usuario):**

1. **Identificadores en inglés, textos en español.** Clases, tablas, endpoints, proyectos e ids
   de módulo en inglés; comentarios, mensajes de error, nombres de tests y nombres comerciales
   en español. El mapa de traducción quedó en la nota de actualización del plan.
2. **Renombrar lo existente ahora** (ids de módulo incluidos): es el único momento barato, no
   hay nada persistido ni commits.
3. **Fases más chicas**: F0 se dividió en A1–A4; B = F1+F2; C = F3+F4.
4. **No compilar ni correr nada pesado sin pedirlo**: la verificación (build + tests) se hace
   completa cuando el usuario lo pida, no por cada bloque.

**Qué se hizo (A1):**

- Proyectos renombrados: `Modules.Clubes`→`Modules.Core`, `Modules.Finanzas`→`Modules.Finance`,
  `Modules.Reservas`→`Modules.Bookings`, `Modules.Futbol`→`Modules.Football` (carpetas y
  csproj; se borraron los `obj/` viejos). Actualizados `ClubSpot.slnx` y todas las referencias
  entre proyectos.
- `Periodo`→`Period` (archivo y struct). Ids de módulo: `nucleo`→`core`, `socios`→`members`,
  `finanzas`→`finance`, `reservas`→`bookings`, `futbol`→`football`; estáticos de `ModuleId` y
  manifiestos renombrados (`CoreModule`, `MembersModule`, `FinanceModule`, `BookingsModule`,
  `FootballModule`; `DisplayName` sigue en español). `ModuleCatalogTests` actualizado con los
  nombres nuevos, tests con nombres en español.
- Docs: regla de idioma nueva en `AGENTS.md` (§3 y §6), árbol y grafo de módulos en inglés en
  `AGENTS.md` §4/§5/§8/§9 y `README.md`, nota de actualización con el mapa de nombres y el
  corte de fases nuevo en el plan.

**Dónde quedó / próximo paso:** A1 tiene el código completo pero **sin verificar**: falta
`dotnet build` + `dotnet test` (el usuario pidió explícitamente no compilar todavía; se hará
una verificación completa cuando lo indique). Al verificar en verde, marcar A1 ✅ y seguir con
A2 (persistencia + tenancy) según el plan §3, previa confirmación del usuario.

### 14/08/2026 — Plan creado. La implementación NO arranca todavía.

**Qué se hizo:**

- Relevamiento completo de tres fuentes: el contrato del mock del frontend
  (`src/frontend/backoffice/src/api/mockApi.ts` + `domain/`), el esqueleto del backend
  (`src/backend`, SharedKernel completo, resto vacío) y los documentos de alcance/diseño.
- Escrito el plan completo en [`plan-backend-backoffice.md`](plan-backend-backoffice.md):
  arquitectura, modelos por módulo, los 19 endpoints, handlers/servicios/repositorios archivo
  por archivo, tests de unidad e integración, y el orden F0→F4.
- Movidos `alcance-socios-mvp.html` y `diseno-detallado-socios.html` de `docs/` a
  `docs/referencia-ourclub/` (pasan a ser material de consulta, no especificación que compita
  con el prototipo). Arregladas todas las referencias: `README.md`, `AGENTS.md` raíz (tabla §2
  y §7) y `docs/referencia-ourclub/AGENTS.md` (sección Precedencia).

**Decisiones tomadas (por el usuario):**

1. **Manda el mockup** sobre el diseño detallado donde divergen (Horario compartido + tarifa y
   reglas por cancha, en vez de Tarifa por tipo de espacio × franja × audiencia).
2. **Sin módulo finanzas**: cobro en la reserva, `deuda` como campo llano de Persona — stubs
   provisionales marcados.
3. **Auth con tablas propias + JWT** (ni Identity ni proveedor externo).
4. **La implementación no arranca todavía** — decisión explícita. Este plan queda escrito y se
   ejecuta cuando el usuario lo pida.

**Contexto útil para el que retome:**

- El backoffice frontend está terminado como cascarón contra mock (ver `AGENTS.md` §10) y fue
  recorrido entero en el navegador en esta misma fecha. Corre en `:5184`.
- El backend sólo tiene SharedKernel + manifiestos de módulo. No hay ni un paquete NuGet de
  EF/auth todavía.
- Los tests de integración van a necesitar Docker (Testcontainers con PostgreSQL real, por la
  exclusion constraint).

**Dónde quedó / próximo paso:** el plan está completo y aprobado en su contenido; **no empezar
a implementar sin pedido explícito del usuario**. Cuando lo pida, arrancar por la fase F0
(plataforma) siguiendo `plan-backend-backoffice.md` §3, y marcar F0 como 🚧 acá antes de tocar
código.
