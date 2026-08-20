# Bitácora — plan del registro de actividad

Registro de avance del [plan](plan-activity-log.md). La entrada más nueva arriba.

## 19/08/2026 — F1 ejecutada: el registro existe y es confiable

Se implementó la fase 1 del plan. Qué quedó en pie:

- **Entidad y tabla.** `ActivityLogEntry` en `Domain/Core/Activity/`, tabla `activityLogEntries`
  con `data` en `jsonb`, **sin foreign keys** hacia bookings, people ni payments, e índices por
  `(tenantId, occurredAt)`, `(tenantId, bookingId)` y `(tenantId, personId)`. Migración
  `20260820014307_ActivityLog`.
- **El actor no se resuelve solo.** `IActivityActor` + `AsyncLocalActivityActor`, copiado del
  patrón de `ITenantContext`: sin ámbito abierto, lanza. En HTTP autenticado lo abre un
  middleware nuevo con `source = counter`; el portal anónimo lo abre su propio filtro con
  `portal`; los webhooks de pago, con `webhook`. Nada adivina un actor.
- **El puerto no hace `SaveChanges`.** La entrada se agrega al mismo `DbContext` y viaja con la
  transacción del hecho. Se sumó un `DiscardPending()` para el único caso donde hace falta: el
  `SaveChanges` que falla por la restricción de exclusión y se reintenta con otro desenlace
  (el pago que llega cuando ya perdió el turno) — la entrada de "confirmado" describe algo que
  no pasó y se descarta antes de escribir la de "huérfano".
- **Cableado.** `bookingCreated`, `holdCreated`, `holdReleased`, `holdExpired`, `checkoutIssued`,
  `paymentApproved`, `paymentRejected`, `paymentOrphaned` (con su motivo: `duplicate`,
  `bookingLost`, `wrongCurrency`, `short`, `slotLost`), `personCreated` (mostrador y portal),
  `personNoteAdded` y `personPaymentRegistered`.
- **Tests**: 5 nuevos (148 en total, todos verdes). Cubren que la entrada viaja con el hecho, que
  el webhook queda como actor sistema con `source=webhook`, que el mostrador deja el usuario que
  actuó, que un tipo destructivo sin motivo lanza y que registrar sin ámbito de actor lanza.
- **Verificación en vivo**: reserva del portal con seña, pagada con el gateway fake, y las dos
  entradas leídas por SQL en orden, con el actor y el origen correctos.

**Lo que quedó deliberadamente afuera de F1, y por qué:**

- **`bookingCancelled` y `personBlocked` no se registran todavía.** Los dos exigen motivo
  (ADR-0017 §7) y hoy ni la API ni el backoffice lo piden: cablearlos ahora obligaba a inventar
  un motivo o a romper la cancelación. Es exactamente el contenido de F2, que los sube de punta
  a punta —API que responde 422 sin motivo y panel que lo pide antes de cancelar—.
- **`checkoutIssued` necesitó un método propio en `IBookingsStore`.** El handler que emite el
  link no escribe nada en la base, así que no había transacción a la cual sumarse. Se agregó
  `RecordCheckoutIssuedAsync`, que registra y confirma: no es un caso que se repita, pero el
  link emitido es justo lo que el canchero pregunta ("¿ya se lo mandé?").
- **`holdExpired` se confirma por su cuenta.** La expiración perezosa ya corre en su propio
  `UPDATE`, no dentro de la transacción de quien la disparó; la entrada se confirma con ella y
  no con el trabajo del que pasaba por ahí, que puede fallar por otra razón.

**Un arreglo de tests que apareció de rebote**: el reset de `SchedulePersistenceTests` borraba
canchas sin borrar antes las reservas que las referencian, y funcionaba sólo porque ningún test
anterior de la colección había vendido nada. Al sumar la clase nueva, la suerte se acabó.

## 19/08/2026 — Objeción abierta: quién puede leer el registro general

El usuario preguntó, sobre el endpoint general de lectura: *"me pregunto qué endpoint de
activityLog estamos queriendo exponer, alguien con usuario mínimo se loguea y me revienta la
base de datos"*.

Es una objeción válida y toca dos riesgos distintos que el plan hoy mezcla:

1. **Quién ve qué.** El plan dice que la pantalla general pide rol administrativo (§3.5), pero no
   dice qué pasa con un usuario de rol bajo que llama la ruta a mano. La respuesta tiene que ser
   la misma que con los módulos: no existe para él.
2. **Cuánto puede pedir.** Un `GET /api/activity` sin ventana obligatoria ni tope de página es un
   `SELECT *` sobre la tabla que más crece del sistema. Eso no lo arregla un rol: lo arregla el
   contrato del endpoint.

**Se resuelve antes de escribir F3**, y hasta entonces la ruta general queda fuera de alcance.
Las dos rutas por sujeto (`/bookings/{id}/activity`, `/people/{id}/activity`) no tienen este
problema: están acotadas por un id y por el permiso de ver ese sujeto.

## 19/08/2026 — ADR y plan escritos, sin arrancar

- El tema apareció al preguntar qué quedaba pendiente después de conectar la base de personas.
  El pedido original fue "el traffic log", y al desarmarlo quedó claro que no era tráfico HTTP
  sino la crónica del negocio: entra un pago y hay que asentarlo, se cancela un turno y hay que
  asentarlo.
- **El usuario amplió el alcance sobre lo que decía AGENTS.md §9.1**: no es sólo auditoría, el
  canchero también tiene que poder ver qué pasó y cómo. Y no son sólo acciones de usuarios:
  también eventos que llegan solos, como la entrada de un webhook, para saber cuándo entró.
- Se evaluó el nombre. `trafficLog` se descartó porque "traffic" en software significa tráfico
  de red — de hecho, al pedirlo así, lo primero que se entregó fue un log de requests HTTP.
  **El usuario eligió `activityLog`**, y con ese nombre quedó.
- Se escribió [ADR-0017](adr/0017-registro-de-actividad-activitylog.md) con las decisiones de
  fondo (un solo registro para operador y auditoría · actor persona o sistema · nunca la frase
  en castellano · tipos inmutables · misma transacción que el hecho · append-only · motivo en
  lo destructivo · sin relleno hacia atrás) y el plan con el esquema, el catálogo de tipos, los
  endpoints, los roles y siete fases.
- **Sin implementar.** El plan espera aprobación.

Dos cosas quedaron anotadas para confirmar con el usuario antes de F1:

1. **La retención de 24 meses es un número puesto**, no averiguado. Conviene confirmarlo con el
   club antes de que empiece a borrar.
2. **`bookingNoShow` depende de un estado que no existe**: `BookingStatus` no modela la
   ausencia. Se detectó el mismo día, al sacar de la ficha de una persona el dato de ausencias
   que el mock inventaba.
