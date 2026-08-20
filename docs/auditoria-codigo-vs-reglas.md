# Auditoría — el código contra sus propias reglas

Cruce de lo que dicen los 18 ADR y las convenciones de AGENTS.md §6 contra lo que hace el código.
La entrada más nueva arriba.

## 20/08/2026 — J2 nunca pudo aplicar un pago, y el portal no contaba nada

Salió de un caso real, no de una revisión: el usuario pagó una seña por Mercado Pago, el túnel de
ngrok estaba caído, el webhook no llegó y el hold venció. El portal mostró *"El pago no se acreditó,
si te cobraron comunicate con el club"* con $7.000 ya cobrados.

### El bug: J2 se caía justo cuando había plata que recuperar

`PaymentsReconciliationDispatcher.RunForTenantAsync` abría el ámbito de **tenant** pero nunca el de
**actor**, y el registro de actividad se niega a escribir sin saber quién actúa —a propósito—. Así
que en cuanto la conciliación encontraba un pago para aplicar, `ActivityLog.Record` lanzaba
`MissingActivityActorException` y se perdía la corrida entera del club.

**Nunca se había notado porque sólo falla cuando hay algo que aplicar.** Las corridas anteriores
decían `2 candidates, 0 applied, 0 orphaned`: encontraban candidatos, el proveedor no devolvía
pagos, y nunca se llegaba a escribir en el registro. La red de seguridad de la plata estaba rota y
se veía sana.

`ActivityActor.Job(...)` existía exactamente para esto. Arreglado, la conciliación recuperó el pago
real: reserva **confirmada**, `174843380880`, $7.000, `source = Reconciliation`.

### Lo que el caso dejó a la vista

- **No hay crónica del vencimiento.** El registro de esa reserva tenía dos entradas —`holdCreated`,
  `checkoutIssued`— y nada más. El vencimiento es perezoso, así que "el turno se liberó" lo
  **infiere la pantalla** comparando `expiresAt` con el reloj: no hay ningún hecho registrado.
  Consecuencia conocida de haber descartado J1; queda anotado, no resuelto.
- **La pantalla de retorno deja de preguntar.** `ReturnScreen` llama a `settle` cada 5 s mientras la
  reserva está pendiente, pero en cuanto el hold vence corta el poll. Una llamada más habría traído
  el pago. Sin resolver.
- **`dev-up.ps1` no levantaba ngrok**, y ni el script ni AGENTS.md decían que el JobService fuera
  obligatorio. Los dos corregidos: ahora son **cinco ventanas** y las dos cosas están escritas con
  su porqué.

### El portal ahora cuenta lo que pasó

`BookingSnapshot` suma `createdAt` y `payments[]`: cada intento que el proveedor reportó —los
rechazados también— con fecha, medio, número de operación, concepto (seña/total/saldo), moneda y
estado. Nada calculado en la pantalla, nada inventado.

- **Mis reservas** dejó de ser una foto de `localStorage` y consulta el estado real de cada reserva
  con su token. Cada fila lleva el chip de estado y **"Reservada el 20 ago, 19:10"**.
- **Detalle de la reserva**, nuevo: total, pagado, saldo, y la lista de movimientos.
- Los montos y el número de operación son los que devuelve el servidor. El motivo por el que un pago
  quedó huérfano **no se expone**: eso es conversación del club, no del cliente.

Test: `The_booking_carries_when_it_was_made_and_every_payment_attempt`. Al sumar `PaymentKind` y
`PaymentStatus` al contrato hubo que registrarlos en `TestJsonOptions` —10 tests se rompieron por
eso hasta hacerlo, que es la señal de que un enum nuevo viaja de verdad—.

**184 tests verdes** (92 + 92), build sin warnings, clientes regenerados, `tsc` limpio, y verificado
en el navegador contra la reserva real.

---

## 20/08/2026 — Merge del PR #4: no compilaba y tenía un bug crítico

El PR de seguridad e idempotencia se fusionó **directo en `main`** (`aa9001f`), no por GitHub. Antes
se verificó en un worktree aparte, y bien que sirvió.

### No compilaba

Su descripción avisaba que nunca se había compilado ni testeado. Era cierto, y fallaba por dos cosas:

- `PaymentEndpoints.cs` usaba `CheckoutReturnUrl` sin `using ClubSpot.Api.Payments`.
- `Program.cs` usaba `ForwardedHeadersOptions.KnownNetworks`, obsoleta en .NET 10 → con
  `TreatWarningsAsErrors` es un error, no un warning. Ahora es `KnownIPNetworks`.

### La migración a mano: verificada de tres maneras

`20260820050000_SecurityHardening`, su `.Designer.cs` y el snapshot estaban escritos a mano, sin
`dotnet ef`. Era el riesgo más alto del PR y salió bien:

1. **Migración fantasma vacía.** Un `dotnet ef migrations add` sobre el modelo fusionado generó una
   migración **sin contenido**: el snapshot a mano coincide con el modelo real. Es la prueba
   definitiva de que un snapshot escrito a mano no miente.
2. **Se aplica sobre datos reales.** Corrida contra la base de desarrollo con 33 reservas y 14
   personas: restricción renombrada a `exBookingsTenantIdCourtIdDateSlot` con `tenantId` adentro,
   `ixPeopleTenantIdEmail` creado, cero filas perdidas.
3. **La restricción sigue bloqueando.** Un `INSERT` solapado fue rechazado por PostgreSQL con el
   nombre nuevo (probado en transacción y revertido).

### El bug crítico que los tests del PR no veían

Una **segunda notificación `pending` del mismo pago devolvía HTTP 500.** `Payment.Accepts(Pending)`
da `true`, así que no cortaba por `AlreadyProcessed`, y caía en `Settle(..., PaymentStatus.Pending)`
— que lanza a propósito. La guarda que contempla el caso estaba **20 líneas más abajo**.

No es un caso de borde: **es el caso normal**. Mercado Pago re-notifica un pago offline (Rapipago,
Pago Fácil) mientras sigue sin pagarse, y reintenta hasta recibir un 2xx — o sea, loop de 500. J2 lo
encuentra de nuevo cada 5 minutos y la excepción **aborta el lote entero del club**. Y la pantalla
de retorno del portal llama a `settle` cada 5 segundos.

Reproducido en vivo (`200` la primera, `500` la segunda) antes de tocar nada. Arreglo: devolver
`Pending` antes de llegar a `Settle` — y **no** `AlreadyProcessed`, para que quien busca el pago que
sí liquidó siga buscando. Test: `A_payment_that_stays_undecided_can_be_reported_again`, comprobado
que falla contra el código anterior con `Expected: OK / Actual: InternalServerError`.

### Dos más, encontrados en la misma revisión

- **El `FOR UPDATE` no alcanzaba.** EF responde una consulta con la instancia que ya trackea, no con
  la fila recién leída, y J2 recorre muchos pagos dentro de un mismo ámbito: la decisión podía
  tomarse contra un estado que el lock existía justamente para congelar. Se agregó
  `db.ChangeTracker.Clear()` al entrar. (La versión que el revisor afirmaba —dos webhooks
  concurrentes— **no** estaba rota: cada request tiene su propio `DbContext`.)
- **`SignInThrottle` perdía cuentas.** `TryGetValue` + `Set` es leer-y-escribir: bajo ráfaga, que es
  cuando el contador importa, se pisan los incrementos. Ahora la entrada guarda un contador mutable
  y el incremento es `Interlocked`.

**Queda anotado a propósito:** entre `IsBlocked` y `RecordFailure` está el hash de la contraseña, así
que una ráfaga concurrente pasa el chequeo antes de que ninguna registre su fallo y consigue más
intentos que el límite en la primera andanada. Es inherente a contar sólo fracasos; el costo del
hash lo acota y el bloqueo de 15 minutos cae igual. Si alguna vez molesta, se arregla registrando el
intento en vuelo y borrándolo al acertar.

### Verificado corriendo

Fuerza bruta: 10 fallos → `429`, y **otro usuario desde la misma IP entra normal** · `returnUrl`
ajeno al crear → `422`; `/api/payments/return` a destino ajeno → `400`, a destino permitido → `302`
· JobService arranca y J2 concilia con los tipos nuevos · tres `pending` seguidos → `200 200 200`
(antes `200 500 500`).

**183 tests verdes** (92 unitarios + 91 de integración), build sin warnings, contrato regenerado y
`tsc` limpio en los dos frontends.

⚠️ **La base de desarrollo ya tiene la migración aplicada**, a propósito, para probar el camino de
actualización.

---

## 20/08/2026 — Superficie de despliegue: cuatro puntos de la sección 9

Distinto tema del de abajo: no es el código contra sus reglas, es el código contra
[`infraestructura-mvp.html` §9](infraestructura-mvp.html#s9), que lista lo que falta para
desplegar. Se verificaron sus **seis bloqueantes: los seis siguen en pie**, con las líneas
corridas porque el documento es anterior al login. Se cerraron cuatro puntos —uno de esa lista y
tres que no estaban en ella— porque son código y no decisión de negocio.

| | Qué | Estado |
|---|---|---|
| CORS | Sale de `Cors:AllowedOrigins`. Development cae en los puertos locales; **Production sin la clave no arranca** | ✅ |
| `/health` | 204 si el proceso vive. **No toca la base a propósito**: un hipo de PostgreSQL no puede hacer que el orquestador mate un contenedor sano | ✅ |
| `/health/ready` | 204 si además la base contesta, 503 si no. Es lo que evita que un deploy con la cadena de conexión mal quede en verde mientras todo responde 500 | ✅ |
| `MapOpenApi` | Apagado en Production. El contrato es salida del build (ADR-0016); servirlo en producción publica el mapa entero de la API, rutas de pago incluidas | ✅ |
| `Hello World!` en `/` | Eliminado | ✅ |

**Sobre `/health/ready` y ADR-0005:** la sonda **no toca el `DbContext`**. Va por un puerto nuevo,
`IDatabaseProbe`, implementado en Infrastructure. La regla de que la Api no usa EF directamente vale
también para una sonda de dos líneas.

**Verificado corriendo la API, no sólo con tests.** En Development: `/health` 204, `/health/ready`
204, `/` 404, `/openapi/v1.json` 200, preflight de `localhost:5184` con `Allow-Origin` y el de un
origen ajeno sin él; login del canchero, token con 11:59:59 de vida, `/api/agenda` 200,
`/api/courts` 403, portal anónimo 200. En Production: `/openapi/v1.json` y `/dev/checkout` 404,
`localhost` ya no pasa CORS, y **sin `Cors:AllowedOrigins` la API muere al arrancar** con el mensaje
esperado. Con la base apuntada a un puerto muerto: `/health` 204 y `/health/ready` **503**.

Cinco tests nuevos en `DeploymentSurfaceTests`; 82 unitarios + 81 de integración verdes, build sin
warnings, contrato OpenAPI sin cambios.

**Lo que sigue faltando de la sección 9**, porque necesita decisiones y no código: los dos
Dockerfiles, quién corre las migraciones en producción, el `CREATE DATABASE` del JobService, los
secretos por variable de entorno, y el build por entorno de los frontends —que son **dos**
variables, no una: el portal usa además `VITE_CLUB_SLUG`—. Y dos que la sección no lista:
`UseForwardedHeaders` (viene en PR #4, y sin él el throttle de login cuenta todo contra Caddy) y
que **PR #4 sigue sin mergear**.

---

## 20/08/2026 — Primera pasada sobre `main` (`9733fdb`)

**Qué se auditó:** las reglas **mecánicamente verificables** — las que se pueden chequear con una
búsqueda sobre el árbol, sin ejecutar nada. Eso cubre 5 de los 18 ADR (0005, 0006, 0010, 0011,
0016) más las convenciones de AGENTS.md §6. Los otros 13 son de dominio y no se auditan así: se
auditan con tests (ver *lo que queda abierto*).

### Lo que estaba mal, y se arregló

| | Regla | Qué pasaba | Arreglo |
|---|---|---|---|
| 1 | *Nunca `DateTime.Now`: se inyecta `IClock`* | `JwtIssuer` fijaba el vencimiento del token con `DateTime.UtcNow`. La vida de la sesión es una decisión de diseño (ADR-0018) y **no se podía testear con reloj falso** | `IClock` inyectado; las 12 horas pasaron a la constante `Lifetime` |
| 2 | idem | `DevSeeder` usaba `DateTimeOffset.UtcNow` en 4 lugares | un solo `clock.UtcNow` al entrar, reusado en los 4 |

Ambos verificados: **build sin warnings, 82 unitarios + 76 de integración verdes** (antes 75).

El test que respalda el arreglo 1 es
`The_token_expires_twelve_hours_after_the_clock_says_it_was_issued`. Se comprobó que **falla contra
el código anterior** —`Expected 2026-03-04T21:15:00Z, Actual 2026-08-21T08:32:38Z`— y pasa contra el
nuevo. Un arreglo sin esa comprobación no está respaldado por nada.

Detalle lateral que vale anotar: al intentar reproducir la regresión dejando el parámetro `clock`
sin usar, `TreatWarningsAsErrors` la convirtió en **error de compilación** (`CS9113`). La
convención se defiende sola una vez que la dependencia está inyectada.

### Lo que parecía mal y no lo estaba

Se anota con su razón para que nadie lo "arregle" en la próxima pasada. **Los tres eran hallazgos
propios de esta auditoría, descartados al verificarlos contra el código.**

- **Los 10 `decimal` para plata de la capa Application.** No son descuido. Son tres bordes
  legítimos —lo que reporta un proveedor externo, las agregaciones en SQL, y los DTO de respuesta—
  y colapsarlos a `Money` **empeoraría el sistema**: haría indetectable `wrongCurrency` y obligaría
  a los adaptadores a inventar una moneda. La regla de AGENTS.md §6 estaba escrita más absoluta de
  lo que el código puede honrar; se corrigió **la regla**, no el código.
- **Los dos endpoints "sin contrato declarado"** (`/api/payments/return` y `/dev/checkout`). Los
  dos llevan `ExcludeFromDescription()`, que **es** la declaración explícita de que no van al
  contrato, más un comentario que explica por qué. Se verificó además que ningún frontend los llama
  a mano, que sería la violación real de ADR-0016.
- **Los textos en español del backend.** Son datos de seed (`"Cancha 1"`, `"Canchero"`) y nombres
  comerciales de módulo (`"Membresías…"`), permitidos por ADR-0006.

### Lo que está limpio

Sin desvíos en: fronteras de módulo (`Bookings` sólo toca `Core`) · cero `HasDatabaseName` /
`HasConstraintName` en las configuraciones · la Api no usa `DbContext` fuera del arranque y el seed
· los 5 enums del contrato viajan en camelCase, todos con converter registrado ·
`ITenantContext.Current` lanza sin tenant, con el comentario que explica por qué · check constraint
`ckClubsDepositPercent IN (50, 100)` · una sola cadena de 11 migraciones en orden ·
`people.debtAmount` marcada como provisional citando ADR-0012 · `Directory.Build.props` con
`nullable`, `TreatWarningsAsErrors` e `InvariantGlobalization=false` · frontend: cero `fetch` fuera
del mutator, cero glifos de texto como íconos, el cliente generado en su lugar.

### Lo que queda abierto

**No se sabe cuáles de los 13 ADR de dominio tienen un test que los cuida.** Un ADR cuya regla no
tiene test es una regla que nadie hace cumplir: el documento no se entera si alguien la rompe. Los
152 tests existen y sus nombres son frases (`The_most_specific_override_wins`), así que el cruce es
posible; falta hacerlo.

Ése es el chequeo que atrapa las vueltas en círculo, y es el próximo paso natural de este
documento.
