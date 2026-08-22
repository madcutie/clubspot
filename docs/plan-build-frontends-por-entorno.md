# Plan — Build de frontends por entorno

**Fecha:** 21/08/2026 · **Estado:** escrito, esperando decisiones · Avance en la
[bitácora](plan-build-frontends-por-entorno.bitacora.md)

Cierra el sexto bloqueante de [`infraestructura-mvp.html`](infraestructura-mvp.html) §9 —*"un
build de frontend por entorno"*— y el pendiente que dejó anotado la
[auditoría del 20/08/2026](auditoria-codigo-vs-reglas.md): *"el build por entorno de los
frontends —que son **dos** variables, no una: el portal usa además `VITE_CLUB_SLUG`"*. Del
`TODO.md` toca el ítem *"necesitamos urls fijas"*.

Es lo que hoy impide publicar el backoffice y el portal fuera de la máquina del developer.

## 1. El defecto, medido

Verificado en el árbol el 21/08/2026, no supuesto:

| # | Qué pasa hoy | Dónde |
|---|---|---|
| 1 | Los dos frontends caen a `http://localhost:5037` cuando falta la variable | `backoffice/src/api/config.ts:1`, `reservas/src/api/config.ts:1` |
| 2 | **Los dos `dist/` que hay en disco tienen `localhost:5037` adentro** | `backoffice/dist/assets/index-BYbusAEb.js`, `reservas/dist/assets/index-BLKyrLZD.js` |
| 3 | El `??` no atrapa la cadena vacía, que es el estado normal de una variable recién creada en el panel de un hosting | `config.ts:1` en las dos apps |
| 4 | Nada falla ni avisa: `npm run build` sin variables termina en verde | — |
| 5 | `Payments:AllowedReturnOrigins` viaja con los dos `localhost` en el `appsettings.json` **versionado y común a todos los entornos**, así que en producción el guard de arranque pasa con valores de desarrollo | `Api/appsettings.json` |
| 6 | `Payments:PortalBaseUrl` no está en ningún `appsettings`: existe sólo como default de código apuntando a `http://localhost:5183` | `Infrastructure/Payments/PaymentsOptions.cs` |
| 7 | El guard de CORS pregunta `IsProduction()`, así que un `Staging` arranca heredando los puertos de desarrollo | `Api/Program.cs:114-123` |
| 8 | El backoffice rutea por path con `BrowserRouter` y no hay ningún *fallback* de SPA versionado: un F5 sobre `/canchas` es 404 del hosting | `backoffice/src/main.tsx:27` |
| 9 | Los dos `vercel.json` se contradicen con su propio `.vercelignore`: el del portal declara `installCommand` y `buildCommand` —o sea, el host construye— mientras `.vercelignore` excluye `dist` | `reservas/vercel.json:4-5`, `reservas/.vercelignore:2` |
| 10 | Los dos `README.md` de frontend dicen que la app corre contra un mock en memoria —falso desde el 19/08/2026— y el del portal recomienda literalmente *"`npm run build` y servir `dist/` en cualquier hosting estático"*, que es la instrucción que produce el defecto 2 | `backoffice/README.md`, `reservas/README.md` |
| 11 | El `README.md` de la raíz enseña `pnpm install && pnpm dev`, cuando las dos apps tienen `package-lock.json` y todo el repo usa npm, y pide *"Node 20+"*, por debajo de lo que exige Vite 7.3.6 (`^20.19.0 \|\| >=22.12.0`) | `README.md:16,19` |

El síntoma de todo esto no es un error: es una pantalla vacía. Un bundle de producción que
apunta a `localhost` **anda perfecto en la única máquina que lo va a probar** —la del
developer, que tiene la API levantada en ese puerto— y falla en todas las demás.

## 2. Objetivo

Que exista un `dist/` de producción confiable para cada frontend, y que sea **imposible
producir uno mal configurado sin enterarse**.

- El build de producción **falla** si le falta la configuración, si viene vacía, si no es una
  URL absoluta o si todavía dice `localhost`.
- En desarrollo no cambia nada: un clon fresco sigue levantando sin crear ni un archivo, y
  `scripts/dev-up.ps1` sigue funcionando igual.
- El borde del backend que se acopla a los dominios del frontend deja de traer valores de
  desarrollo en un archivo versionado.

## 3. Fuera de alcance (explícito)

| Qué | Por qué queda afuera |
|---|---|
| **Configuración en runtime** (`config.json` al lado del `dist/`, `window.__ENV__`, sustitución en el `index.html`) | Es la alternativa de fondo a hornear, y sólo paga cuando haya que **promover** un mismo `dist/` entre entornos en vez de recompilarlo. Queda escrita como alternativa descartada en el ADR, con su razón, para que no se "arregle" sola más adelante |
| **Club por path o por hostname** en el portal, `<title>` saliendo del catálogo, namespaceado de las claves de `localStorage` | Es el plan de multi-club, con ADR propio. Ver la decisión 1 de la sección 5 |
| **Mismo origen para API y frontends con prefijo de path** | Eliminaría CORS, `AllowedReturnOrigins` y la variable entera de un saque, pero es una decisión de topología de despliegue, no de build |
| **CI / GitHub Actions** | Hoy no existe `.github/workflows` ni script `test` en ningún `package.json`. Además reabre el problema del `prebuild` de Orval, que lee fuera de la carpeta del proyecto |
| **Chequeo post-deploy contra los dominios reales** | Sin dominio no hay contra qué correrlo. Es del plan de despliegue |
| **Dockerfile, migraciones y seed en producción, `Network:TrustedProxies`, la base de Hangfire, el reemplazo de ngrok** | [`infraestructura-mvp.html`](infraestructura-mvp.html) §9 y su plan de despliegue. Ver la sección 7: son bloqueantes **externos** a este plan |
| **El valor de producción de `Payments:PublicBaseUrl`** | Este plan sólo exige que no sea loopback; cuál es, lo decide el plan de despliegue |
| **Favicon, `<meta description>`, manifest, accesibilidad y responsive** | Ya listados en `AGENTS.md` §10 *Lo que falta* |

## 4. Decisiones que este plan fija

1. **La configuración de los frontends se hornea en el build.** Se acepta explícitamente la
   consecuencia: **el `dist/` de test y el de producción no son el mismo archivo**, así que en
   el frontend **no hay promoción de artefacto** —a diferencia de la imagen del backend, que
   promueve por tag—. Un rollback es recompilar desde el mismo commit. Esto va a **ADR-0019**:
   es estructural, hay que desandarla el día del tercer entorno, y sin escribirla el próximo
   agente la "arregla" con un `config.json`.
2. **El build de producción falla ruidosamente si su configuración no está bien.** Es el
   espejo exacto de lo que la API ya hace con `Cors:AllowedOrigins`, y por el mismo motivo que
   dice su comentario: heredar los valores de desarrollo se manifiesta como una pantalla vacía
   en el mostrador, no como un error de arranque.
3. **El guard vive en `vite.config.ts` y sólo corre con `mode === 'production'`.** Que falte
   configuración es un error de build, no de runtime: una app que se compila y recién en el
   navegador avisa que no sabe con quién hablar ya se subió a algún lado. Que sólo corra en
   `production` es lo que deja intacto `npm run dev` y, con él, `scripts/dev-up.ps1`.
4. **En desarrollo no cambia nada.** El fallback a `http://localhost:5037` sobrevive fuera de
   `mode=production`, con un comentario de una línea que diga que es sólo de desarrollo y qué
   lo impide en producción (regla *lo provisional se marca*).
5. **El valor de producción va como argumento de un script versionado, nunca en un
   `.env.production`.** `.gitignore` ignora `.env.*`: un `.env.production` sería un archivo
   invisible en una sola laptop y el bundle saldría distinto según quién lo compile.
6. **El `dist/` lo compila la máquina del usuario; el hosting sólo sirve archivos estáticos.**
   Ningún host construye nada: `prebuild` dispara Orval, que lee
   `../../../docs/api/clubspot.openapi.json` con `clean: true` y **necesita el repo entero
   clonado**. Un host que aísle el *root directory* del proyecto borra `src/api/generated` y
   después falla. Esta decisión resuelve además la contradicción del defecto 9: los
   `vercel.json` pierden `framework`, `installCommand` y `buildCommand`, y `.vercelignore` deja
   de excluir `dist`. Como contrapartida, el build **no necesita el SDK de .NET**: tanto el
   documento OpenAPI como los dos clientes generados están versionados.
7. **El fallback de SPA es entregable del repo, no configuración del panel del proveedor.** Se
   versionan los dos formatos —`vercel.json` y `public/_redirects`— para que elegir hosting
   deje de bloquear esta fase; el que no lo lee, lo ignora.
8. **`VITE_CLUB_SLUG` sigue horneado, y queda marcado como deuda** en el ADR, en `AGENTS.md`
   §10 y en un comentario de `config.ts`. Es el mismo tratamiento que ya tiene
   `people.debtAmount`: la violación se anota, no se disimula.
9. **`Payments:AllowedReturnOrigins` sale del `appsettings.json` versionado** y pasa a ser
   configuración por entorno. No cambia ninguna decisión: corrige un defecto.
10. **Ninguna URL de frontend puede resolver a loopback fuera de Development**, y los guards
    por entorno se escriben `!IsDevelopment()`, no `IsProduction()`.
11. **Las tres `VITE_DEV_*` se borran** de `backoffice/src/vite-env.d.ts`: no las consume nadie
    en `src/`, contradicen ADR-0018 —el club sale de `users.tenantId`, no de una variable— y
    una de ellas invita a hornear una contraseña en un archivo que termina en el bundle.
12. **Un solo número de Node en todo el repo.** Hoy el `README.md` de la raíz dice "Node 20+",
    Vite 7.3.6 exige `^20.19.0 || >=22.12.0` y la máquina del usuario corre v24.11.1. El plan
    fija `.nvmrc` y `engines` y alinea el README con ellos.

## 5. Decisiones que necesita el usuario

Las dos primeras **bloquean la ejecución**: sin respuesta, el plan no se puede escribir en
firme. Las otras seis bloquean el despliegue, no el plan: se ejecutan las seis fases sin ellas,
pero el `dist/` real no se puede compilar ni verificar hasta que estén.

### Bloquean la ejecución

**1 · ¿Un club o multi-club desde el primer despliegue?**

| Opción | Qué implica |
|---|---|
| **(a) Un club** *(recomendada)* | `VITE_CLUB_SLUG` sigue horneado, marcado como deuda en ADR-0019, y este plan cierra como está escrito |
| (b) Multi-club ya | El slug sale del build y se resuelve en runtime, por primer segmento del path o por hostname |

**Por qué (a).** Hoy existe un solo club y en producción no hay ni migración ni seed: no hay un
segundo club que resolver. (b) es la solución correcta al problema que **va a** existir
—[ADR-0018](adr/0018-sesion-del-backoffice-token-en-sessionstorage-y-rol-en-la-claim.md) ya
rechazó la misma variable para el backoffice con el argumento *"ata el frontend a un club por
build y no aporta nada que `users.tenantId` no diga mejor"*— pero toca más de diez archivos,
cambia la URL de desarrollo del portal —y con ella `scripts/dev-up.ps1:70`, el README del
portal y el catálogo de 16 casos E2E de
[`plan-disponibilidad-e2e.md`](plan-disponibilidad-e2e.md)—, obliga a namespacear las claves de
`localStorage` y arrastra un test de integración. No acerca ni un día el primer deploy. Es un
plan propio. Lo que sí hay que hacer hoy es **marcarlo**, y eso lo hace F1.

**2 · ¿Se acepta un escape declarado para probar contra la API local?**

| Opción | Qué implica |
|---|---|
| **(a) Guard con escape `VITE_ALLOW_LOCAL_API=1`** *(recomendada)* | `npm run build` de producción rechaza `localhost` salvo que se pida explícitamente |
| (b) Guard sin escape | Para probar `npm run preview` contra la API local hay que comentar el guard a mano |
| (c) Sin chequeo de `localhost` | Sólo se exige que la variable esté |

**Por qué (a).** Probar el `dist/` contra la API local es un uso legítimo y frecuente; sin
escape, el guard se termina comentando a mano, y un guard comentado a mano no vuelve nunca.
(c) deja vivo el modo de falla del defecto 2: el bundle apuntando a `localhost` anda perfecto
justo en la única máquina que lo va a verificar.

### Bloquean el despliegue, no el plan

**3 · ¿Cuál es el dominio de producción y cómo se reparten los tres nombres?** La propuesta de
[`infraestructura-mvp.html`](infraestructura-mvp.html) es un dominio con subdominios `api.`,
`admin.` y `reservas.`: un registro, una renovación y un certificado automático por nombre.
Conviene tener presente el acoplamiento antes de elegir: ese `api.<dominio>` es a la vez el
`VITE_API_URL` horneado en los **dos** bundles, el `Payments:PublicBaseUrl` y la
`NotificationUrl` que Mercado Pago guarda dentro de cada preferencia. Cambiarlo después es
recompilar y republicar los dos frontends, más pagos viejos notificando a una URL que ya no
existe.

**4 · ¿Se despliega también un entorno de test, o sólo producción?** El documento de
infraestructura propone test primero, y es el único lugar donde el webhook de Mercado Pago se
puede probar contra una URL pública estable —hoy eso lo hace ngrok—. El script de F3 soporta
los dos sin ningún cambio: es el mismo comando con otra `-ApiUrl`.

**5 · Si hay entorno de test, ¿qué proveedor de pagos usa?** No puede ser `fake`:
`FakePaymentProvider` arma la URL de checkout como `{ApiBaseUrl}/dev/checkout`, y
`Program.cs:189` mapea esa ruta **sólo en Development**. Un test con `Provider=fake` le entrega
al comprador una URL que ese mismo host responde con 404. Las opciones son el sandbox de
Mercado Pago, o mapear `/dev/checkout` fuera de Development bajo una condición explícita.

**6 · ¿Qué hosting estático?** El documento de infraestructura recomienda DigitalOcean App
Platform (tres sitios estáticos gratis, el cuarto 3 USD; con dos frontends por dos entornos son
cuatro). El único rastro en la máquina es un proyecto de Vercel llamado `forever-spot`
(`reservas/.vercel/project.json`, no versionado), linkeado el 14/08/2026 cuando el portal era
todavía el mockup con datos falsos: si se reusa, hay que decidirlo a sabiendas. **F4 ya no
depende de esta respuesta**, porque versiona los dos formatos de rewrite. Lo que sí depende es
el paso final de subida: con Vercel, subir un `dist/` compilado localmente exige
`vercel build && vercel deploy --prebuilt`, que trabaja sobre `.vercel/output` y no sobre
`dist/`.

**7 · ¿Se aceptan deploys de preview con URL aleatoria?** `ConfirmScreen.tsx:51` manda
`window.location.origin + window.location.pathname` como `returnUrl`, y el backend lo rechaza
con 422 si el origen no está en `AllowedReturnOrigins`. Una URL distinta por deploy no se
puede listar por anticipado: en un preview, **reservar sin pago funciona y reservar con pago
no**, con el mensaje genérico de reintento, indistinguible de "la API está caída".

**8 · ¿Quién guarda los valores de producción?** La recomendación es el script de F3 corrido
por una persona, con los valores en el comando. Las `VITE_*` no son secretos —viajan en el
bundle por definición—, así que no hay nada que proteger; CI es un plan aparte.

## 6. Fases

### F1 — La regla queda escrita antes de ejecutarla

Va primera, no última: toda regla acordada se persiste en el repo **antes** de ejecutarla.

- **ADR-0019 — Configuración de los frontends horneada en el build**: contexto (los dos `??`,
  los dos `dist/` con `localhost:5037` adentro), decisión, consecuencias —incluida la de que
  no hay promoción de artefacto en el frontend— y **alternativas descartadas con su razón**:
  `config.json` o `window.__ENV__` en runtime · mismo origen con prefijo de path · slug del
  club por hostname o por path · dejar el `??` como está.
- El ADR deja anotado **como deuda explícita** que `VITE_CLUB_SLUG` sigue horneado, y que
  ADR-0018 rechazó la misma idea para el backoffice.
- Fila en `docs/adr/README.md` y la convención en `AGENTS.md` §6. La fila del plan en §2 ya
  está: se escribió junto con este documento.

**Archivos:** `docs/adr/0019-configuracion-de-los-frontends-horneada-en-el-build.md` ·
`docs/adr/README.md` · `AGENTS.md`.

**Verificación:** el ADR sigue el formato del índice (contexto → decisión → consecuencias →
alternativas descartadas) y está listado · `git status` no muestra ni un archivo de código.

### F2 — El build de producción falla si le falta su configuración

Los dos `vite.config.ts` pasan a `defineConfig(({ mode }) => …)` con `loadEnv`. Con
`mode === 'production'` se lanza —mensaje en inglés (ADR-0006), nombrando la variable y el
`.env.example`— si `VITE_API_URL`:

- falta o queda vacía después de recortarla;
- no parsea como URL absoluta `http`/`https`;
- termina en `/`;
- contiene `localhost` o `127.0.0.1` **y** no está seteada `VITE_ALLOW_LOCAL_API=1`.

En el portal se exige además `VITE_CLUB_SLUG` no vacía y matcheando
`^[a-z0-9]+(-[a-z0-9]+)*$` con largo ≤ 60 — los 60 son el `HasMaxLength(60)` de
`ClubConfiguration.cs:17`: un slug más largo no puede resolver ningún club. **Fuera de
`production` no se exige nada**, que es lo que deja `npm run dev` y `scripts/dev-up.ps1`
intactos.

`loadEnv` necesita el directorio del proyecto, y ahí hay una trampa medida: los dos
`tsconfig.json` declaran `"types": ["vite/client"]` e **incluyen `vite.config.ts`** en la
compilación, `npm run build` es `tsc -b && vite build`, y **`@types/node` no está instalado en
ninguna de las dos apps**. Usar `process` sin más rompe `tsc -b` antes de que Vite arranque. La
fase agrega `@types/node` a `devDependencies` y `"node"` a `types` en los dos `tsconfig.json`.

Los dos `config.ts` normalizan de la misma forma: recortan, atrapan la cadena vacía —que es lo
que el `??` deja pasar— y sacan la barra final. El fallback **se conserva**, con el comentario
de una línea que lo marca como sólo de desarrollo. En el portal, el comentario de `CLUB_SLUG`
dice además que el slug horneado es provisional y remite a ADR-0019.

`backoffice/src/vite-env.d.ts` pierde `VITE_DEV_CLUB`, `VITE_DEV_EMAIL` y `VITE_DEV_PASSWORD`;
`reservas/src/vite-env.d.ts` gana el `/// <reference types="vite/client" />` que le falta.

**Archivos:** los dos `vite.config.ts` · los dos `src/api/config.ts` · los dos
`src/vite-env.d.ts` · los dos `tsconfig.json` · los dos `package.json` y sus
`package-lock.json`.

**Verificación:** `npm run typecheck` limpio en las dos apps **antes** de dar la fase por buena
—es lo que prueba que `@types/node` quedó bien y que borrar las `VITE_DEV_*` no dejó
referencias— · `npm run build` sin variables falla con exit ≠ 0 nombrando `VITE_API_URL` y no
deja `dist/` nuevo · `VITE_API_URL=` vacía falla igual, que es el caso que el `??` no atrapaba ·
sin esquema falla · con barra final falla · con `localhost` falla, y con `VITE_ALLOW_LOCAL_API=1`
compila · en el portal, sin `VITE_CLUB_SLUG` falla nombrando el slug, y un slug con espacios
falla · `npm run dev` sigue levantando en 5184 y 5183 sin ningún `.env`, y `scripts/dev-up.ps1`
abre las cinco ventanas como siempre.

### F3 — Un solo comando produce los dos `dist/` de producción

`scripts/build-frontends.ps1`, al lado de `db-sql.ps1` y `db-reset.ps1`. `-ApiUrl` obligatorio
y `-ClubSlug` con default `chaco-for-ever`. Valida `https://` y ausencia de barra final, y el
slug con la misma regex que F2, **antes de instalar nada**; setea las variables, y para cada
frontend corre `npm ci` —no `npm i`: `orval` está declarado como `^8.24.0` y un rango abierto
puede regenerar clientes distintos de los versionados sin que nadie lo note— y `npm run build`,
chequeando el código de salida en cada paso.

El orden importa y queda escrito en el script: **primero `dotnet build`**, que reescribe
`docs/api/clubspot.openapi.json`, y se confirma que ese archivo **no cambió**; recién después
se compilan los frontends. Un frontend compilado contra un contrato más nuevo que la API
desplegada llama endpoints que no existen, y como el build del frontend no necesita el SDK de
.NET, por sí solo no puede detectar que el contrato versionado quedó viejo.

Después de cada build, el chequeo que convierte el defecto en error: si aparece `localhost` en
`dist/assets/*.js`, borra el `dist/` y aborta. Es redundante con F2 **a propósito** —F2 mira lo
que entra, esto mira lo que sale, y lo que se sube es lo que sale—. Al cerrar imprime las dos
rutas y recuerda qué orígenes hay que dar de alta en `Cors:AllowedOrigins` y
`Payments:AllowedReturnOrigins`.

`.env.example` versionado en las dos carpetas de frontend, con una línea de comentario por
variable. La excepción `!.env.example` ya existe en el `.gitignore` y **ya se usa en la raíz**
—`./.env.example` alimenta `compose.yaml`—; lo que no existe es ninguno bajo `src/frontend/`.
**No se crea ningún `.env.production`.** `.nvmrc` en la raíz y `engines` en los dos
`package.json`, alineados con lo que exige Vite 7.3.6 (`^20.19.0 || >=22.12.0`) y con lo que
corre la máquina del usuario (v24.11.1): F3 promete un comando reproducible y sin fijar Node la
promesa es falsa en la segunda máquina.

**Archivos:** `scripts/build-frontends.ps1` · los dos `.env.example` · `.nvmrc` · los dos
`package.json`.

**Verificación:** sin `-ApiUrl` pide el parámetro y no corre ningún `npm` · con `http://` aborta
antes de instalar · con barra final aborta · con una URL válida deja los dos `dist/` · buscar
`localhost:5037` en `src/frontend/*/dist/assets/*.js` **no devuelve nada**, contra las dos
coincidencias medidas hoy · la URL nueva sí aparece en los dos bundles ·
`git status --porcelain src/frontend/*/src/api/generated` vacío después de correrlo, que es lo
que prueba que `npm ci` no movió los clientes generados · `git status` no muestra ningún `.env`
fuera de los dos `.env.example`, y `git check-ignore -v` sobre ellos no devuelve nada.

### F4 — El hosting sirve la SPA sin romperse en un F5

El backoffice monta `BrowserRouter` y rutea por path, así que sin rewrite un F5 sobre
`/canchas` es 404 del hosting, no de la app. Se versionan los dos formatos, inertes en el host
que no los lee:

- `backoffice/vercel.json` nuevo: `outputDirectory: dist`, rewrite de `/(.*)` a `/index.html`,
  y encabezados de caché —`no-cache` en `/index.html`, `immutable` en `/assets/*`—. Es la única
  mitigación del riesgo que introduce hornear: un `index.html` cacheado apuntando al bundle
  viejo.
- `backoffice/public/_redirects` con la regla equivalente de Netlify y Cloudflare Pages; Vite
  copia `public/` al `dist/` tal cual.
- `reservas/vercel.json` gana los mismos dos bloques. El portal no rutea por path, pero el
  rewrite es inofensivo y saca la vuelta `?retorno=<guid>` de las manos del host.
- **Se resuelve la contradicción del defecto 9**, coherente con la decisión 6: los dos
  `vercel.json` pierden `framework`, `installCommand` y `buildCommand` —el host no construye—
  y los dos `.vercelignore` dejan de excluir `dist`, que es justamente lo único que hay que
  subir.

Los equivalentes de DigitalOcean App Platform (`catchall_document`) y de Caddy
(`try_files {path} /index.html`, ya escrito en `infraestructura-mvp.html` §11) se transcriben en
una tabla de este plan; no se crea archivo para ellos.

**Archivos:** `backoffice/vercel.json` · `backoffice/public/_redirects` ·
`backoffice/.vercelignore` · `reservas/vercel.json` · `reservas/.vercelignore`.

**Verificación:** con los `dist/` de F3, `npm run preview` en el backoffice y navegar directo a
`/canchas` y `/personas` devuelve la app, no un 404, y un F5 parado en `/horarios` sigue ahí ·
`dist/_redirects` existe después del build · el portal abre en `/` y en `/?retorno=<guid>` entra
por el camino de vuelta del pago · los dos `vercel.json` validan contra su `$schema`.

### F5 — El borde del backend deja de mentir en producción

Tres defectos que hacen que un frontend desplegado no sirva, y ninguno más.

1. **`appsettings.json` pierde `Payments:AllowedReturnOrigins`.** Hoy trae los dos `localhost`
   en el archivo versionado y común a todos los entornos, así que en producción el guard pasa
   con valores de desarrollo: la API arranca en verde y después responde **422 a toda reserva
   online**, que el portal colapsa en *"No se pudo confirmar la reserva"*. Es exactamente el
   modo de falla que el comentario del guard de CORS dice querer evitar. En producción la clave
   viaja por variable de entorno.
2. **Guard nuevo, al lado del que ya existe**: sólo cuando `!IsDevelopment()` y con proveedor de
   pagos cableado, `Payments:PortalBaseUrl` tiene que estar seteada, y ni ella ni ninguna
   entrada de `AllowedReturnOrigins` puede resolver a loopback. `PortalBaseUrl` es de donde
   salen el QR y el link de WhatsApp del cobro en mostrador: sin esto, en producción se le
   entrega al cliente un QR muerto y no falla nada.
3. **El guard de CORS pasa de `IsProduction()` a `!IsDevelopment()`**, así un `Staging` deja de
   heredar `http://localhost:5184`.

Tres consecuencias que van **en la misma fase** o el build y los tests quedan rojos:

- `appsettings.OpenApi.json` corre con proveedor `fake` y hereda las origins del base, y su
  entorno **es `OpenApi`, no Development** (`ClubSpot.Api.csproj:31-35`), así que el cambio del
  guard de CORS también lo alcanza. Necesita sus propias origins de descarte. Sin esto, el
  target que exporta el documento arranca el host, el guard lanza y **`dotnet build` se rompe**.
- `ApiFactory` usa `UseEnvironment("Development")`, así que el guard nuevo no lo toca; pero hoy
  toma las origins del base, así que al sacarlas hay que setearle la del portal.
- `appsettings.Development.json.example` gana el bloque. El `appsettings.Development.json` real
  no se versiona: **el usuario tiene que copiar la clave a su archivo local o la API deja de
  arrancar en dev.** Queda escrito acá y en la bitácora. Ese `.example` **no es plantilla de
  producción** —trae `RequireValidSignature: false`—, y este plan no crea el de producción: es
  del plan de despliegue.

**Tests** nuevos en `DeploymentSurfaceTests`, con el estilo del que ya está: producción se niega
a arrancar con un `PortalBaseUrl` de loopback · ídem con un origen de retorno de loopback ·
`Staging` se niega a arrancar sin sus propias origins de CORS. Cada uno se comprueba **fallando
contra el código anterior** antes de darlo por bueno, como pide la convención que dejó escrita
la auditoría del 20/08.

Los guards son de la Api y no del JobService **a propósito**: el JobService no conoce ninguna
URL de frontend.

**Archivos:** `Api/appsettings.json` · `Api/appsettings.OpenApi.json` ·
`Api/appsettings.Development.json.example` · `Api/Program.cs` ·
`IntegrationTests/Auth/ApiFactory.cs` · `IntegrationTests/Auth/DeploymentSurfaceTests.cs`.

**Verificación:** `dotnet build` verde con `TreatWarningsAsErrors` y reescribe
`docs/api/clubspot.openapi.json` **sin diff**, porque el contrato no cambia · `dotnet test`
verde, con el número exacto y comparado contra el último registrado —**184 verdes, 92 unitarios
+ 92 de integración**, auditoría del 20/08— más los tres nuevos · con el
`appsettings.Development.json` local actualizado, crear una reserva online desde el portal sigue
devolviendo el checkout, no 422 · en `Production`, con CORS puesto, proveedor cableado y **sin**
origen de retorno, la API **falla al arrancar**, cosa que hoy no pasa.

### F6 — Cierre: la documentación deja de mentir

- Los dos `README.md` de frontend se reescriben cortos: qué es la app, cómo se levanta en dev
  copiando el `.env.example`, la tabla de variables con cuáles son obligatorias en producción, y
  una sección de deploy que remite al script, al requisito de fallback de SPA y a la advertencia
  de que el `dist/` no se promueve entre entornos. Hoy los dos describen un mock que no existe,
  nombran archivos que tampoco, y el del portal da la instrucción que produce el defecto 2.
- El `README.md` de la raíz corrige `pnpm` por `npm` y alinea el número de Node con `.nvmrc` y
  `engines`.
- Se corrigen las dos afirmaciones **medidas como falsas** de `infraestructura-mvp.html`: la
  variable de entorno del proveedor de pagos está escrita `Payments__Gateway`, clave que no
  existe —con ella el proveedor queda en `none` y el portal ofrece sólo *pagar en el club*, sin
  error en ninguna parte; la real es `Payments:Provider`—, y la fila que dice que el CORS está
  fijo en `Program.cs:73` dejó de ser cierta. Se marca cerrado su bloqueante #6.
- `docs/auditoria-codigo-vs-reglas.md` suma una entrada: su cierre del 20/08 dice que el build
  por entorno *"sigue faltando"*, y deja de ser verdad.
- `TODO.md`: se marca el ítem *"necesitamos urls fijas"*.
- `AGENTS.md`: §6 suma el comando, aclarando que **sí lo puede correr un agente**, a diferencia
  de `dev-up.ps1`, y alinea `npm i` con `npm ci` donde corresponda; §8 y §9.1 reflejan el
  estado; §10 *Lo que falta* **gana** la deuda del slug horneado y la del `<title>` del portal
  —no pierde nada: hoy no tiene ningún ítem de build por entorno—.

**Archivos:** los dos `README.md` de frontend · `README.md` · `docs/infraestructura-mvp.html` ·
`docs/auditoria-codigo-vs-reglas.md` · `TODO.md` · `AGENTS.md` ·
`docs/plan-build-frontends-por-entorno.bitacora.md`.

**Verificación:** buscar `mock`, `store.ts`, `mockApi` en los dos README no devuelve nada · los
dos documentan `VITE_API_URL` y el del portal además `VITE_CLUB_SLUG` · buscar `pnpm` en el
README de la raíz no devuelve nada · las dos afirmaciones falsas ya no están en el documento de
infraestructura · seguir el README de cero, en un clon limpio, produce un `dist/` sin
`localhost` adentro · recorrida en el navegador contra el `dist/` de producción servido de
verdad: login del backoffice → agenda → panel de venta, y el portal hasta confirmar · la
bitácora cierra con los números medidos.

## 7. Bloqueantes externos a este plan

Se ejecutan las seis fases sin ellos, pero **el resultado no se puede verificar de punta a
punta** hasta que estén. No son alcance de este plan; se listan para que no aparezcan como
sorpresa el día del deploy:

| Qué falta | Por qué bloquea |
|---|---|
| **En producción no hay migración ni seed** | `Program.cs:148-155` migra y siembra sólo en Development, y `DevSeeder` es el único que crea `chaco-for-ever`. El portal desplegado responde 404 desde el primer día porque **el club no existe**. Hay que decidir quién crea el primero |
| **Los dos Dockerfiles y quién corre las migraciones** | `infraestructura-mvp.html` §9. Sin API desplegada no hay contra qué apuntar el `VITE_API_URL` |
| **El reemplazo de ngrok** | Hoy `Payments:PublicBaseUrl` es el túnel. Sin URL pública estable, Mercado Pago no notifica |
| **Los tres nombres de DNS y sus certificados** | Son los valores que el script de F3 necesita como argumento |

## 8. Lo que queda anotado para después

- **El `<title>` del portal tiene el nombre de un club cableado.** Sale del `index.html`, no del
  catálogo. Es la misma deuda que el slug horneado y se resuelve en el mismo plan.
- **Cambiar el dominio de la API obliga a recompilar y republicar los dos frontends**, y el
  `index.html` cacheado sigue apuntando al bundle viejo hasta que expire. Los encabezados de F4
  lo acotan; no lo eliminan.
- **Rotar `Jwt:SigningKey` invalida todos los tokens de reserva del portal en vuelo**, no sólo
  las sesiones del backoffice: `PortalBookingToken.cs:30` firma con la misma clave. Un comprador
  con un checkout abierto pierde la prueba de propiedad de su turno, y "Mis reservas" se vacía
  en silencio. Rotarla es una decisión, no un trámite de checklist.
- **Probar el portal desde el celular necesita su propio valor de `VITE_API_URL`.** Vite expone
  el server en la red local (`host: true`), pero el teléfono no tiene `localhost:5037`: el
  `.env.development` del portal tiene que apuntar a la IP de la máquina. Hoy no está escrito en
  ningún lado.
- **La auditoría del `dist/` vive dentro del script de PowerShell.** Vuelve a hacer falta como
  paso independiente el día que un host corra `vite build` por su cuenta — o sea, junto con CI.
- **`scripts/dev-down.ps1` no existe**, aunque el encabezado de `dev-up.ps1` lo promete.
