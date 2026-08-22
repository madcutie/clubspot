# Bitácora — plan de las reglas de la plata huérfana

Registro de avance del [plan](plan-reglas-de-plata-huerfana.md). La entrada más nueva arriba.

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
