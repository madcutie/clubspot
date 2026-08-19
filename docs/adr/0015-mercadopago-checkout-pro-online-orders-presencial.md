# ADR-0015 — Mercado Pago: online por Checkout Pro; Orders reservado al presencial

**Fecha:** 18/08/2026 · **Estado:** Aceptada

## Contexto

Mercado Pago mantiene dos generaciones de API: la histórica **Payments API** (`/v1/payments`,
sobre la que corre Checkout Pro vía preferencias) y la nueva **Orders API** (`/v1/orders`,
sobre la que corren Checkout API/Bricks, Point y QR). El panel etiqueta el tópico de webhook
`payment` como "Pagos (legacy)", lo que motivó evaluar (18/08/2026, contra documentación
oficial) si convenía unificar todo el cobro —online y futuro presencial— sobre Orders y
eliminar la dependencia de Payments v1. Hallazgos:

1. **La billetera no corre sobre Orders.** En MLA, "Cuenta de Mercado Pago" (dinero en cuenta,
   tarjetas guardadas, pago desde la app) solo existe vía **preferencia**: hasta el Payment
   Brick exige crear una preferencia y redirige al sitio de Mercado Pago con login para ese
   medio. Checkout API vía Orders cubre tarjetas y Rapipago/Pago Fácil, sin Cuenta de Mercado
   Pago ni Cuotas sin Tarjeta. "Todo Orders" pierde el medio de pago número uno del comprador
   argentino, o degenera en un híbrido con **más** conceptos que hoy (Bricks + Orders +
   preferencia para billetera + dos tipos de webhook).
2. El tópico `payment` **no tiene sunset**: es el camino oficial vigente de Checkout Pro; el
   "(legacy)" refiere a la generación de API, no a una deprecación del producto.
3. El SDK .NET oficial soporta Orders **online** desde la v2.5.0, pero no modela las órdenes
   presenciales (`config.point` / `config.qr`) ni siquiera en la 3.5.0 (08/2026).
4. La homologación no es barrera en ningún caso (self-service, medición de calidad
   recomendada, SAQ A en ambos); la única homologación con aprobación real es la del QR
   presencial.

## Decisión

- **El pago online del portal sigue en Checkout Pro**: preferencia + redirect a `init_point` +
  webhook `payment` + confirmación por `GET /v1/payments/{id}`. Un solo concepto que cubre
  todos los medios, incluida la billetera.
- **La Orders API queda reservada al cobro presencial** (Point / QR), cuando se construya. No
  se escribe código de Orders hasta entonces.
- Bajo ADR-0014, ambos son **rails del proveedor `mercadopago`**: `checkout` (hoy) y `order`
  (futuro). El asiento del pago no distingue generaciones de API de Mercado Pago: registra
  proveedor + canal.

## Reevaluación explícita

Si Mercado Pago habilita la billetera sobre Orders en MLA, se reevalúa migrar el online — con
un ADR nuevo. Chequear el changelog de MP Developers al pasar a credenciales productivas y al
encarar el plan presencial.

## Consecuencias

- La integración verificada con dinero real se mantiene; no se reescribe el checkout.
- El presencial entrará como capacidad nueva del vendor `ClubSpot.Infrastructure.MercadoPago`
  (REST crudo si el SDK sigue sin modelar órdenes presenciales), sin tocar el asiento.
- El webhook podrá recibir dos tópicos (`payment` y `orders`) conviviendo; la firma
  `x-signature` es común a ambos.

## Alternativas descartadas

- **Todo Orders + Bricks ahora:** pierde la billetera o la reintroduce vía preferencia — el
  híbrido tiene más piezas que lo actual y el beneficio de "un solo concepto" no se cumple por
  una limitación de Mercado Pago, no del diseño propio.
- **Dejar la decisión implícita:** la pregunta "¿por qué usamos el tópico legacy?" volvería en
  cada sesión; queda registrada con su evidencia.
