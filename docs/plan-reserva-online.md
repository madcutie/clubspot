# Plan — reserva online desde el portal

> **Estado**: aprobado el 17/08/2026 — **etapas 1 y 2 cerradas y verificadas con el gateway
> fake** (17/08). Mercado Pago real espera credenciales de sandbox; la etapa 3 (login) no
> arrancó. Avance en [`plan-reserva-online.bitacora.md`](plan-reserva-online.bitacora.md).

Convierte el portal de reservas (`src/frontend/reservas/`) de sólo lectura a un canal real de
venta, en tres etapas. Cada etapa deja algo utilizable y se verifica con navegador + SQL antes
de pasar a la siguiente (mismo protocolo que el plan de disponibilidad).

## Etapas

| | Etapa | Qué habilita |
|---|---|---|
| ✅ | **1 — Reserva sin pago online** | El cliente reserva desde el portal con nombre y teléfono; paga en el club. Equivale al flujo actual por teléfono/WhatsApp |
| ✅ | **2 — Pago online (gateway fake; MP escrito, sin probar)** | Seña o total online. Trae hold con TTL, webhook idempotente y el gateway abstraído (detalle abajo) |
| ⬜ | **3 — Login del socio** | "Mis reservas" en serio, deuda, credencial. Es la fase 4 del orden general del producto; acá sólo se registra que va después de la 2 |

Decisión de orden (usuario, 17/08/2026): la etapa 1 va primero porque casi todo ya existe
—constraint de exclusión, endpoint de creación, cálculo de precio en el servidor— y el riesgo
de ausentismo sin seña es el mismo que el club ya corre hoy con las reservas telefónicas.

## Etapa 1 — alcance

### Backend

- **`POST /api/portal/{clubSlug}/bookings`** — anónimo, dentro del grupo del portal (mismo
  filtro de slug + gating de módulo que catálogo y disponibilidad).
  - Request: `courtId`, `date`, `startMinute`, `durationMinutes`, `customerName`,
    `customerPhone`, `customerEmail` (opcional). Nombre y teléfono obligatorios acá
    (en mostrador el teléfono es opcional).
  - Respuestas: `201` con `{id, price}` · `400` datos faltantes · `404` cancha desconocida ·
    `422` turno inválido · `409` turno ocupado.
  - El precio lo calcula el servidor; el cliente no manda importes.
- **La reserva del portal se linkea a una persona de `core`** (regla del usuario, 17/08/2026,
  alineada con ADR-0012 — el `customerName` suelto era provisional): con los datos del
  formulario se busca en `people` **primero por email y después por celular** (`phoneDigits`);
  si no hay coincidencia se **crea la persona** con esos datos (`origin = app`) en la misma
  transacción que la reserva. La reserva guarda `personId` + el nombre y teléfono tal como se
  cargaron (snapshot del contacto de ese turno). Si el email matchea una persona y el celu
  otra, **gana el email**. No se pisan datos de una persona existente.
- **`Booking.Origin`** (`counter` | `portal`): columna nueva `origin`, para distinguir el canal.
  `createdBy` pasa a nullable — una reserva del portal no tiene operador. `personId` nullable:
  las de mostrador siguen sin vínculo por ahora (el panel del backoffice no pide email; ver
  preguntas). Migración aditiva.
- **Aviso mínimo**: el portal respeta `MinimumNoticeMinutes` con la hora real del club (igual
  que la disponibilidad que se le muestra). El mostrador mantiene el bypass: el operador vende
  en el momento.
- La protección contra doble venta no cambia: sigue siendo el constraint de exclusión + chequeo
  previo, `409` en conflicto.

### Frontend (portal)

- Pantalla de confirmación: se quita el selector de forma de pago y la seña (vuelven en la
  etapa 2); en su lugar, un aviso "el pago se hace en el club". Se quita el campo email (no se
  persiste; vuelve con los pagos). Se quita el texto de cancelación de 12 h (regla inexistente
  — no se inventa).
- CTA habilitado con nombre y teléfono cargados → `POST` → pantalla de éxito con el resumen.
- `409` durante la confirmación: aviso "ese turno se acaba de ocupar", vuelta a disponibilidad
  con las consultas invalidadas.
- **Mis reservas**: lista local (localStorage del dispositivo). Sin login no hay identidad;
  la lista server-side llega con la etapa 3.
- Textos del home: desaparece la promesa de seña.

### Verificación (navegador + SQL, sin infra de E2E)

1. Reservar un turno desde el portal → `bookings` tiene la fila con `origin = 'Portal'`,
   `createdBy` null y el precio del servidor.
2. El turno desaparece de la disponibilidad del portal y aparece en la agenda del backoffice.
3. Intentar reservar el mismo turno de nuevo → aviso de ocupado, sin fila nueva.
4. Datos incompletos → el CTA no habilita.

## Etapa 2 — alcance

### Modelo

- **Hold con TTL**: la reserva con pago online nace `pendingPayment` con `expiresAt`
  (15 minutos, configurable). Pasa a `confirmed` cuando el pago se aprueba, o a `expired`
  cuando vence. La de mostrador y la del portal "pagás en el club" siguen naciendo
  `confirmed` directo.
- **Expiración perezosa, sin job**: los holds vencidos se expiran inline en el camino de venta
  (un `UPDATE` acotado por cancha y fecha antes de insertar) y se excluyen de disponibilidad y
  agenda por su `expiresAt`. Mientras el hold viva, el constraint de exclusión sostiene el
  turno, así que no hay carrera. J1 queda como limpieza cosmética para cuando exista la
  infraestructura de jobs — no es necesario para la corrección.
- **El constraint de exclusión** pasa a cubrir `confirmed` **y** `pendingPayment`.
- **Tabla `payments`** (append-only): gateway, id externo, monto, tipo (total/seña), estado.
  Única por (gateway, id externo) — la idempotencia del webhook se apoya ahí. Vive en el
  módulo `bookings` **provisionalmente**: se muda cuando se defina la granularidad de finanzas
  (ADR-0012).
- **Pago aprobado sobre un hold ya expirado**: si el turno sigue libre, la reserva se
  resucita a `confirmed` (el propio constraint valida al actualizar); si otro lo tomó, el pago
  queda registrado como huérfano para revisión manual. La devolución es decisión pendiente.

### Gateway

- Puerto `IPaymentGateway` + dos implementaciones: **Mercado Pago** (Checkout Pro: preferencia
  con `external_reference` = id de la reserva, redirect, webhook) y **fake para desarrollo**
  (una pantalla de checkout simulada servida por la API sólo en Development, con botones
  aprobar/rechazar que pegan al mismo webhook real).
- Se elige por configuración (`Payments:Gateway`: `none` | `fake` | `mercadopago`). Sin
  gateway configurado el portal ofrece sólo "pagás en el club" — el catálogo publica si el
  pago online está habilitado.
- **El webhook lleva el slug del club en la URL** (`/api/payments/{gateway}/webhook/{clubSlug}`),
  fijado al crear la preferencia: así reusa el filtro de tenant del portal y no hace falta
  saltear el filtro global.
- Mercado Pago real queda **escrito pero sin probar** hasta tener credenciales de sandbox del
  usuario (pregunta abierta).

### Flujo

1. `POST /bookings` con `paymentMode`: `club` (etapa 1) · `onlineFull` · `onlineDeposit`
   (usa el `depositPercent` del club, sin redondeo inventado). Online → responde el hold +
   `checkoutUrl`.
2. El portal redirige al checkout; al volver (`?retorno={bookingId}`) consulta
   `GET /bookings/{id}` hasta ver `confirmed`, `expired` o rechazo.
3. El webhook idempotente registra el pago y confirma la reserva; reprocesar el mismo evento
   no duplica nada.

### Verificación (navegador + SQL, con el gateway fake)

1. Reserva con pago total → hold en la base → checkout fake → aprobar → `confirmed`,
   fila en `payments`, turno visible en backoffice.
2. Mientras el hold vive, el turno no se ofrece en el portal ni se puede vender en mostrador.
3. Rechazar el pago → el hold queda y expira; pasado el TTL el turno se vuelve a ofrecer.
4. Reintento del mismo webhook → sin pago duplicado.
5. Reserva con seña → pago por el monto de la seña, saldo visible.

## Fuera de alcance (de la etapa 1)

| Qué | Por qué |
|---|---|
| Hold con TTL | Sólo tiene sentido con pago online (etapa 2) |
| Pagos, seña, Mercado Pago | Etapa 2 |
| Cancelación desde el portal | Necesita identidad (etapa 3) y una regla de cancelación que el usuario todavía no definió |
| Login / cuentas de cliente | Etapa 3 |
| Notificaciones (email/WhatsApp de confirmación) | Necesita el outbox (J4), que no existe |

## Decisiones del usuario sobre pagos

1. ✅ **Cancelación** (17/08/2026): **con menos de 24 horas de anticipación se cobra el 50%
   del turno; con más, sin cargo.** Consecuencias por forma de pago:
   - Pagó **total online** y cancela tarde → se le devuelve el 50% (reembolso parcial por el
     gateway).
   - Pagó **seña (50%)** y cancela tarde → la seña no se devuelve. Con más de 24 h, se
     devuelve entera.
   - **Paga en el club** y cancela tarde → debe el 50%; cobrarlo necesita la cuenta corriente
     del módulo `finance`, que no existe todavía.
   La regla hoy se **informa** en el portal; la automatización (reembolsos por MP, cargo por
   deuda) queda para cuando existan cancelación desde el portal (etapa 3, requiere identidad)
   y los reembolsos. Mientras tanto la aplica el operador a mano.
2. ✅ Credenciales de sandbox cargadas y primer pago real verificado (17/08); ver bitácora.
3. ⬜ ¿Seña y total conviven siempre, o el club elige qué ofrecer? (hoy se ofrecen ambos).
