# Plan — cobro con Mercado Pago desde el backoffice

**Estado:** escrito 19/08/2026, **esperando aprobación** · avance en la
[bitácora](plan-cobro-en-mostrador.bitacora.md).

## El problema

El panel de reserva del backoffice tiene un botón **Cobrar** que sólo muestra un aviso: es una
cáscara del prototipo, igual que Reprogramar, Marcar ausencia y WhatsApp. Una reserva de
mostrador nace `Confirmed` **sin ningún registro de plata**: el sistema no sabe si se cobró,
cuánto ni cómo. El club vende por teléfono y en el mostrador, así que hoy la única venta con
plata trazable es la del portal.

## Qué habilita este plan

El operador abre un turno, toca **Cobrar con Mercado Pago** y aparece un **QR en pantalla**
para que el cliente lo escanee con su celular, más un **link** para mandarle por WhatsApp si no
está presente. El cliente paga desde su teléfono y el turno queda pagado solo, por el mismo
webhook que ya funciona. El operador ve confirmarse el cobro sin refrescar.

Reusa entero el circuito verificado el 18/08: preferencia de Checkout Pro → `init_point` →
webhook → `ApplyPaymentAsync`. **No agrega conceptos de pago**: el asiento cae en `payments`
con `provider=mercadopago`, `rail=checkout`, `source=webhook`, indistinguible de un pago del
portal salvo por el `origin` de la reserva. Es la primera prueba de que ADR-0014 sirve.

## Decisiones de diseño

1. **No hay hold, y el QR se regenera las veces que haga falta** (decisión del usuario,
   19/08/2026: *"el canchero le genera un qr para que el cliente pague, si hay error, le puede
   generar otro, la cancha está reservada ya, no hay hold"*). La reserva ya está confirmada:
   el turno es del cliente antes de pagar, así que el checkout no bloquea nada y pedir otro es
   una acción barata. **El link vence cuando termina el turno** — una sola regla, sin topes ni
   casos especiales: nunca queda un link vivo para un partido que ya se jugó. Si vence sin
   pagar, la reserva **sigue confirmada e impaga**: cobrar o no es decisión del club.
2. **Se cobra el saldo, no el precio.** Si la reserva ya tiene pagos aprobados (una seña del
   portal), el checkout se emite por lo que falta. Saldo cero ⇒ no se ofrece cobrar.
3. **El comprobante lo ve el cliente en su celular**: la `returnUrl` del checkout es la pantalla
   de retorno del portal, que ya muestra "¡Pago acreditado!" y espera la confirmación. Cero
   pantallas nuevas para eso.
4. **El operador se entera solo**: mientras el panel de cobro está abierto, consulta el estado
   de la reserva cada 3 s (y usa la conciliación puntual ya existente si el webhook tarda);
   cuando entra el pago, avisa "Cobrado $X" y cierra.
5. **El pago de más se asienta y se muestra, no se previene.** Varios links vivos para el mismo
   turno son varios cobros posibles; el sistema ya lo soporta —cada pago entra por su
   `externalId` propio y el segundo queda registrado sobre la reserva confirmada— así que el
   riesgo no es perder plata sino no verla. La agenda **marca el excedente** cuando lo pagado
   supera el precio, para que el operador lo devuelva. Ninguna regla impide emitir de nuevo.
6. **La agenda muestra el estado de cobro.** Hoy la tarjeta del turno sólo dice el precio; pasa
   a decir si está pagado, si debe y cuánto. Sin eso, el cobro es invisible para el operador.

## Fuera de alcance, a propósito

- **Cobro en efectivo / caja.** Registrar plata que no pasó por un proveedor obliga a decidir
  cómo se modela (¿un `provider` propio del mostrador?) y arrastra sesión de caja, recibos y
  cierre con diferencia: eso es el módulo `finance`, no este plan. El botón dirá **"Cobrar con
  Mercado Pago"** — honesto sobre lo que hace.
- **Point y QR presencial integrados** (el aparato): es la Orders API, plan aparte (ADR-0015).
  Este plan es el sustituto barato mientras tanto — no requiere hardware.
- Reembolsos, reprogramar, marcar ausencia y la nota de cancelación: van en el plan de
  cancelación.

## Fases

### F1 — Backend

- **`POST /api/bookings/{id}/checkout`** (autenticado, módulo `bookings`): emite el checkout
  del saldo y devuelve `{url, amount, expiresAt}`.
  - `404` reserva inexistente · `409` si no hay saldo · `422` si el club no tiene proveedor con
    capacidad de checkout (regla ADR-0014: no se ofrece, no falla feo).
  - **Sin estado nuevo**: como el link se puede regenerar libremente (decisión 1), cada llamada
    emite uno y listo — ni columnas ni migración.
- **La agenda devuelve `paidAmount`** por reserva activa (la consulta de pagos ya existe para
  las inactivas; se generaliza).

**Verificación:** tests de integración — emite por el saldo · dos pedidos seguidos dan dos links
válidos · sin saldo da 409 · el webhook sobre esa reserva la deja pagada · un segundo pago
aprobado queda asentado y el pagado supera al precio.

### F2 — Backoffice

- Panel de cobro: monto, **QR** (`qrcode.react`, dependencia nueva y chica), link con botón de
  copiar y atajo de WhatsApp con el texto armado, y **"Generar otro"** siempre a mano (si el
  cliente tuvo un error, el canchero emite uno nuevo sin pensarlo).
- Consulta de estado cada 3 s mientras está abierto; al acreditarse, tostada y cierre.
- La tarjeta de la grilla y el panel muestran pagado / debe, y **avisan del excedente** cuando
  lo pagado supera el precio (decisión 5).

**Verificación:** navegador — cobrar un turno de mostrador escaneando el QR con un celular real
(usuario de prueba de MP), ver la tarjeta pasar a pagada sin refrescar.

### F3 — Punta a punta con plata real

Turno vendido en el backoffice → QR → pago con tarjeta de prueba → asiento verificado por SQL
(`provider=mercadopago, rail=Checkout, source=Webhook`) y turno pagado en la grilla.

## Preguntas abiertas para el usuario

- **¿El QR alcanza, o el operador necesita también imprimir/mandar comprobante?** (hoy el
  comprobante es el de Mercado Pago, en el celular del cliente).
- **¿Se puede cobrar de más (propina, alquiler de paletas)?** El plan cobra el saldo exacto.
