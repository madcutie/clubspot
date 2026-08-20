# Bitácora — plan de cancelación con motivo

Registro de avance del [plan](plan-cancelacion-con-motivo.md). La entrada más nueva arriba.

## 20/08/2026 — F1 y F2 ejecutadas: no se cancela sin decir por qué

- **`Booking.Cancel(at, reason)`** recorta el motivo, lanza si queda vacío o si pasa de 300
  caracteres, y lo guarda en `CancellationReason`. La invariante está en el agregado, no en la
  pantalla. Columna `cancellationReason` en `bookings`, migración
  `20260820023005_BookingCancellationReason`.
- **`CancelAsync(id, reason, ct)`** valida antes de tocar la base y devuelve `MissingReason`;
  el endpoint responde 422. La entrada `bookingCancelled` se registra en la misma transacción,
  **sin payload**: lo que la reserva no tiene y el registro sí es *quién* canceló, y eso ya va
  en las columnas de actor.
- **Un uso de `CancelAsync` que no era una cancelación.** El portal lo llamaba para tirar el
  hold cuando falla la creación del checkout (`PortalEndpoints.cs`). Ahí no decide nadie, así
  que no hay motivo que dar: pasó a `ReleaseHoldAsync`, que es el método que existe para eso y
  además es condicional sobre `PendingPayment`. Sin este cambio habría habido que inventar un
  motivo del sistema.
- **`ReservaPanel`** dejó de cancelar en un click: el botón abre un paso de confirmación con el
  campo de motivo, y el confirmar queda deshabilitado mientras esté vacío. Si la reserva tiene
  plata cobrada, arriba aparece el aviso en ámbar con el monto y la advertencia de que cancelar
  no devuelve nada.
- **El motivo se lee sin pantallas nuevas**: `AgendaInactiveBooking` lo devuelve y la fila de
  la lista de inactivas lo muestra.
- **Tests**: 153 verdes (82 unitarios + 71 de integración). Nuevos: motivo vacío o de 301
  caracteres lanza en el agregado · 422 sin motivo y la reserva queda intacta · el motivo
  queda recortado en la reserva y cancelar dos veces deja una sola entrada.
- **Verificación en vivo** contra la base de desarrollo: 422 sin motivo, 422 con 301
  caracteres, 204 con motivo, 204 al repetir; la fila de `bookings` guarda
  `"Se suspendió por lluvia"` ya recortado y la entrada del registro suma `actorName =
  Administrador`, `source = Counter`. El agenda devuelve el motivo en la inactiva.

**Lo que quedó afuera a propósito**, por pedido de acotar el alcance al defecto reportado:

- **El motivo al bloquear una ficha.** Mismo defecto, otra pantalla; `personBlocked` sigue sin
  cablearse.
- **El endpoint y la pantalla de lectura del registro de actividad.** Con el motivo como campo
  de la reserva ya no hacen falta para responder "¿por qué se canceló?". Siguen siendo F3 y F4
  del plan de actividad.

## 20/08/2026 — Corrección del usuario: el dato de negocio no vive en el registro

El plan decía que el motivo vivía únicamente como la columna `reason` de la entrada del
registro de actividad. El usuario lo vetó:

> *"No puedo dejar cosas de negocio en el registro de actividad, esos son logs casi sin
> lectura; el reason tiene que ser un campo del booking y listo."*

Es correcto y es la decisión de fondo del plan. El registro de actividad es una crónica: se
escribe siempre y se lee poco. Un dato que la operación necesita no puede tener ahí su única
casa, porque entonces leerlo depende de una pantalla de auditoría que no existe. **El dato de
negocio vive en su agregado; el registro guarda la foto de quién lo hizo y cuándo.**

Efecto lateral bueno: la fase de lectura dejó de ser un requisito. Con el motivo en la reserva,
la agenda lo muestra sin endpoint nuevo, y el plan se pudo recortar de cuatro fases a dos.

## 20/08/2026 — Plan escrito, sin arrancar

- El tema salió al preguntar qué quedaba pendiente después de cerrar el contrato de API y F1
  del registro de actividad. De todo lo abierto, éste es el único **defecto vivo que toca
  plata**, y por eso encabezó la lista.
- El origen es el ítem del `TODO.md` que escribió el usuario: *"cancelar reserva aunque esté
  pagada, me lo quita sin consultar"*. Al revisarlo contra el código, el problema resultó ser
  más ancho que la falta de una confirmación.
- **Se unificó con F2 del [plan del registro de actividad](plan-activity-log.md)**, que estaba
  pendiente exactamente por lo mismo: la API no pedía motivo, así que `bookingCancelled` no se
  podía cablear sin inventar uno.
