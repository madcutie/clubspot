# Plan — Disponibilidad de punta a punta: backoffice → base → portal de reservas

**Fecha:** 16/08/2026 · **Estado:** aprobado el 16/08/2026 — **F1, F2, F2R y F3 cerradas y
verificadas** (17/08); F4–F5 pendientes · Avance en la
[bitácora](plan-disponibilidad-e2e.bitacora.md)

## 1. Objetivo

Que el ciclo completo de disponibilidad funcione de verdad y se pueda probar de manera
automática con el navegador:

1. El operador configura horarios, canchas y excepciones en el **backoffice** (`:5184`).
2. Eso queda en la **base** (PostgreSQL), implementando
   [ADR-0013](adr/0013-disponibilidad-patron-semanal-mas-excepciones.md).
3. El **portal de reservas** (`:5183`) dibuja los huecos disponibles que salen de esa
   configuración, calculados por el backend.
4. Cada paso se verifica **conduciendo el navegador con la extensión de Chrome y consultando
   la base por SQL** después de cada acción, contra el catálogo de casos de la sección 7.

**Sólo se prueba eso.** El alcance es deliberadamente angosto: abrir horarios y ver cómo lo ve
la app cliente.

## 2. Fuera de alcance (explícito)

| Qué | Por qué queda afuera |
|---|---|
| Autenticación visible | No interesa para esta prueba (pedido del usuario). El backoffice hace **auto-login de desarrollo** contra el usuario sembrado, sin pantalla. El portal usa endpoints anónimos |
| Crear una reserva desde el portal | Depende del hold+TTL que ADR-0013 dejó abierto. El flujo confirmar→pagar del portal **queda en mock**, marcado |
| Tarifas por duración / historizadas | Validadas conceptualmente el 16/08 (precio no lineal por duración, `effective_from`), pero se difieren a su propio ADR. Acá rige el modelo actual día/noche |
| Personas (`/personas`) del backoffice | Sigue contra el mock; conectarla es un paso aparte (el backend de People ya existe). La Agenda **salió** de esta lista el 16/08: se conecta en F2R |
| Pantallas de módulo apagado, roles finos, responsive, accesibilidad | Nada de eso cambia acá |
| **Infraestructura de E2E real** (Playwright/Cypress, corridas en CI) | Explícitamente diferida (usuario, 16/08/2026): todavía no se quiere. La prueba de este hito es el catálogo de la sección 7 conducido por el agente con la extensión de Chrome + SQL; lo determinístico va en tests unitarios y de integración, que ya tienen infraestructura |

## 3. Decisiones que este plan fija

Se listan para que queden a la vista; cualquiera se puede vetar antes de aprobar.

1. **Codificación de `windows` en las excepciones**: el mismo objeto `TimeRange`
   (`{"opensAtMinute":540,"closesAtMinute":660}`) que ya usa `weeklyRanges`, no los pares
   `[[540,660]]` ilustrativos del ADR-0013. Regla "un concepto, un tipo": un rango horario se
   serializa de una sola manera en toda la base.
2. **API de excepciones = INSERT y DELETE, sin replace-all.** A diferencia de schedules/courts
   (PUT de colección completa con `xmin`), las excepciones son filas independientes: `POST`
   crea, `DELETE` borra. Es literalmente la consecuencia 1 de ADR-0013.
3. **Un solo endpoint de disponibilidad para el portal, por rango de fechas.** El backend hace
   la cuenta de ADR-0013 (patrón → excepciones → incremento/duración → aviso mínimo → precio)
   y devuelve los huecos de hasta 14 días de una vez; el frontend deriva de ese payload todo lo
   demás (contadores por día, grilla de horas, sugerencias). El motor de hash del prototipo
   **se borra**: el precio y la disponibilidad son autoridad del servidor.
4. **El portal identifica al club por slug en la ruta** (`/api/portal/{clubSlug}/…`), con un
   **filtro de grupo propio — no el patrón del sign-in**. El sign-in abre el ámbito de tenant
   adentro del handler, pero `RequireModule` es un endpoint filter que corre **antes** del
   handler y sin tenant explota (`ITenantModules` → `ITenantContext.Current` →
   `MissingTenantException` → 500). El grupo del portal registra primero un filtro que lee
   `{clubSlug}`, lo resuelve con `IClubDirectory` (404 si no existe — `clubs` no es
   `ITenantOwned`, funciona sin ámbito) y abre el ámbito de tenant **alrededor de
   `await next(...)`**; el chequeo del módulo `bookings` corre después, ya con tenant (404 si
   no está contratado). Además, `sport` llega por query string, donde el binding de enums es
   case-sensitive y los converters JSON no aplican: se recibe como **string y se parsea
   case-insensitive** (`"padel"`/`"football"`, coherente con la convención camelCase), con 400
   controlado si no matchea.
5. **Precio del turno, provisional**: tarifa por hora día/noche de la cancha, banda decidida
   por el minuto de inicio contra `nightStartsAtMinute`, escalado lineal por duración. Lleva
   el comentario de provisional; lo reemplaza el ADR de tarifas.
6. **La migración `Initial` se regenera** (base de dev descartable, decisión del 16/08): el
   esquema nuevo entra en la única migración existente, no en una segunda.
7. **UI mínima de excepciones**, provisional hasta que el usuario defina la pantalla (pregunta
   abierta de ADR-0013): pestaña "Excepciones" dentro de `/horarios` con lista de próximas,
   alta (alcance club o cancha · fechas agregadas de a una a un conjunto · cerrado o tramos ·
   motivo) y borrado. El modelo aguanta cualquier UI futura.
8. **Auto-login de desarrollo en el backoffice**: al arrancar, si no hay token, `POST
   /api/auth/session` con las credenciales sembradas leídas de `.env.development` (ya están en
   texto plano en el repo). Sin pantalla de login; archivo marcado como provisional.

## 4. Estado de partida (relevado el 16/08/2026)

Lo que condiciona el trabajo; el detalle completo está en el código.

- **Backend**: `Schedule` todavía tiene `TimeZone` (espejo muerto de `Club.TimeZone`, ya
  marcado en el código) y `SpecialDates` (una fecha por entrada — el problema exacto de
  ADR-0013). `Court` ya tiene todo lo demás: `IsActive`, `Durations`, `StartIncrementMinutes`,
  `MinimumNoticeMinutes`, `DayPrice`/`NightPrice` en `Money`, `NightStartsAtMinute`. No existe
  ninguna tabla de excepciones ni cálculo de disponibilidad ni tabla de reservas.
  `IClubSettings` no expone `TimeZone` ni `DepositPercent`. CORS sólo admite `:5184`. El seed
  no crea horarios ni canchas. Una sola migración: `20260816041600_Initial`.
- **Portal reservas**: cero HTTP. `availability.ts` inventa la disponibilidad con un hash
  determinístico; catálogo de canchas y club hardcodeados; el contrato es `api/mockApi.ts`
  (las pantallas no lo saben). Sin variables de entorno. Reservas hechas van a localStorage.
- **Backoffice**: `/horarios` y `/canchas` editan borradores y guardan **arrays enteros sin
  versión** (`guardarHorarios(Horario[])`); el backend real exige `version` (xmin) y devuelve
  409. Tipos del front: `Tramo=[number,number]`, `semanal: Record<number,Tramo[]>` (clave
  `getDay()`, 0=domingo), `fechas: FechaEspecial[]` (a reemplazar por excepciones), campo `tz`
  (a eliminar), `deporte: 'padel'|'futbol'` (el backend dice `football`).
- **Entorno**: Docker Desktop instalado pero **el daemon está apagado**; `compose.yaml` levanta
  `postgres:17`; la API en Development migra y siembra sola al arrancar (`:5037`); no hay
  `psql` en PATH (la base se consulta con `docker exec … psql`); `dotnet-ef` es tool local
  (`dotnet tool restore`, no documentado); `src/frontend/reservas` no tiene `node_modules`;
  el fallback del `ClubSpotDbContextFactory` usa password `postgres` en vez de `clubspot`.

## 5. Fases y paquetes

Cada paquete es autónomo y termina compilando en verde, con una excepción declarada:
**F1.1–F1.4 forman una sola unidad de verificación** — entre F1.1 (que saca columnas del
modelo EF) y F1.4 (que regenera la migración), la migración `Initial` vieja sigue creando
`timeZone`/`specialDates` NOT NULL y los tests de integración de schedules fallarían contra la
base migrada; vuelven a verde recién al cierre de F1.4. Convención de ejecución: la
especificación y las decisiones son de esta sesión; la ejecución mecánica se delega a un
workflow de modelo más barato **por paquete**, con verificación independiente posterior.

### F1 — Backend: ADR-0013 en código

| # | Paquete | Contenido |
|---|---|---|
| F1.1 | `Schedule` adelgaza | Quitar `TimeZone` y `SpecialDates` del agregado, la configuración EF, los DTOs y todos los tests que los construyen. Borrar `SpecialDate.cs`. Agregar `TimeZone` y `DepositPercent` a `IClubSettings`/`ClubInfo` |
| F1.2 | Agregado `AvailabilityOverride` | `Domain/Bookings/`: `Id`, `TenantId`, `CourtId?` (null = club), `Dates` (conjunto no vacío), `Windows` (`IReadOnlyList<TimeRange>`, vacío = cerrado, misma validación de superposición que el patrón), `Reason?`, `CreatedAt`, `CreatedBy`. Invariantes en el constructor. Las fechas se modelan como **entidad fila** `AvailabilityOverrideDate { OverrideId, TenantId, Date }` que implementa `ITenantOwned` y el agregado expone como colección — **no** como owned type: el loop de tenancy del `DbContext` llama `modelBuilder.Entity<T>()` sobre cada `ITenantOwned` y sobre un owned eso explota al arrancar, y sin `ITenantOwned` nada estampa el `tenantId` |
| F1.3 | `AvailabilityCalculator` | Servicio de dominio puro: (cancha, patrón, excepciones aplicables, ahora-local-del-club, rango de fechas) → por fecha: ventanas efectivas (cancha pisa club pisa patrón; empate de alcance → `CreatedAt` más reciente) → arranques por incremento y duración que entren en la ventana → filtro de aviso mínimo → precio por banda día/noche. Tests unitarios exhaustivos de la **parte de cálculo** de C1–C9 y C11–C15; C10 (cascada) y C16 (concurrencia) no son expresables acá y van a los tests de integración de F1.7 |
| F1.4 | Persistencia | Tablas `availabilityOverrides` (windows jsonb, reason, createdAt, createdBy; FK a courts con borrado en cascada) y `availabilityOverrideDates` (PK compuesta `(overrideId, date)`, índice `(tenantId, date)`, cascada desde el override), mapeadas según F1.2. Configuraciones EF; la convención de nombres físicos ya los nombra sola. **Regenerar `Initial`** (acá se caen también `timeZone`/`specialDates` de la tabla; cierre de la unidad F1.1–F1.4: `dotnet test` completo en verde) |
| F1.5 | Application + endpoints backoffice | Puerto + handlers: listar por rango, crear, borrar. `GET/POST /api/availability-overrides`, `DELETE /api/availability-overrides/{id}` — política `ConfigurationEdit` + `RequireModule(bookings)`, mismos códigos de error que el resto (422 validación) |
| F1.6 | Endpoints del portal | `GET /api/portal/{clubSlug}/catalog` (club: nombre, moneda, seña; canchas **activas** por deporte con duraciones — las inactivas no se exponen) y `GET /api/portal/{clubSlug}/availability?sport&from&to` (huecos calculados; `from` se recorta a hoy-del-club: nunca se dibuja hacia atrás; rango máximo 31 días; cada día incluye **todas** las canchas activas del deporte, con `slots` vacío si ese día no ofrece nada, para que el portal pueda mostrarla como no disponible). Mecánica de tenant y parseo de `sport` según la decisión 3.4 (filtro de grupo slug→ámbito **antes** de `RequireModule`; `sport` string case-insensitive). CORS: agregar `http://localhost:5183` |
| F1.7 | Seed + tests de integración | Sembrar base determinística, con todas las reglas fijadas: horario "Base" L–D 08:00–23:00 asignado a las tres canchas; Cancha 1 (pádel, techada, 60/90/120, inc. 30, aviso 0, día \$14.000, noche \$18.000); Cancha 2 (pádel, descubierta, 60/90/120, inc. 30, aviso 0, día \$12.000, noche \$16.000); Fútbol A (fútbol, 60, inc. 60, aviso 0, día \$30.000, noche \$36.000); noche desde 19:00 en las tres; moneda ARS del club. Tests de integración: CRUD de excepciones, disponibilidad del portal con excepción de club y de cancha, borrado en cascada (C10), conflicto de versión 409 (C16), gating 404, 401 en overrides sin token |

### F2 — Backoffice contra la API real (`/horarios` y `/canchas`)

| # | Paquete | Contenido |
|---|---|---|
| F2.1 | Configuración + auto-login | `.env.development` (`VITE_API_URL`, credenciales dev), cliente HTTP mínimo con el token en memoria, auto-login al arrancar. Provisional marcado |
| F2.2 | Adaptador HTTP de horarios/canchas | Reemplazar en `api/` las funciones de horarios/canchas por llamadas a `/api/schedules` y `/api/courts`. Regla madre: **el PUT es replace-all, así que el adaptador reenvía íntegro todo campo que vino en el GET** — un campo no mapeado se destruye en el primer guardado. Mapeo completo: `Tramo` ↔ `TimeRange` · `semanal` (clave `getDay()`) ↔ `weeklyRanges` (clave `DayOfWeek` PascalCase) · `futbol` ↔ `football` · `techada` ↔ `isCovered` · `ci` ↔ `sortOrder` · `noche` ↔ `nightStartsAtMinute` · precios ↔ `dayPrice`/`nightPrice`. Los tipos del front ganan `id` real y `version`; **`sel` en la URL pasa a ser el id** (hoy es índice posicional y el GET ordena por nombre: un rename reordena y `sel` apuntaría a otro elemento). Un 409 muestra "No se pudo guardar: la configuración cambió en el servidor. Recargá para ver lo último" (cubre las dos causas que el backend no distingue: versión vieja y horario en uso) y no pisa el borrador |
| F2.3 | Quitar `tz` y `fechas` — con su radio de rotura completo | El tipo `Horario` es compartido; quitarle campos rompe compilación fuera del editor y cada resto se cubre acá: la sección "Horas para fechas específicas" del editor (se elimina) · `CanchasScreen` línea "N fechas propias" (se elimina) · `tramosDelDia`/`arranquesFecha` en `domain/horarios.ts` quedan sólo con el patrón semanal (la vista previa de Canchas y la agenda mock pasan a ignorar fechas específicas, a propósito) · las semillas de `store.ts` pierden `tz`/`fechas` y ganan `id`/`version` fabricados, para que la Agenda mock siga compilando |
| F2.4 | Pestaña "Excepciones" | La UI mínima de la decisión 3.7, contra los endpoints de F1.5. `VistaHorario` en `rutas.ts` se extiende a `'lista' \| 'cal' \| 'excepciones'` (el parser hoy colapsa todo a `'lista'`), y la pestaña se renderiza **a nivel pantalla, no dentro del horario seleccionado**: las excepciones son de club o de cancha, no de un horario. Lista de próximas (alcance, fechas, ventanas, motivo), alta y borrado |

> ⚠️ **Desincronización aceptada** ~~: la Agenda (`/reservas`) sigue leyendo el mock en
> memoria~~ — **superada el mismo 16/08**: al ver la agenda con datos inventados, el usuario
> pidió eliminar los mocks de datos y mostrar lo real en las dos apps. Ver la fase F2R.

### F2R — Reservas reales mínimas y agenda conectada (ampliación del 16/08/2026)

Pedido del usuario al ver la agenda y el portal con datos inventados: **los mocks de datos ya
no sirven; las dos apps muestran lo que realmente está en la base.** Eso exige que exista la
reserva real. Decisiones que fija esta fase:

1. **La reserva del operador se implementa ya** — es exactamente lo que ADR-0002 decidió ("el
   operador vende en el momento", agenda calculada en lectura, exclusion constraint contra la
   doble venta). No necesita hold ni pago. Estados: `confirmed` / `cancelled`, nada más.
2. **Sin plata**: ni cobro, ni seña, ni estado de pago (finance pendiente, ADR-0012). La
   reserva guarda un **snapshot informativo del precio** en `Money`, calculado por el
   servidor al crear. Los botones Cobrar/Seña/Ausente del panel quedan como avisos
   provisionales.
3. **Cliente de la reserva**: `customerName` obligatorio + `customerPhone` opcional, campos
   propios de `bookings` — **provisional** hasta definir el vínculo con `people` (ADR-0012:
   se venden turnos a no-socios; el link a `personId` llega con el flujo de identidad).
4. **Doble venta**: exclusion constraint `btree_gist` sobre (cancha, fecha, rango de minutos)
   **sólo para confirmadas**; el endpoint devuelve 409. La validación previa (ventana,
   duración e incremento de la cancha) da 422; el aviso mínimo **no** aplica al operador.
5. **La migración `Initial` deja de regenerarse**: esta fase entra como migración **aditiva**
   (`Bookings`), porque trae SQL a mano (extensión + exclusion constraint) que una
   regeneración pisaría. De acá en adelante la cadena crece.
6. **La disponibilidad resta reservas**: la 4.ª consulta de ADR-0013 se vuelve real — el
   calculador descarta arranques que se superpongan con una confirmada, y el portal lo
   refleja.
7. **Reservar desde el portal sigue afuera** (hold+TTL y pagos, pendientes del usuario): el
   portal muestra disponibilidad real y el CTA de reserva queda gateado con aviso
   provisional; "Mis reservas" pierde las semillas de localStorage y muestra vacío real.

| # | Paquete | Contenido |
|---|---|---|
| F2R.1 | Dominio y persistencia de `Booking` | Agregado (id, courtId, date, startMinute, durationMinutes, price `Money`, customerName, customerPhone?, status, createdAt/createdBy, cancelledAt?) con invariantes y `Cancel()`; enum con converter camelCase; tabla `bookings` + índices; `btree_gist` + exclusion constraint en migración aditiva; tests unitarios |
| F2R.2 | Endpoints y resta de disponibilidad | El calculador recibe las confirmadas del día y descarta arranques superpuestos (tests de solape parcial/exacto/adyacente). `POST /api/bookings` (valida ventana+duración+incremento, precio snapshot server-side, 409 en choque con backstop del constraint), `POST /api/bookings/{id}/cancel`, `GET /api/agenda?sport&date` (por cancha: ventanas efectivas, huecos con precio y reservas confirmadas con nombre). Política de operación espejando las existentes + `RequireModule(bookings)`. Tests de integración: crear→el portal pierde el hueco, doble venta→409, cancelar→vuelve |
| F2R.3 | Agenda del backoffice real | `/reservas` deja el mock: la grilla se dibuja del endpoint de agenda (cerrado desde ventanas reales, tarjetas desde reservas reales, celdas libres desde los huecos con precio); Vender crea la reserva real (nombre/teléfono) y Cancelar cancela; Cobrar/Seña/Ausente/Bloquear siguen como avisos provisionales; el encabezado deja de mostrar "$ por cobrar" (no hay datos de pago). `store.ts` pierde canchas/horarios/agenda; **personas sigue en mock** (su conexión es un paso aparte — el backend de People ya existe) |

### F3 — Portal de reservas contra la API real

| # | Paquete | Contenido |
|---|---|---|
| F3.1 | Configuración | `npm i` inicial, `.env.development` (`VITE_API_URL`, `VITE_CLUB_SLUG=chaco-for-ever`) |
| F3.2 | Adaptador HTTP | `mockApi.ts` pasa a pedir `catalog` una vez y `availability` por deporte (14 días en una llamada, cache React Query), mapeando `padel`/`futbol` ↔ `padel`/`football` en la query y a la vuelta. Borrar el motor de hash de `availability.ts` y el catálogo hardcodeado; el precio es el del servidor. Decisiones que el adaptador no puede improvisar: **la grilla de horarios pasa de horas enteras a arranques reales** — `HourDto.h` deja de ser "hora (8…23)" y pasa a minuto de inicio, la grilla enumera los `startMinute` del payload (el seed produce arranques :30) y `covered`/labels/keys se ajustan en `AvailabilityScreen` · **el índice de día se ancla a la primera `date` del payload**, no al reloj del navegador · **una cancha sin huecos ese día se muestra "No disponible" sin precio** (viene con `slots` vacío) · los contadores de canchas del Home salen del catalog, no del literal "4 canchas"/"3 canchas" |
| F3.3 | Fin de los datos falsos, y el destino de cada campo de `CLUB` | (Ajustado por F2R.) El flujo confirmar/pagar se **gatea**: elegido el hueco, la pantalla de confirmación avisa que la reserva online todavía no está habilitada (hold+TTL y pagos pendientes) y no simula ningún pago; el motor de hash, el catálogo hardcodeado, las semillas de localStorage y `PaymentRejectedError` **se borran**; "Mis reservas" muestra vacío real. Campo por campo de `CLUB`: `senaPct` → `depositPercent` del catalog, pasado por parámetro a `senaOf` · `cancelHoras` → constante local marcada provisional · `apertura`/`cierre` → el copy se reescribe sin esos valores · `diasVisibles` → constante de UI · la rama `reason: 'torneo'` y `TORNEO_DIA_IDX` se eliminan · `nombre`/`direccion` → del catalog. `staleTime` corto o invalidación al volver, para que refleje cambios del backoffice sin recargar |

### F4 — Herramientas de prueba E2E

| # | Paquete | Contenido |
|---|---|---|
| F4.1 | Scripts | `scripts/dev-up.ps1` (arranca Docker Desktop si hace falta, `docker compose up -d postgres`, API y los dos Vite en background) · `scripts/db-reset.ps1` (**detiene la API si está corriendo** y hace `compose down -v` + `up -d`; la migración y el seed **no** los hace este script: ocurren al arrancar la única API de `dev-up.ps1` — así nunca hay dos instancias peleando el puerto 5037) · `scripts/db-sql.ps1 "<SQL>"` (envuelve `docker exec … psql -U postgres -d clubspot -c`) |
| F4.2 | Higiene | Alinear el password del fallback de `ClubSpotDbContextFactory` a `clubspot`; documentar `dotnet tool restore`; corregir el README (pnpm→npm, agregar backoffice y API al arranque rápido) |

### F5 — Ejecución del catálogo

Con todo arriba: correr los 16 casos de la sección 7 conduciendo Chrome sobre `:5184` y
`:5183` y verificando la base tras cada acción con `db-sql.ps1`. Resultados (pasa/falla,
evidencia, correcciones) van a la bitácora. Un caso que falla se arregla y se re-corre entero.

## 6. Contratos nuevos (resumen)

```
GET  /api/availability-overrides?from&to      → [{ id, courtId, dates[], windows[], reason, createdAt, createdBy }]
POST /api/availability-overrides              ← { courtId?, dates[≥1], windows[] ([]=cerrado), reason? } → 201 { id }
DELETE /api/availability-overrides/{id}       → 204

GET /api/portal/{clubSlug}/catalog            → { club: { name, venue, currency, depositPercent },
                                                  sports: [{ sport, courts: [{ id, name, detail, isCovered, durations[] }] }] }
GET /api/portal/{clubSlug}/availability?sport&from&to
                                              → { currency, days: [{ date, courts: [{ courtId,
                                                  slots: [{ startMinute, duration, price }] }] }] }
```

Los rangos horarios viajan siempre como `{ "opensAtMinute": n, "closesAtMinute": n }`. En el
portal, `sport` viaja como string camelCase (`padel`/`football`) y el backend lo parsea
case-insensitive (decisión 3.4); `days[].courts` trae **todas** las canchas activas del
deporte, con `slots: []` cuando ese día no ofrecen nada.

## 7. Catálogo de casos de prueba

Convenciones: la base se consulta con `db-sql.ps1`; **todo identificador va entre comillas
dobles** (son camelCase). "Portal" = `:5183`, "BO" = backoffice `:5184`. Cada caso parte del
estado que dejó el anterior salvo que indique reset.

| # | Caso | Acción (BO) | Esperado en portal | Esperado en base |
|---|---|---|---|---|
| C1 | Estado sembrado | — (reset limpio) | Pádel muestra huecos 08:00–23:00 en las 2 canchas, 14 días; fútbol lo suyo | 1 fila en `schedules`, 3 en `courts`; `SELECT count(*) FROM "availabilityOverrides"` = 0 |
| C2 | Editar horas semanales | En `/horarios`, martes: dejar sólo 08:00–13:00. Guardar | El martes de la semana próxima sólo ofrece mañana | `"weeklyRanges"->'Tuesday'` = un solo rango 480–780 |
| C3 | Día cerrado por patrón | Domingo sin tramos. Guardar | Los domingos aparecen sin horarios ("LLENO") | `"weeklyRanges"` sin clave `Sunday` (o `[]`) |
| C4 | Crear horario y asignarlo | Crear "Reducido" L–V 16:00–20:00; en `/canchas` asignarlo a Cancha 2 | Cancha 2 sólo ofrece tarde; Cancha 1 no cambia | `courts."scheduleId"` de Cancha 2 apunta al nuevo id |
| C5 | Excepción club cerrada (feriado cargado a mano) | Excepciones: alcance club, un miércoles futuro, cerrado, motivo "feriado" | Ese día: ningún deporte ofrece nada | 1 fila `"availabilityOverrides"` con `"courtId" IS NULL`, `windows='[]'`; 1 fila en `"availabilityOverrideDates"` |
| C6 | Excepción de una cancha | Alcance Cancha 1, un viernes futuro, cerrado | Ese viernes Cancha 1 figura "No disponible" (sin huecos ni precio) en la lista de canchas; Cancha 2 sigue ofreciendo | Fila con `"courtId"` = id de Cancha 1 |
| C7 | Ventanas parciales | Alcance club, un jueves futuro, tramos 08:00–09:00 y 11:00–13:00 | Ese jueves: arranques 08:00 y 11:00–12:xx; nada de 09:00 a 11:00 | `windows` con los dos objetos `TimeRange` |
| C8 | La más específica gana | Un **lunes** futuro (fecha nueva, distinta de la de C5 — sus excepciones persisten hasta el final de la corrida): excepción club cerrada + excepción Cancha 1 con 10:00–12:00, ambas para esa fecha | Cancha 1 ofrece 10–12; Cancha 2 nada | Dos filas, una por alcance |
| C9 | Empate → más reciente | Dos excepciones de Cancha 1 misma fecha: primero 08–10, después 14–16 | Cancha 1 ofrece sólo 14–16 | Dos filas mismo `"courtId"`; decide `"createdAt"` mayor |
| C10 | Borrar excepción | Borrar la excepción de C5 | El miércoles vuelve a dibujarse por patrón | La fila y sus fechas desaparecen (cascada): `count(*)` en dates para ese id = 0 |
| C11 | Conjunto de fechas salteadas | Una excepción con lun, mié y vie de una misma semana, cerrada | Esos tres días sin huecos; mar y jue intactos | 1 fila en overrides, **3** en dates con el mismo `"overrideId"` |
| C12 | Incremento y duración | Cancha 1: incremento "En punto". Guardar | La verificación es **por cancha**, no por grilla (la grilla junta canchas y Cancha 2 sigue con inc. 30): en un día hábil, en el arranque 16:30 aparece sólo Cancha 2 y en el de 17:00 aparecen las dos; con 90' además no se ofrece un arranque que no entre antes del cierre | `courts."startIncrementMinutes"` = 60 |
| C13 | Aviso mínimo | Cancha 1: aviso 12 h. **Correr antes de las 20:00 hora del club** (después, hasta el hueco de mañana 08:00 cae dentro de la ventana y "mañana completo" sería falso) | Verificar sobre Cancha 1 en la lista de canchas: hoy, en una hora dentro de las próximas 12 h, Cancha 1 no aparece (Cancha 2 sí); mañana Cancha 1 completa | `courts."minimumNoticeMinutes"` = 720 |
| C14 | Cancha desactivada | Desactivar Fútbol A | Fútbol queda sin canchas ofrecidas | `courts."isActive"` = false |
| C15 | Cambio de precio | Subir precio noche de Cancha 1. Guardar | Un hueco de 20:00 muestra el precio nuevo; uno de 10:00 el diurno sin cambios | `"nightPriceAmount"` actualizado; moneda sigue `ARS` |
| C16 | Concurrencia entre operadores | Dos pestañas de `/horarios`, cada una con una **edición distinta y concreta** (sin editar no hay borrador y Guardar es inerte; y si las ediciones coinciden EF no emite UPDATE y no hay conflicto): pestaña A cambia el cierre del miércoles, pestaña B el del jueves. Guardar A, después guardar B | — | B recibe 409, muestra el aviso y **no** pisa: la base conserva el cambio de A y no tiene el de B |

Casos negativos incluidos en tests de integración (no manuales): excepción sin fechas → 422;
tramos superpuestos → 422; `courtId` inexistente → 422; overrides sin token → 401; portal con
slug inexistente → 404; módulo `bookings` no contratado → 404.

## 8. Protocolo de ejecución

1. **Prerrequisito manual**: Docker Desktop arrancado (el daemon está apagado en la máquina).
2. `scripts/db-reset.ps1` → base limpia, migrada y sembrada. **El Postgres de ClubSpot escucha
   en el 5433** (el 5432 lo ocupa el Postgres de otro proyecto que arranca solo con Docker;
   override con `CLUBSPOT_PG_PORT`).
3. `scripts/dev-up.ps1` → API `:5037`, BO `:5184`, portal `:5183`.
4. Correr C1→C16 en orden con la extensión de Chrome; después de cada acción del BO, la
   verificación SQL y la verificación visual en el portal (recargar o esperar la
   invalidación).
5. Registrar cada caso en la bitácora: pasa/falla, y si falla, la corrección y la re-corrida.

## 9. Qué queda abierto después de esto

- **Hold+TTL y crear la reserva desde el portal** — la pregunta abierta de ADR-0013; es el
  paso natural siguiente si esta prueba cierra bien.
- **ADR de tarifas** (precio por duración no lineal, `effective_from`, banda, audiencia) —
  validado conceptualmente el 16/08 contra el modelo externo, pendiente de escribirse.
- **Cambio de horario programado a futuro** (temporadas): anotado el 16/08; se decide cuando
  se defina con cuánta anticipación abre la agenda.
- **Pantalla definitiva de excepciones** — el usuario todavía no definió calendario
  multi-fecha vs. rangos; la pestaña mínima de F2.4 es provisional.
- Conectar Agenda y Personas del backoffice; borrar del todo `store.ts`.
