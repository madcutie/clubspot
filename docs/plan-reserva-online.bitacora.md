# Bitácora — plan de reserva online

Registro de avance del [plan](plan-reserva-online.md). Lo más nuevo arriba.

## 17/08/2026 — etapa 2 ejecutada y verificada con el gateway fake

Pago online completo funcionando contra el gateway de desarrollo. Mercado Pago quedó escrito
(`MercadoPagoGateway`, Checkout Pro + webhook) pero **sin probar: falta la credencial de
sandbox del usuario**.

Qué se construyó:

- **Hold con TTL**: la reserva online nace `pendingPayment` con `expiresAt` (15 min
  configurables) y pasa a `confirmed` con el webhook aprobado. Sin job: **expiración
  perezosa** — un `UPDATE` acotado en el camino de venta y el filtro por `expiresAt` en
  disponibilidad y agenda. El constraint de exclusión ahora cubre `Confirmed` y
  `PendingPayment`; verificado por SQL.
- **Tabla `payments`** append-only, única por (gateway, externalId): la idempotencia del
  webhook se apoya ahí. Pago aprobado sobre hold vencido: se intenta resucitar; si el turno ya
  se vendió queda `ApprovedOrphan` para revisión manual.
- **Webhook con slug en la URL** (`/api/payments/{gateway}/webhook/{clubSlug}`): reusa el
  filtro de tenant del portal (extraído a `ClubScope`); no hay salto del filtro global.
- **Checkout fake** servido por la API sólo en Development (`/dev/checkout`), cuyos botones
  pegan al webhook real. `Payments:Gateway` en `appsettings.Development.json` elige `fake`;
  con `none` el portal ofrece sólo "pagás en el club" (el catálogo publica `onlinePayments`).
- **Portal**: selector de forma de pago (club / total / seña con `depositPercent` real),
  redirect al checkout, pantalla de retorno que sondea `GET /bookings/{id}` hasta ver el
  resultado. **Backoffice**: el hold se dibuja en ámbar punteado con "pago pendiente".

Verificación (navegador + SQL):

1. Pago total online: hold en la base (`PendingPayment`/`OnlineFull`) → checkout fake →
   aprobar → retorno "¡Pago acreditado!" → `Confirmed` + fila en `payments` ($18.000, Full,
   Approved) → visible en la agenda del backoffice.
2. Reintento del mismo webhook en vivo → `alreadyProcessed`, sigue habiendo un solo pago.
3. Seña sobre Cancha 2 ($16.000 → cobra $8.000): mientras el hold vivía, el backoffice lo
   mostró como "pago pendiente"; aprobado, quedó `confirmed` con pagado $8.000 y saldo $8.000.
4. Rechazo, expiración perezosa y bloqueo del hold cubiertos por tests de integración
   (5 nuevos en `PaymentFlowTests`). Total: **58 unit + 43 integración en verde**.

Decisiones tomadas en ejecución (revisables): TTL 15 min · la seña es
`round(precio × depositPercent / 100, 2)` sin redondeo a $100 · un pago rechazado no mata el
hold (se puede reintentar hasta el TTL) · `payments` vive en `bookings` hasta que se defina la
granularidad de finanzas (ADR-0012).

## 17/08/2026 — etapa 1 ejecutada y verificada

Reserva sin pago online funcionando de punta a punta. Verificación con navegador + SQL:

1. Reserva desde el portal (Pádel, mar 18/08, 19:00–20:00, Cancha 1) → fila en `bookings` con
   `origin='Portal'`, `createdBy` null, precio nocturno $18.000 calculado por el servidor.
2. **Vínculo con persona**: se creó la persona en `people` con `origin='App'`, email y celular
   normalizados (`valen.rios@test.com`, `3624558899`), y la reserva quedó linkeada por
   `personId`. El matching email → celular → crear quedó cubierto por tests de integración
   (un segundo pedido con el mismo email y otro celular reusa la persona).
3. El turno desapareció de la disponibilidad del portal (Cancha 1 sin arranques 19:00;
   Cancha 2 los sigue ofreciendo) y apareció en la agenda del backoffice con nombre y precio.
4. Reintento del mismo turno → `409`, sin fila nueva y sin persona basura (el chequeo de
   ocupación corre antes de resolver la persona).
5. "Mis reservas" (localStorage del dispositivo) lista la reserva real.

Detalles de ejecución:

- Migración `20260817213115_PortalBookings`: `origin` (default `'Counter'` para lo
  preexistente), `personId` nullable con FK a `people`, `createdBy` nullable.
- Contrato `IPeopleLink` en `Application/Core/People`, implementado en Infrastructure sobre el
  mismo `DbContext` **sin flush propio**: persona y reserva se confirman en la misma
  transacción (`SaveChanges` del store de reservas).
- El portal respeta el aviso mínimo con la hora real del club; el mostrador mantiene el bypass.
- Tests: 58 unit + 38 integración en verde (4 nuevos del endpoint del portal, 2 nuevos de
  invariantes de `Booking`).
- Los servicios de desarrollo ahora los corre el agente en background (pedido del usuario,
  17/08): `dev-up.ps1` queda para uso manual.

## 17/08/2026 — plan escrito, etapa 1 en curso

- El usuario aprobó el orden: etapa 1 (reserva sin pago) → prueba con navegador → etapa 2
  (Mercado Pago). El login quedó para después (etapa 3).
- Se escribió el plan y arrancó la implementación de la etapa 1.
