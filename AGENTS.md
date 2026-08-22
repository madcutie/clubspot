# ClubSpot — instrucciones para agentes

Sistema de **gestión de clubes** con backend .NET, pensado como producto **configurable por
módulos**: cada club contrata los que usa.

Este repo arranca el 14/08/2026. Reemplaza el enfoque del repo `Ticketing` (venta de entradas),
que **no se toca ni se migra acá** — se encara de otra manera.

---

## 1. Qué se está construyendo

Un club real —Club Atlético Chaco For Ever— usa hoy un SaaS llamado **OurClub**. Se relevó
entero y ese relevamiento es la referencia. Los dolores a resolver:

1. **Gestión del socio** — padrón, cuota, cobro.
2. **Reservas de canchas** de pádel y fútbol — funcionalidad nueva, sin equivalente real en
   el sistema actual.

**La venta de entradas para partidos NO es parte de este producto.**

## 2. Documentos — fuente de verdad

Leer antes de proponer cualquier cosa de dominio. Están en `docs/`:

| Documento | Qué contiene |
|---|---|
| [`docs/adr/`](docs/adr/README.md) | **Decisiones de arquitectura escritas en piedra** (ADRs): monolito modular, agenda en lectura, auth propia, idioma, capas, esquema y persistencia. No se rediscuten; si una cambia, se escribe un ADR nuevo |
| [`docs/plan-backend-backoffice.md`](docs/plan-backend-backoffice.md) + su [bitácora](docs/plan-backend-backoffice.bitacora.md) | **Plan vigente del backend** y el registro de avance. La bitácora dice qué fase está en curso y dónde quedó |
| [`docs/plan-disponibilidad-e2e.md`](docs/plan-disponibilidad-e2e.md) + su [bitácora](docs/plan-disponibilidad-e2e.bitacora.md) | **Plan aprobado (16/08/2026)**: disponibilidad de punta a punta —ADR-0013 en el backend, horarios/canchas del backoffice y portal de reservas contra la API real— con catálogo de 16 casos E2E por navegador + SQL. **F1 (backend) cerrada y verificada**; F2–F5 pendientes |
| [`docs/plan-cobro-en-mostrador.md`](docs/plan-cobro-en-mostrador.md) + su [bitácora](docs/plan-cobro-en-mostrador.bitacora.md) | **Escrito 19/08/2026, esperando aprobación**: cobrar un turno con Mercado Pago desde el backoffice (QR en pantalla + link por WhatsApp), reusando Checkout Pro. **Sin arrancar** |
| [`docs/plan-activity-log.md`](docs/plan-activity-log.md) + su [bitácora](docs/plan-activity-log.bitacora.md) | **En curso (19/08/2026)**: registro de actividad (ADR-0017) — una crónica append-only de qué pasó, quién lo hizo y por qué, que ve tanto el canchero como la auditoría. **F1 cerrada y verificada** (entidad, actor por ámbito, tipos de reservas y pagos cableados); F2–F7 pendientes |
| [`docs/plan-cancelacion-con-motivo.md`](docs/plan-cancelacion-con-motivo.md) + su [bitácora](docs/plan-cancelacion-con-motivo.bitacora.md) | **Cerrado y verificado (20/08/2026)**: cancelar un turno exige motivo —guardado en la reserva, no en el registro de actividad— y el panel avisa la plata cobrada antes de hacerlo. Queda pendiente el mismo tratamiento al bloquear una ficha |
| [`docs/plan-reglas-de-plata-huerfana.md`](docs/plan-reglas-de-plata-huerfana.md) + su [bitácora](docs/plan-reglas-de-plata-huerfana.bitacora.md) | **Escrito 20/08/2026, esperando decisiones**: por qué entra plata que el club no acordó. De los cinco motivos, tres pasan de verdad, uno es un defecto propio y uno no puede pasar. Cuatro puntos a resolver, uno de ellos —el TTL del hold— decisión de negocio. **Sin arrancar** |
| [`docs/plan-pagos-multiproveedor.md`](docs/plan-pagos-multiproveedor.md) + su [bitácora](docs/plan-pagos-multiproveedor.bitacora.md) | **Plan aprobado (18/08/2026)**: asiento de pago transparente al proveedor y al canal (ADR-0014/0015) — `payments.provider` + `payments.rail`, puerto `IPaymentProvider` con capacidades. **Cerrado y verificado (fake y Mercado Pago real)** |
| [`docs/plan-reserva-online.md`](docs/plan-reserva-online.md) + su [bitácora](docs/plan-reserva-online.bitacora.md) | **Plan aprobado (17/08/2026)**: reserva online desde el portal en 3 etapas. **Etapas 1 y 2 cerradas**: reserva sin pago con vínculo a persona (email → celular → crear), y pago online (hold con TTL perezoso, webhook idempotente, tabla `payments`) verificado con el **gateway fake**; Mercado Pago escrito pero sin probar (faltan credenciales). Etapa 3 (login) pendiente |
| [`docs/plan-login-backoffice.md`](docs/plan-login-backoffice.md) + su [bitácora](docs/plan-login-backoffice.bitacora.md) | **En curso (20/08/2026)**: login del backoffice empezando por el canchero (ADR-0018) — login sólo con email, email único global, claims cortas, y consola dibujada según el rol. **F1–F5 escritas y verdes** (build, 79 unitarios y 72 de integración); falta la recorrida en el navegador |
| [`docs/plan-contrato-api.md`](docs/plan-contrato-api.md) + su [bitácora](docs/plan-contrato-api.bitacora.md) | **Plan aprobado y ejecutado (19/08/2026)**: documento OpenAPI generado por el build de la Api y clientes TypeScript generados con Orval para los dos frontends (ADR-0016). **Cerrado y verificado**: F1–F5 |
| [`docs/plan-build-frontends-por-entorno.md`](docs/plan-build-frontends-por-entorno.md) + su [bitácora](docs/plan-build-frontends-por-entorno.bitacora.md) | **Escrito 21/08/2026, esperando decisiones**: poder construir y publicar los dos frontends fuera de la máquina del developer. Hoy los dos `dist/` que hay en disco tienen `localhost:5037` adentro y nada avisa. El build de producción pasa a fallar si le falta la configuración, y el borde del backend deja de traer valores de desarrollo en archivos versionados. Dos decisiones bloquean la ejecución. **Sin arrancar** |
| [`docs/auditoria-codigo-vs-reglas.md`](docs/auditoria-codigo-vs-reglas.md) | **Auditoría del código contra sus propias reglas** (20/08/2026): qué desvíos había, qué parecía desvío y no lo era —anotado con su razón para no repetir la vuelta— y qué queda por chequear |
| `src/frontend/backoffice/` | **El mock manda** (decisión del 14/08/2026): donde el prototipo y cualquier otra fuente difieran, gana el prototipo. Ver sección 10 |

> **16/08/2026 — se eliminó `docs/referencia-ourclub/`** (relevamiento de OurClub, alcance del
> MVP y diseño detallado), por decisión del usuario: ese material venía confundiendo más de lo
> que aportaba. Estaba desfasado —el relevamiento se hizo cuando el alcance todavía incluía
> boletería— y describía **cómo lo hace un sistema ajeno**, no qué construir acá. Sigue en el
> historial de git (commit `9e2f079`) si alguna vez hace falta consultarlo.
>
> Lo que ese material fundaba y todavía se da por válido está resumido en este archivo: los
> dolores a resolver (§1), los 11 jobs (§7), los roles y las partes a desarrollar (§9). Las
> preguntas abiertas de §3 siguen abiertas.

Ante una duda de dominio: **primero buscar en los ADRs, el plan y el prototipo**, no improvisar.
Si la respuesta no está, es una pregunta para el usuario, no una decisión a tomar sola.

## 3. Reglas de trabajo

- **Los commits los hace el usuario.** No hacer `git commit` ni `git push` salvo pedido
  explícito. (Regla heredada de cómo trabaja en sus otros repos — confirmar si cambia.)
- **Idioma** (ADR-0006, 15/08/2026, reemplaza al "todo en español" original): **el código va
  entero en inglés** —identificadores, comentarios, mensajes de excepción y nombres de tests—.
  En español queda lo que no es código: la documentación del repo y los textos que ve el
  usuario final. Detalle en la sección 6.
- **Comentarios: casi cero** (ADR-0006). Sólo lo muy importante que el código no puede decir
  por sí mismo; nada de doc-comments decorativos.
- **Sin primera persona** en documentos entregables. Voz impersonal.
- **No inventar números.** Si algo es una estimación, decirlo. El usuario lleva estos
  documentos a reuniones con el club.
- Antes de borrar o pisar algo, mirarlo. Preferir copiar y avisar antes que mover y perder.

### Lo que está esperando definición del usuario

- ✅ **Frontend del backoffice** — el diseño llegó (14/08/2026) y el cascarón está implementado
  en `src/frontend/backoffice/`. Ya no está bloqueado. Ver sección 10.
- **Configuración de canchas y deportes** (ADR-0008): cómo se administran las canchas y a qué
  deporte pertenecen — diseño pendiente con el usuario; mientras tanto, enum fijo.
- **Granularidad de finanzas y capacidades** (ADR-0012): cobrar un turno y hacer liquidaciones
  son capacidades distintas que se venden por separado, así que `finance` como bloque único
  está mal cortado. Cómo se parte, y cómo las capacidades contratadas habilitan o no ciertas
  features, **se define más adelante**. No partirlo por anticipado.
- Las 7 preguntas abiertas del documento de alcance (facturación electrónica, cobrador
  domiciliario, débito automático, estrategia de migración, tolerancia de deuda para reservar,
  alquiler a no socios, acumulación de becas).

## 4. Arquitectura

**Monolito modular** en .NET 10. Un solo host, un solo despliegue. Las decisiones grandes de
arquitectura están escritas en piedra en [`docs/adr/`](docs/adr/README.md) — **leerlas antes de
proponer un cambio estructural**; si una decisión cambia, se escribe un ADR nuevo, no se edita
el viejo.

La estructura es **por capas** (ADR-0005, espejo del proyecto anubis del usuario): las capas
son proyectos, los módulos son **carpetas** dentro de cada capa. Todo el código fuente cuelga
de `src/`, con backend y frontend separados. La solución .NET vive entera dentro de
`src/backend/` —incluidos `global.json` y `Directory.Build.props`—, así que esa carpeta se
puede abrir sola y compila.

```
src/
├─ backend/
│  ├─ ClubSpot.slnx
│  ├─ global.json
│  ├─ Directory.Build.props
│  └─ src/
│     ├─ Core/
│     │  ├─ ClubSpot.SharedKernel/     primitivas: Money, TenantId, IClock, ModuleId, ModuleCatalog
│     │  ├─ ClubSpot.Domain/           agregados y servicios de dominio puros — carpeta por módulo
│     │  └─ ClubSpot.Application/      casos de uso (handlers) y puertos — carpeta por módulo
│     ├─ Infrastructure/
│     │  ├─ ClubSpot.Infrastructure/   EF Core, repositorios, tenancy, gateway fake
│     │  └─ ClubSpot.Infrastructure.MercadoPago/  SDK de MP aislado (regla de vendors, §6)
│     ├─ Api/
│     │  └─ ClubSpot.Api/              host: endpoints, JWT, middleware, DI, arranque
│     ├─ Jobs/
│     │  └─ ClubSpot.JobService/       host de jobs: Hangfire (base propia clubspot-hangfire), J2
│     └─ Tests/
│        ├─ ClubSpot.UnitTests/
│        └─ ClubSpot.IntegrationTests/
└─ frontend/
   ├─ backoffice/                    consola del club (React+Vite) — ver sección 10
   └─ reservas/                      prototipo React+Vite del portal de reservas (ya existía)

docs/                                ADRs, plan del backend y su bitácora
```

Referencias entre capas: `Api → Application + Infrastructure` · `Infrastructure → Application`
· `Application → Domain` · `Domain → SharedKernel`. Los manifiestos del catálogo de módulos
viven en `Application/Modularity/ProductModules.cs`. `ClubSpot.JobService` referencia
`Infrastructure` igual que la Api; su único job por ahora es **J2** (conciliación de pagos,
[`docs/plan-jobservice.md`](docs/plan-jobservice.md)) — J1 fue descartado por decisión del
usuario (la expiración perezosa del hold ya garantiza la corrección).

### Grafo de módulos

```
core (núcleo, no se puede apagar)
 ├─ finance ───────────► core
 ├─ members ───────────► core, finance
 └─ bookings ──────────► core, finance
```

**No hay módulos por deporte** (ADR-0008, 16/08/2026): `bookings` se contrata una vez y cubre
reservas de cualquier deporte. El deporte es **configuración de la cancha** (`Court.Sport`),
no una unidad comercial; cómo se configuran las canchas y a qué deporte pertenecen es una
decisión de diseño pendiente con el usuario. Los módulos `padel` y `football` que existieron
hasta el 16/08/2026 fueron eliminados.

⚠️ **Las flechas hacia `finance` son provisionales** (ADR-0012): hoy no expresan una
dependencia real —hay clientes que alquilan canchas sin nada de plata, y otros que cobran el
turno pero no hacen liquidaciones— sino que la parte financiera se está desarrollando junto
con reservas. Se corrigen cuando se defina la granularidad de finanzas y el concepto de
**capacidades**, que es una decisión pendiente del usuario.

### Reglas de frontera entre módulos

Los módulos ya no son proyectos, así que la frontera **no la impone el compilador**: es
convención de carpetas, cuidada en revisión (ADR-0005).

- Una carpeta de módulo (`Domain/Bookings/`, `Application/Bookings/`) **no usa tipos** de la
  carpeta de otro módulo.
- La Api **no usa EF ni los DbContexts directamente**: todo caso de uso entra por un handler
  o un puerto de Application (ADR-0005). Los endpoints sólo traducen HTTP ↔ Application.
- Lo que dos módulos necesitan compartir va como **contrato** (interfaz), implementado por el
  módulo dueño y cableado por DI. Ejemplo: la habilitación del socio la define `members` y la
  consume `bookings` sin conocerlo.
- La lógica de dominio **nunca pregunta si un módulo está habilitado**. Eso se resuelve en el
  borde: el endpoint responde 404 y el job no se encola.

## 5. Configurabilidad por módulos

Es requisito del producto, no una feature futura. Las reglas de composición están en
[ADR-0012](docs/adr/0012-composicion-de-modulos-por-tenant.md); en resumen:

- **El módulo es la unidad más chica que se vende por separado.** Si un cliente puede pagar
  por A sin B, A y B son módulos distintos. El corte lo define **lo que se vende**, no cómo
  está organizado el código. Test: *¿existe un cliente que quiera esto sin aquello?*
- **Dependencia dura es sólo "sin el otro el concepto no existe".** Que un módulo aproveche a
  otro cuando está presente **no** lo vuelve dependencia.
- **La persona es una sola y es de `core`**, que guarda quién es. Ser socio, anotarse en una
  actividad, alquilar una cancha o deber plata son **vínculos**: cada módulo los guarda en sus
  propias tablas contra `personId`. ⇒ **Ningún módulo agrega columnas a `people`.**
- **Ningún módulo asume el vínculo de otro**: se le vende un turno a quien no es socio, y hay
  socios que nunca reservaron.
- **La integración entre módulos es por contrato y opcional**: si el módulo dueño no está
  contratado, la funcionalidad **no se ofrece** en vez de fallar.

Ejemplos que el producto tiene que soportar: un cliente con club + reservas (la misma persona
es socia, hace karate y el sábado alquila una cancha) · otro con **sólo reservas** de fútbol 5,
con cobro o sin cobro y sin liquidaciones · otro con club + reservas + finanzas.

- Cada módulo se declara a sí mismo implementando `IClubModule` (id estable, nombre comercial,
  dependencias, si es núcleo).
- `ModuleCatalog` valida el grafo al arrancar: dependencias inexistentes o ciclos hacen fallar
  el arranque, no producen comportamiento raro en runtime.
- `ModuleCatalog.Resolve` expande al cierre transitivo: contratar `members` trae `finance` y
  `core` solos.
- La tabla `club_module` persiste **sólo lo contratado comercialmente**; la habilitación es el
  cierre resuelto en lectura (ADR-0009). Nadie lee esa tabla directo: el único camino es
  `ITenantModules`, que ya devuelve el cierre.
- **Módulo apagado ⇒ 404, no 403.** Quien no contrató un módulo no tiene por qué enterarse de
  que existe.
- **Apagar un módulo no borra datos.** Corta el acceso; los datos quedan.

## 6. Convenciones de código

- **.NET 10**, `nullable` habilitado, `TreatWarningsAsErrors=true`, `InvariantGlobalization=false`
  (el club opera en es-AR y las fechas y montos dependen de la cultura).
- **Idioma** (ADR-0006): **el código va entero en inglés** — clases, métodos, tablas,
  columnas, endpoints, proyectos, ids de módulo, comentarios, mensajes de excepción y nombres
  de tests (`The_product_catalog_is_valid`). En español queda lo que no es código: la
  documentación del repo (ADRs, plan, bitácora, este archivo), los textos de la UI y los
  nombres comerciales (`DisplayName = "Socios"`). Los errores de la API viajan con código de
   regla; el texto en español que ve el operador lo pone el frontend.
- **Persistencia** (ADR-0011, detalle completo ahí): un único `ClubSpotDbContext` y una sola
  cadena de migraciones con la tabla estándar `__EFMigrationsHistory` (ADR-0010) · todo nombre
  físico en **camelCase** · **tablas en plural** (`clubs`, `people` — plural inglés real,
  incluidos los irregulares) y **columnas en singular** · los nombres de claves, índices y
  foráneas los pone **una convención en el contexto** (`pkPeople`, `ixPeopleTenantIdSearchName`,
  `uxUsersTenantIdEmail`, `fkCourtsScheduleId`): **no se escribe `HasDatabaseName` ni
  `HasConstraintName` en las configuraciones**, sólo los check constraints se nombran a mano.
- **Comentarios: casi cero** (ADR-0006). Se comenta únicamente lo muy importante que el código
  no puede decir solo —una invariante no obvia, una lista blanca, un orden obligatorio, un
  "a propósito" que sin nota parecería un error—, en una o dos líneas y en inglés. Prohibidos
  los doc-comments decorativos y los resúmenes de lo que ya dice la firma.
- **Todo SDK de vendor externo va en un proyecto de Infrastructure propio** (decisión del
  usuario, 17/08/2026): p. ej. las dependencias de MercadoPago viven en
  `ClubSpot.Infrastructure.MercadoPago`, no en `ClubSpot.Infrastructure`. El proyecto del
  vendor implementa el puerto que declara Application y se cablea por DI en la Api; el resto
  de la solución no referencia el SDK. Regla general para cualquier gateway o servicio
  externo que se integre.
- **Todo endpoint declara su contrato** (ADR-0016): `TypedResults` y uniones
  `Results<Ok<T>, NotFound, …>` en vez de `IResult`, DTO nombrados y accesibles (nada de
  objetos anónimos ni de `private record` en la respuesta), y `WithName`/`WithTags` en cada
  ruta. `WithName` es el nombre de la función que va a usar el frontend, así que es contrato,
  no decoración. Un endpoint sin contrato declarado sale vacío en el documento OpenAPI, y eso
  es un bug.
- **Todo acceso a la API desde el frontend pasa por el cliente generado** (ADR-0016). El
  documento `docs/api/clubspot.openapi.json` lo reescribe el build de la Api y los clientes los
  genera Orval; **ni el documento ni lo generado se editan a mano**. No se escribe un servicio,
  un `fetch` ni una `interface` con la forma de un DTO del backend "por ahora": si falta un
  endpoint se agrega en la Api y se regenera. Un cliente escrito a mano deja **huérfano** al
  generado —cuando el contrato cambia, ese camino no lo acompaña y nadie se entera—. La única
  excepción es el *mutator* de cada app (`http.ts` y su equivalente del portal): un solo
  archivo, el único lugar donde vive `fetch`.
- **Nunca un `decimal` suelto para plata**: se usa `Money`, que lleva la moneda. Esto incluye
  tarifas y precios de canchas, no sólo deudas y pagos. La regla vale **dentro** del sistema; tiene
  tres bordes donde `decimal` es correcto y **no se "arreglan"** (auditado el 20/08/2026):
  - **Lo que reporta un proveedor externo** (`PaymentNotification`): la moneda puede no venir, y por
    eso viaja como campo aparte que se puede leer como ausente. Colapsarlo a `Money` obligaría al
    adaptador a inventarla y volvería indetectable `wrongCurrency`.
  - **Las agregaciones en SQL** (`SumAsync(payment => payment.Amount.Amount)`): la base suma
    columnas, no tipos. La mezcla de monedas la impide el filtro `Approved` — un pago en otra
    moneda queda `ApprovedOrphan` y no entra en la suma.
  - **Los DTO de respuesta** (`AgendaSlot`, `BookingSnapshot`): la moneda viaja una sola vez por
    payload, en `Agenda.Currency`, en vez de repetirse en cada importe.

  Fuera de esos tres, un `decimal` con plata adentro es un defecto.
- **Una tabla pertenece a un módulo.** Antes de agregar una columna, preguntarse de quién es el
  dato: si un vínculo entre una persona y algo de otro módulo, va en las tablas de ese módulo
  contra `personId` (ADR-0012). `people.debtAmount` es la violación que queda en pie, marcada
  como provisional hasta que se defina la parte financiera.
- **La seña es 50 % o 100 %** (decisión del usuario, 19/08/2026): no existe otro porcentaje. Lo
  imponen el agregado `Club` y un check constraint, no la UI.
- **La moneda la define `Club.Currency`**: `Money` no tiene moneda por defecto ni existe una
  constante "ARS" en el código. Todo importe nace con la moneda del club en curso.
- **Nunca `DateTime.Now`**: se inyecta `IClock`. Todo lo que el negocio llama "día" se resuelve
  con `ClubCalendar` en la zona del club, no en UTC.
- **Nunca un `TenantId` implícito en background**: `ITenantContext.Current` lanza si no hay
  tenant, a propósito.
- **Ids: `Guid` crudo.** Los únicos ids tipados son `TenantId` y `ModuleId`, que llevan reglas
  propias. No se crean `PersonId`/`BookingId`: la consistencia pesa más que la ceremonia.
- **Un concepto, un tipo.** Nada de enums o records duplicados entre capas "por comodidad":
  el `Sport` duplicado entre SharedKernel y Domain fue la raíz de la revisión del 16/08.
- **Los enums viajan en camelCase** (`"counter"`, `"padel"`), registrando **un converter por
  enum concreto** en `Program.cs`. El converter abierto también renombraría las **claves de
  diccionario** con clave enum, como los días de la semana de un horario.
- **Lo provisional se marca.** Un stub que devuelve un valor fijo o incumple una regla del
  dominio lleva un comentario de una línea que diga que es provisional y qué lo reemplaza; si
  no, el que venga después lo lee como un error y lo "arregla".
- **El registro de actividad no es la fuente de verdad de ningún dato de negocio** (decisión
  del usuario, 20/08/2026). Es una crónica: se escribe siempre y se lee poco. Un dato que la
  operación necesita vive en su agregado —el motivo de una cancelación es una columna de
  `bookings`, no la columna `reason` de una entrada del log—; el registro guarda como mucho una
  foto de ese dato, más lo único que aporta él: **quién** lo hizo y **cuándo**. Antes de mandar
  algo al `activityLog`, preguntarse si alguien va a necesitar leerlo para operar: si la
  respuesta es sí, va también —o sólo— en su tabla.
- Movimientos de dinero **append-only**: no se editan, se anulan con contra-asiento.
- Las invariantes del dominio se imponen en el agregado y en la base. El sistema de referencia
  las "valida" con carteles en pantalla y por eso tiene datos rotos: grupos familiares de un
  integrante, categorías huérfanas. **Eso no se replica.**

### Comandos

```powershell
.\scripts\dev-up.ps1                # SÓLO EL USUARIO — levanta todo en ventanas sueltas
.\scripts\db-sql.ps1 '<consulta>'   # consulta la base (psql dentro del contenedor)
.\scripts\db-reset.ps1              # borra la base; la API la recrea al arrancar
```

#### Cómo levanta un agente lo que necesita

**`dev-up.ps1` no lo corre un agente** (decisión del usuario, 20/08/2026): abre una ventana de
PowerShell por servicio, que el agente no puede ni ver ni frenar. Es la comodidad del usuario
para levantar todo de una.

Un agente levanta **sólo el servicio que toca** y lo hace **en background dentro de su propia
sesión**, para poder leerle la salida, reiniciarlo y bajarlo cuando termina. Quien trabaja en el
backend baja y sube la API a gusto sin tocar los frontends, y al revés.

```powershell
# PostgreSQL (lo comparte todo el mundo; no se baja porque sí)
docker compose -f compose.yaml up -d postgres

# API — :5037
$env:ASPNETCORE_ENVIRONMENT='Development'; $env:ASPNETCORE_URLS='http://localhost:5037'
dotnet run --project src/backend/src/Api/ClubSpot.Api --no-launch-profile

# JobService (sin puerto) — OBLIGATORIO, no es opcional
$env:DOTNET_ENVIRONMENT='Development'; dotnet run --project src/backend/src/Jobs/ClubSpot.JobService

# Frontends — :5184 y :5183
npm --prefix src/frontend/backoffice run dev
npm --prefix src/frontend/reservas run dev

# ngrok — OBLIGATORIO si se va a tocar Mercado Pago
ngrok http 5037 --url=noe-uncephalic-jerome.ngrok-free.dev
```

**Sin ngrok, Mercado Pago no funciona y falla en silencio.** El túnel es el
`Payments:PublicBaseUrl` de `appsettings.Development.json`, y por ahí van las dos cosas que MP
necesita alcanzar: el **webhook** (`NotificationUrl`) y el **rebote de vuelta**
(`/api/payments/return`, que existe porque `auto_return` sólo acepta https). Con el túnel caído el
comprador paga, la plata se cobra, y **la reserva se queda en `pendingPayment`** porque la
notificación no llega a ninguna parte — el navegador muestra `ERR_NGROK_3200` y nada más.

El dominio es estático, así que levantarlo de nuevo recupera la misma URL y no hay que tocar ni la
configuración ni el panel de Mercado Pago. Para saber si está arriba:
`curl http://127.0.0.1:4040/api/tunnels`.

**El JobService tampoco es opcional.** Corre **J2** cada 5 minutos (`*/5 * * * *`): por cada reserva
online sin pagar de las últimas 48 h le pregunta a Mercado Pago si tiene un pago que no nos llegó, y
lo aplica por el mismo camino idempotente que el webhook. Es **la única red que atrapa un webhook
perdido** — un túnel caído, la API abajo, un corte de red—. Sin él, el comprador paga, el hold
vence, el turno se libera y **nadie se entera nunca**: no queda ni una fila en `payments` ni una
entrada en el registro de actividad. La única evidencia queda del lado de Mercado Pago.

Con todo levantado son **cinco ventanas**: API, JobService, Backoffice, Reservas y ngrok.

Para bajar uno: matar el proceso que escucha su puerto
(`Get-NetTCPConnection -State Listen -LocalPort 5037`), no cerrar ventanas ajenas.

**La API expone dos sondas** (20/08/2026, para poder desplegar): `GET /health` responde 204 si el
proceso está vivo y **no toca nada más**, y `GET /health/ready` responde 204 si además la base
contesta —o 503 si no—. Las dos son anónimas y están fuera del contrato OpenAPI. Para saber si la
API arrancó, `/health` alcanza.

```bash
cd src/backend && dotnet build      # compilar la solución (y reescribir docs/api/clubspot.openapi.json)
cd src/backend && dotnet test       # correr los tests (los de integración necesitan Docker)
cd src/frontend/backoffice && npm i && npm run dev   # consola del club — :5184
cd src/frontend/reservas && npm i && npm run dev     # portal de reservas — :5183
npm run api:gen                                      # regenera el cliente (lo corre solo predev/prebuild)
```

La API corre en `:5037` y **PostgreSQL en el `5432` estándar** (decisión del usuario,
17/08/2026; si otro proyecto lo ocupa, override con `CLUBSPOT_PG_PORT`). En Development la
API migra y siembra la base sola al arrancar. `dotnet ef` necesita `dotnet tool restore` una
vez por clon.

**`appsettings.Development.json` no se versiona** (decisión del usuario, 17/08/2026): es la
configuración local del developer y ahí van también los secretos de dev (p. ej. el access
token de Mercado Pago). En un clon fresco se crea copiando
`appsettings.Development.json.example`, que sí está versionado y no lleva ningún secreto.

## 7. Los procesos de background

El diseño identificó **11 jobs para el MVP**. Esta tabla es ahora la única referencia que
queda de ellos en el repo (§2):

| | Job | Cadencia |
|---|---|---|
| J1 | Expiración de reservas | 30 s |
| J2 | Conciliación de pagos con el proveedor | 5 min |
| J3 | Reproceso de la bandeja de webhooks | 1 min |
| J4 | Despachador de notificaciones (outbox) | 30 s |
| J5 | Apertura de agenda de canchas | diario |
| J6 | Snapshot de habilitación | diario |
| J7 | Preliquidación | bajo demanda |
| J8 | Aplicación de liquidación | bajo demanda |
| J9 | Avisos de cobranza | diario |
| J10 | Recordatorio de reservas | cada hora |
| J11 | Retención y purga | diario |

Reglas que cumple **todo** job, sin excepción: idempotente · lock distribuido por (job, tenant) ·
acotado y por lotes · reanudable · en hora local del club · sin efectos externos dentro de la
transacción · emite métrica de resultado · **recibe el tenant como parámetro explícito**.

Y dos que no son jobs aunque lo parezcan: marcar cargos vencidos (es una comparación de fecha)
y recalcular saldos (se actualizan en la misma transacción del movimiento).

## 8. Estado actual

> ✅ **16/08/2026 — remediación de modelado cerrada.** El trabajo de
> [`docs/plan-remediacion-modelado.md`](docs/plan-remediacion-modelado.md) (ADR-0008 a 0011)
> se ejecutó y verificó contra la base recreada; el detalle está en la bitácora del plan del
> backend. Lo siguiente es el plan de disponibilidad de punta a punta
> ([`docs/plan-disponibilidad-e2e.md`](docs/plan-disponibilidad-e2e.md)), que implementa
> ADR-0013 — esperando aprobación del usuario.

| | Qué |
|---|---|
| ✅ | Solución por capas (7 proyectos: SharedKernel, Domain, Application, Infrastructure, Api y 2 de tests) |
| ✅ | `SharedKernel`: `TenantId`, `ITenantContext`, `IClock` + `ClubCalendar`, `Money`, `Period` |
| ✅ | Modularidad: `ModuleId`, `IClubModule`, `ModuleCatalog` (valida grafo y cierre transitivo), `ITenantModules`; manifiestos de los 4 módulos (`core`, `members`, `finance`, `bookings`) |
| ✅ | Plataforma (fase A): EF Core + PostgreSQL en esquema `public`, tenancy con filtro global, auth propia con JWT y roles, módulos por club con gating 404, seed de desarrollo |
| 🚧 | Fase B: People completo (agregado, búsqueda, endpoints); Schedules y Courts persistidos con concurrencia optimista `xmin` — falta contrato final de configuración |
| ✅ | Documentos en `docs/` y prototipo de reservas en `src/frontend/reservas/` |
| ✅ | Backoffice en `src/frontend/backoffice/` — las 4 pantallas contra la API real, sin mock (sección 10) |
| ⬜ | Todo lo demás — ver abajo y la bitácora del plan |

**No hay todavía**: jobs más allá de J2, outbox, auditoría ni observabilidad.

---

## 9. Partes a desarrollar

> 📋 Existe un **plan aprobado para el backend de las 4 pantallas del backoffice** (plataforma,
> tenancy, auth, personas, reservas, canchas, horarios y sus tests):
> [`docs/plan-backend-backoffice.md`](docs/plan-backend-backoffice.md). Su avance se registra en
> [`docs/plan-backend-backoffice.bitacora.md`](docs/plan-backend-backoffice.bitacora.md) —
> **leer la bitácora antes de retomar**: dice qué fase está en curso y dónde quedó.
> La implementación **no arranca sin pedido explícito del usuario**.

Leyenda: ✅ hecho · 🚧 bloqueado · ⬜ pendiente

### 9.1 Plataforma (transversal, habilita todo lo demás)

| | Parte | Notas |
|---|---|---|
| ⬜ | **Persistencia** | EF Core + PostgreSQL. Un único esquema `public` y un único `ClubSpotDbContext` (ADR-0007, ADR-0010); los módulos se separan en código, no en la base. Filtro global por tenant, con lista blanca auditada de los lugares que lo ignoran |
| ⬜ | **Migraciones** | Una sola cadena, con la tabla de historial estándar `__EFMigrationsHistory` (ADR-0010). Un cambio que toca dos módulos entra en una sola migración |
| ⬜ | **Tenancy** | Resolución por token/host en HTTP + **ámbito explícito en background**. Test que verifique que un job sin tenant lanza en vez de procesar |
| ⬜ | **Autenticación y roles** | Usuarios, JWT, y los 7 roles operativos de la sección 6 del diseño. Incluye **separación de funciones**: quien calcula la liquidación no puede aprobarla |
| ⬜ | **Configuración de módulos por club** | Persistir qué contrató cada tenant · endpoint de capacidades para el frontend · filtro que devuelve **404** en módulo apagado · gating del despachador de jobs |
| ⬜ | **Infraestructura de jobs** | Hangfire sobre PostgreSQL · lock distribuido por (job, tenant) · despachador que encola una ejecución por tenant y aísla el fallo de uno · registro de resultado por corrida |
| ⬜ | **Outbox de notificaciones** | Tabla + despachador (J4) + proveedor de email. La fila se escribe en la misma transacción que el hecho que la origina |
| 🚧 | **Registro de actividad (`activityLog`)** | Quién, cuándo y por qué en cada transición de estado — y también los eventos que llegan solos (webhook, job, vencimiento). Un solo registro append-only para el operador y para la auditoría ([ADR-0017](docs/adr/0017-registro-de-actividad-activitylog.md), [plan](docs/plan-activity-log.md)) |
| ⬜ | **Observabilidad** | Métricas por job y **pantalla de operación** dentro del sistema: última corrida, pagos en revisión manual, outbox fallido, divergencias de habilitación |
| ✅ | **Contrato de API** | ADR-0016 implementado (19/08/2026): el build de la Api reescribe `docs/api/clubspot.openapi.json` con los 29 endpoints, y los dos frontends hablan por clientes generados con Orval ([plan](docs/plan-contrato-api.md)) |

### 9.2 Módulo `core`

| | Parte |
|---|---|
| ⬜ | Agregado **Persona** con sus invariantes y la unicidad de documento impuesta en base |
| ⬜ | Los **tres identificadores buscables**: documento, número de socio, código de credencial |
| ⬜ | Buscador de personas — es la pantalla más usada del sistema |
| ⬜ | Domicilio y datos de contacto |
| ⬜ | Usuarios, roles y asignación |
| ⬜ | Configuración del club: zona horaria, moneda, datos institucionales |

### 9.3 Módulo `members`

| | Parte |
|---|---|
| ⬜ | **Membresía**: alta, baja, suspensión, reactivación, cambio de categoría, cambio de número |
| ⬜ | Catálogo de **categorías** |
| ⬜ | **Grupo familiar**: titular, integrantes, cambio de titularidad, disolución — con las invariantes que el sistema actual no impone |
| ⬜ | Antigüedad derivada + descuento de antigüedad acreditable |
| ⬜ | **Excepciones a la recategorización** como dato versionado por estatuto, no como `if` |
| ⬜ | **Habilitación**: proyección materializada + recálculo por evento + contrato que consumen reservas y, a futuro, el control de acceso |
| ⬜ | Alta express de mostrador ("socio al minuto") |
| ⬜ | **Alta online**: pago → alta, sin que pueda quedar plata cobrada sin socio creado |
| ⬜ | **Actividades**: deportes dictados por profesores, con alumnos. Confirmado por el usuario el 16/08/2026 que **son parte del módulo de club, no un módulo aparte**. Profesor y alumno son **vínculos** sobre `Person` (ADR-0012), no entidades nuevas ni columnas de `people`; el alumno puede además pertenecer a un grupo familiar |

### 9.4 Módulo `finance`

| | Parte |
|---|---|
| ⬜ | **Conceptos** y precios por categoría y audiencia, **historizados** |
| ⬜ | Descuentos y becas (uno vigente por membresía en el MVP) |
| ⬜ | **Cuenta corriente**: cargos, imputaciones, pagos, saldo. Append-only |
| ⬜ | **Liquidación**: lote, preliquidación (J7), aplicación (J8), reversión por contra-asientos |
| ⬜ | **Recategorización por edad** dentro de la preliquidación, con previsualización obligatoria |
| ⬜ | **Recibos**: numeración, emisión, anulación individual con motivo |
| ⬜ | **Caja**: sesión por operador, cobro de mostrador, cierre con efectivo declarado y diferencia |
| ⬜ | **Pagos**: gateway abstraído, checkout, webhook idempotente, conciliación (J2), bandeja de revisión manual |
| ⬜ | Listados exportables: deudores, cobranza del período, altas y bajas. **Sin dashboards** |

### 9.5 Módulo `bookings`

| | Parte |
|---|---|
| ⬜ | **Disponibilidad** ([ADR-0013](docs/adr/0013-disponibilidad-patron-semanal-mas-excepciones.md)): patrón semanal reusable + **excepciones** con conjunto de fechas y alcance (cancha o club), que lo pisan. Cerrar es una excepción sin ventanas — no hay entidad "bloqueo" ni tipo "feriado"; gana la más específica. Sólo se dibuja hacia adelante, así que el patrón no se versiona |
| ⬜ | **Tarifas** por tipo de espacio × franja horaria × socio/no socio |
| ⬜ | **Materialización de turnos** (J5) — la fila del turno es el punto de serialización |
| ⬜ | **Reserva**: hold con TTL → pago → confirmada, con el `UPDATE` condicional atómico |
| ⬜ | Cancelación con ventana · marcar ausente |
| ⬜ | **Series recurrentes** (turno fijo), creadas por el operador |
| ⬜ | API de agenda día/semana — la UI espera diseño |
| ⬜ | Elegibilidad vía el contrato de habilitación |

### 9.6 Deporte y configuración de canchas (ADR-0008)

Ya no existen módulos por deporte. Lo pendiente acá es **diseño con el usuario**, no código:

| | Parte |
|---|---|
| ⬜ | **Definir cómo se configuran las canchas y a qué deporte pertenecen**: ¿enum fijo o catálogo administrable? ¿Formatos F5/F7/F11 como atributo de la cancha? ¿Tipos de espacio? Nada se infiere desde la base |
| ⬜ | Si un deporte llega a tener reglas comerciales propias (partido abierto, alquiler de paletas, seña distinta), evaluar recién entonces si lo amerita — hoy la respuesta es configuración, no módulo |

### 9.7 Frontend

| | Parte |
|---|---|
| ✅ | **Backoffice del club** — cascarón implementado en `src/frontend/backoffice/`. Detalle y pendientes en la sección 10 |
| ⬜ | **Portal del socio**: mi cuenta, deuda, pagar, credencial, mis reservas |
| ✅ | Portal de reservas `src/frontend/reservas/` contra la API real — sin mock (etapas 1 y 2 del plan de reserva online) |

### 9.8 Fase cero — migración del padrón

| | Parte |
|---|---|
| ⬜ | Importador **idempotente y reejecutable**, con **informe de rechazos** por registro |
| ⬜ | Orden: personas → membresías → grupos familiares → saldos → becas → códigos de credencial |
| ⬜ | Decidir: ¿histórico completo de cuenta corriente o sólo saldo de apertura? (recomendación del diseño: saldo de apertura) |
| ⬜ | Resolver **antes de migrar** los datos que violan las invariantes nuevas |

> Sin padrón migrado no hay sistema utilizable. Define la fecha real de puesta en marcha.

### 9.9 Orden sugerido

Cada fase deja algo utilizable. Del documento de diseño:

| Fase | Contenido | Al terminar |
|---|---|---|
| 0 | Plataforma (9.1) + migración del padrón (9.8) | hay socios reales en el sistema |
| 1 | `core` + `members` sin dinero: buscador y ficha | el mostrador puede consultar y dar de alta |
| 2 | `finance`: conceptos, liquidación, cuenta corriente | existe la deuda |
| 3 | Cobro de mostrador, recibos, cierre de caja | el club cobra |
| 4 | Portal del socio + pago online | el socio se autogestiona |
| 5 | `bookings` | las canchas se venden |
| 6 | Habilitación como servicio consumible desde afuera | queda listo para integrar con lo que venga |

La habilitación se **define** en la fase 1 aunque se **integre** en la 6: es la bisagra del
producto y no puede quedar improvisada.

---

## 10. Backoffice — `src/frontend/backoffice/`

Consola de operación del club. Traducción a React del diseño **"Backoffice Consola"** del
proyecto de Claude Design *Diseño Chaco Forever en blanco y negro*, importado el 14/08/2026.

**Corre entero contra la API real** (19/08/2026): el mock en memoria se borró. Las cuatro
pantallas hablan HTTP con `:5037`, autenticadas con el JWT que emite `/api/auth/session`.

```bash
cd src/frontend/backoffice && npm i && npm run dev   # http://localhost:5184
```

### Qué hay

| Ruta | Pantalla | Qué resuelve |
|---|---|---|
| `/reservas` | Agenda del día | Grilla cancha × media hora. Vender, cobrar, marcar ausencia, cancelar |
| `/canchas` | Editor de cancha | Horario que usa, duraciones, incremento, aviso mínimo, precios, vista previa |
| `/horarios` | Editor de horario | Horas semanales, fechas específicas que pisan la semana, vista de calendario |
| `/personas` | Base de personas | Búsqueda, filtros, ficha, alta de mostrador, importación (sólo la pantalla) |

### Cómo está armado

```
src/
├─ domain/    tipos y lógica pura: horarios, agenda, fechas, dinero
├─ api/       http.ts (fetch + sesión) · apiHttp.ts y personasHttp.ts (adaptadores) · queries.ts (React Query)
├─ ui/        theme.ts (paleta y controles) · Panel · Navegación · Tostadas · estados
└─ modulos/   una carpeta por módulo: reservas, canchas, horarios, personas
```

- **React Query es la única fuente de datos.** Nada de estado servidor en `useState`.
- **La sesión es el token** ([ADR-0018](docs/adr/0018-sesion-del-backoffice-token-en-sessionstorage-y-rol-en-la-claim.md)):
  el login pide sólo email y contraseña —el club sale de `users.tenantId`—, el token vive en
  `sessionStorage` y `auth/sesion.ts` lo decodifica para saber quién entró. **No hay endpoint de
  "quién soy"**: preguntarle al servidor algo que ya viaja firmado en el token es un round-trip de
  más. `auth/permisos.ts` traduce rol → qué se dibuja, y es **de presentación**: apaga botones y
  esconde módulos, no autoriza nada. Lo que un rol no puede usar no aparece en la navegación y su
  URL redirige.
- **Los adaptadores son la frontera.** `apiHttp.ts` (horarios, canchas, agenda) y
  `personasHttp.ts` (personas, contexto del club) traducen el JSON de la API a los tipos del
  dominio y devuelven las fechas **ya escritas** ("hace 3 días"): la pantalla muestra, no
  calcula. Los componentes no saben que existe HTTP.
- **Debajo de los adaptadores va el cliente generado** (ADR-0016): el
  código que Orval genera desde el contrato OpenAPI es la capa de cable; los adaptadores lo
  consumen y siguen siendo la frontera hacia el dominio. Nada de servicios escritos a mano al
  lado de lo generado — ver la regla en la sección 6.
- **Lo que se está mirando vive en la URL** (`rutas.ts`): módulo, deporte, día, filtro,
  búsqueda, ficha abierta. Lo transitorio —qué panel está abierto, un borrador sin guardar—
  se queda en el componente.
- **Los editores trabajan sobre un borrador.** Canchas y Horarios acumulan cambios en estado
  local y recién persisten al Guardar; Descartar vuelve a lo guardado.
- **Estilos inline con tokens** en `ui/theme.ts`, como el diseño. Lo único en CSS es el reset,
  las animaciones y los `:hover` / `:focus`, que un objeto de estilo no puede expresar.
- **Íconos con `lucide-react`** (decisión del usuario, 17/08/2026, vale para los dos
  frontends): nada de glifos de texto (`←`, `✓`, `×`) ni emojis como íconos de UI. Tamaños
  chicos (11–22 px), `strokeWidth` 1.8–2.5 según peso visual, `aria-hidden` cuando el botón ya
  tiene `aria-label`.

### Lo que falta

| | Parte |
|---|---|
| ⬜ | **Gating por módulo contratado**: hoy los cuatro módulos se montan siempre; falta el endpoint de capacidades y que una ruta de módulo apagado dé 404 |
| ⬜ | Acciones que todavía son sólo un aviso: bloquear horario, reprogramar, WhatsApp, exportar, elegir archivo de importación |
| ⬜ | **Ausencias**: la ficha las mostraba inventadas y se sacó el dato. No están modeladas todavía |
| ⬜ | Accesibilidad: foco visible, navegación por teclado en la grilla, atajo ⌘K que hoy es sólo el cartel |
| ⬜ | Responsive: está pensado para un monitor de mostrador, abajo de ~1000 px no se acomoda |

### Decisiones tomadas sobre el diseño

Van acá porque no se deducen del HTML y conviene revisarlas con el usuario:

- **Ruteo por módulo con react-router** en vez del `module` en estado. El diseño es una sola
  pantalla con un switch; un backoffice necesita URLs.
- **El precio de un turno sale de la tarifa de la cancha** (`precioDia` / `precioNoche` /
  `noche`), no de una constante por deporte. Con la configuración de fábrica da exactamente lo
  mismo que el diseño, pero además responde si el club cambia un precio.
- **"Descartar" vuelve a lo guardado**, no a los valores de fábrica.
- **Las duraciones que ofrece el panel de venta siguen siendo por deporte** (pádel 1 h / 1 h 30
  / 2 h, fútbol 1 h / 2 h), como en el diseño, aunque cada cancha ya tenga las suyas
  configurables. Es la inconsistencia que quedó: hay que decidir cuál manda.
