# Plan — Reglas de la plata huérfana

**Fecha:** 20/08/2026 · **Estado:** escrito, **esperando decisiones del usuario** · Avance en la
[bitácora](plan-reglas-de-plata-huerfana.bitacora.md)

## 1. De dónde salió

Al cerrar la columna `payments.orphanReason` apareció la pregunta del usuario:

> *"¿Realmente es posible, con todos los casos que tenemos, que entre un pago y no sepamos por
> qué entró? Yo genero un link de pago, el usuario paga su cancha, ¿cómo puede venir menos?"*

Es la pregunta correcta. Al medir los cinco motivos contra el código, **tres pueden pasar de
verdad, uno es un defecto nuestro disfrazado de caso de negocio, y uno no puede pasar hoy**.

## 2. Los cinco motivos, medidos

| Motivo | ¿Puede pasar hoy? | Por qué |
|---|---|---|
| `bookingLost` | **Sí** | El hold se libera desde `ReturnScreen.tsx:53` cuando el cliente aprieta "Volver" con la reserva todavía en `pendingPayment`; si ya pagó y el webhook no llegó, la liberación gana. También pasa si el club cancela el turno mientras el cliente está pagando el link |
| `slotLost` | **Sí** | El TTL del hold es de 5 minutos (`appsettings.json`). Si tarda más en pagar y otro compra la cancha, el pago llega sin turno |
| `duplicate` | **Sí** | Lo habilita nuestra propia reemisión del link, que es gratis a propósito. Dos links vivos, el cliente paga los dos |
| `short` | **No por el cliente** | En Checkout Pro el monto lo fija la preferencia y del webhook se lee `TransactionAmount`, que es el bruto: las comisiones de Mercado Pago no lo achican. **Pero se dispara por un defecto nuestro** — ver B |
| `wrongCurrency` | **No** | Con una cuenta de Mercado Pago argentina todo liquida en ARS; una tarjeta extranjera igual paga en pesos y la conversión la hace el banco del cliente. Es código defensivo para un futuro con otro proveedor o país |

## 3. Lo que hay para hacer

Cuatro puntos. Los tres primeros están verificados contra el código; el tercero es una decisión
de negocio y no la puede tomar nadie más que el usuario.

### A. Liberar un hold deja `Cancelled`, y debería dejar `Expired`

Es el hallazgo más importante y no es cuestión de apretar una tuerca: **la regla está mal**.

| El hold muere por… | Queda en | Si después entra el pago |
|---|---|---|
| vencer el TTL | `Expired` | **se confirma**, si la cancha sigue libre |
| apretar "Volver" | `Cancelled` | **siempre huérfano**, aunque la cancha esté libre |

Es la misma situación —el hold ya no está y el cliente pagó— resuelta de dos maneras distintas,
y la peor le toca justo al que pagó. `Booking.ConfirmPayment()` acepta `PendingPayment` o
`Expired`; `ReleaseHoldAsync` deja `Cancelled`, que no está en esa lista.

**El arreglo es una línea**, y se verificó que no rompe nada:

- La restricción de exclusión sólo mira `Confirmed` y `PendingPayment`: no cambia qué bloquea la
  cancha.
- `GetInactiveBookingsAsync` ya incluye los dos estados: la lista de inactivas de la agenda no
  cambia.
- **Arregla una etiqueta que hoy miente**: un hold liberado aparece en la agenda como
  *"cancelada · La canceló el club"*. Pasaría a *"abandonada · Empezó a reservar con pago online
  y no completó el pago"*, que es lo que realmente pasó.
- **Lo mete en el radar de J2**: la conciliación busca `PendingPayment` y `Expired`. Hoy un hold
  liberado que en realidad se pagó es invisible para el job — nadie lo rescata, y sólo aparece
  cuando el webhook lo convierte en huérfano.

Efecto colateral bueno: `Cancelled` pasa a significar **exclusivamente** "una persona decidió y
dejó un motivo", con lo cual el check constraint que se descartó el 20/08 (`cancelada ⇒ tiene
motivo`) se vuelve posible.

**Recomendación: hacerlo.**

### B. Lo acordado se recalcula dentro del webhook

```csharp
var club = await clubSettings.GetAsync(cancellationToken);
var expected = ChargeAmountFor(booking.PaymentMode, booking.Price, club.DepositPercent);
```

`BookingsStore.cs:167`, dentro de `ApplyPaymentCoreAsync`. Lo esperado se calcula con el
`depositPercent` **del momento del pago**, no con el que estaba cuando se emitió el link.

Como la seña es 50 % o 100 % y nada más, alcanza con cambiar el club de 50 a 100 mientras hay
señas en vuelo: **cada pago correcto de 50 % entra como corto y queda huérfano**. Plata bien
pagada, marcada como problema.

Esto no es un caso para triar en una bandeja, es un agujero para tapar: lo acordado tiene que
congelarse en la reserva cuando se toma el hold, no recalcularse contra la configuración viva.
Hoy `CreateAsync` calcula el `chargeAmount`, lo devuelve y lo tira sin persistirlo.

**Recomendación: hacerlo.** Es el único caso de los cinco donde el sistema marca como problema
un pago que estuvo bien.

### C. El TTL del hold es de 5 minutos — decisión del usuario

Cinco minutos para completar un checkout de Mercado Pago con tarjeta, código del banco y a veces
validación en la app. Dato llamativo: **los tests corren con 15 y la producción con 5**
(`ApiFactory.cs:18` contra `appsettings.json`).

Acá no hay regla que agregar; hay un intercambio que decidir: cuánto tiempo se está dispuesto a
tener una cancha bloqueada por alguien que quizás no compra. Subirlo baja `slotLost` y sube el
bloqueo inútil.

**No hay recomendación técnica: es una decisión de negocio.** Como referencia, 10–15 minutos es
lo habitual en checkouts hospedados.

### D. Reemitir el link crea una preferencia nueva cada vez

Es a propósito —el turno ya es del cliente, no hay hold que proteger— pero deja dos links vivos y
el cliente puede pagar los dos.

Ahora que `bookingCheckouts` guarda URL y vencimiento (20/08/2026), la regla se puede escribir:
**si ya hay un link vivo por el mismo monto, devolver ese en vez de pedirle otro al proveedor.**

No mata el caso de que alguien pague dos veces el mismo link —Mercado Pago lo permite en algunos
flujos— pero eso ya es un huérfano de verdad y no uno que fabricamos nosotros.

**Recomendación: hacerlo.** Es la continuación natural de la tabla que se creó el 20/08.

## 4. Fuera de alcance, y por qué

| Qué | Por qué |
|---|---|
| **Devolver la plata** | Contra-asiento (los movimientos son append-only) más una capacidad de *refund* en `IPaymentProvider`. Es un plan propio, y es lo que hoy bloquea tanto la bandeja de revisión manual como la cancelación de un turno pagado |
| **`wrongCurrency`** | No se toca. Es código defensivo para un futuro que hoy no existe |
| **La bandeja de revisión manual** | Con tres motivos reales que terminan todos en "devolver o reubicar", no hace falta una pantalla de triage de cinco categorías: alcanza una lista de *pagos sin turno* con dos acciones. Y no tiene sentido construirla antes de que devolver exista |

## 5. Lo demás que quedó abierto

Anotado acá para no perderlo, aunque no sea de este plan:

- **Motivo al bloquear una ficha.** Mismo tratamiento que la cancelación; `personBlocked` y
  `personUnblocked` siguen declarados y sin cablear.
- **El cobro de mostrador contra una ficha** (`personPaymentRegistered`): `RegisterPayment()`
  pisa la deuda con cero y el monto no queda en ninguna tabla. Ya marcado como provisional en
  `Person.cs`, esperando el módulo de finanzas.
- **La retención de 24 meses** del registro de actividad es un número puesto, no averiguado.
  Conviene confirmarlo con el club antes de que J11 empiece a borrar.
- **`bookingNoShow`** depende de un `BookingStatus` que no modela la ausencia.
