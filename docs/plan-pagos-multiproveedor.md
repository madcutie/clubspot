# Plan — Asiento de pago multiproveedor

**Estado:** aprobado 18/08/2026 · implementa [ADR-0014](adr/0014-asiento-de-pago-agnostico-del-proveedor.md)
y [ADR-0015](adr/0015-mercadopago-checkout-pro-online-orders-presencial.md) · avance en la
[bitácora](plan-pagos-multiproveedor.bitacora.md).

## Objetivo

Que el asiento del pago sea transparente al proveedor y al canal: cada fila de `payments` dice
**qué proveedor** procesó el pago (`mercadopago`, `fake`, mañana `redlink`), **por qué canal**
(`checkout` online hoy; `order` presencial mañana) y **cómo llegó** (`source`: webhook o
conciliación — ya existe). Agregar un proveedor o un canal no toca el dominio ni las reservas:
es una implementación nueva detrás del mismo puerto.

Este plan **no** implementa el cobro presencial ni ningún proveedor nuevo: prepara las
costuras para que entren como agregado, no como cirugía.

## Estado de partida

- `Payment` (dominio) lleva `Gateway` (string), `ExternalId`, `Amount`, `Kind`, `Status`,
  `Source`; unique `(gateway, externalId)`.
- `IPaymentGateway`: un solo puerto con `Name`, `CreateCheckoutAsync` y `FindPaymentsAsync`;
  una sola implementación registrada por configuración (`Payments:Gateway`).
- El webhook resuelve el proveedor por el segmento `{gateway}` de la ruta; J2 usa "el" gateway
  registrado.

## Diseño

### 1. El asiento

- `Payment.Gateway` pasa a llamarse **`Provider`** (columna `provider`) — el nombre que usa el
  negocio. Migración con rename, sin pérdida.
- Nueva propiedad **`Rail`** (enum `PaymentRail { Checkout, Order }`, columna `rail`,
  camelCase en JSON como todos los enums). Las filas existentes nacieron todas por
  `Checkout` — default del migration.
- Unique pasa a `uxPaymentsProviderExternalId` sobre `(provider, externalId)` (mismo
  contenido, nombre por convención del contexto).
- `PaymentNotification` gana `Rail`: quien conoce el canal es la implementación del proveedor
  que produjo la notificación, no quien la aplica.

### 2. El puerto: proveedor + capacidades

`IPaymentGateway` se parte en identidad común y capacidades opcionales:

```
IPaymentProvider                    // identidad + conciliación: todo proveedor la tiene
 ├─ string Name
 └─ FindPaymentsAsync(bookingId)    // J2; devuelve notificaciones con su Rail

IHostedCheckout : IPaymentProvider  // capacidad: checkout online con redirect
 └─ CreateCheckoutAsync(request)
```

- El presencial del futuro será otra capacidad (`IInPersonOrders` o similar) definida en su
  propio plan; acá solo se deja la forma.
- Proveedor sin una capacidad ⇒ esa forma de cobro no se ofrece (regla ADR-0014). El portal
  ofrece pago online si el proveedor activo implementa `IHostedCheckout`.
- `fake` y `mercadopago` implementan ambos niveles; sus proyectos no se mueven.

### 3. Resolución por nombre

- Un registro `IPaymentProviders` (colección chica cableada por DI) resuelve proveedor por
  nombre. Hoy contiene uno; el diseño no asume cardinalidad.
- El webhook `/api/payments/{provider}/webhook/{clubSlug}` resuelve contra el registro: 404 si
  el nombre no existe. La ruta ya tenía el segmento; solo cambia el nombre del parámetro.
- J2 concilia **por proveedor registrado**, y el resultado por corrida se loguea por
  proveedor.
- `Payments:Gateway` de configuración pasa a `Payments:Provider` (mismo contenido). Qué
  proveedor usa cada club cuando haya más de uno es **pregunta abierta** — hoy es
  configuración global y así queda.

## Fases

### F1 — Asiento (dominio + persistencia)

- Rename `Gateway`→`Provider` en `Payment`, `PaymentNotification` y `BookingsStore`; nueva
  `Rail` con su converter camelCase en `Program.cs` de Api y JobService.
- Migración: rename de columna + rename del unique + columna `rail` default `Checkout`.
- Tests de dominio y de integración ajustados; el de webhook verifica que el asiento quede con
  `provider`, `rail` y `source` correctos.

**Verificación:** build + tests verdes; `db-sql` muestra las filas viejas con
`provider='mercadopago', rail='Checkout'`.

### F2 — Puerto y registro

- Partir `IPaymentGateway` en `IPaymentProvider` + `IHostedCheckout`; adaptar `fake` y
  MercadoPago; registro `IPaymentProviders` y DI.
- Webhook y J2 resuelven por nombre contra el registro; portal ofrece online solo si el
  proveedor activo tiene `IHostedCheckout`.

**Verificación:** build + tests; webhook con proveedor inexistente → 404; J2 corre y loguea
por proveedor.

### F3 — Punta a punta

- Reserva online completa con el fake y con Mercado Pago real (webhook y conciliación),
  verificando el asiento (`provider`/`rail`/`source`) en la base en los dos casos.

**Verificación:** las dos corridas documentadas en la bitácora con los ids reales.

## Fuera de alcance

- Cobro presencial (Point/QR vía Orders): plan propio cuando se encare (ADR-0015).
- Proveedores nuevos (Redlink): entran por ADR-0014 cuando existan.
- Proveedor por tenant: pregunta abierta, no se anticipa.
- Reembolsos y anulaciones: siguen pendientes del lado de finanzas.
