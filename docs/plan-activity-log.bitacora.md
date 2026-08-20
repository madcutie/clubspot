# Bitácora — plan del registro de actividad

Registro de avance del [plan](plan-activity-log.md). La entrada más nueva arriba.

## 20/08/2026 — Auditoría de la frontera: se sacaron dos datos de negocio del registro

Después de que el usuario fijara que **el registro de actividad no es fuente de verdad de
ningún dato de negocio** (AGENTS.md §6), se auditaron las 11 llamadas a `activityLog.Record` que
había en el backend, una por una, contra las tablas donde vive cada clave del payload. Cuatro
resultaron huérfanas —un dato que la operación necesita y cuya única casa era el log—. Se
corrigieron las dos que tocan plata:

**1. Por qué un pago quedó huérfano.** `Payment.MarkOrphaned()` sólo dejaba
`Status = ApprovedOrphan`; el motivo real viajaba como clave `why` del payload y no existía en
ninguna columna. Es plata que el club tiene y que —según el comentario de
`AvailabilityQueries.cs`— *"needs a person to decide what happens to it"*, con esa persona sin
forma de leer por qué está marcada. Ahora hay un enum `PaymentOrphanReason` (`duplicate`,
`bookingLost`, `wrongCurrency`, `short`, `slotLost`), `MarkOrphaned(reason)` lo exige, y la
columna `orphanReason` lo guarda. La entrada del registro conserva su copia como foto.

**2. El link de cobro emitido.** El propio contrato lo admitía por escrito: *"the only trace it
leaves here is this entry"*. Peor todavía, la URL que devolvía el proveedor no se guardaba en
ningún lado, ni siquiera en el log. Ahora hay una tabla `bookingCheckouts` —append-only, una
fila por link emitido, con proveedor, URL, monto y vencimiento— y el mismo link se puede volver
a mostrar en vez de pedirle otro al proveedor. **También se registra el link que emite el
portal**, que antes no dejaba rastro de ninguna clase.

Decisiones tomadas al hacerlo:

- **No se agregó `issuedBy` ni `cancelledBy`.** *Quién* hizo algo es exactamente lo que el
  registro aporta y la tabla no; duplicarlo sería el error simétrico al que se está corrigiendo.
- **Sin check constraint `orphan ⇒ tiene motivo`.** Las filas huérfanas que ya existen no tienen
  motivo, y rellenarlas sería inventar datos. La invariante la impone el agregado, que no deja
  marcar un huérfano sin razón.
- **Sin relleno hacia atrás**, igual que en el resto del registro (ADR-0017, decisión 10).

**Las otras dos huérfanas quedan pendientes**, por decisión de alcance:

- **El cobro de mostrador contra una ficha** (`personPaymentRegistered`): `RegisterPayment()`
  pisa la deuda con cero y el monto no queda en ninguna tabla —`payments` no puede alojarlo
  porque su `bookingId` es no nulable—. Ya estaba marcado como provisional en `Person.cs`,
  esperando el módulo de finanzas.
- **Abandono de checkout contra cancelación del club**: se cerró solo. Las dos dejaban el mismo
  par `Cancelled` + `cancelledAt`; con `bookings.cancellationReason` ya se distinguen.

**Un arreglo de rebote**: el FK nuevo de `bookingCheckouts` hacia `bookings` rompía los
`ResetAsync` de cinco clases de test, que borraban reservas sin borrar antes lo que las
referencia. Es el mismo tropiezo que apareció el 19/08 con `payments`.

**Tests**: 154 verdes. Los cuatro escenarios de pago huérfano que ya existían ahora afirman el
motivo, y hay dos nuevos sobre el link guardado —que reemitir deja dos filas, y que lo guardado
coincide con lo que se le devolvió al operador—.

**Verificación en vivo**: dos emisiones del link de cobro dejaron dos filas en
`bookingCheckouts` con la URL real de Mercado Pago; un pago corto contra un hold del portal
quedó `ApprovedOrphan` con `orphanReason = Short`.

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
