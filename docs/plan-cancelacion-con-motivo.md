# Plan — Cancelar con motivo (y qué pasa con la plata)

**Fecha:** 20/08/2026 · **Estado:** aprobado y en ejecución · Avance en la
[bitácora](plan-cancelacion-con-motivo.bitacora.md)

Cierra el ítem del `TODO.md` —*"cancelar reserva aunque esté pagada, me lo quita sin
consultar"*— y ejecuta la parte de cancelación de **F2 del
[plan del registro de actividad](plan-activity-log.md)**.

## 1. El defecto, medido

Verificado en el código, no supuesto:

| # | Qué pasa hoy | Dónde |
|---|---|---|
| 1 | Se cancela **cualquier** estado sin distinguir: un turno confirmado con seña cobrada se cancela igual que un hold vacío | `BookingsStore.cs:101` |
| 2 | No se pide motivo — ni la API ni la pantalla | `BookingEndpoints.cs:80`, `ReservaPanel.tsx:151` |
| 3 | Un click cancela. No hay confirmación de ningún tipo | `ReservaPanel.tsx:153` |
| 4 | No queda registro de quién canceló ni por qué | — |
| 5 | La plata cobrada no se toca ni se nombra: los `payments` quedan `Approved` colgando de un turno `Cancelled` | — |

El único rastro que existe hoy de una cancelada con plata es la línea ámbar `pagó $7.000` en
la lista de inactivas de la agenda —y sólo se ve parado en **ese** día.

## 2. Objetivo

Que cancelar deje de ser un accidente posible, y que quede escrito por qué.

- Cancelar un turno pagado **avisa cuánta plata hay cobrada antes de hacerlo**.
- No se cancela nada sin escribir un motivo.
- El motivo se lee donde aparece la reserva cancelada, sin pantallas nuevas.

## 3. Fuera de alcance (explícito)

| Qué | Por qué queda afuera |
|---|---|
| **Devolver la plata** | Una devolución es un contra-asiento (los movimientos son append-only) más una capacidad de *refund* en `IPaymentProvider` que hoy no existe. Es un plan propio |
| **Motivo al bloquear una ficha** | Mismo defecto, otra pantalla. Se hace después; este plan arregla lo que se reportó |
| **Endpoint y pantalla de lectura del registro de actividad** | Con el motivo como campo de la reserva, leerlo ya no depende de eso. Sigue siendo F3/F4 del plan de actividad |
| **Política de cancelación con ventana** | Este plan no introduce penalidades ni plazos |
| **`bookingNoShow`** | Depende de un `BookingStatus` que no modela la ausencia |
| **Cancelar desde el portal** | El cliente sólo libera holds (`releasePortalBooking`), que no es destructivo |
| **Código de regla en el 422** | El resto de la API devuelve `UnprocessableEntity` desnudo; unificar eso es otro trabajo |

## 4. Decisiones que este plan fija

1. **El motivo es un campo de la reserva, no del registro de actividad.** `Booking` gana
   `CancellationReason`, al lado del `CancelledAt` que ya tiene. **Corrección del usuario del
   20/08/2026**, y es la decisión de fondo de este plan:

   > *"No puedo dejar cosas de negocio en el registro de actividad, esos son logs casi sin
   > lectura; el reason tiene que ser un campo del booking y listo."*

   El registro de actividad es una crónica: se escribe siempre y se lee poco. Un dato que la
   operación necesita no puede tener ahí su única casa, porque entonces leerlo depende de una
   pantalla de auditoría que no existe. **El dato de negocio vive en su agregado; el registro
   guarda la foto de quién lo hizo y cuándo.**
2. **La entrada del registro igual lleva el motivo**, como copia inmutable del momento. No es
   su fuente de verdad: es parte de la foto, igual que `actorName`. Cancelar es terminal, así
   que las dos copias no pueden divergir.
3. **Cancelar un turno pagado se permite.** No se bloquea desde la agenda: el club cancela por
   lluvia, por corte de luz o porque el cliente avisó. Lo que cambia es que no se puede hacer
   **de casualidad** ni **en silencio**.
4. **No hay devolución automática.** La plata queda donde está y el acuerdo con el cliente lo
   asienta el motivo. Que la devolución no exista todavía es un hueco conocido, no un olvido.
5. **El motivo es texto libre obligatorio**, entre 1 y 300 caracteres. La invariante la impone
   el agregado, no la pantalla.
6. **La cancelación sigue siendo idempotente y no escribe una segunda entrada.**
7. **La confirmación es un paso dentro del panel, no un `window.confirm`.** Un diálogo del
   navegador bloquea la página, no se puede estilar y no puede pedir el motivo en el mismo
   gesto.

## 5. Fases

### F1 — El motivo es parte de la reserva

- `Booking.Cancel(DateTimeOffset at, string reason)`: recorta el motivo, lanza si queda vacío
  o si pasa de 300 caracteres, y lo guarda en `CancellationReason`.
- Columna `cancellationReason` en `bookings` (`text(300)`, nula mientras el turno vive), con
  su migración.
- `IBookingsStore.CancelAsync(Guid id, string reason, CancellationToken)`;
  `BookingCancelOutcome` suma `MissingReason`.
- El endpoint recibe `CancelBookingRequest(string Reason)` y responde
  `Results<NoContent, NotFound, UnprocessableEntity>` (ADR-0016).
- Se registra `bookingCancelled` con el motivo, en la misma transacción. **Sin payload**: lo
  que el registro aporta y la reserva no tiene es *quién* canceló, y eso ya va en las columnas
  de actor de la entrada.
- `AgendaInactiveBooking` suma el motivo para que la agenda lo pueda mostrar.

**Tests:** cancelar sin motivo es 422 y no cambia el estado · con motivo es 204, la reserva
guarda el motivo y queda una entrada `bookingCancelled` · cancelar dos veces deja una sola
entrada · un motivo de 301 caracteres es 422.

### F2 — El panel pregunta antes, y muestra la plata

`ReservaPanel` deja de cancelar en un click. "Cancelar reserva" abre un paso de confirmación
dentro del mismo panel:

- **si `pagado > 0`**, un aviso destacado: cuánto hay cobrado y que cancelar **no devuelve la
  plata**;
- un campo de motivo (`textarea`, `maxLength` 300), con el botón de confirmar deshabilitado
  mientras esté vacío;
- "Confirmar cancelación" / "Volver".

En la lista de inactivas de la agenda, la reserva cancelada muestra su motivo.

**Verificación:** cancelar un turno pagado desde la pantalla exige leer el aviso y escribir el
motivo; el motivo se lee después en la fila de la inactiva.

## 6. Lo que queda anotado para después

- **La devolución de plata.** Este plan la hace visible y deliberada; no la resuelve.
- **El motivo al bloquear una ficha**, con el mismo criterio: campo del agregado, no del
  registro. Mientras tanto `personBlocked` sigue sin cablearse.
- **El pendiente sólo se ve en la agenda de ese día.** Una cancelada con plata del martes que
  viene no aparece en ningún lado hasta que se abra el martes.
- **`bookings` no tiene token de concurrencia** (`xmin`), a diferencia de `courts` y
  `schedules`.
