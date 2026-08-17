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
| B | Schedules, Courts y People: agregados, GET/PUT masivos con xmin, búsqueda y ficha, endpoints y tests | 🚧 16/08/2026 — People completo; Schedules/Courts persistidos con concurrencia `xmin`, falta contrato final |
| R | **Remediación de modelado** (ADR-0008/0009/0010): sin módulos por deporte, sin `PreferredSport`, `Money` en tarifas, capas de bookings, `club_module` contratado, tablas en plural, un solo `DbContext` — ver [`plan-remediacion-modelado.md`](plan-remediacion-modelado.md) | ✅ 16/08/2026 — build sin warnings, 22 unitarios + 13 de integración verdes |
| C | Agenda y Bookings (exclusion constraint, servicios de dominio, 6 endpoints) + conexión del frontend (`http.ts` reemplaza `mockApi.ts`, se borra `store.ts`, login mínimo) | ⬜ **bloqueada hasta cerrar R** |

Leyenda: ⬜ pendiente · 🚧 en curso · ✅ terminada (build + tests verdes).

---

## Entradas

### 16/08/2026 — ADR-0013: el modelo de disponibilidad, con Calendly como referencia.

**Disparador:** el usuario preguntó si el modelo de horarios era escalable, señalando que
`courts` tiene un solo FK a `schedules` y que `specialDates` es jsonb. Planteó los casos
reales: "del 19 al 25 de junio la cancha 1 libre de 12 a 17", "del 17 al 24 la cancha 2 normal",
"de golpe digo: mañana está cerrada la cancha 3 por reparaciones" — y la analogía con Calendly,
donde el usuario final elige entre los huecos que quedan.

**Dos definiciones del usuario que simplificaron el modelo más que cualquier análisis:**

1. **"Siempre dibujo hacia adelante; lo que pasó, ya pasó."** Eliminó de un plumazo el
   versionado del patrón por vigencias, que era la parte más cara de la propuesta inicial.
2. **Los feriados se cargan a mano.** No son un concepto: son una excepción como cualquier
   otra. El usuario tuvo que repetirlo porque el asistente los siguió nombrando como categoría
   aparte después de haberlos colapsado.

**Correcciones del usuario a las propuestas intermedias:**

- Se había afirmado que no se podía cerrar una sola cancha ni programar cambios "sin reescribir
  el pasado". Era impreciso: cerrar una sola **se puede** duplicando el horario (con el costo de
  que se desincronice), y el pasado **no se reescribe** — se redibuja, porque la agenda se
  calcula en lectura. Lo único realmente imposible era programar un cambio a futuro.
- Las capturas de Calendly mostraron que las *date-specific hours* se aplican a un **conjunto de
  fechas** seleccionadas en un calendario, no a un rango. Se corrigió el modelo: cabecera de
  excepción + tabla de fechas.

**Qué quedó decidido** ([ADR-0013](adr/0013-disponibilidad-patron-semanal-mas-excepciones.md),
reflejado en `AGENTS.md` §9.5): patrón semanal reusable (sigue jsonb, que para eso está bien) +
`availabilityOverrides` con conjunto de fechas y alcance cancha o club · cerrar es una excepción
sin ventanas · la excepción reemplaza al patrón · **gana la más específica**, y a igual alcance
la más reciente · `courts.scheduleId` **se queda**: el problema no era el FK sino que la
excepción no tenía dónde apuntar · `schedules.timeZone` se va, la zona es del club.

**Abierto y anotado en el ADR:** el hold con TTL (ADR-0002 lo difirió asumiendo que no había
portal; con usuario final eligiendo huecos, esa premisa se cae) y la pantalla de excepciones
(fechas sueltas vs rangos), que no bloquea porque el modelo aguanta las dos.

**Dónde quedó / próximo paso:** ADR escrito, **sin tocar código todavía**. Lo que sigue es el
plan de implementación: sacar `specialDates` y `timeZone` de `schedules`, crear las dos tablas
nuevas y el cálculo de disponibilidad.

### 16/08/2026 — ADR-0012: cómo se componen los módulos y de quién es la persona.

**Disparador:** el usuario señaló las columnas `debtAmount`/`debtCurrency` en la tabla `people`
como mal diseño, y pidió que la regla de composición quedara escrita antes de seguir, para no
arrastrar el malentendido.

**Qué se entendió, en las palabras del usuario:** un cliente puede tener club + reservas —la
misma persona es socia, hace karate y el sábado alquila una cancha con amigos, que va por otro
lado—; otro puede tener **sólo reservas** de fútbol 5; otro puede tener club + reservas +
finanzas. Y la parte financiera puede estar o no.

**Corrección del usuario a la primera versión de la regla:** decir que el cliente de sólo
reservas era "invendible" era falso y además escondía lo importante. Lo que hay es
**capacidades de distinto tamaño**: un cliente puede pagar por cobrar el turno sin tener la
capacidad de hacer liquidaciones. O sea que `finance` como bloque único está mal cortado.

**Qué quedó decidido** ([ADR-0012](adr/0012-composicion-de-modulos-por-tenant.md), reflejado
en `AGENTS.md` §4, §5 y §6):

- El módulo es **la unidad más chica que se vende por separado**; el corte lo define lo que se
  vende, no el código.
- Dependencia dura es sólo "sin el otro el concepto no existe"; aprovechar ≠ depender.
- **La persona es una sola y es de `core`.** Ser socio, anotarse en una actividad, alquilar o
  deber plata son **vínculos** que guarda cada módulo contra `personId`. ⇒ **ningún módulo
  agrega columnas a `people`**.
- Ningún módulo asume el vínculo de otro; la integración es por contrato y opcional.

**Qué NO se hizo, por decisión explícita del usuario:** no se parte `finance` ni se define
todavía el concepto de **capacidades** ("más adelante vamos a definir capacidades; si el
cliente tiene capacidades financieras se habilitan o no ciertas features"). Por ahora se avanza
sobre reservas y la parte financiera va de la mano. Las flechas `members → finance` y
`bookings → finance` quedan marcadas como provisionales en `AGENTS.md` §4, sin tocar el
catálogo: cortar sin saber qué se vende sería adivinar.

**Deuda técnica reconocida:** `people.debtAmount`/`debtCurrency` viola la regla y queda como
stub marcado en `Person.cs`, apuntando al ADR. Es lo primero que el trabajo de finanzas tiene
que absorber. Nada nuevo se cuelga de ahí mientras tanto.

**Aclaración del usuario (16/08/2026, cierra una de las preguntas abiertas):** las
**actividades** —deportes dictados por profesores, con alumnos— **son parte del módulo de
club, no un módulo aparte**. Corrige la suposición contraria que había quedado anotada en el
ADR. Los alumnos "parten de personas", que además pueden pertenecer a grupos familiares: o
sea, profesor y alumno son **vínculos** sobre la misma `Person`, lo que confirma la regla 3 de
ADR-0012. Registrado en el ADR y en `AGENTS.md` §9.3. **No se implementa nada de esto ahora**:
el foco está en reservas de canchas.

**Dónde quedó / próximo paso:** reglas escritas, sin cambios de código más allá del comentario
del stub. Continuar con reservas.

### 16/08/2026 — Nombres físicos por convención (ADR-0011) y base de desarrollo recreada.

**Decisión del usuario:** los nombres que EF generaba solo se corrigen **en el código**, no a
mano en la base; la base de desarrollo es descartable, se tira y se recrea.

**Qué se hizo:**

- `ClubSpotDbContext` asigna los nombres de claves primarias, índices y foráneas en una pasada
  final sobre el modelo terminado: `pk<Tabla>`, `ix`/`ux<Tabla><Columnas>`, `fk<Tabla><Columnas>`.
  Se eliminaron los `HasDatabaseName` de las configuraciones para no tener dos fuentes de
  verdad. Registrado como [ADR-0011](adr/0011-convenciones-fisicas-de-postgresql.md), que
  además consolida en un solo lugar las convenciones que estaban sueltas (esquema, camelCase,
  plural, columnas en singular).
- Regenerada la migración única y recreada la base con `docker compose down -v`. **Verificado
  contra PostgreSQL real:** las 8 tablas en plural, y los 26 índices y constraints en camelCase
  (`pkPeople`, `ixPeopleTenantIdSearchName`, `uxCourtsTenantIdSportSortOrder`,
  `fkCourtsScheduleId`, `ckClubsDepositPercent`). El único nombre fuera de la convención es
  `PK___EFMigrationsHistory`, que crea EF para su propia tabla.
- La API aplicó migración y seed sobre la base limpia: 1 club, 1 usuario, **2 módulos
  contratados** (`bookings`, `members`) y el resto por cierre en lectura, como manda ADR-0009.
- `dotnet build` sin advertencias y `dotnet test` completo verde: 22 unitarios + 13 de
  integración.

**Reglas del día que quedaron escritas** (pedido del usuario de que no se pierdan): ADR-0008
(deporte como configuración), ADR-0009 (`club_module` contratado), ADR-0010 (un `DbContext`),
ADR-0011 (convenciones físicas); y en `AGENTS.md` §4 y §6: la Api no usa EF, `Money` también
para tarifas, la moneda la define el club, ids en `Guid` crudo, un concepto un tipo, enums
camelCase con converter por enum, y lo provisional se marca.

**Dónde quedó / próximo paso:** cerrado. Retomar B (contrato de configuración) y después C.

### 16/08/2026 — R cerrada: los 8 paquetes aplicados y verificados.

**Qué se hizo:** se ejecutaron los 8 paquetes de
[`plan-remediacion-modelado.md`](plan-remediacion-modelado.md) con agentes Sonnet
supervisados, más dos revisiones independientes (checklist y diff contra la spec).

- Deporte y módulos (ADR-0008), bookings por Application con agregados endurecidos, `Money` en
  las tarifas con la moneda del club, `club_module` contratado (ADR-0009), contexto completo
  con enums camelCase en el wire, tablas en plural, y un solo `ClubSpotDbContext` con una sola
  migración (ADR-0010).
- **Verificación:** `dotnet build` sin errores ni advertencias; `dotnet test` completo en
  verde — 22 unitarios y 13 de integración contra PostgreSQL real. Checklist de la spec
  completo: cero apariciones de `PreferredSport`, módulos por deporte, `DefaultCurrency`,
  contextos viejos, historiales por módulo y nombres de tabla en singular. La migración única
  crea las ocho tablas en plural y los endpoints de la Api ya no usan EF.

**Dos correcciones aplicadas sobre lo que dejaron los agentes:**

1. El converter global de enums camelCase también cambiaba las **claves del diccionario** de
   `weeklyRanges` (`"Monday"` → `"monday"`), cosa que la spec prohibía. Reemplazado por un
   converter por enum concreto, con nota en el código explicando por qué.
2. El helper nuevo de tests se iba a versionar bajo `src/backend/src/Tests/` (mayúscula)
   mientras el resto del proyecto está en el índice como `src/backend/src/tests/`. En un
   checkout case-sensitive habría partido el proyecto en dos carpetas. Registrado en minúscula.

**Dónde quedó / próximo paso:** R queda ✅. Sigue el cierre de nombres físicos (entrada de
arriba) y después B (contrato de configuración) y C.

### 16/08/2026 — Se elimina `docs/referencia-ourclub/`.

**Decisión del usuario:** el relevamiento de OurClub y los documentos de alcance y diseño
detallado se eliminan del repo porque venían confundiendo más de lo que aportaban: describían
**cómo lo hace un sistema ajeno** y estaban desfasados respecto del alcance vigente.

Los 29 archivos siguen en el historial de git (commit `9e2f079`). Se actualizaron `AGENTS.md`
§2 (la fuente de verdad pasa a ser: ADRs, plan y prototipo del backoffice) y `README.md`. Las
referencias que quedan en documentos históricos —ADR-0002, entradas viejas de esta bitácora y
el cuerpo del plan— **no se editan**: registran lo que era cierto cuando se escribieron.

### 16/08/2026 — Revisión de modelado: ADR-0008 y 0009, y arranque de la remediación (R).

**Contexto:** una revisión completa del modelado del backend encontró que el deporte estaba
representado tres veces sin conexión (dos enums `Sport` idénticos, módulos `padel`/`football`
sin comportamiento ni mapeo), tarifas de `Court` en `decimal` pelado contra la regla de
`Money`, la moneda con dos dueños (`Money.DefaultCurrency = "ARS"` vs `Club.Currency`),
`club_module` sin semántica definida (`Resolve` sólo en el seeder), Courts/Schedules usando EF
directo desde la Api mientras People pasa por Application, y stubs sin marcar.

**Decisiones del usuario:**

1. **No hay módulos por deporte** ([ADR-0008]): `bookings` se contrata una vez y cubre
   cualquier deporte; el deporte es configuración de la cancha. Cómo se configuran las canchas
   y a qué deporte pertenecen queda como diseño pendiente explícito. `Person` no tiene deporte
   preferido. No se renombra `Football` a `Football5`: es formato/presentación.
2. **Frenar el desarrollo nuevo** (fase C) hasta que estas reglas estén alineadas en el
   código.
3. Arreglar las inconsistencias restantes: plata, semántica de `club_module` ([ADR-0009],
   propuesta aceptada: la tabla guarda lo contratado, la habilitación se resuelve en lectura),
   capas según ADR-0005, stubs señalizados.
4. **Los nombres de tabla van en plural** (pedido durante la ejecución de la remediación):
   `clubs`, `users`, `userRoles`, `clubModules`, `people`, `personNotes`, `schedules`,
   `courts`. Se decidió sobre la marcha que índices y constraints sigan al nombre de la tabla,
   también en plural; las columnas quedan en singular. Convención fijada en `AGENTS.md` §6 y
   ejecutada como paquete 7 de la remediación.

**Qué se hizo:**

- Escritos [ADR-0008] y [ADR-0009]; actualizado el índice de ADRs.
- Actualizados `AGENTS.md` (grafo §4, reglas de frontera, §5 con ADR-0009, moneda en §6,
  estado §8, §9.6 reescrita, pendiente de frontend en §10) y `README.md` (grafo).
- Nota de actualización 16/08/2026 agregada al plan (sin módulos por deporte, sin
  `PersonaId`/`ReservaId` tipados — se estandariza `Guid`, tarifas `Money`, `club_module`).
- Escrito [`plan-remediacion-modelado.md`](plan-remediacion-modelado.md): 6 paquetes
  ejecutables con criterios de aceptación y checklist final, para ejecutar con un modelo más
  económico bajo supervisión.

**Dónde quedó / próximo paso:** ejecutar los paquetes 1–6 de la remediación y verificar
(build sin warnings + tests completos con Docker). Al cerrar R: recrear la base local
(`docker compose down -v && docker compose up -d postgres`), marcar R ✅ y recién entonces
retomar B/C.

[ADR-0008]: adr/0008-deporte-como-configuracion-no-modulo.md
[ADR-0009]: adr/0009-club-module-guarda-lo-contratado.md

### 16/08/2026 — Persistencia: esquema PostgreSQL único.

**Decisión del usuario:** las tablas físicas no se separan por módulo. ClubSpot usa sólo el
esquema estándar PostgreSQL `public`; `dbo` corresponde a SQL Server, no a PostgreSQL.

**Qué se hizo:**

- Registrado ADR-0007: la separación modular permanece en código, contratos y catálogo, no en
  esquemas de la base.
- `CoreDbContext` y `BookingsDbContext` ahora crean tablas en `public`. Conservan historiales
  EF separados, `__EFMigrationsHistoryCore` y `__EFMigrationsHistoryBookings`, para que los dos
  contextos puedan migrar de forma independiente sin colisionar.
- Regeneradas las migraciones iniciales de desarrollo. El token de concurrencia `xmin` sigue
  siendo una columna de sistema PostgreSQL, no una columna creada por la migración.
- Eliminado el volumen local autorizado y reconstruida la base. La API aplicó migraciones y
  seed; la inspección con PostgreSQL confirma que las 10 tablas actuales están en `public`.
- `dotnet test` pasó: 20 unitarios y 13 de integración.

**Dónde quedó / próximo paso:** DBeaver debe refrescar `clubspot > Schemas > public`. B sigue
en curso, pendiente de contrato y validaciones HTTP de configuración.

### 16/08/2026 — B: Schedules y Courts, primer bloque verificable.

**Qué se hizo:**

- Agregados `Schedule`, franjas semanales, fechas especiales, `Court` y sus invariantes básicas
  en el módulo `bookings`. Ambos usan filtro por tenant; horarios se persisten como JSONB con
  converter explícito, compatible con Npgsql 10.
- Creadas `BookingsDbContext`, su migración inicial y endpoints autenticados/gateados:
  `GET`/`PUT /api/schedules` y `GET`/`PUT /api/courts`.
- La FK de cancha a horario usa `RESTRICT`; el reemplazo masivo de horarios detecta el caso y
  devuelve 409 en vez de producir un 500.
- Pruebas de dominio y de integración verifican JSONB real y los dos endpoints. Validación HTTP
  manual contra PostgreSQL local: login, creación y lectura de horario/cancha correctas.
- `dotnet build` y `dotnet test --no-build` pasaron: 20 unitarios y 11 de integración.

**Dónde quedó / próximo paso:** B sigue en curso. Falta incorporar `xmin`/409 de concurrencia y
terminar el contrato de configuración, antes de conectar estas pantallas en la fase C.

### 16/08/2026 — B: concurrencia optimista de Horarios y Canchas.

**Qué se hizo:**

- `Schedule` y `Court` usan la columna de sistema PostgreSQL `xmin` como token de concurrencia
  de EF Core. No se agrega una columna física: la migración sólo actualiza el modelo que EF usa
  para conocer el token.
- Los `GET` exponen `version`; los `PUT` requieren esa versión para modificar un registro
  existente. Una versión ausente o incongruente se rechaza; si PostgreSQL detecta que la fila
  cambió tras la lectura, la API responde 409.
- Pruebas de integración HTTP cubren ambos casos: una primera escritura con la versión leída
  pasa y una segunda escritura con esa versión ya vencida devuelve 409.
- `dotnet test` pasó: 20 unitarios y 13 de integración.

**Dónde quedó / próximo paso:** falta terminar el contrato de configuración y sus validaciones
HTTP antes de cerrar B. La concurrencia optimista de los editores ya está resuelta.

### 16/08/2026 — Convención física PostgreSQL: camelCase y reset de desarrollo.

**Decisión del usuario:** toda la base PostgreSQL usa camelCase en sus identificadores físicos,
incluidos tablas, columnas, índices, constraints y la tabla de historial de EF. No existe una
decisión previa que justificara snake_case; la nomenclatura había sido introducida como default
de implementación.

**Qué se hizo:**

- Corregidos mappings de los módulos `core` y `bookings`, incluido `authorUserId` y los nombres generados
  explícitamente para índices y constraints.
- Eliminadas las migraciones de desarrollo no versionadas y regeneradas dos iniciales completas:
  `InitialCore` y `InitialBookings`.
- Por confirmación explícita, eliminado el volumen local `clubspot-postgres` y reconstruida la
  base con Docker Compose. La API aplicó ambos esquemas y el seed desde cero; el login de
  desarrollo respondió correctamente.

**Dónde quedó / próximo paso:** convención fijada en `AGENTS.md`; toda base local existente debe
recrearse con `docker compose down -v` y `docker compose up -d postgres` antes de iniciar la
API. Retomar B con las pruebas HTTP de Schedules y Courts.

### 16/08/2026 — B: People.

**Qué se hizo:**

- Agregados `Person` y `Note` al módulo `core`, con búsqueda normalizada sin acentos, teléfono sólo dígitos, bloqueo, notas y el stub temporal de deuda/pago.
- Persistidos en `core.person` y `core.person_note` con filtro global de tenant, índices de búsqueda y migración `AddPeople`.
- Implementados repositorio, consultas paginadas de 14, los endpoints `/api/people`, políticas existentes de personas y gating 404 del módulo `core`.
- Agregadas pruebas unitarias del agregado y una prueba HTTP de alta, búsqueda, nota, bloqueo y pago.

**Dónde quedó / próximo paso:** People queda implementado y verificado. Schedules y Courts siguen fuera de este bloque; continuar B respetando el trabajo concurrente sobre esas áreas.

---

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
