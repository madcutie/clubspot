# Bitácora — Plan asiento de pago multiproveedor

Registro de avance del [plan](plan-pagos-multiproveedor.md). La entrada más nueva arriba.

## 18/08/2026 — F1 y F2 cerradas; F3 verificada con el fake, falta la pata Mercado Pago real

- **F1 (asiento):** `Payment.Gateway` → `Provider`, nueva `Rail` (`Checkout` | `Order`),
  `PaymentNotification` lleva `Provider` + `Rail`. Migración `PaymentProviderRail` (rename de
  columna e índice `uxPaymentsProviderExternalId` + columna `rail` con default `Checkout`).
  Verificado en base: las 4 filas preexistentes quedaron `provider` correcto y `rail=Checkout`.
- **F2 (puerto):** `IPaymentGateway` partido en `IPaymentProvider` (identidad + conciliación) e
  `IHostedCheckout` (capacidad de checkout online). Clases renombradas a `FakePaymentProvider`
  y `MercadoPagoProvider`; config `Payments:Gateway` → `Payments:Provider` en todos los
  appsettings. **Desvío consciente del plan:** no se creó la clase registro `IPaymentProviders`
  — la colección `IEnumerable<IPaymentProvider>` de DI ya es el registro y ningún consumidor
  necesita más; se crea el día que haga falta resolver por nombre fuera de las rutas.
  J2 concilia iterando los proveedores registrados y loguea por proveedor
  (`Reconciliation for tenant … via mercadopago: …` verificado en vivo).
- **F3 con fake, verificada:** reserva por API (`841015ad…`, onlineFull) → hold
  `pendingPayment` → webhook fake aprobado → reserva `Confirmed` y asiento
  `provider=fake, rail=Checkout, source=Webhook, Approved, 14000.00` comprobado por SQL.
- Tests: 111 verdes (68 unit + 43 integración); se agregó
  `Every_registered_provider_reconciles_and_reports_on_its_own`.
- **Pendiente para cerrar F3:** la corrida con Mercado Pago real — bloqueada en que Defender
  volvió a poner ngrok en cuarentena tras el reboot (lo restaura el usuario) y en un pago de
  prueba del usuario. Al hacerla, verificar además que el webhook ahora responde 200
  (RequireValidSignature=false) y que el auto_return redirige solo.

## 18/08/2026 — Plan escrito; implementación no arrancada

- Contexto de la decisión: el usuario preguntó si convenía unificar todo el cobro en la Orders
  API de Mercado Pago. La investigación (4 puntos contra doc oficial, resumen en ADR-0015)
  mostró que la billetera no corre sobre Orders en MLA, así que el online queda en Checkout
  Pro y Orders se reserva al presencial.
- De esa discusión salió el requisito de este plan (pedido del usuario, 18/08/2026): el
  asiento del pago tiene que ser transparente — proveedor uno o proveedor dos, canal checkout
  o canal orders, el proceso es el mismo y la fila dice con quién y por dónde se asentó.
- Escritos ADR-0014 (asiento agnóstico) y ADR-0015 (Checkout Pro online / Orders presencial),
  este plan y esta bitácora. **F1–F3 pendientes; no se implementa sin pedido explícito.**
