# Bitácora — plan de cobro con Mercado Pago desde el backoffice

Registro de avance del [plan](plan-cobro-en-mostrador.md). La entrada más nueva arriba.

## 19/08/2026 — F2 (backoffice) cerrada y verificada en el navegador

- **Panel de cobro** (`CobroPanel.tsx`): monto, **QR** (`qrcode.react`, dependencia nueva),
  "Copiar link de pago", "Mandar por WhatsApp" (con el texto armado y el número normalizado a
  formato internacional) y **"Generar otro"**. Al pie, "Esperando el pago…"; cuando el saldo
  llega a cero avisa "Cobrado $X" y cierra solo.
- **Estado de cobro en la agenda**: la tarjeta del turno pasó de mostrar sólo el precio a decir
  `pagado` / `debe $X` / `cobrado de más`, y el punto de color se rellena cuando está cobrado
  (en una grilla llena el color se lee antes que el texto). El panel de la reserva suma la fila
  `debe` / `pagado` / `cobrado de más` y sólo ofrece cobrar si hay saldo.
- **Dos bugs de integración encontrados y corregidos durante la verificación**, ambos de la
  misma familia (estado que no sobrevive un remonte):
  1. El `CobroPanel` colgaba de `ReservaPanel`, que hace `return null` mientras la agenda se
     refresca; cada refresco desmontaba el panel y **reemitía el checkout**. Pasó a montarse
     como panel hermano desde `AgendaScreen`, con la reserva capturada al abrir y lo cobrado
     releído de la agenda.
  2. El link se pedía con `useMutation`: al desmontar el componente, React Query descarta tanto
     los callbacks de `mutate()` como el observer, así que el resultado llegaba a la nada y la
     pantalla quedaba clavada en "Generando el código…" pese al `200`. Ahora es `useQuery`
     (`useCobro`), cuyo cache es global y sobrevive el remonte; "Generar otro" es `refetch`.
- **Verificación en el navegador**: reserva de mostrador → *Cobrar con Mercado Pago* → QR real
  en pantalla con "$14.000", "Vence a las 07:00 p.m." (fin del turno, como manda la regla) y los
  tres botones. Typecheck limpio.
- **Falta F3**: pago real escaneando el QR con un celular.

## 19/08/2026 — F1 (backend) cerrada y verificada contra Mercado Pago real

- **`POST /api/bookings/{id}/checkout`** (autenticado, módulo `bookings`) + `CreateBookingCheckoutHandler`:
  emite el checkout del **saldo** (`price - paidAmount`) sobre la primera capacidad
  `IHostedCheckout` registrada. `404` inexistente · `409` no cobrable (cancelada, o no debe
  nada) · `422` sin proveedor. **Sin estado nuevo ni migración**, como quedó tras la corrección
  del usuario.
- **Vencimiento**: fin del turno en la zona del club, con **piso de 1 hora** — así el link
  muere con el partido, pero un cobro tardío (turno de ayer impago) sigue siendo posible, que
  es justo cuando el canchero lo pide.
- **La agenda ahora informa `paidAmount` por reserva**: se extrajo
  `IAvailabilityQueries.GetPaidAmountsAsync` (una sola consulta para activas e inactivas,
  huérfanos incluidos) y `GetInactiveBookingsAsync` volvió a devolver `Booking` pelado — el
  record `InactiveBooking` desapareció.
- **Reuso** (TODO del usuario): la construcción de la URL de retorno estaba duplicada; quedó en
  `Api/Payments/CheckoutReturnUrl.cs` y la usan el portal y el mostrador. El comprobante del
  cliente es la pantalla de retorno del portal (`Payments:PortalBaseUrl`, configurable).
- `ClubInfo` ganó `Slug` (lo necesita la URL de webhook de la preferencia).
- **Verificación**: 124 tests verdes (71 unit + 53 integración), 7 nuevos en
  `CounterCheckoutTests` — cobra el precio completo · reemitir es libre · sin saldo 409 ·
  cobra sólo lo adeudado · cancelada 409 · pagar dos veces asienta ambos y el pagado duplica al
  precio · la agenda informa lo pagado. **En vivo contra MP real**: reserva de mostrador →
  `POST /checkout` → `https://www.mercadopago.com.ar/checkout/v1/redirect?pref_id=3623770644-9efd779b…`,
  `amount 14000`, `expiresAt 2026-08-21T22:00:00Z` (= 19:00 hora del club, exactamente el fin
  del turno de 18:00 a 19:00).
- **F2 (backoffice) y F3 pendientes.**

## 19/08/2026 — Plan escrito; sin arrancar

- Origen: el usuario preguntó por dónde seguir y si "no venía la parte de cobro por MP en el
  backoffice". Verificado: el botón **Cobrar** del panel de reserva es un aviso vacío desde el
  prototipo, y el cobro en mostrador **no figuraba en ningún plan** — había quedado del lado de
  la parte financiera, nunca escrita.
- Elegido por el usuario entre cuatro frentes abiertos (los otros tres: cancelación con nota y
  reembolso · el Canchero y los roles · observabilidad).
- El plan reusa el circuito de Checkout Pro ya verificado con plata real y **no agrega
  conceptos de pago**: es la primera prueba de que el asiento agnóstico de ADR-0014 aguanta un
  canal nuevo sin tocar el modelo.
- **Corrección del usuario sobre el borrador** (19/08/2026): el plan traía "un checkout a la vez
  por reserva" y un vencimiento con tope de 24 h. El usuario lo desarmó con el argumento
  correcto —*"la cancha está reservada ya, no hay hold"*—: sin hold no hay nada que proteger, así
  que emitir otro QR tiene que ser gratis. Quedó: **regenerar libre**, **vencimiento = fin del
  turno** (una sola regla) y, como contrapartida del riesgo que eso abre, la agenda **avisa del
  excedente** cuando lo pagado supera el precio. Beneficio lateral: el endpoint queda sin estado
  y el plan pierde una migración.
- **F1–F3 pendientes; no se implementa sin pedido explícito.**
