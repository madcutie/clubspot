# Research — qué falta para salir a producción

**Fecha:** 20/08/2026 · **Estado:** relevamiento, **no es un plan aprobado** · No propone
implementar nada: mide el código contra lo que hace falta el día que un club real lo use.

Origen: la pregunta del usuario —*"veo que estamos hardcodeando los nombres como Chaco For Ever
y son datos que deberían ir dentro del tenant"*—, ampliada a todo lo que separa al repo de un
sistema en línea.

Complementa a [`infraestructura-mvp.html`](infraestructura-mvp.html), que responde **dónde** se
despliega y **cuánto sale**. Este documento responde **qué hay que tocar y qué hay que decidir**.

> **Advertencia sobre la verificación.** Todo lo que se afirma acá se leyó del código y se cita
> con archivo y línea. Lo que **no** se pudo hacer en esta sesión es compilar y correr los tests:
> el entorno no tiene el SDK de .NET. Los conteos de tests verdes que se mencionan son los que
> registró la [auditoría](auditoria-codigo-vs-reglas.md) del 20/08, no una medición nueva.

---

## 1. La respuesta corta

Falta menos de lo que parece en el dominio y más de lo que parece alrededor. El backend hace bien
lo que hace: la identidad del club **ya es del tenant** y las dos pantallas la leen de la API.
Lo que no existe todavía es **el camino para que un club nuevo aparezca en una base vacía**, y
**todo lo que rodea a un sistema en línea**: imágenes, migraciones, secretos, logs, backups,
avisos al cliente y devoluciones.

Los tres bloqueantes que no están escritos en ninguna lista previa:

| | Bloqueante | Por qué no aparecía |
|---|---|---|
| 1 | **Nadie puede crear un club en producción** | `DevSeeder` es el único camino y sólo corre en `Development` |
| 2 | **Nadie puede crear un usuario ni cambiar una contraseña** | La única ruta de auth es `POST /api/auth/session` |
| 3 | **El cliente que reserva y paga no recibe ningún aviso** | No hay una sola línea de envío de mail o mensaje en el backend |

---

## 2. Los datos del club: qué ya es del tenant y qué sigue cableado

**`clubs` ya tiene la mayor parte de lo que hace falta.** La tabla existe, es la única fuera del
filtro de tenancy a propósito (`Club.cs:5`), y guarda seis datos:

| Columna | Qué resuelve | Quién la lee |
|---|---|---|
| `slug` | La URL pública del portal, `/api/portal/{clubSlug}/…` | `ClubDirectory`, `ClubScope` |
| `name` | El nombre que se muestra | backoffice (`/api/context`) y portal (`/catalog`) |
| `venue` | La sede, como texto libre | ídem |
| `timeZone` | Qué es "hoy" para el club | `ClubCalendar`, toda la agenda |
| `currency` | La moneda de cada `Money` | precios, pagos, checkout |
| `depositPercent` | La seña, 50 o 100, con check constraint | reserva online y cobro |

Y las dos UIs **ya la consumen**: el backoffice por `/api/context`
(`ContextEndpoints.cs:20-24` → `personasHttp.ts:78`) y el portal por
`/api/portal/{slug}/catalog` (`PortalHandlers.cs:41` → `HomeScreen.tsx:44-47`). El nombre del
club no está escrito en ninguna pantalla.

### 2.1 Lo que sí sigue cableado

| Qué | Dónde | Peso |
|---|---|---|
| Slug por defecto `'chaco-for-ever'` | `frontend/reservas/src/api/config.ts:2` | **Alto** — decide a qué club le reserva el portal |
| Título de la pestaña, "Chaco Forever Spot" | `frontend/reservas/index.html:7` | Medio — lo ve el cliente |
| Descripción del paquete | `frontend/reservas/package.json:6` | Cosmético |
| Club, GUID de tenant, usuarios, canchas y precios de fábrica | `DevSeeder.cs:21-80` | **Alto** — ver §3 |
| Contraseña `clubspot-dev` y emails `@chacoforever.test` | `DevSeeder.cs:46-66` | **Alto** — no pueden existir en producción |
| Paleta, tipografías, ausencia de logo y de favicon | `reservas/src/ui/theme.ts`, `backoffice/src/ui/theme.ts` | Decisión de marca, ver §2.3 |

El slug es el caso interesante: `VITE_CLUB_SLUG` se resuelve **en tiempo de build**
(`config.ts:2`), así que un portal compilado sirve a **un** club. Es correcto para salir con uno
solo, y es una pared el día que haya dos.

### 2.2 Lo que ni siquiera tiene dónde guardarse

Datos que un club real va a pedir el primer día y para los que **no hay columna**:

- **Contacto**: teléfono, WhatsApp, email, dirección de verdad (hoy `venue` es un texto de 120
  caracteres), redes, link al mapa.
- **Identidad visual**: logo y favicon. Hoy el portal no tiene ni siquiera un favicon propio.
- **Reglas comerciales**: ventana de cancelación, política de devolución, términos y condiciones.
- **Configuración de pago**: proveedor, credenciales, TTL del hold, URLs de retorno. Todo eso vive
  **por proceso**, no por club (`PaymentsOptions.cs`, `MercadoPagoOptions.cs`), con un único
  `Payments:MercadoPago:AccessToken` para toda la instalación.

### 2.3 La decisión que ordena todo esto

**¿"Live" es un club, o es el producto vendiéndose a varios?** No es la misma obra:

| | Un club | Varios clubes |
|---|---|---|
| Contacto, logo, políticas | Alcanza configuración del entorno | Columnas nuevas en `clubs` (o una tabla `clubSettings`) |
| Slug del portal | `VITE_CLUB_SLUG` en el build | Resolver por subdominio o por ruta |
| Mercado Pago | Un token en el entorno | **Credenciales por club** — cambio real: hoy el proveedor es un singleton |
| Marca | Puede ser la del club | Tiene que ser la de ClubSpot, con el nombre del club encima |

Mientras la respuesta sea "un club", casi nada de §2.2 es urgente. En cuanto sean dos, el
`AccessToken` por proceso deja de servir y hay que mudar la configuración de pago al tenant.

---

## 3. Provisión: el bloqueante que no estaba en ninguna lista

En una base de producción recién creada **no hay ningún club, ningún usuario y ninguna cancha**.
El único código que crea todo eso es `DevSeeder`, y `Program.cs:146-153` lo corre —junto con
`MigrateAsync`— **sólo en `Development`**. En producción, el primer arranque deja una base vacía
contra la que ni siquiera se puede iniciar sesión.

Lo que hace falta decidir y después escribir:

1. **Cómo nace un club**: un comando de provisión, un endpoint de plataforma protegido, o SQL
   documentado y ejecutado a mano. Cualquiera sirve; ninguna existe.
2. **Cómo nace el primer usuario** y con qué contraseña, sin que quede escrita en un archivo.
3. **Qué módulos se le contratan** (`clubModules`) — hoy lo decide el seeder.
4. **La carga inicial de canchas y horarios**: el backoffice ya las edita, pero alguien tiene que
   poder entrar para cargarlas, y para entrar hace falta el paso 2.

Detalle asociado: `DevSeeder.cs:26` usa un **GUID de tenant fijo**. Está bien para desarrollo
—hace reproducibles los datos— y no debe viajar a producción.

### 3.1 Usuarios: no hay alta, ni cambio, ni recuperación

La única ruta de autenticación es `POST /api/auth/session` (`AuthEndpoints.cs:11`). No existe
alta de usuario, cambio de contraseña, recuperación, desactivación ni asignación de roles por
API. El `User` tiene `IsActive` y roles, pero sólo se pueblan por seeder.

Para salir con un administrador y un canchero alcanza con crearlos en la provisión. Lo que **no**
tiene salida hoy es una contraseña olvidada: el único remedio es un `UPDATE` con el hash a mano.

---

## 4. Infraestructura: los seis bloqueantes, re-medidos hoy

[`infraestructura-mvp.html` §9](infraestructura-mvp.html) los listó el 19/08 y la
[auditoría](auditoria-codigo-vs-reglas.md) cerró varios el 20/08. Estado verificado en este
relevamiento:

| Punto | Estado | Evidencia |
|---|---|---|
| CORS por configuración | ✅ cerrado | `Program.cs:112-123`; sin `Cors:AllowedOrigins`, Production no arranca |
| `/health` y `/health/ready` | ✅ cerrado | `Program.cs:160-167` |
| OpenAPI apagado en Production | ✅ cerrado | `Program.cs:185` |
| `UseForwardedHeaders` | ✅ cerrado | `Program.cs:96-110, 156` |
| **Dockerfiles** | ⬜ no existen | Sólo `compose.yaml`, y sólo levanta PostgreSQL |
| **Migraciones en producción** | ⬜ nadie las corre | `Program.cs:146-153` es `IsDevelopment()` |
| **`CREATE DATABASE` del JobService** | ⬜ en pie | `Jobs/Program.cs:22, 57-70`: se conecta a `postgres` y crea `clubspot-hangfire`; varios PostgreSQL gestionados no lo permiten |
| **Secretos por variable de entorno** | ⬜ pendiente | El código ya lee de configuración; falta el proveedor y el `Jwt:SigningKey` real |
| **Un build de frontend por entorno** | ⬜ pendiente | Son **dos** variables: `VITE_API_URL` en ambos, más `VITE_CLUB_SLUG` en el portal |
| ICU en la imagen base | ⬜ al escribir el Dockerfile | `InvariantGlobalization=false` es a propósito; `aspnet:10.0` trae ICU, la variante `-alpine` no |

Agregados que aparecieron en este relevamiento:

- **Sin integración continua**: no hay `.github/workflows`. Nada compila ni corre los tests en un
  push, y el contrato OpenAPI —que es salida del build (ADR-0016)— puede quedar desfasado sin que
  nadie se entere.
- **El portal tiene `vercel.json` y el backoffice no**. Hay media decisión de hosting tomada para
  uno solo de los dos frontends.
- **Dominios y TLS**: sin dominio no se puede probar el webhook de Mercado Pago, que necesita una
  URL pública HTTPS. El informe de infraestructura lo marca como lo único con plazo de entrega
  real.
- **HTTPS también en local**, pedido explícito del usuario en `TODO.md`.

---

## 5. Plata: lo que más cuesta dejar para después

### 5.1 Devolver no existe

Cancelar un turno pagado **no mueve un peso**. El sistema lo dice en voz alta y no hace nada más:
la entrada del registro lleva `refundPending` (`BookingsStore.cs:113-127`) y el adaptador de
Mercado Pago lee `refunded` y `charged_back` **como rechazo**, marcado como provisional
(`MercadoPagoProvider.cs:113-125`). En la práctica: el club devuelve a mano por el panel de
Mercado Pago y el sistema nunca se entera.

Es una decisión de negocio antes que un plan técnico: **con plata real entrando, hay que definir
qué pasa cuando el club cancela un turno cobrado.** Además bloquea otras dos cosas ya
identificadas: la bandeja de revisión manual y la cancelación limpia de un turno pagado.

### 5.2 Las cuatro reglas de la plata huérfana

De [`plan-reglas-de-plata-huerfana.md`](plan-reglas-de-plata-huerfana.md), escrito y sin arrancar:

- **A** — liberar un hold deja `Cancelled` cuando debería dejar `Expired`; el que pagó justo ahí
  queda huérfano siempre. Arreglo de una línea, ya verificado.
- **B** — lo acordado se recalcula contra la configuración viva dentro del webhook. Cambiar la
  seña de 50 a 100 con pagos en vuelo marca como problema **pagos que estuvieron bien**. Es el
  único de los cinco motivos donde el sistema se equivoca.
- **C** — el TTL del hold es de 5 minutos en producción y 15 en los tests. **Decisión del
  usuario**, y en un checkout con validación del banco 5 puede ser corto.
- **D** — reemitir el link crea una preferencia nueva cada vez y deja dos links vivos.

De los cuatro, **B es el que no conviene llevar a producción**: fabrica huérfanos con pagos
correctos.

### 5.3 Cobro en mostrador

[`plan-cobro-en-mostrador.md`](plan-cobro-en-mostrador.md) está escrito y esperando aprobación; su
bitácora registra la F2 verificada en el navegador. Si el club va a cobrar en el mostrador desde
el día uno, esto entra en el alcance de salida; si no, no.

---

## 6. Lo que se nota el primer día de operación

| | Qué falta | Evidencia |
|---|---|---|
| **Avisos** | Nada. Ni mail ni WhatsApp automáticos: quien reserva online no recibe confirmación y el club no recibe alerta de reserva nueva | No hay `IEmailSender`, SMTP ni proveedor de mensajes en todo el backend; el outbox (J4) está en la lista y sin construir |
| **Turnos pasados en el portal** | Se muestran sin distinguir de los futuros | `TODO.md` |
| **Gating por módulo contratado** | El rol gatea la consola; el catálogo contratado no gatea nada | `backoffice/src/App.tsx:24-27`, dicho ahí mismo |
| **Ausencias** | No modeladas: `bookingNoShow` depende de un `BookingStatus` que no existe | AGENTS §10, plan de plata huérfana §5 |
| **Motivo al bloquear una ficha** | `personBlocked` / `personUnblocked` declarados y sin cablear | ídem |
| **Cobro contra una ficha** | `RegisterPayment()` pisa la deuda con cero y el monto no queda en ninguna tabla; marcado como provisional | `Person.cs` |
| **Accesibilidad y responsive** | Foco, teclado en la grilla, y nada por debajo de ~1000 px | AGENTS §10 |
| **Acciones que son sólo un aviso** | Bloquear horario, reprogramar, exportar, elegir archivo de importación | AGENTS §10 |

---

## 7. Operación: hoy no se puede ver qué está pasando

- **Logs**: ningún paquete de observabilidad en toda la solución —sin Serilog, sin OpenTelemetry,
  sin Sentry (verificado en los seis `.csproj`)—. Queda el logger por defecto escribiendo a la
  consola del contenedor. El `TODO.md` pide exactamente lo contrario: *"necesitamos logs de las
  cosas que van pasando fácilmente accesible"* y *"una base de datos de logs de tráfico"*.
- **Métricas y pantalla de operación**: pendientes desde AGENTS §9.1. Sin eso no hay forma de
  saber si J2 corrió, ni cuántos pagos quedaron en revisión.
- **Hangfire**: el JobService no expone dashboard. La única señal de que la conciliación corre son
  los logs del contenedor.
- **Backups y restauración**: una base gestionada los trae; lo que no está escrito es **quién
  prueba un restore**. El informe de infraestructura lo pone como tarea de la etapa 2.
- **El registro de actividad ya existe** y es la mejor pieza que hay para diagnosticar: F1 cerrada,
  con actor por ámbito y reservas y pagos cableados. Le faltan F2–F7, entre ellas la lectura.

---

## 8. Seguridad y cumplimiento

| | Qué | Detalle |
|---|---|---|
| ⚠️ | **`NOTES.md` está versionado con credenciales** | Usuario y contraseña de prueba de Mercado Pago, código de verificación y una tarjeta de test. Son de sandbox, pero el archivo está en git desde `0d95e4a` y el hábito es el problema |
| ⬜ | **Términos, condiciones y política de privacidad** | Ninguna pantalla del portal los muestra. Un checkout que cobra con tarjeta normalmente los necesita |
| ⬜ | **Datos personales** | `people` guarda nombre, teléfono y email, y el padrón migrado va a traer documento. Falta política de retención y qué se le dice al titular |
| ⬜ | **Retención del registro de actividad** | Los 24 meses son un número puesto, no averiguado con el club |
| ⬜ | **Facturación electrónica** | Sigue como pregunta abierta en AGENTS §3, y define si se puede cobrar en blanco |
| ✅ | Lo que sí está | Contraseñas hasheadas, throttle de login por email e IP, firma del webhook exigida en producción, rate limit del portal por llamador y club, sin redirecciones abiertas en el retorno del pago |

---

## 9. Calidad: qué garantiza que esto no se rompa

- **Sin CI** (§4). Es lo más barato de agregar y lo que más cubre.
- **E2E de disponibilidad**: F1 cerrada, **F2–F5 pendientes**
  ([plan](plan-disponibilidad-e2e.md)), incluido el catálogo de 16 casos por navegador.
- **Login del backoffice**: F1–F5 escritas y verdes, falta la recorrida en el navegador
  ([bitácora](plan-login-backoffice.bitacora.md)).
- **ADRs sin cruce contra tests**: la auditoría lo deja anotado como el próximo paso — un ADR sin
  test es una regla que nadie hace cumplir.

---

## 10. Lo que hay que decidir antes de escribir código

Ninguna de estas es técnica; todas bloquean algo:

| | Decisión | Qué desbloquea |
|---|---|---|
| 1 | **¿Un club o varios?** | Provisión, dominios, si Mercado Pago se muda al tenant (§2.3) |
| 2 | **Dónde se despliega** | Dockerfiles, secretos, base. El informe de infraestructura recomienda DigitalOcean App Platform con PostgreSQL gestionado, y deja Hostinger + Neon como alternativa más barata |
| 3 | **Qué dominio** | Es lo único con plazo de entrega real, y sin él no se prueba el webhook |
| 4 | **Quién corre las migraciones** | Un paso del deploy, o el arranque de la API con lock |
| 5 | **Cómo nace un club y su primer usuario** | §3 entero |
| 6 | **Qué pasa con la plata de un turno cancelado** | §5.1 |
| 7 | **TTL del hold** | §5.2.C |
| 8 | **Qué avisos recibe el cliente, y por qué canal** | §6, y con eso el outbox J4 |
| 9 | **Retención de datos personales y del registro** | §8 |

---

## 11. El camino más corto a un club real usándolo

Orden propuesto, cada etapa deja algo verificable. **No son estimaciones de tiempo**, es
dependencia: cada una necesita la anterior.

| Etapa | Qué |
|---|---|
| **0 · Decidir** | Los puntos 1 a 5 de §10. Sin el 1 y el 2, todo lo demás se hace dos veces |
| **1 · Que la imagen exista** | Los dos Dockerfiles, arrancar la API local con `ASPNETCORE_ENVIRONMENT=Production` y ver qué revienta, Hangfire en esquema propio o base creada por fuera, CI que compile y corra los tests |
| **2 · Que un club pueda nacer** | Camino de provisión (club, módulos, primer usuario), sin seeder y sin contraseñas escritas. Cambio de contraseña, aunque sea mínimo |
| **3 · Entorno de test público** | Dominio, TLS, dos builds de frontend, secretos por variable de entorno. Recién acá se puede probar el webhook de Mercado Pago de verdad |
| **4 · Que la plata no mienta** | Puntos A, B y D de la plata huérfana, y la decisión sobre devoluciones |
| **5 · Que se pueda operar a ciegas** | Logs accesibles, métricas de J2, restore de backup probado una vez |
| **6 · Que el cliente se entere** | Outbox y el primer aviso: confirmación de reserva |
| **7 · Cargar el club y abrir** | Canchas, horarios, precios y usuarios reales cargados desde el backoffice |

Lo que **no** hace falta para salir con reservas: el módulo de socios, finanzas, la migración del
padrón y los diez jobs que no son J2. Salir con `bookings` es un alcance legítimo y es el que está
más cerca.
