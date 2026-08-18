# Plan — JobService y conciliación de pagos (J2)

> **Estado**: aprobado el 17/08/2026 (usuario: "arma el JobService para J2 con Hangfire, base
> `clubspot-hangfire`"). Avance en [`plan-jobservice.bitacora.md`](plan-jobservice.bitacora.md).

Primer pedazo de la infraestructura de jobs (§9.1 de AGENTS.md), con un solo habitante: **J2,
la conciliación de pagos online**. El incidente que lo motiva quedó registrado en la bitácora
del plan de reserva online (17/08): un pago real de Mercado Pago aprobado cuyo webhook nunca
llegó — el rescate fue manual y J2 automatiza exactamente ese rescate.

## Decisiones

| Qué | Decisión |
|---|---|
| Host | **`ClubSpot.JobService`**: ejecutable propio, separado de la Api. Se reinicia y despliega solo; un job nunca compite con un request |
| Scheduler | **Hangfire** sobre PostgreSQL, en una **base propia `clubspot-hangfire`** (decisión del usuario): el estado interno del scheduler no se mezcla con los datos del negocio. El servicio la crea si no existe |
| Alcance | **Sólo J2** (usuario, 17/08/2026). J1 no se construye: la expiración perezosa ya garantiza la corrección y el job sería cosmético |
| Capas | El job del host sólo orquesta; el caso de uso es un handler de Application (`ReconcileOnlinePaymentsHandler`) que trabaja contra puertos. El JobService referencia Application + Infrastructure, igual que la Api |
| Dashboard | Todavía no — la pantalla de operación llega con observabilidad (§9.1). Por ahora el resultado de cada corrida se **loguea** (provisional hasta que existan métricas) |

## J2 — diseño

Cada **5 minutos**, un dispatcher:

1. Lista los clubes (`IClubDirectory.GetAllClubIdsAsync`, puerto nuevo).
2. Por cada club abre el **ámbito de tenant explícito**; si el módulo `bookings` no está
   habilitado, saltea (el job no corre para quien no lo contrató).
3. Toma un **lock distribuido por (job, tenant)** — dos corridas del mismo tenant no se pisan.
4. Corre el handler, **acotado**: reservas online (`paymentMode != club`) que estén
   `pendingPayment` o `expired`, creadas en las últimas **48 horas**, **sin pago aprobado**
   registrado, con tope de lote.
5. Por cada candidata le pregunta al gateway (`IPaymentGateway.FindPaymentsAsync`, puerto
   nuevo: en Mercado Pago es `GET /v1/payments/search?external_reference={bookingId}`; el fake
   devuelve vacío) y aplica lo encontrado por el mismo `ApplyPaymentAsync` **idempotente** del
   webhook — pago aprobado confirma la reserva (o la resucita; si el turno ya se vendió queda
   `ApprovedOrphan`), rechazado se registra.
6. Loguea el resultado de la corrida: candidatas, aplicados, huérfanos.

Cumplimiento de las reglas de AGENTS §7: idempotente (hereda del webhook) · lock por (job,
tenant) · acotado y por lotes (48 h + tope) · reanudable (sin estado propio) · sin efectos
externos dentro de la transacción (la consulta a MP ocurre antes de aplicar) · tenant siempre
explícito · resultado registrado (log, provisional).

## Verificación

1. Tests de unidad del handler (puertos falsos): aplica el pago hallado, ignora candidatas sin
   pago, respeta el tope.
2. **Prueba viva contra Mercado Pago**: desaplicar a mano el pago real conciliado el 17/08
   (borrar la fila de `payments` y devolver la reserva a `pendingPayment`) y dejar que J2 lo
   reencuentre por `external_reference` y reconstruya el estado — el mismo rescate, ahora solo.

## Fuera de alcance

Dashboard de Hangfire · métricas reales · el resto de los jobs (J1 explícitamente descartado;
J3–J11 cuando toquen) · bandeja de revisión manual de huérfanos (pantalla de operación, §9.1).
