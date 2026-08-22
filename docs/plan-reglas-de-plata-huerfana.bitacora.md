# Bitácora — plan de las reglas de la plata huérfana

Registro de avance del [plan](plan-reglas-de-plata-huerfana.md). La entrada más nueva arriba.

## 21/08/2026 — Verificación del camino del dinero: 5 de 6 refutados

Se relanzó el verificador que había quedado sin correr. De los seis hallazgos del camino del dinero,
**uno confirmado y cinco refutados**, cada uno con evidencia contra el código.

**Confirmado — dos operadores pueden emitir dos links pagables a la vez** (bajado a severidad baja).
Buscar el link vivo, pedirle uno al proveedor y asentarlo son tres pasos con una llamada de red en el
medio, sin nada que los serialice. Dentro de una misma pestaña React Query lo dedupe, así que hace
falta que dos sesiones —el canchero y un administrador, o la misma reserva en dos máquinas— aprieten
dentro de la misma llamada a Mercado Pago. **Corregido**: `RecordCheckoutIssuedAsync` toma el mismo
`FOR UPDATE` sobre la fila de la reserva que ya toma el camino del pago, asienta, y **devuelve el link
que hay que entregar**, que no siempre es el recién emitido — el del que perdió la carrera queda sin
publicar en el proveedor. El handler y el portal entregan lo que devuelve el store.

**Refutados, con lo que hay que recordar de cada uno:**

- **El pago que supera el precio no se marca huérfano.** La aritmética del hallazgo es correcta y el
  arreglo propuesto (`settled + amount > price`) no produce falsos positivos en ningún flujo — se
  verificó contra seña+saldo, pago completo exacto y pago doble genuino, sin deriva de redondeo—.
  Pero **el estado inicial no es producible en producción**: no existe ningún endpoint que asiente un
  cobro en efectivo, así que "el canchero cobró 5000 en efectivo" no pasa; con Mercado Pago cada
  preferencia se emite por el saldo del momento y el saldo sólo baja. La única forma de llegar ahí es
  el webhook falso, que sólo existe en Development. Además es **idéntico en `main`** y este PR lo hace
  *menos* probable, no más. Queda como **ticket propio de severidad baja**: se abre el día que se
  registre un cobro de mostrador en efectivo, que es justo lo que este plan anticipa.
- **Reusar el link no deja entrada en la crónica.** El escenario que lo justificaba no existe:
  **reenviar por WhatsApp no toca el servidor** —`CobroPanel` arma el mensaje con la URL que ya tiene—,
  así que "lo mandé tres veces" nunca estuvo en el registro, ni antes ni después. Lo que se pierde es
  "el operador reabrió el panel", que nadie necesita leer para operar.
- **Las reservas liberadas quedan 48 h en el lote de J2.** El método que las selecciona ya incluía
  `Expired` en `main` y no se tocó; lo que se suma son sólo los holds liberados desde el portal, un
  subconjunto de los abandonados, cuyo hermano mayor —los vencidos por TTL— ya estaba adentro. Y cada
  fila agregada es exactamente la que el punto A existe para rescatar. La inanición del lote requeriría
  más de 200 reservas online impagas en 48 h **para un solo club**.
- **`Expired` puede quedar con `ExpiresAt` futuro.** Ningún lector se rompe: todos los que deciden algo
  con `ExpiresAt` exigen además `PendingPayment`, y los dos frontends cortan por estado antes de mirar
  la fecha. Invariante sin consumidores, sin constraint y sin comentario que la afirme.
- **`BookingLost` cambió de significado.** No cambió: su definición es "la reserva se canceló mientras
  el comprador pagaba", y eso sigue siendo exacta y únicamente lo que cubre — `CancelAsync` acepta una
  reserva en cualquier estado, `PendingPayment` incluido.

## 21/08/2026 — Revisión de código: la regla de reuso del link no cubría el caso más común

Del pipeline de `code-reviewer` salieron dos correcciones sobre lo entregado el mismo día. Los
verificadores de esta parte los frenó el usuario antes de que devolvieran, así que **estas dos no
tienen verificación independiente**: se aplicaron porque se pueden comprobar leyendo el código.

**D — el reuso del link no funcionaba cuando más importa.** La regla entregada reusaba el link cuyo
vencimiento coincidía exactamente con el que tendría uno nuevo. Eso vale mientras el turno no
terminó, pero desde una hora antes del fin del turno entra el piso de una hora y el vencimiento pasa
a ser `ahora + 1h`, distinto en cada pulsación: la igualdad no da nunca y cada "Cobrar" emitía otra
preferencia pagable. Como el cobro en mostrador ocurre justo alrededor de la hora de juego —para un
turno de 60 minutos cobrado al empezar, el piso ya gana—, **el caso que la regla no cubría era el
habitual**. La condición pasó a ser "cualquier link del mismo cobro que no haya vencido":
emitir otro no anula el anterior, así que un segundo link sólo agrega una forma de pagar dos veces.
De paso desaparece la fragilidad de comparar un `DateTimeOffset` por igualdad exacta contra un
`timestamptz`, que tiene menos resolución.

**El helper de tests actualizaba todos los clubes.** `SetDepositPercentAsync` corría
`UPDATE public.clubs SET "depositPercent" = …` **sin `WHERE`** —el filtro global de tenant no alcanza
al SQL crudo— y restauraba un `50` literal en vez del valor que había. Otras clases de la misma
colección dejan clubes propios en la base, así que el día que alguno se siembre con 100 el `finally`
lo pisaría en silencio. Ahora filtra por el id del club sembrado y devuelve lo que reemplazó.

**Quedaron reportados y sin aplicar** (sus verificadores no llegaron a correr): que un pago que llega
por un link viejo y supera el precio se asienta como aprobado en vez de marcarse huérfano —el chequeo
mira lo cobrado *antes* del pago y no la suma—; que reusar un link no deja entrada en la crónica; que
dos operadores apretando "Cobrar" a la vez siguen produciendo dos links; que las reservas liberadas
ahora quedan 48 h en el lote de J2; y varios huecos de tests. Están en la lista del PR.

## Estado por punto

| Punto | Qué | Estado |
|---|---|---|
| A | Liberar un hold deja `Expired`, no `Cancelled` | ✅ 21/08/2026 |
| B | Lo acordado se congela en la reserva, no se recalcula en el webhook | ✅ 21/08/2026 |
| C | TTL del hold | ✅ 21/08/2026 — **decidido: queda en 5 minutos** |
| D | Reemitir el link devuelve el link vivo | ✅ 21/08/2026 |

## 21/08/2026 — A, B y D ejecutados; C decidido sin tocar nada

Trabajo hecho en el worktree `plan/plata-huerfana-y-logging`, junto con el frente de logging
([ADR-0019](adr/0019-logging-estructurado-y-diagnostico.md)).

**C — el TTL queda en 5 minutos.** Decisión del usuario, tomada sobre las tres opciones que se le
plantearon (5, 10, 15). No se cambió ni la configuración ni los tests: el `ApiFactory` sigue
corriendo con 15 porque un test que espera un vencimiento no puede depender del valor de producción.
Queda cerrado como decisión, no como pendiente.

**A — liberar deja `Expired`.** `ReleaseHoldAsync` pasó de escribir `Cancelled` + `cancelledAt` a
escribir sólo `Expired`. Lo que eso cambió, medido:

- Un pago que llega después de que el cliente apretó "Volver" **confirma la reserva** si la cancha
  sigue libre, en vez de quedar huérfano siempre. Es el arreglo que el plan describía.
- Si en el medio alguien más compró el turno, el pago sigue quedando huérfano, pero con el motivo
  correcto: `slotLost` en vez de `bookingLost`. Los dos casos quedaron con test propio.
- **La etiqueta de la agenda se arregló sola.** `apiHttp.ts:172` ya rotulaba como *abandonada* todo
  lo que no fuera `cancelled`, así que el hold liberado dejó de decir "la canceló el club" sin tocar
  una línea de frontend.
- `Cancelled` pasa a significar exclusivamente "una persona decidió y dejó un motivo". El check
  constraint `cancelada ⇒ tiene motivo` que se descartó el 20/08 queda posible, pero **no se hizo**:
  las filas viejas siguen sin motivo y rellenarlas sería inventar datos.

**B — lo acordado se congela.** Columna `depositPercent` en `bookings` (nullable, migración
`20260822015052_BookingDepositPercent`), no nula sólo en un hold de seña, con check constraint
`IS NULL OR IN (50, 100)` e invariante en el agregado: `Booking.Hold` exige el porcentaje para
`OnlineDeposit` y lo rechaza para el resto.

Se eligió **guardar el porcentaje y no el `chargeAmount`**: el precio y el `paymentMode` ya están
congelados en la fila, así que con el porcentaje el cálculo depende sólo de la reserva; guardar el
monto habría agregado una segunda columna de moneda al lado de `priceCurrency`, redundante por
definición. `ApplyPaymentCoreAsync` usa `booking.DepositPercent ?? club.DepositPercent`: el fallback
sólo lo alcanzan los holds creados antes de la migración, que mueren con el TTL de 5 minutos.

**D — reemitir devuelve el link vivo.** `IBookingsStore.FindLiveCheckoutAsync(bookingId, provider,
amount, expiresAt)` busca en `bookingCheckouts` y `CreateBookingCheckoutHandler` la consulta antes de
pedirle nada al proveedor.

La regla quedó más simple de lo que el plan anticipaba: **se reusa el link cuyo vencimiento coincide
con el que tendría uno nuevo**. Mientras el turno no terminó, todos los links de una reserva vencen
en el mismo instante —el fin del turno—, así que la igualdad significa "el mismo cobro, todavía
válido". Cuando entra a jugar el piso de una hora, cada link vive más que el anterior, ninguno
coincide, y un cobro tardío sigue recibiendo uno fresco. Una sola condición cubre los dos regímenes,
sin inventar un concepto de "le queda poco".

El monto también entra en la comparación: si el canchero cobró parte en efectivo, lo adeudado cambió
y el link viejo cobraría de más. Ese caso emite uno nuevo, con test.

**Tests**: se reescribieron dos que cambiaban de premisa (`Releasing_a_hold_frees_the_slot_immediately`
ahora espera `Expired`; el pago sobre un hold liberado ahora confirma) y se agregaron cuatro
(`A_payment_landing_on_a_released_hold_is_orphaned_once_the_slot_is_gone`,
`A_deposit_paid_after_the_club_moved_the_percentage_is_still_the_agreed_one`,
`Asking_again_for_the_same_charge_hands_back_the_same_link`,
`A_link_for_a_different_amount_is_a_new_link`).

**Lo que sigue fuera de alcance**, sin cambios: devolver la plata (necesita el modelo de reembolsos),
`wrongCurrency`, y la bandeja de revisión manual.

**Dónde quedó / próximo paso:** los cuatro puntos cerrados. Falta la verificación con plata real,
que necesita al usuario y va junto con la F3 del plan de cobro en mostrador.

## 20/08/2026 — Escrito, sin arrancar

- Salió de una pregunta del usuario al ver la columna `orphanReason` recién agregada: si el
  monto lo fija la preferencia de Mercado Pago, cómo puede entrar un pago corto. La duda era
  válida y la respuesta obligó a medir los cinco motivos contra el código en vez de darlos por
  buenos.
- **De los cinco, dos no eran lo que parecían.** `wrongCurrency` no puede pasar con una cuenta
  argentina, y `short` no lo puede provocar el cliente —del webhook se lee `TransactionAmount`,
  que es el bruto, así que ni las comisiones lo achican—: lo provoca el sistema al recalcular lo
  esperado con el `depositPercent` vivo en vez del acordado.
- **El hallazgo grande no era ninguno de los cinco**, sino la asimetría entre liberar un hold y
  dejarlo vencer: la misma situación resuelta de dos maneras, y la peor le toca al cliente que
  pagó. Se verificó que el arreglo no toca la restricción de exclusión, ni la lista de
  inactivas, y que además corrige una etiqueta que hoy miente en la agenda y un agujero de la
  conciliación J2.
- **Sin implementar.** El plan espera las decisiones del usuario, en particular el TTL del hold,
  que es un intercambio de negocio y no una decisión técnica.
