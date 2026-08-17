# Bitácora — plan de disponibilidad de punta a punta

Registro de avance de [`plan-disponibilidad-e2e.md`](plan-disponibilidad-e2e.md). La entrada
más nueva va arriba.

---

## 17/08/2026 — F2R y F3 ejecutadas y verificadas ✅ — se acabaron los mocks de datos

**Origen**: el usuario vio la agenda y el portal con datos inventados (4 canchas de pádel, 3
de fútbol, reservas de mentira) y pidió eliminarlos: *"ya no necesito esos mocks... hay que
quitar y mostrar lo que realmente está, tanto en reservas como en backoffice"*. Eso obligó a
crear la reserva real, que estaba diferida: se escribió la fase **F2R** en el plan con sus 7
decisiones antes de ejecutar.

**Qué se implementó**: agregado `Booking` (confirmed/cancelled, sin plata, contacto walk-in
provisional por ADR-0012) · tabla `bookings` con **exclusion constraint `btree_gist`
anti-doble-venta** en migración **aditiva** (la `Initial` deja de regenerarse) · la
disponibilidad **resta las reservas confirmadas** (la 4.ª consulta de ADR-0013 se volvió
real) · endpoints de crear/cancelar reserva y de agenda por día · la agenda del backoffice
y el portal completos contra la API.

**Verificación de punta a punta, con el navegador y SQL:**
- Backend: build 0 warnings · **56 unitarios + 34 de integración en verde**.
- Agenda del backoffice: muestra **2 canchas de pádel reales** (no las 4 del mock), 0 turnos,
  días reales desde hoy, sin "$ por cobrar".
- **Se creó una reserva desde la UI** (clic en el hueco de las 15:00 → panel con el precio
  real \$14.000 → Confirmar): fila en PostgreSQL (`date`, `startMinute` 900, 60', "Dario
  Quintana", 14000.00, Confirmed, Cancha 1) y tarjeta en la grilla.
- **El portal lo refleja**: en las 15:00, "1 cancha libre", **Cancha 1 = NO DISPONIBLE sin
  precio** y Cancha 2 = \$12.000 "última libre". Por HTTP: 15:00 de Cancha 1 desaparece,
  Cancha 2 la conserva, 14:00-120' (que solaparía) desaparece y 14:00-60' (adyacente)
  sobrevive.
- Portal sin datos falsos: club y sede del catalog, 2 canchas de pádel + 1 de fútbol,
  arranques cada 30 min (granularidad real), sin horarios pasados.

**Notas**: la reserva de prueba quedó en la base a propósito, como dato de demo. El CTA de
reservar del portal está gateado con aviso (hold+TTL y pagos siguen pendientes de decisión del
usuario). Personas del backoffice sigue en mock: conectarla es un paso aparte.

**Entorno**: tras un reinicio de la máquina, el 5432 quedó tomado por el Postgres de otro
proyecto que arranca solo con Docker → el Postgres de ClubSpot pasó al **5433** (compose,
appsettings y factory de `dotnet ef`; override con `CLUBSPOT_PG_PORT`).

**F4.1 hecho el mismo día**, por necesidad: los servicios que el agente levanta en segundo
plano mueren al terminar cada turno, así que se escribieron `scripts/dev-up.ps1` (Docker +
PostgreSQL + API + los dos frontends, cada uno en su ventana), `scripts/db-sql.ps1` (psql
dentro del contenedor: no hace falta instalarlo) y `scripts/db-reset.ps1` (borra el volumen;
**no** migra ni siembra — eso lo hace la API al arrancar, evitando el doble arranque que
señaló la crítica). `db-sql.ps1` verificado contra la base. Comandos documentados en AGENTS.md
§6, junto con el puerto 5433 y `dotnet tool restore`.

**Dónde quedó**: F2R, F3 y F4.1 cerradas. **Próximo paso**: F4.2 (higiene menor: README) y F5
(catálogo de 16 casos), o lo que el usuario priorice.

---

## 16/08/2026 — F2 ejecutada y verificada ✅

Tres paquetes por workflow: F2.1 (config + auto-login) en Sonnet; F2.2+F2.3 (adaptador HTTP,
migración de tipos y radio de rotura) y F2.4 (pestaña Excepciones) en Fable. Los tres con
typecheck y build en verde.

**Qué quedó**: `src/api/config.ts` + `http.ts` (auto-login dev single-flight, `ApiError`,
retry único ante 401) · `apiHttp.ts` con el mapeo completo (semanal↔weeklyRanges PascalCase,
futbol↔football, ci↔sortOrder, techada↔isCovered, replace-all que reenvía todo) ·
`Horario`/`Cancha` con `id`/`version`, sin `tz`/`fechas`, `FechaEspecial` eliminada · `sel`
por id en la URL · 409 → toast sin pisar el borrador · pestaña Excepciones completa (alcance
club/cancha, fechas como conjunto, cerrado o bloques múltiples, motivo, borrado).

**Verificación** (contrato por HTTP + navegador con la extensión):
- Flujo del front por HTTP: login → GET schedules/courts con Bearer → PUT con versión
  correcta 204 → **PUT con versión vieja y cambio real 409 y la base no se pisa** → CRUD de
  excepciones completo (crear con 2 fechas → el portal da 0 slots ese día → DELETE 204).
- Con el navegador sobre `:5184`: `/horarios` muestra "Base" del seed real con las 3 canchas
  aplicadas; **editar el martes a 08–13 y Guardar quedó en la base** (jsonb verificado) y se
  revirtió igual por UI; la pestaña Excepciones **creó un feriado de club desde el
  formulario** (fila con `courtId NULL`, `windows []`, fecha y motivo verificados por SQL),
  el portal devolvió 0 slots ese día, y el botón "−" **borró con cascada** (ambas tablas en
  0); `/canchas` muestra las 3 canchas reales con precios y reglas del seed; `/reservas`
  (agenda mock, desincronización aceptada) renderiza sin errores de consola.

**Observaciones menores anotadas, fuera del alcance de F2**: el header lateral (sede/operador
"Rubén Medina") sigue siendo mock — es de la parte de contexto/roles pendiente; el dominio
mock del backoffice tiene la fecha simulada clavada (chips "vie 14" en agenda y vista previa
de canchas) — desaparece cuando la agenda se conecte. Un intento de PUT malformado (objeto en
vez de array, error de tooling de prueba, no del front) devuelve 500 en vez de 400: nicety de
manejo de body inválido para anotar, no bloquea.

**Dónde quedó**: F2 cerrada. **Próximo paso**: F3 (portal de reservas contra la API) cuando
el usuario lo pida.

---

## 16/08/2026 — F1 ejecutada y verificada ✅

El usuario aprobó el plan ("dale, arranca con F1"). Los 7 paquetes se ejecutaron por workflow
delegado: F1.1–F1.5 en Sonnet (mecánicos, spec cerrada) y F1.6–F1.7 en Fable — a pedido del
usuario, que preguntó si no convenía Fable; criterio acordado: **Sonnet para lo mecánico,
Fable para lo sutil** (filtro de tenant del portal, diseño de tests de integración). El
calculador (F1.3), que ya había corrido en Sonnet, se revisó línea por línea en la
verificación en vez de re-ejecutarse.

**Verificación independiente (además del build/tests por paquete):**
- Suite completa: **46/46 unitarios + 26/26 de integración en verde** (Testcontainers).
- `AvailabilityCalculator` revisado línea por línea: precedencia cancha>club>patrón con
  desempate por `CreatedAt`, arranques alineados al reloj, aviso mínimo cruzando días, corte
  nocturno inclusivo, sin duplicados posibles. Correcto.
- Migración `Initial` regenerada: `schedules` quedó `id/tenantId/name/weeklyRanges/xmin`;
  tablas nuevas `availabilityOverrides`/`availabilityOverrideDates` con cascadas y
  `ixAvailabilityOverrideDatesTenantIdDate`, todo nombrado por la convención.
- Smoke test real: base de dev recreada (`compose down -v`), API arriba migrando y sembrando
  sola, y por HTTP: catalog anónimo con las 3 canchas · availability de un martes = **84
  slots por cancha** (29 de 60' + 28 de 90' + 27 de 120', 08:00→23:00 cada 30) · precios
  nocturnos exactos (18.000×1,5=27.000 / 16.000×1,5=24.000) · `sport=basura` → 400 ·
  slug inexistente → 404. En la base: 11 tablas camelCase plural, seed exacto, jsonb con
  claves PascalCase (`"Monday"`) como esperan los casos C2/C3.

**Desvíos aceptados tras revisión (quedan registrados):**
1. **Redondeo de precio**: el mock del backoffice redondea el precio del turno al centenar;
   el calculador escala lineal exacto sin redondear. Con los precios del seed no hay
   diferencia observable (todo da múltiplo de 100). Se resuelve de una vez en el ADR de
   tarifas — anotado ahí como insumo.
2. **Fix de producción en `Schedule`** (surgió en F1.7): la validación de superposición
   aplanaba los rangos de TODOS los días juntos — bug latente que impedía repetir el mismo
   rango en dos días (el horario "Base" era inconstruible). Ahora valida por día, que es lo
   que ADR-0013 describe. Con test de regresión.
3. **F1.5 sin capa de handlers**: los endpoints de overrides llaman al puerto directo,
   espejando el patrón existente de `PeopleEndpoints`→`IPeopleQueries`.

**Dónde quedó**: F1 cerrada. **Próximo paso**: F2 (backoffice contra la API real) cuando el
usuario lo pida.

---

## 16/08/2026 — Ronda de crítica adversarial incorporada al plan

Antes de presentarlo, tres agentes críticos contrastaron el plan contra el código real
(backend, frontends, catálogo). Encontraron 2 bloqueantes y 6 mayores; todos quedaron
resueltos **en el texto del plan**, que era el objetivo: que ningún paquete delegado herede
una decisión abierta. Lo más importante:

- **Bloqueante (backend)**: el mecanismo del portal "abrir el ámbito de tenant en el handler,
  como el sign-in" no funcionaba — `RequireModule` es un filter que corre **antes** del
  handler y sin tenant lanza `MissingTenantException` → 500 en cada request. El plan ahora
  especifica un filtro de grupo que resuelve el slug y abre el ámbito alrededor de `next()`,
  con el chequeo de módulo después (decisión 3.4).
- **Bloqueante (portal)**: la grilla del portal modela horas enteras y el seed produce
  arranques :30 — C7/C12 eran inverificables. F3.2 ahora fija que la grilla enumera los
  `startMinute` reales del payload.
- Mayores: `?sport=padel` daba 400 (binding de enums case-sensitive en query — verificado
  empíricamente; se parsea string case-insensitive) · el mapeo de las fechas del override
  chocaba con la maquinaria de tenancy del DbContext en sus dos caminos naturales (fijado:
  entidad fila `ITenantOwned` no-owned) · quitar `tz`/`fechas` del tipo `Horario` rompía
  compilación en archivos que ningún paquete cubría (F2.3 ahora enumera el radio completo) ·
  el mapeo de canchas omitía `sortOrder`/`isCovered` sobre un PUT replace-all que destruye lo
  no mapeado (regla madre agregada) · varios campos de `CLUB` del portal quedaban sin fuente
  (F3.3 ahora los resuelve campo por campo) · C16 no producía el 409 tal como estaba escrito
  (reescrito con ediciones distintas por pestaña).
- Menores: fechas de C8/C10 en colisión, C13 dependiente de la hora de corrida, doble
  arranque de la API entre scripts, `sel` posicional vs. reordenamiento por nombre, mapeo
  `futbol↔football` faltante en el portal, alcance real de los tests unitarios del calculador.
  Todos corregidos en el plan.

Los críticos también **confirmaron** puntos que estaban bien: el jsonb con claves PascalCase
que citan C2/C3, los chips del backoffice que el catálogo necesita, los nombres SQL de todos
los casos, la cascada `courtId`→`courts` sin choque con el replace-all, y que la Agenda mock
no explota en runtime con la desincronización aceptada.

**Dónde quedó**: plan corregido y completo. **Próximo paso**: aprobación del usuario.

---

## 16/08/2026 — El plan se escribe; esperando aprobación

**Contexto.** El usuario validó contra dos páginas externas (un modelo ER y datos de ejemplo
de un sistema de reservas de pádel, generados en otra conversación) que el diseño de ADR-0013
va por buen camino, y pidió: implementar el backend, conectar las pantallas de horarios y
canchas del backoffice y el portal de reservas, y poder probar el ciclo completo de manera
automática con la extensión del navegador verificando la base. Sin autenticación visible.
Sólo se prueba eso: abrir horarios y ver cómo lo ve la app cliente.

**De la validación externa quedó**, además de la confirmación del modelo de excepciones:
- El precio por duración **no es lineal** (60'=\$14.000, 90'=\$20.000 en el ejemplo externo) y
  las tarifas historizadas con `effective_from` son válidas → **ADR de tarifas pendiente**, no
  entra en este plan.
- El snapshot del precio en la reserva es lo que hace seguro cambiar precios → se anota para
  cuando exista la reserva.
- El cambio de horario programado a futuro (temporadas) es el único agujero honesto de
  ADR-0013 → queda anotado, se decide junto con la anticipación de apertura de agenda.

**Se relevó el estado real** (workflow de 4 lectores: backend, portal, backoffice, entorno).
Hallazgos que moldearon el plan: `Schedule` todavía carga `TimeZone` y `SpecialDates`; el
portal no tiene ni una llamada HTTP y su disponibilidad es un hash; el backoffice guarda
arrays sin versión contra un backend que exige `xmin`; CORS no admite al portal; el seed no
crea canchas ni horarios; Docker Desktop está instalado pero apagado y no hay `psql` (la base
se consulta con `docker exec`).

**Decisiones fijadas en el plan** (sección 3): `windows` con el mismo `TimeRange` de
`weeklyRanges` — no los pares del ejemplo del ADR —; excepciones por INSERT/DELETE sin
replace-all; un solo endpoint de disponibilidad por rango, anónimo, con el club por slug en la
ruta; precio provisional día/noche lineal; migración `Initial` regenerada; UI mínima de
excepciones provisional; auto-login de desarrollo sin pantalla.

**Aclaración del usuario (mismo día)**: todavía **no** quiere E2E reales — nada de
Playwright/Cypress ni corridas en CI. La verificación de este hito es el catálogo conducido
por el agente (extensión de Chrome + SQL); quedó agregado a "Fuera de alcance" del plan.

**Dónde quedó**: plan escrito, catálogo de 16 casos listo. **Próximo paso**: aprobación del
usuario; después F1 (backend) en adelante, con ejecución mecánica delegada por paquete y
verificación independiente.
