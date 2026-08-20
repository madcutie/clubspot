# Bitácora — plan de reserva online

Registro de avance del [plan](plan-reserva-online.md). Lo más nuevo arriba.

## 19/08/2026 — la base de personas contra la API real; se borró el mock del backoffice

Última pantalla del backoffice que quedaba contra `api/store.ts`. Se borraron `store.ts` y
`mockApi.ts`: **la consola ya no tiene una sola línea de mock**.

Lo que hubo que terminar en el backend para que la pantalla no mintiera:

- **Los contadores de turnos de una persona estaban stubbeados en cero** desde que se escribió
  `PeopleQueries` ("hasta que existan las reservas"), y el filtro "Sin turnos" daba verdadero
  para todo el mundo. Ahora salen de un contrato nuevo, **`IPersonBookings`**, declarado en
  `Application/Bookings/` e implementado en Infrastructure: cuántos turnos tiene, cuándo jugó
  por última vez, quiénes reservaron alguna vez y el historial de una ficha.
- **Se consulta sólo si el tenant contrató `bookings`** (`ITenantModules`). Sin el módulo nadie
  tiene turnos, que es una configuración soportada y no una feature rota (ADR-0012, §5).
- El **historial de la ficha** es un endpoint del módulo de reservas
  (`GET /api/people/{id}/bookings`, `RequireModule(Bookings)`), no de core: si el club no lo
  contrató da 404 y el portal de la ficha se dibuja igual, sin la pestaña con datos.
- **Una definición de "turno"**: confirmado o con hold vivo. Cancelado y vencido no cuentan ni
  en el contador ni en el historial, así que los dos números no pueden discrepar.
- **Bug encontrado por un test nuevo**: `GET /api/people?filter=withoutBookings` devolvía
  **400**. La validación comparaba el valor crudo contra una lista en minúsculas, o sea que la
  API rechazaba la grafía que ella misma publica en `totals`.
- `PeoplePage` ahora informa su `pageSize`, para que el frontend no repita el 14 del servidor.

En el frontend: `Persona.id` pasó de `number` a `string` (los ids son `Guid`), el adaptador
`personasHttp.ts` entrega las fechas ya escritas, y se sacó de la ficha el dato de **ausencias**,
que el mock inventaba a partir de si la persona estaba bloqueada.

Verificado contra la API real (log de tráfico completo: login, contexto, tabla, búsqueda por
nombre y por teléfono, los cuatro filtros, ficha, historial, alta, nota, bloqueo individual y
masivo, pago, 404 de ficha inexistente, 400 de filtro inventado, 401 sin token).

Tests: 79 unit + 64 integración en verde.

## 19/08/2026 — la seña es 50% o 100%, y otros cierres chicos del code review

Decisiones del usuario tomadas hoy:

- **La seña es media entrada o entrada completa: 50 % o 100 %, nada en el medio.** Antes
  `depositPercent` admitía cualquier valor entre 0 y 100, y en 0 la reserva se confirmaba
  cobrando cero. Ahora la regla la imponen el agregado `Club` y un check constraint
  (`"depositPercent" IN (50, 100)`, migración `DepositPercentHalfOrFull`).
- **El saldo de una seña se asienta como `PaymentKind.Balance`.** Antes el tipo salía del
  `PaymentMode` de la reserva, así que el segundo pago de una seña también decía `Deposit`.
  Ahora lo decide lo que la reserva ya tenía cobrado, que es la misma consulta que detecta
  plata duplicada.

Y tres correcciones chicas que quedaban del code review del 18/08:

- **La fila de pago guarda la moneda con la que liquidó el proveedor**, no la del club: antes
  un pago en otra moneda quedaba marcado huérfano pero la fila decía ARS.
- **Rate limiting**: se sacó la rama `X-Forwarded-For`, que era código muerto (`RemoteIpAddress`
  nunca es null) y además habría sido un bypass, porque la cabecera la pone el cliente. Detrás
  de un proxy el camino correcto es `UseForwardedHeaders`.
- **Mercado Pago**: un estado que no es `approved` ni `rejected` —un reembolso, un contracargo—
  deja un `LogWarning`. No implementa reembolsos (F07 sigue diferido), pero deja rastro.

En el backoffice se sacó el **deporte de la base de personas** (columna, alta y ficha):
`Person` ya no tiene deporte preferido desde ADR-0008.

Tests: 79 unit + 63 integración en verde. Nuevos: la regla 50/100 en el agregado, la moneda
ajena que se asienta como tal y queda huérfana, y el tipo del saldo de una seña.

## 18/08/2026 — liberación del hold al abandonar y disponibilidad sin cache viejo

El usuario detectó que tras bloquear un turno y volver atrás lo veía disponible. Diagnóstico:
el bloqueo real (hold de 5 min) estaba sano en el backend; lo que mentía era el cache del
portal (staleTime 15 s + bfcache del botón atrás), y además abandonar el checkout dejaba el
hold puesto hasta vencer el TTL. Cambios:

- **`POST /api/portal/{club}/bookings/{id}/release`**: libera el hold al abandonar. Update
  condicional (`WHERE status = PendingPayment`): jamás cancela una reserva que el webhook
  confirmó un instante antes; idempotente (204 también si ya no estaba pendiente).
- **Pago sobre hold liberado ⇒ huérfano**: `ApplyPaymentAsync` asienta el pago con
  `ApprovedOrphan` en vez de lanzar (antes `ConfirmPayment` habría tirado 500 al webhook).
- **Portal**: "Volver al inicio" sin pagar llama al release y refresca disponibilidad; un
  `pageshow` en `main.tsx` invalida el cache cuando el navegador restaura la página congelada
  (botón atrás desde el checkout de MP).
- 3 tests de integración nuevos (release libera el turno · release no toca confirmadas · pago
  sobre hold liberado queda huérfano) — 114 verdes. Verificado además en vivo por API.
- **Conciliación puntual** (`POST /bookings/{id}/settle`, `SettleBookingHandler`): a los 5 s de
  espera sin webhook, el portal pide conciliar esa reserva contra los proveedores (mismo camino
  idempotente que J2) y repite cada 5 s. Peor caso pasa de "próximo tick de J2" a segundos.
  El reloj de arena de la espera ahora es una animación (`hourglass-flip`).
- **Canceladas visibles en la agenda** (elección del usuario: lista debajo de la grilla): la
  agenda devuelve `inactive` — canceladas, vencidas y holds muertos del día con el **monto
  pagado** (huérfanos incluidos: plata sobre reserva muerta es lo que el operador debe ver) —
  y el backoffice las lista con estado y "pagó $X" resaltado. Cancelar ya no hace desaparecer
  la reserva.

También hoy, del lado de MP: la URL de webhook de **modo prueba** del panel seguía apuntando
al túnel viejo (por eso el pago 173600673583 rebotó con 502 y lo rescató J2). Corregida al
dominio fijo `noe-uncephalic-jerome.ngrok-free.dev`, verificada con "Simular notificación"
(200) y con el reintento real del webhook (200, idempotente: `AlreadyProcessed`). Falta un
pago nuevo para ver la confirmación instantánea por webhook (`source=Webhook`).

## 17/08/2026 — primer pago real contra el sandbox de Mercado Pago

Se cerró el ciclo con MP de verdad: reserva desde el portal → preferencia real (Checkout Pro,
`binary_mode`, vencimiento = TTL del hold) → pago aprobado en el checkout real con tarjeta de
prueba → reserva `confirmed` y fila en `payments` con el id real de la operación.

- Cambios de esta tanda: SDK oficial en el proyecto vendor `ClubSpot.Infrastructure.MercadoPago`
  (regla de vendors del usuario) · validación de firma `x-signature` (HMAC, 6 tests) ·
  `auto_return` sólo con retorno https (MP lo exige) · si falla la creación del checkout, el
  hold se cancela en vez de bloquear el turno todo el TTL · secretos en
  `appsettings.Development.json`, que dejó de versionarse (ver AGENTS §6).
- Se conectó el **MCP oficial de MP** a la sesión: webhook configurado y cuenta compradora
  gestionadas por tooling, y documentación consultada de fuente (confirmado: el tópico
  `payment` es el vigente para Checkout Pro; "legacy" en el panel refiere a otros productos).
- ⚠️ **Pendiente**: la entrega espontánea del webhook en sandbox no llegó (cero tráfico en el
  túnel; historial de MP vacío — el pago con credenciales de prueba corre por la app sombra
  del vendedor de prueba). El aviso se disparó a mano firmado con la clave real y de ahí el
  flujo fue el de producción. Refuerza la necesidad de la conciliación (J2), que automatiza
  exactamente ese rescate. Confirmación definitiva de la entrega: con credenciales productivas.

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
