# Plan — Backend para el backoffice (Reservas · Canchas · Horarios · Personas)

> **Estado de avance:** ver [`plan-backend-backoffice.bitacora.md`](plan-backend-backoffice.bitacora.md).
> Antes de retomar este plan, leer la bitácora: dice qué fase está en curso y dónde quedó.

Fecha del plan: 14/08/2026. Actualizado el 15/08/2026 (idioma y fases — ver recuadro).

> **Actualización 15/08/2026 — dos decisiones del usuario que modifican este plan:**
>
> **1. Identificadores en inglés, textos en español.** Todo identificador de código —clases,
> métodos, tablas, columnas, endpoints, proyectos, ids de módulo— va en inglés; comentarios,
> mensajes de error, nombres de tests y nombres comerciales siguen en español. El plan está
> escrito con los nombres en español; al implementar se traducen así:
>
> | En el plan | En el código | | En el plan | En el código |
> |---|---|---|---|---|
> | `Persona` | `Person` | | módulo `nucleo` | `core` |
> | `Nota` | `Note` | | módulo `socios` | `members` |
> | `Usuario` / `Rol` | `User` / `Role` | | módulo `finanzas` | `finance` |
> | `Cancha` | `Court` | | módulo `reservas` | `bookings` |
> | `Horario` / `Tramo` | `Schedule` / `TimeRange` | | módulo `futbol` | `football` |
> | `Reserva` | `Booking` | | `/api/personas` | `/api/people` |
> | `Deporte` | `Sport` | | `/api/canchas` | `/api/courts` |
> | `Periodo` | `Period` | | `/api/horarios` | `/api/schedules` |
> | esquema `nucleo` | `core` | | `/api/reservas` | `/api/bookings` |
> | esquema `reservas` | `bookings` | | `/api/contexto` | `/api/context` |
>
> Los proyectos ya fueron renombrados: `Modules.Clubes` → `Modules.Core`, `Modules.Finanzas` →
> `Modules.Finance`, `Modules.Reservas` → `Modules.Bookings`, `Modules.Futbol` →
> `Modules.Football`.
>
> *Ajuste posterior del mismo día ([ADR-0006](adr/0006-codigo-entero-en-ingles-casi-sin-comentarios.md)):
> el código va **entero** en inglés — también comentarios, mensajes de excepción y nombres de
> tests — y con casi cero comentarios. Donde este plan diga nombres de tests en español,
> leerlos en inglés.*
>
> **2. Fases más chicas.** Las fases F0–F4 de la sección 8 se reagrupan así (el contenido no
> cambia, sólo el corte): **A1** renombres a inglés · **A2** persistencia + tenancy · **A3**
> auth JWT · **A4** módulos por club + borde HTTP (juntas equivalen a F0) · **B** = F1 + F2
> (Schedules, Courts, People) · **C** = F3 + F4 (agenda, reservas y conexión del frontend).
> El estado por fase se lleva en la bitácora.
>
> **3. Arquitectura por capas** ([ADR-0005](adr/0005-capas-con-application-modulos-como-carpetas.md)):
> las capas son proyectos (`ClubSpot.SharedKernel` / `Domain` / `Application` /
> `Infrastructure` / `Api`), los módulos son **carpetas** dentro de Domain y Application.
> Donde este plan dice "proyecto `ClubSpot.Modules.X`" leer "carpeta `X/` en Domain y
> Application"; los handlers y las interfaces de repositorio van en `Application/<módulo>/`,
> los agregados y servicios de dominio en `Domain/<módulo>/`. Los proyectos `Modules.*` y
> `Jobs` ya no existen.

## 1. Contexto

El backoffice (`src/frontend/backoffice/`) corre entero contra un mock en memoria. Este plan
define el backend .NET que lo soporta de verdad: estructuras, modelos, APIs, handlers,
servicios, repositorios y tests, para las cuatro pantallas **Reservas, Canchas, Horarios y
Personas**. La app es multi-tenant: cada club tiene sus propios usuarios de sistema con roles.

**El contrato a implementar es `src/frontend/backoffice/src/api/mockApi.ts`.** Cuando el
backend exista, ese archivo se reemplaza por llamadas HTTP y `store.ts` se borra.

### Decisiones vinculantes (del usuario, 14/08/2026)

1. **Manda el mockup.** Donde `docs/referencia-ourclub/diseno-detallado-socios.html` modela
   distinto (Tarifa por tipo de espacio × franja × audiencia, GrillaHoraria por espacio), gana
   el modelo del prototipo: entidad `Horario` compartida entre canchas + precio / incremento /
   aviso mínimo / umbral noche **por cancha**. La tarifa por audiencia (socio / no socio) queda
   para cuando exista el módulo `socios`.
2. **Sin módulo finanzas en este plan.** El estado de cobro (`nada` / `sena` / `total`) vive en
   la reserva; `deuda` es un campo llano de `Persona`; registrar pago la pone en 0. Son **stubs
   provisionales, marcados como tales en el código**: cuando exista finanzas pasan a cuenta
   corriente append-only y saldo derivado.
3. **Auth con tablas propias + JWT.** Ni ASP.NET Identity ni proveedor externo.

## 2. Decisiones de arquitectura

- **Agenda calculada en lectura, sin turnos materializados.** La grilla del día se computa al
  leer: tramos del `Horario` (la fecha especial pisa el día semanal entero) + configuración de
  la `Cancha` + reservas confirmadas del día. La doble venta la impide una **exclusion
  constraint de PostgreSQL** (`btree_gist`) sobre `(tenant, cancha, fecha, rango de minutos)`:
  punto de serialización atómico sin fila de turno. El diseño detallado exige turnos
  materializados (J5) para el flujo futuro de portal con hold+TTL; esta decisión deja la puerta
  abierta — cuando llegue el portal se agrega el estado de hold sin romper nada de esto.
- **Tenancy.** `TenantId` viaja en un claim del JWT. Un middleware lo resuelve y abre un ámbito
  `AsyncLocal`; `ITenantContext.Current` sigue lanzando fuera de ámbito (comportamiento
  existente, a propósito). Filtro global de EF en toda entidad tenant-owned + guardia en
  `SaveChanges` (estampa el tenant en las altas, lanza ante tenant ajeno). Única lectura sin
  filtro: la tabla `club`, que *es* el registro de tenants — lista blanca de un elemento,
  documentada en el código.
- **Gating por módulo.** `ITenantModules` se implementa leyendo los módulos contratados
  persistidos por club (cache 30 s). Cada grupo de endpoints lleva un `IEndpointFilter` que
  responde **404** si el módulo está apagado; la agenda además verifica el módulo del deporte
  pedido. El dominio jamás pregunta por módulos.
- **Errores.** ProblemDetails (RFC 7807): 422 reglas de negocio (código de regla en
  `extensions`), 409 conflictos (solapamiento, concurrencia optimista, horario con canchas),
  404 (no existe / módulo apagado), 401/403 auth. Un `IExceptionHandler` central mapea las
  excepciones de dominio.
- **Capas.** Endpoint (Minimal API en `ClubSpot.Api`) → handler por caso de uso (clase sellada
  en el proyecto del módulo) → repositorio con interfaz en el módulo e implementación EF en
  `ClubSpot.Infrastructure`. La lógica pura (arranques, precios, agenda, máquina de estados) va
  en servicios de dominio del módulo, testeables sin EF. Sin MediatR. Los DTOs de wire viven en
  `ClubSpot.Api/Contracts`; el mapeo `Money` → decimal se hace ahí.
- **Sin jobs.** Ninguna de las cuatro pantallas necesita background. No se agrega Hangfire.

## 3. Fase F0 — Plataforma

### Paquetes

- `ClubSpot.Infrastructure`: `Npgsql.EntityFrameworkCore.PostgreSQL`,
  `Microsoft.Extensions.Identity.Core` (sólo por `PasswordHasher<T>`),
  `Microsoft.EntityFrameworkCore.Design`.
- `ClubSpot.Api`: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.OpenApi`.
- `ClubSpot.IntegrationTests`: `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `Respawn`.

### Persistencia — un DbContext por módulo

- `NucleoDbContext` → esquema `nucleo` (club, club_modulo, persona, persona_nota, usuario, usuario_rol).
- `ReservasDbContext` → esquema `reservas` (cancha, horario, reserva).

Cada contexto tiene su carpeta de migraciones (`Infrastructure/Migrations/Nucleo`,
`Migrations/Reservas`) y su tabla de historial propia. Se aplican en orden de grafo: nucleo
antes que reservas. Ambos comparten la **misma `NpgsqlConnection` scoped**, lo que permite una
transacción única entre módulos vía `ITransactionRunner` (interfaz en SharedKernel,
implementación en Infrastructure) — necesaria para "crear persona + crear reserva" atómico.

**Excepción de lectura auditada:** `PersonasQueries` (Infrastructure) cruza los esquemas
`nucleo` y `reservas` para los contadores derivados de la lista de personas (turnos jugados,
última vez) con filtro y paginación en SQL. Regla: **los read models pueden cruzar esquemas
dentro de Infrastructure; las escrituras jamás.**

### Tenancy

- SharedKernel nuevo: `Tenancy/ITenantScopeFactory.cs` (`ITenantScope BeginScope(TenantId)`) y
  `Tenancy/ITenantOwned.cs` (`TenantId TenantId`).
- Infrastructure: `Tenancy/AsyncLocalTenantContext.cs` — implementa `ITenantContext` +
  `ITenantScopeFactory` sobre `AsyncLocal<TenantId?>`, singleton.
- Api: `Tenancy/TenantResolutionMiddleware.cs` — lee el claim `tenant` del JWT y abre el ámbito
  por request. Jobs y tests abren ámbito explícito con la factory.
- `ModuleDbContextBase`: convención que aplica `HasQueryFilter` a toda entidad `ITenantOwned` y
  un override de `SaveChangesAsync` que estampa el tenant en las altas y lanza ante una entidad
  con tenant ajeno.

### Tenant y módulos contratados

- Entidad `Club` (tabla `nucleo.club`): `Id` (= TenantId), `Slug` (único), `Nombre`, `Sede`,
  `ZonaHoraria` (default Argentina), `Moneda` (default ARS), `SenaPorcentaje` (default 50),
  `CreadoEn`.
- `nucleo.club_modulo (club_id, modulo_id, contratado_en)` — PK compuesta. Contratar guarda el
  **cierre transitivo** que devuelve `ModuleCatalog.Resolve`, así la tabla nunca miente.
- `Modularity/TenantModulesProvider.cs` (Infrastructure) implementa `ITenantModules` con
  `IMemoryCache` (TTL 30 s por tenant).
- `Modularity/RequireModuleFilter.cs` (Api) + extensión `RequireModule(ModuleId)` sobre
  `RouteGroupBuilder` → 404.

### Autenticación y roles

- `Usuario`: id, tenant_id, email (único por tenant), nombre, hash_password, activo, creado_en.
  `usuario_rol (usuario_id, rol)`. Hash con `PasswordHasher<Usuario>` envuelto en
  `IPasswordHasher` propio del módulo (el módulo no referencia ASP.NET).
- Enum `Rol { Administrador, MesaDeSocios, Tesoreria, RecepcionCanchas, ControlDeAcceso,
  Profesor, Socio }` — el catálogo completo de los 7 roles del diseño, persistido como texto.
  Los roles sin permisos mapeados hoy quedan como catálogo extensible.
- Login: `club (slug) + email + password` → JWT con claims `sub`, `tenant`, `name` y un claim
  por rol. TTL 12 h (una jornada de mostrador); sin refresh token en este alcance. 401 genérico
  (no filtra existencia de club ni de usuario).
- Políticas en `Api/Auth/Permisos.cs`:

| Política | Roles |
|---|---|
| `personas.ver` | Administrador, MesaDeSocios, RecepcionCanchas |
| `personas.gestionar` | Administrador, MesaDeSocios |
| `agenda.operar` | Administrador, RecepcionCanchas |
| `configuracion.editar` | Administrador |

### Composición, CORS, OpenAPI, seed

- DI por extensiones explícitas: `AddClubSpotPersistence` / `AddClubSpotTenancy` /
  `AddClubSpotAuth` / `AddModuloNucleo` / `AddModuloReservas`, y mapeo `MapAuth` / `MapContexto`
  / `MapPersonas` / `MapAgenda` / `MapReservas` / `MapCanchas` / `MapHorarios`. Sin hook de
  reflexión sobre `IClubModule`: con dos módulos con datos, explícito es más legible.
- CORS: política para `http://localhost:5184` (el JWT va en `Authorization`, sin cookies).
- OpenAPI code-first con `AddOpenApi()` — queda anotada la pregunta abierta de AGENTS.md §9.1
  sobre contract-first.
- `Api/Seed/DevSeeder.cs` (sólo Development, idempotente): club Chaco For Ever con los 6
  módulos, un usuario administrador y uno de recepción, los horarios y canchas de fábrica del
  mock, ~40 personas **inventadas** y reservas de ejemplo. **Nunca datos reales del
  relevamiento** (regla de `docs/referencia-ourclub/AGENTS.md`).

**Verificable al terminar F0:** compila; tests de tenancy/auth/módulos verdes; login por curl
devuelve token; `GET /api/contexto` lista los módulos contratados.

## 4. Modelos por módulo

### SharedKernel (agregar)

| Archivo | Qué es |
|---|---|
| `Personas/PersonaId.cs` | id tipado (uuid, `From` valida) |
| `Personas/IPersonasDelClub.cs` | contrato Clubes→Reservas: `ObtenerResumen(PersonaId)` → { Existe, Nombre, Tel, Bloqueada } y `CrearExpress(nombre, deporte)` → PersonaId — el alta de mostrador dentro de crear reserva, para que el turno nunca quede huérfano |
| `Primitives/Deporte.cs` | enum { Padel, Futbol }, vocabulario compartido |
| `Tenancy/ITenantScopeFactory.cs` · `Tenancy/ITenantOwned.cs` · `Persistence/ITransactionRunner.cs` | plataforma (§3) |

### Módulo nucleo (`ClubSpot.Modules.Clubes`) — esquema `nucleo`

**`Persona`** (agregado):

| Campo | Tipo | Notas |
|---|---|---|
| Id | PersonaId (uuid) | PK |
| TenantId | TenantId | filtro global |
| Nombre | string(120) | requerido, trim |
| NombreBusqueda | string(120) | lower sin acentos, mantenido por el agregado; índice `(tenant, nombre_busqueda)` |
| Tel | string(30) | como se tipeó |
| TelDigitos | string(20) | sólo dígitos, mantenido por el agregado; índice — la búsqueda numérica busca sólo acá |
| Email | string(200) | lower para búsqueda |
| Origen | enum { App, Mostrador } (texto) | |
| Deporte | Deporte (texto) | deporte preferido |
| Bloqueada | bool | |
| Deuda | Money | **provisional** (§8) |
| AltaEn | timestamptz | del `IClock`; el "14 ago 2026" lo formatea el frontend |
| CreadaPor | uuid? | UsuarioId |

Métodos: `Bloquear()` / `Desbloquear()`, `AgregarNota(texto, usuarioId, clock)`,
`RegistrarPago()` (deuda → 0, devuelve lo pagado). Los contadores `turnos` y `ultima` **no se
almacenan**: se derivan en lectura desde las reservas.

**`Nota`** (tabla `persona_nota`): id, tenant_id, persona_id FK, texto (500),
autor_usuario_id, creada_en. El nombre del autor sale por join con usuario al leer; el
"· ahora" lo formatea el frontend.

**`Usuario` / `usuario_rol` / `Club`**: ver §3.

### Módulo reservas (`ClubSpot.Modules.Reservas`) — esquema `reservas`

**`Horario`** (agregado): id (uuid), tenant_id, nombre (80), zona_horaria (informativa),
`tramos_semanales` **jsonb** (`Dictionary<DayOfWeek, Tramo[]>` con `Tramo(AperturaMin,
CierreMin)`), `fechas_especiales` **jsonb** (`FechaEspecial(DateOnly, Tramo[])`; lista vacía =
cerrado; la fecha pisa el día semanal entero). Jsonb porque el horario se lee y escribe siempre
entero (editor con PUT masivo) y nunca se consulta por tramo. Invariantes en el agregado:
cierre > apertura, tramos sin superposición (la regla `tramoMalo` del frontend), fechas sin
duplicados. Concurrencia optimista por `xmin`. Método `TramosDelDia(DateOnly, DayOfWeek)`.

**`Cancha`** (agregado): id (uuid), tenant_id, deporte, orden (el `ci` del mock; único por
`(tenant, deporte, orden)`), nombre, detalle, techada, activa, horario_id FK **`ON DELETE
RESTRICT`** (la base impide borrar un horario con canchas — la invariante que la UI valida con
cartel), duraciones `integer[]` con check de no-vacío, incremento_min, aviso_min, precio_dia
Money, precio_noche Money, noche_desde_min. `xmin`.

**`Reserva`** (agregado):

| Campo | Tipo | Notas |
|---|---|---|
| Id | ReservaId (uuid) | PK |
| TenantId, CanchaId | | |
| PersonaId | PersonaId | FK por valor entre esquemas, **sin navegación EF**; la constraint física se agrega por SQL crudo en la migración de reservas. Existencia y bloqueo se validan vía `IPersonasDelClub` en el handler |
| Numero | bigint | secuencia `reservas.reserva_numero_seq`; se presenta "TRN-{numero}" |
| Fecha | date | día en hora del club (`ClubCalendar`) |
| InicioMin, DuracionMin | smallint | minutos desde medianoche |
| Precio, Sena | Money | congelados al crear |
| Cobro | enum { Nada, Sena, Total } (texto) | `Saldo` es derivado |
| Estado | enum { Confirmada, Cancelada } (texto) | |
| Ausente | bool | sólo alternable en Confirmada |
| CreadaEn/CreadaPor, CanceladaEn/CanceladaPor | | auditoría mínima de transición |

Máquina de estados de este alcance: nace `Confirmada` (sin hold/TTL) → `Cancelada` (final).
`Cobrar()` y `MarcarAusencia(bool)` sólo en Confirmada; `Cancelar()` sólo una vez.

Constraints e índices:

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;
ALTER TABLE reservas.reserva ADD CONSTRAINT ex_reserva_solapamiento
  EXCLUDE USING gist (
    tenant_id WITH =, cancha_id WITH =, fecha WITH =,
    int4range(inicio_min, inicio_min + duracion_min) WITH &&
  ) WHERE (estado = 'confirmada');
-- ix_reserva_dia (tenant_id, fecha, cancha_id)          → agenda
-- ix_reserva_persona (tenant_id, persona_id, fecha DESC) → historial y contadores
```

La constraint es la garantía atómica; el pre-chequeo del handler existe sólo para dar un
mensaje amable, y la violación (23P01) se traduce a 409.

**Servicios de dominio puros** (en el módulo, sin EF — portados de `domain/horarios.ts`,
`domain/agenda.ts` y `domain/dinero.ts` del frontend):

- `Servicios/CalculadoraDePrecios.cs` — tarifa nocturna si `inicio >= noche` (evaluada sólo
  sobre el arranque, como el mock); `base × dur / 60` redondeado a la centena; seña = porcentaje
  del club redondeado a la centena. Todo en `Money`.
- `Servicios/ReglasDeReserva.cs` — valida: cancha activa · duración habilitada · arranque
  múltiplo del incremento (anclado a medianoche) · turno completo dentro de **un** tramo (no
  cruza tramos contiguos) · no en el pasado · aviso mínimo si es hoy · persona no bloqueada.
  Cada regla con su código para el 422.
- `Servicios/ArmadoDeAgenda.cs` — el `columnaDe` del mock sin el hash: recorre 08:00–24:00 en
  filas de 30 min, emite SlotOcupado desde las reservas reales y SlotLibre con
  `span`/`cerrado`/`offGrid` desde horario + incremento; cancha inactiva = columna cerrada
  entera; calcula turnos / ocupación / porCobrar.

## 5. Endpoints — mapeo completo del contrato

Todos con JWT salvo el login. Reservas/agenda/canchas/horarios detrás de
`RequireModule(Reservas)`; personas detrás de `RequireModule(Nucleo)`.

| Operación del mock | Endpoint | Notas / errores | Política |
|---|---|---|---|
| — (no existía) | `POST /api/auth/sesion` `{club, email, password}` → `{token, operador}` | 401 genérico | anónimo |
| `fetchClub` + capacidades | `GET /api/contexto` → club, operador, roles, **módulos** | | autenticado |
| `fetchPersonas` | `GET /api/personas?q&filtro&pagina` → página de 14 + contadores (padron, atencion, deudaTotal, totales) | dígitos ⇒ sólo teléfono; texto ⇒ nombre+email sin acentos; filtro inválido 400 | personas.ver |
| `fetchFicha` | `GET /api/personas/{id}` → persona + historial tipado desde reservas reales | 404 | personas.ver |
| `crearPersona` | `POST /api/personas` → 201 | 422 validación | personas.gestionar |
| `bloquearPersonas` | `POST /api/personas/bloqueos` `{ids, bloqueado}` → `{afectadas}` | | personas.gestionar |
| `alternarBloqueo` | `PUT /api/personas/{id}/bloqueo` `{bloqueado}` — estado deseado, idempotente | 404 | personas.gestionar |
| `agregarNota` | `POST /api/personas/{id}/notas` → 201 `{texto, autorNombre, creadaEn}` | texto requerido ≤ 500 | personas.gestionar |
| `registrarPago` | `POST /api/personas/{id}/pagos` → `{pagado}` — **stub provisional** | 404 | personas.gestionar |
| `fetchAgenda` | `GET /api/agenda?deporte&fecha=YYYY-MM-DD` → columnas con `canchaId` + `duraciones`; slots con `reservaId`, `codigo`, `personaId`, `personaNombre`, `tel`, `cobro`, `precio`, `saldo`, `ausente` | 404 si el módulo del deporte está apagado; fecha inválida 400 | agenda.operar |
| `fetchPresupuesto` | `GET /api/canchas/{canchaId}/presupuesto?fecha&inicio&duracion` → `{precio, sena}` | 404 cancha | agenda.operar |
| `crearReserva` | `POST /api/reservas` `{canchaId, fecha, inicio, duracion, personaId \| nuevaPersona: {nombre}, cobro}` → 201 ReservaDto | 422 por regla (con código); **409 solapamiento** (exclusion constraint) | agenda.operar |
| `cobrarTurno` | `POST /api/reservas/{id}/cobro` → ReservaDto (cobro = total, saldo 0) | 404; 422 cancelada | agenda.operar |
| `cancelarTurno` | `POST /api/reservas/{id}/cancelacion` → 200 | 404; 422 repetida | agenda.operar |
| `alternarAusencia` | `PUT /api/reservas/{id}/ausencia` `{ausente}` → `{ausente}` | 404; 422 cancelada | agenda.operar |
| `fetchCanchas` | `GET /api/canchas` → CanchaDto[] (con `id` y `version`) | | configuracion.editar |
| `guardarCanchas` | `PUT /api/canchas` — id null = alta; ausente = baja | 422 (≥1 duración, incremento > 0, horario existente, precios ≥ 0); 409 baja con reservas futuras confirmadas; 409 versión vieja (xmin) | configuracion.editar |
| `fetchHorarios` | `GET /api/horarios` → HorarioDto[] (con `id` y `version`) | | configuracion.editar |
| `guardarHorarios` | `PUT /api/horarios` → `{horarios, idsAsignados}` — mapea los ids temporales que inventa el frontend a uuids reales | 422 tramos (≥1 horario, sin superposición, cierre > apertura); 409 eliminar horario con canchas (RESTRICT); 409 versión vieja | configuracion.editar |

**Cambios de contrato respecto del mock** (aceptados; `mockApi.ts` se reemplaza): ids reales en
vez de `(deporte, ci)` y de `dateIdx` (el frontend ya tiene `isoDe`); timestamps en vez de
strings pre-formateados (`ultima`, `alta`, `autor · ahora` — el formateo relativo pasa al
frontend); `cobrarTurno` deja de mandar `datos` (el servidor ya lo sabe); `version` para
concurrencia optimista en los editores.

## 6. Archivos concretos por proyecto

### `ClubSpot.Modules.Clubes/`
- `Personas/`: `Persona.cs`, `Nota.cs`, `Origen.cs`, `BusquedaDePersonas.cs` (regla pura:
  dígitos ⇒ sólo teléfono; texto ⇒ nombre+email sin acentos), `IPersonasRepository.cs`,
  `IPersonasQueries.cs`, `PersonasDelClub.cs` (implementa `IPersonasDelClub`).
- `Personas/Handlers/`: `BuscarPersonasHandler`, `ObtenerFichaHandler`, `CrearPersonaHandler`,
  `BloquearPersonasHandler`, `AgregarNotaHandler`, `RegistrarPagoHandler`.
- `Usuarios/`: `Usuario.cs`, `Rol.cs`, `IUsuariosRepository.cs`, `IPasswordHasher.cs`.
- `Configuracion/`: `Club.cs`, `IClubesRepository.cs`.

### `ClubSpot.Modules.Reservas/`
- `Horarios/`: `Horario.cs`, `Tramo.cs`, `FechaEspecial.cs`, `HorarioId.cs`.
- `Canchas/`: `Cancha.cs`, `CanchaId.cs`.
- `Reservas/`: `Reserva.cs`, `ReservaId.cs`, `EstadoReserva.cs`, `EstadoDeCobro.cs`,
  `ReglaDeReservaException.cs`.
- `Servicios/`: `CalculadoraDePrecios.cs`, `ReglasDeReserva.cs`, `ArmadoDeAgenda.cs`.
- Interfaces: `ICanchasRepository.cs`, `IHorariosRepository.cs`, `IReservasRepository.cs`
  (incluye `ReservasDelDia(fecha, canchas)` y `DePersona(personaId)`).
- `Handlers/`: `ObtenerAgendaHandler`, `CalcularPresupuestoHandler`, `CrearReservaHandler`
  (usa `IPersonasDelClub` + `ITransactionRunner`; las invariantes viven en `Reserva` /
  `ReglasDeReserva`, el handler sólo orquesta), `CobrarReservaHandler`,
  `CancelarReservaHandler`, `MarcarAusenciaHandler`, `ObtenerCanchasHandler`,
  `GuardarCanchasHandler`, `ObtenerHorariosHandler`, `GuardarHorariosHandler`.

### `ClubSpot.Infrastructure/`
- `Persistence/`: `NucleoDbContext.cs`, `ReservasDbContext.cs`, `ModuleDbContextBase.cs`
  (filtro y guardia de tenant), `MoneyConverter.cs`, `IdConverters.cs`,
  `NpgsqlTransactionRunner.cs`, `DesignTimeFactories.cs`.
- `Repositorios/`: `PersonasRepository.cs`, `PersonasQueries.cs` (lectura cruzada auditada),
  `UsuariosRepository.cs`, `ClubesRepository.cs`, `CanchasRepository.cs`,
  `HorariosRepository.cs`, `ReservasRepository.cs`.
- `Tenancy/AsyncLocalTenantContext.cs` · `Modularity/TenantModulesProvider.cs` ·
  `Auth/AspNetPasswordHasher.cs` · `DependencyInjection/ServiceCollectionExtensions.cs`.
- `Migrations/Nucleo/…` y `Migrations/Reservas/…` (una inicial por módulo; la de reservas
  incluye el SQL crudo de `btree_gist`, `ex_reserva_solapamiento` y `fk_reserva_persona`).

### `ClubSpot.Api/`
- `Program.cs` (composición completa) · `Auth/JwtEmisor.cs`, `Auth/Permisos.cs` ·
  `Tenancy/TenantResolutionMiddleware.cs` · `Modularity/RequireModuleFilter.cs` ·
  `Errores/ProblemDetailsExceptionHandler.cs` · `Contracts/*.cs` · `Endpoints/*.cs`
  (`AuthEndpoints`, `ContextoEndpoints`, `PersonasEndpoints`, `AgendaEndpoints`,
  `ReservasEndpoints`, `CanchasEndpoints`, `HorariosEndpoints`) · `Seed/DevSeeder.cs`.

## 7. Tests

Convención existente: xunit, nombres en español con guiones bajos (como
`Contratar_padel_arrastra_todo_lo_que_necesita`).

### Unit (`ClubSpot.UnitTests`)

- `Reservas/CalculadoraDePreciosTests` — umbral nocturno sólo sobre el arranque · redondeo a la
  centena más cercana · seña.
- `Reservas/HorarioTests` — la fecha especial pisa el día entero · sin tramos = cerrado ·
  superposición y cierre ≤ apertura rechazados.
- `Reservas/ReglasDeReservaTests` — arranques anclados a múltiplos del incremento desde
  medianoche · turno completo en un solo tramo · no cruza tramos contiguos · aviso mínimo hoy ·
  no en el pasado · duración habilitada · cancha inactiva no vende.
- `Reservas/ReservaTests` — cobrar deja saldo 0 · no se cobra cancelada · cancelar dos veces
  falla · ausencia sólo en confirmadas.
- `Reservas/ArmadoDeAgendaTests` — cancha inactiva cerrada todo el día · agrupado de huecos
  hasta el próximo arranque · ocupación sobre la grilla completa.
- `Clubes/BusquedaDePersonasTests` — dígitos ⇒ sólo teléfono · texto ⇒ nombre+email sin acentos.
- `Clubes/PersonaTests` — pago deja deuda 0 · nota con autor y momento.
- `Tenancy/TenantScopeTests` — sin ámbito lanza · el ámbito no se filtra entre flujos async.
- Huecos de SharedKernel: `MoneyTests`, `ClubCalendarTests`, `PeriodoTests` (hoy sólo existe
  `ModuleCatalogTests`).

### Integración (`ClubSpot.IntegrationTests`)

Estrategia: **Testcontainers.PostgreSql** (un contenedor por corrida, collection fixture) +
**Respawn** para limpiar entre tests. Postgres real es imprescindible: la exclusion constraint
no existe en SQLite/InMemory. `ApiFactory : WebApplicationFactory<Program>` sembrando **dos
clubes**.

- `AutenticacionTests` — sin token 401 · Recepción no edita canchas (403) · el login emite un
  token con el tenant.
- `TenancyTests` — un club no ve las personas del otro · un proceso sin ámbito de tenant lanza
  en vez de procesar.
- `ModulosTests` — módulo apagado responde **404, no 403** (fútbol apagado en el club B →
  `GET /api/agenda?deporte=futbol` da 404).
- `PersonasEndpointsTests` — alta · búsqueda por dígitos y por texto · filtros con contadores ·
  paginación de 14 · ficha inexistente 404 · nota · bloqueo masivo · pago deja deuda 0.
- `ReservasEndpointsTests` — crear y verla en la agenda con precio/seña correctos · **dos
  ventas simultáneas del mismo hueco: una 201 y una 409** (requests en paralelo contra
  `ex_reserva_solapamiento`) · `nuevaPersona` crea la ficha en la misma transacción · cada
  regla devuelve su 422 · cobrar · cancelar libera el hueco · ausencia · bloqueada no reserva.
- `CanchasHorariosTests` — roundtrip del PUT masivo · `idsAsignados` mapea los ids inventados ·
  eliminar horario con canchas → 409 · versión vieja → 409 · tramos inválidos → 422.

## 8. Orden de implementación

| Fase | Contenido | Verificable al terminar |
|---|---|---|
| **F0** | Plataforma: paquetes, DbContexts + migraciones, tenancy, Club + club_modulo + gating 404, auth completa, ProblemDetails, CORS, OpenAPI, seeder, `POST /api/auth/sesion`, `GET /api/contexto` | compila; tests de tenancy/auth/módulos verdes; login por curl; `/api/contexto` lista módulos |
| **F1** | Canchas y Horarios: agregados con invariantes, GET/PUT masivos, xmin, `idsAsignados` | `/canchas` y `/horarios` conectables |
| **F2** | Personas: agregado, `PersonasQueries` (contadores en 0 hasta F3, aceptable), los 6 endpoints | `/personas` conectable salvo historial real |
| **F3** | Agenda y Reservas: Reserva + exclusion constraint, servicios de dominio, `IPersonasDelClub`, los 6 endpoints, historial de la ficha desde reservas reales | las 4 pantallas operan contra la API |
| **F4** | Conexión del frontend: `src/api/http.ts` reemplaza `mockApi.ts` conservando firmas; **se borra `store.ts`**; fechas ISO, ids reales, formateo relativo como util de `domain/fechas.ts`, `version` en editores, login mínimo, menú gateado por `/api/contexto` | el backoffice corre entero contra la API real; cero referencias al mock |

Cada fase termina con `dotnet build` + `dotnet test` verdes, y con su entrada en la bitácora.

## 9. Divergencias y decisiones anotadas

1. **Mockup vs. diseño detallado** — por decisión del usuario (14/08/2026) manda el mockup. Los
   documentos de alcance y diseño pasaron a `docs/referencia-ourclub/` como material de
   consulta; la tarifa por audiencia (socio/no socio) queda para cuando exista `socios`.
2. **Sin finanzas** — `Persona.Deuda` y `POST /personas/{id}/pagos` son stubs provisionales
   marcados en el código; cuando exista finanzas pasan a cuenta corriente append-only y saldo
   derivado. El estado de cobro de la reserva vive en la reserva por el mismo motivo.
3. **Seña** — porcentaje configurable por club (`club.sena_porcentaje`, default 50); el redondeo
   a la centena es constante de `CalculadoraDePrecios`.
4. **Aviso mínimo como error duro** también en mostrador (coherente con la UI actual);
   revisable cuando el aviso apunte al portal.
5. **Duraciones del panel de venta** — se resuelve la inconsistencia de AGENTS.md §10 a favor
   de la cancha: la agenda expone `duraciones` por columna y el panel ofrece esas.
6. **Materialización de turnos / hold TTL** — diferida; la exclusion constraint es el punto de
   serialización interino (§2).
7. **Diferido deliberadamente** — portal del socio, series recurrentes, bloqueos puntuales de
   agenda, reprogramar, WhatsApp, exportaciones, importador real (queda sólo la pantalla),
   refresh tokens, jobs, accesibilidad/responsive del frontend.
