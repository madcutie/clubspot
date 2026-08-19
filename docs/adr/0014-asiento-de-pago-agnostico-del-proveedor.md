# ADR-0014 — El asiento del pago es agnóstico del proveedor y del canal

**Fecha:** 18/08/2026 · **Estado:** Aceptada

## Contexto

El pago online funciona con Mercado Pago (Checkout Pro) y quedó verificado con dinero real.
La tabla `payments` registra `gateway`, `externalId` y `source` (webhook o conciliación J2), y
el puerto `IPaymentGateway` tiene una sola implementación activa por vez.

En el horizonte hay dos expansiones seguras: el **cobro presencial** (Point / QR, que en
Mercado Pago corre por la Orders API — otro canal del mismo proveedor) y, más adelante,
**otros proveedores** (Redlink u otro). El riesgo es acoplar el asiento del pago y el puerto a
los conceptos de Mercado Pago, y que cada proveedor o canal nuevo obligue a tocar el dominio.

## Decisión

**El asiento del pago registra de forma explícita y agnóstica: qué proveedor lo procesó, por
qué canal, y con qué identificador externo.** El resto del sistema opera sobre el asiento sin
conocer al proveedor.

- `payments` lleva `provider` (id estable del proveedor: `mercadopago`, `fake`, mañana
  `redlink`) y `rail` (el canal del proveedor por el que se asentó: `checkout` hoy; `order`
  cuando exista el presencial). `provider` es un string estable, como los ids de módulo — no
  un enum del dominio.
- La idempotencia sigue anclada en **unique (provider, externalId)**.
- **Todo pago entra por el mismo camino** (`ApplyPaymentAsync`), venga del webhook o de la
  conciliación; `source` ya registra cuál de los dos fue. Proveedor nuevo o canal nuevo no
  agregan caminos de escritura.
- **Un puerto por proveedor, con canales como capacidades opcionales.** La identidad y la
  conciliación son comunes a todo proveedor; cada forma de cobrar (checkout online hosted,
  orden presencial) es una capacidad que el proveedor implementa o no. Proveedor sin una
  capacidad ⇒ esa forma de cobro **no se ofrece**, no falla — la misma regla que rige los
  módulos (ADR-0012).
- Cada proveedor vive en su proyecto de Infrastructure propio (regla de vendors del
  17/08/2026) y se registra por nombre; webhook y J2 resuelven el proveedor por ese nombre.

## Consecuencias

- Agregar un proveedor = un proyecto vendor nuevo que implementa el puerto y sus capacidades.
  Agregar un canal = una capacidad nueva. En ninguno de los dos casos cambia `payments` ni la
  lógica de reservas.
- Las filas existentes de `payments` son retrocompatibles: todas nacieron por el canal
  `checkout`.
- El detalle de implementación está en `docs/plan-pagos-multiproveedor.md`.

## Alternativas descartadas

- **Una tabla de pagos por proveedor:** multiplica caminos de escritura y consultas; la
  pregunta del negocio ("¿esta reserva está paga?") es una sola.
- **Un puerto monolítico que obligue a cada proveedor a implementar todos los canales:**
  fuerza stubs que lanzan `NotSupported` — el anti-patrón que la composición por capacidades
  evita.
- **Enum de proveedor en el dominio:** cada proveedor nuevo tocaría el dominio; el string
  estable ya funcionó para los ids de módulo.
