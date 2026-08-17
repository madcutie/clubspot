# Research — MercadoPago como gateway de cobro de reservas

> **Investigación del 17/08/2026.** No es un plan de implementación: releva qué ofrece
> MercadoPago, cuál es el enfoque mínimo para cobrar una reserva y qué hace falta para tener
> un entorno de pruebas funcionando. La implementación sale de acá recién cuando haya un plan
> aprobado. Fuentes al pie.

---

## 1. Alcance

El flujo a cubrir es el de §9.5 de AGENTS.md: **hold con TTL → pago → confirmada**, con el
`UPDATE` condicional atómico. El gateway entra en el paso del medio. El plan ya prevé el
gateway abstraído, el webhook idempotente, la conciliación (J2) y la bandeja de webhooks (J3)
en §9.4 — esta research los aterriza a MercadoPago.

## 2. Enfoque mínimo recomendado: Checkout Pro

MercadoPago ofrece tres integraciones online: **Checkout Pro** (redirección a una página de
pago hosteada por MP), **Checkout Bricks** (componentes de UI embebidos) y **Checkout API /
Orders** (control total, la tarjeta se tokeniza en el frontend propio).

Para el MVP la recomendación es **Checkout Pro**:

- **Cero manejo de tarjetas** en el sistema: sin alcance PCI, sin formulario de pago propio.
- Es la integración con menos código: un POST server-side que crea una *preferencia* y
  devuelve una URL (`init_point`); el resto pasa en MercadoPago.
- Acepta tarjeta, dinero en cuenta MP y demás medios sin trabajo adicional.
- Detalle no menor para el sandbox: **Checkout Bricks no soporta cuentas de prueba**, así que
  el camino de testing de Bricks es más incómodo. Checkout Pro sí se prueba entero con
  cuentas de prueba.

Bricks/API quedan como evolución si más adelante se quiere el pago embebido en el portal sin
salir de la página. El puerto de Application (`IPaymentGateway` o equivalente) tiene que ser
neutral a esa decisión.

## 3. El flujo mapeado a reservas

```
portal            API ClubSpot                     MercadoPago
  │  crear hold        │                                │
  ├──────────────────► │  booking = Hold(TTL)           │
  │                    ├─ POST /checkout/preferences ──►│
  │  ◄─ init_point ────┤   external_reference=bookingId │
  │                    │   expires + expiration_date_to │
  │  redirect ────────────────────────────────────────► │  (el socio paga)
  │                    │ ◄── POST webhook (payment) ────┤
  │                    ├─ GET /v1/payments/{id} ───────►│
  │                    │  si approved:                  │
  │                    │  UPDATE condicional → Confirmed│
  │  ◄─ back_url ──────────────────────────────────────┤  (sólo UX, no fuente de verdad)
```

Piezas de la preferencia relevantes para el dominio:

| Campo | Uso en reservas |
|---|---|
| `external_reference` | El id de la reserva (`Guid`). Es lo que une el pago con el hold cuando llega el webhook. |
| `expires` + `expiration_date_from/to` | La preferencia vence junto con el TTL del hold (ISO 8601). Un pago no puede nacer sobre un hold ya expirado por J1. |
| `binary_mode: true` | El pago sale sólo `approved` o `rejected`; los estados intermedios se rechazan solos. Simplifica el hold (no hay que sostenerlo esperando un `in_process`), a costa de rechazar medios que quedan pendientes. Ver §7. |
| `notification_url` | Adónde llega el webhook. Tiene prioridad sobre la URL configurada en el panel. |
| `back_urls` (`success`/`failure`/`pending`) + `auto_return` | Sólo UX de retorno. **La reserva no se confirma acá**: el comprador puede cerrar el navegador antes del redirect. La fuente de verdad es el webhook. |

Reglas del flujo:

- **El webhook confirma, el back_url sólo muestra.** El handler del webhook hace
  `GET /v1/payments/{id}` con el access token (nunca confía en el body del POST) y aplica el
  `UPDATE` condicional `Hold → Confirmed`.
- **Idempotencia obligatoria**: MercadoPago reintenta y puede notificar varias veces el mismo
  evento. Encaja con la bandeja de webhooks ya prevista (J3): se persiste la notificación, se
  responde rápido, se procesa aparte.
- **Conciliación (J2)**: `GET /v1/payments/search` por `external_reference` permite barrer
  pagos cuyos webhooks se perdieron.
- **Pago tardío sobre hold expirado**: si el webhook llega con el hold ya vencido (J1), el
  pago quedó cobrado sin reserva. La salida es el reembolso automático (§6). La ventana se
  achica con `expiration_date_to`, pero no desaparece.

## 4. Webhooks

- Se configuran en el panel **Tus integraciones → Webhooks** (por aplicación) o por
  transacción con `notification_url`; la de la transacción gana. El tópico que interesa es
  **`payment`** (cubre Checkout Pro); a futuro `topic_chargebacks_wh` para contracargos.
- El POST trae un JSON chico: id del evento, tipo, acción y `data.id` (el id del pago). El
  dato real se busca con `GET /v1/payments/{data.id}`.
- **Responder HTTP 200/201 en menos de 22 segundos.** Si no, MercadoPago reintenta cada
  15 minutos. Otra razón para persistir-y-responder en vez de procesar inline.
- **Validación de firma**: el header `x-signature` trae `ts=<timestamp>,v1=<hmac>`. Se valida
  calculando HMAC-SHA256 en hexadecimal con la **clave secreta** que muestra el panel de
  Webhooks, sobre el template `id:[data.id];request-id:[x-request-id];ts:[ts];` (se omiten las
  partes sin valor). El SDK trae un `WebhookSignatureValidator`. Sin esta validación cualquiera
  puede confirmar reservas con un `curl`.
- El panel incluye un **simulador de notificaciones** para probar el endpoint antes de que
  exista un pago real.

## 5. Estados de un pago

`approved` · `pending` · `in_process` (en revisión) · `rejected` · `cancelled` (venció o se
canceló) · `refunded` · `charged_back` · `in_mediation` (disputa). Con `binary_mode: true` el
espacio efectivo se reduce a `approved`/`rejected`/`cancelled`, que es exactamente lo que el
hold con TTL necesita. La transición del dominio se dispara **sólo** desde estos estados
leídos por API, nunca desde el redirect.

## 6. Reembolsos

`POST /v1/payments/{id}/refunds` — total (body vacío) o parcial (`amount`). Sirve para:

- la **cancelación con ventana** ya prevista en §9.5, y
- el caso borde del pago que llega sobre un hold ya expirado.

Los reembolsos son un movimiento más: en el modelo append-only del módulo `finance` entran
como contra-asiento, no como edición del cobro.

## 7. SDK y ubicación en la solución

- SDK oficial: paquete NuGet **`mercadopago-sdk`** ([mercadopago/sdk-dotnet](https://github.com/mercadopago/sdk-dotnet)),
  .NET 8+ / .NET Standard 2.1+, mantenido activamente. Configuración mínima:
  `MercadoPagoConfig.AccessToken = "..."`.
- **La dependencia va en un proyecto propio de Infrastructure** —
  `ClubSpot.Infrastructure.MercadoPago`—, no dentro de `ClubSpot.Infrastructure` (decisión del
  usuario, 17/08/2026, regla general para todo vendor). Ese proyecto implementa el puerto de
  pagos que declara Application y se cablea por DI en la Api. `ClubSpot.Infrastructure` no
  referencia el SDK; el dominio no sabe que MercadoPago existe.
- El access token es un secreto: en dev va por user-secrets o variable de entorno, nunca en
  `appsettings.json` commiteado. La public key no se necesita en Checkout Pro server-side.

## 8. Checklist mínimo — sandbox y credenciales de prueba

Lo que hay que hacer, en orden, para poder desarrollar y jugar con credenciales de testing:

1. **Cuenta real de MercadoPago** (sirve una personal) y entrar a
   [Tus integraciones](https://www.mercadopago.com.ar/developers/panel/app) → **crear una
   aplicación** (elegir "Pagos online" / Checkout Pro).
2. **Crear 2 cuentas de prueba** desde la aplicación → sección *Cuentas de prueba*:
   una **Vendedor** y una **Comprador**, país **Argentina** (no se puede cambiar después),
   con saldo ficticio. Límite: 15 cuentas, y no se pueden borrar — nombrarlas bien.
3. **Obtener las credenciales de prueba**: iniciar sesión en MercadoPago **con la cuenta
   vendedor de prueba** (ventana de incógnito), entrar a su propio panel de desarrolladores,
   crear una aplicación ahí y copiar su **Access Token** (y public key, por si después se usa
   Bricks). Ese token es el que usa la API de ClubSpot en dev.
4. **Configurar el webhook** en esa aplicación de prueba: URL del endpoint + copiar la **clave
   secreta** para validar `x-signature`. Probar con el **simulador** del panel.
5. **Túnel público para el webhook en local**: MercadoPago no llega a `localhost`; usar
   `ngrok`/`cloudflared` apuntando al `:5037`, o desarrollar el handler contra el simulador y
   payloads guardados.
6. **Pagar con las tarjetas de prueba** (Argentina, cualquiera con vencimiento `11/30`):

   | Tarjeta | Número | CVV |
   |---|---|---|
   | Mastercard crédito | 5031 7557 3453 0604 | 123 |
   | Visa crédito | 4509 9535 6623 3704 | 123 |
   | Amex | 3711 803032 57522 | 1234 |
   | Mastercard débito | 5287 3383 1025 3304 | 123 |
   | Visa débito | 4002 7686 9439 5619 | 123 |

   El **resultado se elige con el nombre del titular**: `APRO` aprueba, `OTHE` rechaza,
   `CONT` deja pendiente, `FUND` fondos insuficientes, `SECU` CVV inválido, `EXPI` vencimiento
   inválido, `CALL` rechazo con validación, `FORM` error de formulario (hay más en la doc).
   DNI de prueba: `12345678`. El comprador es la **cuenta comprador de prueba**, logueada en
   el checkout.
7. **Instalar el SDK** en el proyecto nuevo `ClubSpot.Infrastructure.MercadoPago`
   (`dotnet add package mercadopago-sdk`) con el token por user-secrets.

Con eso se recorre el ciclo completo — preferencia → checkout → webhook → consulta del pago →
reembolso — sin plata real.

## 9. Preguntas abiertas para decidir con el usuario

- ✅ **Resuelto (17/08/2026): los medios offline no van por MercadoPago.** El efectivo existe
  sólo como asiento del canchero en el backoffice; el checkout online cobra únicamente medios
  que resuelven al instante. En la preferencia: `binary_mode: true` **y**
  `payment_methods.excluded_payment_types: [{ "id": "ticket" }]` (Rapipago/Pago Fácil), para
  que el medio ni se ofrezca en vez de rechazarse después.
- **Comisiones y plazos de acreditación**: no se relevaron números — dependen del acuerdo de
  la cuenta del club y varían; consultarlos en la cuenta real antes de prometer nada.
- **Seña vs pago total** del turno: la preferencia cobra un monto; cuál es ese monto es una
  regla de tarifas (§9.5), no del gateway.
- El **contrato del puerto de pagos** (qué expone Application) se diseña en el plan de
  implementación, no acá.

---

### Fuentes

- [Cuentas de prueba — Mercado Pago Developers](https://www.mercadopago.com.ar/developers/es/docs/your-integrations/test/accounts)
- [Tarjetas de prueba — Mercado Pago Developers](https://www.mercadopago.com.ar/developers/es/docs/checkout-pro/additional-content/your-integrations/test/cards)
- [Webhooks — Mercado Pago Developers](https://www.mercadopago.com.ar/developers/es/docs/your-integrations/notifications/webhooks)
- [Vigencia de preferencias (expires / expiration_date_to)](https://www.mercadopago.com.br/developers/en/docs/checkout-pro/additional-settings/expiration-date)
- [Referencia de preferencias (binary_mode, external_reference, auto_return)](https://www.mercadopago.com.ar/developers/en/reference/preferences/_checkout_preferences_id/put)
- [Consulta de pagos `GET /v1/payments/{id}`](https://www.mercadopago.com.ar/developers/en/docs/wallet-connect/payment-flow/get-payment-information)
- [SDK oficial .NET — mercadopago/sdk-dotnet](https://github.com/mercadopago/sdk-dotnet)
- [Simulador de webhooks y firma secreta (anuncio)](https://www.mercadopago.com.pe/developers/en/news/2024/01/11/Webhooks-Notifications-Simulator-and-Secret-Signature)
