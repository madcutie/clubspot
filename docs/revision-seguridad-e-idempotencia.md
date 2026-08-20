# Revisión de seguridad, idempotencia y consistencia del dinero

Fecha: 20/08/2026 · Alcance: backend (`src/backend/`), con foco en reservas y pagos.

Es un relevamiento. Cada hallazgo lleva la ruta y la línea donde se ve, qué pasa hoy, y qué haría
falta para cerrarlo.

## Estado (20/08/2026)

Las rutas y líneas de abajo son las del momento del relevamiento y **no se actualizaron**: sirven
para entender el hallazgo, no para navegar el código de hoy.

| | Estado |
|---|---|
| §1 webhook pendiente quema la clave | ✅ cerrado — `PaymentStatus.Pending` y transición en la misma fila |
| §2 `returnUrl` esquiva la lista blanca | ✅ cerrado — validado donde entra el dato, y el arranque falla si no está configurada |
| §3 login sin límite de intentos | ✅ cerrado — `SignInThrottle`, cuenta sólo fracasos. El side-channel de tiempo ya lo había cerrado el PR #3 |
| §4 pagos concurrentes no detectados | ✅ cerrado — transacción + `SELECT … FOR UPDATE` sobre la reserva |
| §5 identidad por mail sin verificar | 🚧 parcial — se agregó el índice; **qué significa "es la misma persona" sigue siendo decisión del usuario** |
| §6 cancelar una reserva paga | 🚧 parcial — la plata queda asentada en el registro de actividad; **devolverla necesita el modelo de reembolsos** |
| 7.1 `IsBlocked` no se consulta al reservar | ⬜ abierto — qué implica "bloqueado" no está definido en ningún ADR; es pregunta para el usuario |
| 7.2 `expected` usa siempre la seña | ✅ cerrado — se calcula según el `kind` del pago |
| 7.3 token del portal sin vencimiento | ✅ cerrado — firma el momento de emisión, vence a los 30 días |
| 7.4 ámbito de tenant y el `IResult` | ✅ cerrado — el resultado se ejecuta dentro del ámbito |
| 7.5 `RegisterPersonPayment` sin asiento | ⬜ abierto — entra al plan de finanzas (ADR-0012) |
| 7.6 exclusión sin `tenantId` | ✅ cerrado — migración `SecurityHardening` |
| 7.7 sin `UseForwardedHeaders` | ✅ cerrado — configurable por `Network:TrustedProxies`, nunca asumido |

Se solapa con [`plan-reglas-de-plata-huerfana.md`](plan-reglas-de-plata-huerfana.md), que sigue
**esperando decisiones**: sus puntos A (liberar deja `Expired`), B (congelar lo acordado en la
reserva) y D (reusar el link vivo) **no se tocaron acá** a propósito.

### Configuración que hace falta al desplegar

- `Payments:AllowedReturnOrigins` tiene que incluir el origen real del portal. Con un proveedor de
  pagos configurado y la lista vacía, la API **no arranca**: es deliberado, porque la alternativa
  era que la reserva online devolviera 422 en silencio.
- `Network:TrustedProxies` (opcional): las direcciones del proxy reverso. Sin esto no se leen los
  encabezados `X-Forwarded-*`, que es lo correcto cuando no hay proxy.

Lo que **no** se encontró, y conviene decirlo primero porque es lo que más suele fallar:

- **La sobreventa del turno está bien resuelta.** El punto de serialización real es el
  `EXCLUDE USING gist` sobre `(courtId, date, rango de minutos)` con
  `WHERE status IN ('Confirmed','PendingPayment')`
  ([`20260817215448_OnlinePayments.cs:78`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Persistence/Migrations/20260817215448_OnlinePayments.cs)),
  no la consulta previa de `taken`. Dos reservas concurrentes sobre el mismo turno terminan en
  `409`, no en dos filas. La expiración perezosa acotada a `(cancha, fecha)` mantiene coherente
  lo que la restricción ve con lo que la disponibilidad muestra.
- **La reposición de un webhook no duplica plata**, por el índice único
  `(provider, externalId)` ([`PaymentConfiguration.cs:28`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Persistence/Configurations/PaymentConfiguration.cs)).
- **El multi-tenancy no filtra por descuido**: el filtro global más la guarda de escritura en
  `ClubSpotDbContext` cubren todas las entidades `ITenantOwned`, y `clubs` está documentada como
  la única excepción.
- La firma del webhook de Mercado Pago valida manifiesto **y frescura del `ts`**, y el redirect
  de `/api/payments/return` compara orígenes **parseados**, no por prefijo de string.

---

## 1. Un webhook "pendiente" quema la clave de idempotencia y deja el pago sin aplicar

**Severidad: alta — plata cobrada con la reserva sin confirmar.**

`MercadoPagoProvider.GetPaymentAsync` colapsa **todo estado que no sea `approved` en
`Approved = false`**
([`MercadoPagoProvider.cs:77-84`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure.MercadoPago/MercadoPagoProvider.cs)),
y `ApplyPaymentCoreAsync` persiste eso como una fila `Payment` con estado `Rejected`
([`BookingsStore.cs:182`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Repositories/BookingsStore.cs)).
Esa fila ocupa el par `(provider, externalId)`, que es exactamente el ancla de idempotencia que
se chequea al entrar ([`BookingsStore.cs:154`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Repositories/BookingsStore.cs)).

Secuencia:

1. Mercado Pago dispara `payment.created`. El pago todavía está `pending` (es el caso normal de
   los medios offline —Rapipago, Pago Fácil—, que `binary_mode` no convierte en binarios; también
   se ve como ventana breve en tarjeta).
2. Se graba `Payment(provider = "mercadopago", externalId = 123, status = Rejected)`.
3. El cliente paga. Llega `payment.updated` con `approved` y el **mismo `externalId` 123**.
4. `ApplyPaymentCoreAsync` encuentra la fila y devuelve `AlreadyProcessed`. **La reserva nunca se
   confirma, el turno se libera al vencer el hold, y la plata está cobrada.**

Lo peor es que **la conciliación no lo repara**: J2 y el `settle` del portal pasan por el mismo
`ApplyPaymentAsync`, así que también reciben `AlreadyProcessed`
([`ReconcileOnlinePaymentsHandler.cs`](../src/backend/src/Core/ClubSpot.Application/Bookings/ReconcileOnlinePaymentsHandler.cs),
[`SettleBookingHandler.cs`](../src/backend/src/Core/ClubSpot.Application/Bookings/SettleBookingHandler.cs)).
No hay ninguna red debajo.

El test que existe no lo cubre: `A_rejected_payment_keeps_the_hold_pending`
([`PaymentFlowTests.cs:81`](../src/backend/src/tests/ClubSpot.IntegrationTests/Bookings/PaymentFlowTests.cs))
usa un `externalId` que después nunca se aprueba.

**Qué haría falta.** Que "no aprobado todavía" y "rechazado definitivamente" dejen de ser el mismo
hecho. Dos caminos, no excluyentes:

- Que el proveedor devuelva el estado real y que un pago no terminal **no escriba fila** (o escriba
  una en un estado `Pending` que la lógica sepa reemplazar).
- Que la idempotencia se ancle sobre `(provider, externalId)` pero permita la **transición**
  `Pending/Rejected → Approved` sobre la misma fila, en vez de tratar la existencia de la fila como
  respuesta final.

---

## 2. El `returnUrl` del portal es del que llama, y esquiva la lista blanca

**Severidad: alta — redirección abierta post-pago, sobre un endpoint anónimo.**

`POST /api/portal/{clubSlug}/bookings` recibe `ReturnUrl` del cuerpo y sólo verifica que **no esté
vacío** ([`PortalEndpoints.cs:51`](../src/backend/src/api/ClubSpot.Api/Endpoints/PortalEndpoints.cs)).
Ese valor viaja a `CheckoutReturnUrl.For`
([`PortalEndpoints.cs:73`](../src/backend/src/api/ClubSpot.Api/Endpoints/PortalEndpoints.cs)) y
termina en los tres `back_urls` de la preferencia de Mercado Pago
([`MercadoPagoProvider.cs:48-50`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure.MercadoPago/MercadoPagoProvider.cs)).

`PaymentsOptions.AllowedReturnOrigins` existe y está bien usada, pero **sólo la aplica
`/api/payments/return`** ([`PaymentEndpoints.cs:40`](../src/backend/src/api/ClubSpot.Api/Endpoints/PaymentEndpoints.cs)),
que es el rebote de desarrollo. Y `CheckoutReturnUrl.For` sólo rebota cuando la URL **no** empieza
con `https` ([`CheckoutReturnUrl.cs`](../src/backend/src/api/ClubSpot.Api/Payments/CheckoutReturnUrl.cs)):
un `returnUrl` que ya sea `https://…` salta el rebote por completo. Peor, ese mismo `https` es lo
que activa `AutoReturn = "approved"`
([`MercadoPagoProvider.cs:54`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure.MercadoPago/MercadoPagoProvider.cs)),
así que el comprador queda **redirigido automáticamente** al destino elegido por quien creó la
reserva, con la pantalla de "pago aprobado" de Mercado Pago como antesala. Es el escenario clásico
de phishing de comprobante.

**Qué haría falta.** Validar `ReturnUrl` contra `AllowedReturnOrigins` **en el endpoint que lo
acepta**, con la misma comparación parseada que ya usa `Return`, y rechazar con `422` lo que no
esté en la lista. La lista blanca ya está escrita; falta aplicarla donde entra el dato.

---

## 3. `/api/auth/session` no tiene límite de intentos

**Severidad: alta — fuerza bruta de contraseñas sin fricción.**

El login es anónimo y **no lleva `RequireRateLimiting`**
([`AuthEndpoints.cs:13`](../src/backend/src/api/ClubSpot.Api/Endpoints/AuthEndpoints.cs)). El
limitador está configurado y funciona, pero sólo se aplica a los grupos del portal
([`Program.cs`](../src/backend/src/api/ClubSpot.Api/Program.cs)). No hay bloqueo por usuario, ni
retardo, ni registro de intentos fallidos en el `activityLog`. El endpoint además devuelve el
`clubSlug` como parte del intento, así que sirve para enumerar clubes.

Hay un detalle menor asociado: cuando el usuario no existe, `SignInAsync` corta **antes** de
verificar el hash, así que el tiempo de respuesta distingue "mail que existe" de "mail que no". Se
cierra verificando siempre contra un hash señuelo.

**Qué haría falta.** Una política de rate limit propia para el login, particionada por IP **y** por
`(club, email)`, y un asiento en el registro de actividad por intento fallido —que es justamente lo
que ADR-0017 quiere ver.

---

## 4. Dos pagos aprobados concurrentes sobre la misma reserva no se detectan

**Severidad: media-alta — plata cobrada de más sin quedar marcada.**

`ApplyPaymentCoreAsync` decide si un pago es duplicado leyendo `settled`, la suma de lo aprobado
hasta ese momento ([`BookingsStore.cs:166`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Repositories/BookingsStore.cs)),
y compara `settled >= booking.Price.Amount`. Esa lectura **no está serializada contra nada**:
`bookings` no tiene token de concurrencia (a diferencia de `courts` y `schedules`, que sí llevan
`xmin` — ver [`BookingConfiguration.cs`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Persistence/Configurations/BookingConfiguration.cs)
contra [`CourtConfiguration.cs:38`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Persistence/Configurations/CourtConfiguration.cs)).

Dos pagos aprobados que entren a la vez —con `externalId` distintos, así que el índice único no los
para— leen ambos `settled = 0`, ninguno se ve como duplicado, y los dos se graban `Approved`. La
reserva queda con el doble de lo que vale y **sin ninguna marca `ApprovedOrphan`** que le avise a
nadie. La detección secuencial de duplicados sí funciona y está testeada
(`A_second_payment_on_a_fully_paid_booking_is_orphaned`); lo que falla es la carrera.

No es teórico: `POST /api/bookings/{id}/checkout` está pensado para reemitirse
([`CreateBookingCheckoutHandler.cs`](../src/backend/src/Core/ClubSpot.Application/Bookings/CreateBookingCheckoutHandler.cs)),
y el plan de cobro en mostrador contempla mostrar el QR **y** mandar el link por WhatsApp. Dos
links vivos sobre el mismo saldo es el caso de uso, no el accidente.

**Qué haría falta.** Que el asiento del pago y la lectura del saldo compartan una serialización:
un token de concurrencia en `bookings` que la confirmación tenga que ganar, o un `SELECT … FOR
UPDATE` sobre la reserva al entrar a `ApplyPaymentCoreAsync`, o una restricción en base que impida
que la suma de aprobados supere el precio.

---

## 5. La identidad de la persona se toma del mail y el teléfono sin verificar

**Severidad: media hoy, alta cuando exista el login del socio.**

`EnsurePersonAsync` busca una persona existente por mail y, si no la encuentra, por dígitos del
teléfono, y devuelve la primera ([`PeopleLink.cs:22-38`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Repositories/PeopleLink.cs)).
El que reserva desde el portal es **anónimo**: escribe el mail que quiera. Escribir el mail de otro
socio ata la reserva a la ficha de esa persona. Hoy eso ensucia datos y filtra en el backoffice
(`GET /api/people/{id}/bookings` muestra la reserva ajena en la ficha de la víctima). Cuando llegue
la etapa 3 del plan de reserva online —el login— pasa a ser un camino de apropiación de cuenta si
la identidad sigue anclada al mismo dato no verificado.

Dos problemas de segundo orden en el mismo lugar:

- **No hay índice único sobre `people.email` ni sobre `people.phoneDigits`**
  ([`PersonConfiguration.cs:29-30`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Persistence/Configurations/PersonConfiguration.cs):
  los dos índices que hay no son únicos). El "buscar o crear" es entonces una carrera: dos reservas
  simultáneas con el mismo mail nuevo crean dos personas.
- **`email` no está indexado en absoluto.** La búsqueda por mail de un endpoint anónimo hace scan
  secuencial de `people`, que es la tabla que va a crecer con la migración del padrón.

**Qué haría falta.** Decidir con el usuario qué significa "es la misma persona" antes del login
(ADR pendiente). Como piso: índice sobre `email`, y unicidad —o una regla explícita de por qué no—
antes de que la migración del padrón cargue datos reales.

---

## 6. Cancelar una reserva paga no toca la plata

**Severidad: media — la plata queda registrada contra un turno que no existe.**

`CancelAsync` cambia el estado y nada más
([`BookingsStore.cs:100-110`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Repositories/BookingsStore.cs)).
Los pagos siguen `Approved`, así que `GetPaidAmountsAsync` los sigue contando como plata de esa
reserva, y ninguna pantalla dice que hay algo que devolver. Tampoco hay ventana de cancelación
—la parte 9.5 la lista como pendiente—, ni marca de quién canceló más allá del asiento del
`activityLog`.

Está emparentado con el hueco ya documentado en `MercadoPagoProvider`: un reembolso o un contracargo
llegan como estado no-`approved` y hoy se registran como rechazo, dejando la reserva confirmada.
Ambos son la misma deuda: **los reembolsos no están modelados**. Vale la pena que el plan de
finanzas lo tome explícito y no como consecuencia.

---

## 7. Hallazgos menores, ordenados

| | Dónde | Qué |
|---|---|---|
| 7.1 | [`BookingsStore.cs`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Repositories/BookingsStore.cs) `CreateAsync` | No se consulta `Person.IsBlocked`. Una persona bloqueada reserva por el portal sin obstáculo. La habilitación como contrato está en 9.3, pero el bloqueo ya existe y no se usa |
| 7.2 | [`BookingsStore.cs:160-170`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Repositories/BookingsStore.cs) | `expected` se calcula siempre con `ChargeAmountFor(PaymentMode, …)`, que devuelve la **seña**, aun cuando el pago se clasificó como `Balance`. Con seña de 50 % los dos números coinciden por casualidad y el bug queda tapado; con 100 % o con cualquier otro esquema futuro, no |
| 7.3 | [`PortalBookingToken.cs`](../src/backend/src/api/ClubSpot.Api/Endpoints/PortalBookingToken.cs) | El token es `HMAC(clave, id)` puro: **no vence nunca y no se puede revocar**. Si se filtra (historial del navegador, log de un proxy, captura de pantalla del comprobante) sirve para siempre. Alcanza con meter el `expiresAt` de la reserva dentro del manifiesto firmado |
| 7.4 | [`ClubScope.cs`](../src/backend/src/api/ClubSpot.Api/Endpoints/ClubScope.cs) | El `using` del ámbito de tenant se cierra cuando el filtro devuelve, pero el `IResult` se **ejecuta después**. Hoy es inofensivo porque todos los resultados son DTO ya materializados; deja de serlo el día que uno serialice algo perezoso, y va a fallar como `MissingTenantException` lejos de la causa |
| 7.5 | [`PeopleEndpoints.cs`](../src/backend/src/api/ClubSpot.Api/Endpoints/PeopleEndpoints.cs) `RegisterPaymentAsync` | `POST /api/people/{id}/payments` no recibe monto, no lleva clave de idempotencia y borra la deuda sin contra-asiento (`Person.RegisterPayment`). Ya está marcado como provisional en el código; se anota acá para que entre al plan de finanzas y no se olvide |
| 7.6 | [`20260817215448_OnlinePayments.cs`](../src/backend/src/Infrastructure/ClubSpot.Infrastructure/Persistence/Migrations/20260817215448_OnlinePayments.cs) | La restricción de exclusión **no incluye `tenantId`**. Hoy no es explotable porque `courtId` es un `Guid` y no se comparte entre clubes, pero la invariante queda apoyada en eso en vez de estar escrita |
| 7.7 | [`Program.cs`](../src/backend/src/api/ClubSpot.Api/Program.cs) | El rate limit del portal particiona por `RemoteIpAddress`, que es lo correcto, pero **no hay `UseForwardedHeaders`** configurado. Detrás de un proxy real todas las peticiones caen en la misma partición y el límite se vuelve global por club. El comentario del código ya anticipa el arreglo; falta hacerlo antes de publicar |

---

## Orden sugerido para atacarlo

1. **§1** y **§4** — son las dos formas en que hoy se puede perder o duplicar plata.
2. **§2** y **§3** — superficie anónima expuesta, arreglo acotado y sin decisiones de diseño.
3. **§5** — necesita una decisión del usuario sobre identidad; conviene tomarla antes de la etapa 3
   del plan de reserva online y antes de migrar el padrón.
4. **§6** y **7.5** — entran naturalmente al plan de finanzas cuando se defina la granularidad
   (ADR-0012).
5. El resto, oportunista.
