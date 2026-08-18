# Bitácora — JobService y J2

Registro de avance del [plan](plan-jobservice.md). Lo más nuevo arriba.

## 17/08/2026 — J2 construido y verificado contra Mercado Pago real

- `ClubSpot.JobService` corriendo: Hangfire sobre la base propia `clubspot-hangfire` (el
  servicio la crea si falta), recurrente cada 5 minutos, lock distribuido por (job, tenant),
  gating por módulo `bookings`, gateway elegido por configuración.
- Puertos nuevos: `IPaymentGateway.FindPaymentsAsync` (en MP: `payments/search` por
  `external_reference`; el fake devuelve vacío) · `IBookingsStore.GetUnsettledOnlineBookingIdsAsync`
  (48 h de ventana, lote acotado) · `IClubDirectory.GetAllClubIdsAsync`. El caso de uso es
  `ReconcileOnlinePaymentsHandler` (Application), con 3 tests de unidad. 110 tests en verde.
- **Prueba viva**: se desaplicó a mano el pago real del 17/08 (fila de `payments` borrada,
  reserva devuelta a `pendingPayment`) y a la corrida siguiente J2 lo reencontró en MP y
  reconstruyó el estado: `2 candidates, 1 applied, 0 orphaned` — la reserva volvió a
  `Confirmed` con su pago `174375272506`. El escenario del webhook perdido ahora se cura solo.
- `dev-up.ps1` levanta también el JobService. Config local en su propio
  `appsettings.Development.json` (no versionado, con `.example`).

## 17/08/2026 — plan aprobado, implementación en curso

- El usuario decidió: JobService como proyecto propio, Hangfire con base `clubspot-hangfire`,
  **sólo J2** (J1 descartado — la expiración perezosa ya alcanza).
- Se escribió el plan y arrancó la implementación.
