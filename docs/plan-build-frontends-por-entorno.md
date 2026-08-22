# Plan — Frontends listos para producción, multi-club

**Fecha:** 21/08/2026 · **Estado:** escrito, esperando decisiones · Avance en la
[bitácora](plan-build-frontends-por-entorno.bitacora.md)

Cierra el sexto bloqueante de [`infraestructura-mvp.html`](infraestructura-mvp.html) §9 —*"un
build de frontend por entorno"*— y el pendiente que dejó anotado la
[auditoría del 20/08/2026](auditoria-codigo-vs-reglas.md). Del `TODO.md` toca el ítem
*"necesitamos urls fijas"*.

Son dos trabajos que hay que hacer juntos para poder publicar:

1. **La configuración por entorno se hornea en el build y falla si falta** — hoy los dos
   frontends caen a `localhost:5037` en silencio.
2. **El portal deja de estar atado a un club** — hoy el slug se hornea en el build, lo que
   convertiría un producto multi-club en un build por club.

> **Corrección del usuario del 21/08/2026.** La primera versión de este plan proponía dejar
> `VITE_CLUB_SLUG` horneado y marcarlo como deuda. El usuario lo vetó:
>
> > *"la aplicación es multiclub, es un frontend para todos los clubes… ¿como que voy a
> > compilar el fronte para cada club? esto es una locura"*
>
> Tiene razón, y la propuesta estaba mal. **Un build por club no existe.** El slug sale del
> build y pasa a resolverse en runtime, por el primer segmento del path. Es la misma conclusión
> a la que ya había llegado
> [ADR-0018](adr/0018-sesion-del-backoffice-token-en-sessionstorage-y-rol-en-la-claim.md) para
> el backoffice —*"ata el frontend a un club por build y no aporta nada"*—, aplicada ahora donde
> faltaba.

## 1. Qué ya es multi-club y qué no

Relevado el 21/08/2026. La buena noticia es que **el problema está acotado al frontend del
portal**:

| Pieza | ¿Multi-club? | Cómo resuelve el club |
|---|---|---|
| API | ✅ | `/api/portal/{clubSlug}/…` — el slug es segmento de ruta, y `clubs.slug` tiene índice único |
| Backoffice | ✅ | Sale de `users.tenantId`, firmado en el token (ADR-0018). No hay ninguna variable de club |
| Portal (frontend) | ❌ | `VITE_CLUB_SLUG` horneado en el build, con default `chaco-for-ever` |

Fuera del portal, `chaco-for-ever` aparece sólo en `DevSeeder.cs` y en los tests de
integración, que es donde corresponde.

Lo que ata el portal a un club son **tres cosas**, no una:

| # | Qué | Dónde |
|---|---|---|
| 1 | El slug horneado en el build | `reservas/src/api/config.ts:2` |
| 2 | El `<title>` con el nombre de un club adentro | `reservas/index.html:7` — *"Chaco Forever Spot · Reserva de canchas"* |
| 3 | Las dos claves de `localStorage` sin separar por club: con dos clubes en el mismo origen, las reservas de uno aparecen en el otro | `reservas/src/state/myBookings.ts:8`, `reservas/src/state/bookingTokens.ts:8` |

## 2. El defecto de configuración, medido

Verificado en el árbol el 21/08/2026:

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
| 11 | El `README.md` de la raíz enseña `pnpm install && pnpm dev`, cuando las dos apps tienen `package-lock.json`, y pide *"Node 20+"*, por debajo de lo que exige Vite 7.3.6 (`^20.19.0 \|\| >=22.12.0`) | `README.md:16,19` |

El síntoma de todo esto no es un error: es una pantalla vacía. Un bundle de producción que
apunta a `localhost` **anda perfecto en la única máquina que lo va a probar** —la del
developer, que tiene la API levantada en ese puerto— y falla en todas las demás.

## 3. Objetivo

- **Un solo despliegue del portal sirve a todos los clubes.** Dar de alta un club nuevo es una
  fila en `clubs`, no un build.
- Existe un `dist/` de producción confiable para cada frontend, y es **imposible producir uno
  mal configurado sin enterarse**: el build falla si le falta la configuración, si viene vacía,
  si no es una URL absoluta o si todavía dice `localhost`.
- En desarrollo no cambia nada más que la URL del portal, que gana el segmento del club.
- El borde del backend que se acopla a los dominios del frontend deja de traer valores de
  desarrollo en un archivo versionado.

## 4. Fuera de alcance (explícito)

| Qué | Por qué queda afuera |
|---|---|
| **Dominio propio por club** (`reservas.chacoforever.com.ar`) | Se puede agregar encima del club por path sin recompilar nada: es una tabla host → slug y DNS del lado del club. Se hace el día que un club lo pida |
| **Pantalla de listado público de clubes en la raíz** | Publicar la lista de clientes es una decisión comercial, no técnica. La raíz muestra "no encontramos ese club" (ver decisión 5) |
| **Branding por club** (colores, logo, tipografía) | El portal es uno solo y hoy tiene un solo aspecto. Que cada club tenga el suyo es un plan de producto, no de despliegue |
| **Configuración en runtime de la URL de la API** (`config.json` al lado del `dist/`) | Sólo paga cuando haya que **promover** un mismo `dist/` entre entornos en vez de recompilarlo. Queda escrita como alternativa descartada en ADR-0019 |
| **Mismo origen para API y frontends con prefijo de path** | Eliminaría CORS y `AllowedReturnOrigins` de un saque, pero es una decisión de topología de despliegue, no de build |
| **CI / GitHub Actions** | Hoy no existe `.github/workflows` ni script `test` en ningún `package.json`. Además reabre el problema del `prebuild` de Orval, que lee fuera de la carpeta del proyecto |
| **Chequeo post-deploy contra los dominios reales** | Sin dominio no hay contra qué correrlo. Es del plan de despliegue |
| **Dockerfile, migraciones y seed en producción, `Network:TrustedProxies`, la base de Hangfire, el reemplazo de ngrok** | [`infraestructura-mvp.html`](infraestructura-mvp.html) §9 y su plan de despliegue. Ver la sección 8: son bloqueantes **externos** a este plan |
| **Alta de un club desde alguna pantalla** | Hoy el único que crea clubes es `DevSeeder`. Quién crea el primer club en producción es la decisión 6 |

## 5. Decisiones que este plan fija

**Sobre el club:**

1. **El portal resuelve el club por el primer segmento del path** —
   `reservas.<dominio>/<slug>`. Decisión del usuario del 21/08/2026. Un dominio, un
   certificado, y **dar de alta un club nuevo no toca infraestructura ni recompila nada**.
   Va a **ADR-0020**.
2. **`VITE_CLUB_SLUG` se elimina.** No queda como deuda ni como fallback: desaparece de
   `config.ts`, de `vite-env.d.ts` y del `.env.development`. Un default de club es exactamente
   lo que hace que un error de ruteo se vea como "el portal anda" mostrando el club equivocado.
3. **Sin slug en la URL no hay portal.** La raíz muestra la misma pantalla que un slug que no
   existe: *no encontramos ese club*. No hay redirección a un club por defecto ni listado.
4. **El `<title>` sale del catálogo**, de `PortalCatalogResponse.club.name`, que ya viaja en el
   contrato. El `index.html` queda con un título neutro hasta que el catálogo responde.
5. **Las claves de `localStorage` se separan por club.** `clubspot.misReservas` y
   `clubspot.tokensReserva` pasan a llevar el slug adentro. Sin esto, dos clubes en el mismo
   origen comparten las reservas y los tokens de prueba de propiedad.
6. **La URL de desarrollo del portal cambia** a `http://localhost:5183/chaco-for-ever`. Es
   consecuencia directa, y arrastra `scripts/dev-up.ps1`, el README del portal y el catálogo de
   16 casos E2E de [`plan-disponibilidad-e2e.md`](plan-disponibilidad-e2e.md).

**Sobre la configuración por entorno:**

7. **`VITE_API_URL` se hornea en el build**, y el `dist/` de test y el de producción no son el
   mismo archivo: en el frontend **no hay promoción de artefacto**, a diferencia de la imagen
   del backend, que promueve por tag. Un rollback es recompilar desde el mismo commit. Va a
   **ADR-0019**. Es **una variable por entorno, no por club**: esa es toda la diferencia con lo
   que se descartó.
8. **El build de producción falla ruidosamente si su configuración no está bien.** Es el
   espejo de lo que la API ya hace con `Cors:AllowedOrigins`, y por el mismo motivo que dice su
   comentario: heredar los valores de desarrollo se manifiesta como una pantalla vacía en el
   mostrador, no como un error de arranque.
9. **El guard vive en `vite.config.ts` y sólo corre con `mode === 'production'`.** Que falte
   configuración es un error de build, no de runtime. Que sólo corra en `production` es lo que
   deja intacto `npm run dev` y, con él, `scripts/dev-up.ps1`.
10. **En desarrollo el fallback a `http://localhost:5037` sobrevive**, con un comentario de una
    línea que lo marca como sólo de desarrollo y dice qué lo impide en producción.
11. **El valor de producción va como argumento de un script versionado, nunca en un
    `.env.production`.** `.gitignore` ignora `.env.*`: sería un archivo invisible en una sola
    laptop y el bundle saldría distinto según quién lo compile.
12. **El `dist/` lo compila la máquina del usuario; el hosting sólo sirve archivos estáticos.**
    Ningún host construye nada: `prebuild` dispara Orval, que lee
    `../../../docs/api/clubspot.openapi.json` con `clean: true` y **necesita el repo entero
    clonado**. Esto resuelve la contradicción del defecto 9: los `vercel.json` pierden
    `framework`, `installCommand` y `buildCommand`, y `.vercelignore` deja de excluir `dist`.
    Como contrapartida, el build **no necesita el SDK de .NET**: el documento OpenAPI y los dos
    clientes generados están versionados.
13. **El fallback de SPA es entregable del repo, no configuración del panel del proveedor.** Con
    el club en el path deja de ser un detalle: sin rewrite, `/<slug>` es 404 del hosting y el
    portal no abre para nadie. Se versionan los dos formatos, `vercel.json` y
    `public/_redirects`.
14. **Ninguna URL de frontend puede resolver a loopback fuera de Development**, y los guards por
    entorno se escriben `!IsDevelopment()`, no `IsProduction()`.
15. **`Payments:AllowedReturnOrigins` sale del `appsettings.json` versionado** y pasa a ser
    configuración por entorno.
16. **Las tres `VITE_DEV_*` se borran** de `backoffice/src/vite-env.d.ts`: no las consume nadie,
    contradicen ADR-0018 y una invita a hornear una contraseña en el bundle.
17. **Un solo número de Node en todo el repo**, entre `.nvmrc`, `engines` y el README de la
    raíz.

## 6. Decisiones que necesita el usuario

La primera **bloquea la ejecución**. Las otras bloquean el despliegue, no el plan.

**1 · ¿Se acepta un escape declarado para probar contra la API local?**

| Opción | Qué implica |
|---|---|
| **(a) Guard con escape `VITE_ALLOW_LOCAL_API=1`** *(recomendada)* | `npm run build` de producción rechaza `localhost` salvo que se pida explícitamente |
| (b) Guard sin escape | Para probar `npm run preview` contra la API local hay que comentar el guard a mano |
| (c) Sin chequeo de `localhost` | Sólo se exige que la variable esté |

**Por qué (a).** Probar el `dist/` contra la API local es un uso legítimo y frecuente; sin
escape, el guard se termina comentando a mano, y un guard comentado a mano no vuelve nunca.
(c) deja vivo el modo de falla del defecto 2: el bundle apuntando a `localhost` anda perfecto
justo en la única máquina que lo va a verificar.

**2 · ¿Cuál es el dominio de producción y cómo se reparten los nombres?** La propuesta de
[`infraestructura-mvp.html`](infraestructura-mvp.html) es un dominio con subdominios `api.`,
`admin.` y `reservas.`. Con el club por path, `reservas.<dominio>` alcanza para todos los
clubes. Conviene tener presente el acoplamiento: ese `api.<dominio>` es a la vez el
`VITE_API_URL` horneado en los dos bundles, el `Payments:PublicBaseUrl` y la `NotificationUrl`
que Mercado Pago guarda dentro de cada preferencia. Cambiarlo después es recompilar y
republicar los dos frontends, más pagos viejos notificando a una URL que ya no existe.

**3 · ¿Se despliega también un entorno de test, o sólo producción?** El documento de
infraestructura propone test primero, y es el único lugar donde el webhook de Mercado Pago se
puede probar contra una URL pública estable —hoy eso lo hace ngrok—. El script soporta los dos
sin ningún cambio: es el mismo comando con otra `-ApiUrl`.

**4 · Si hay entorno de test, ¿qué proveedor de pagos usa?** No puede ser `fake`:
`FakePaymentProvider` arma la URL de checkout como `{ApiBaseUrl}/dev/checkout`, y
`Program.cs:189` mapea esa ruta **sólo en Development**. Un test con `Provider=fake` le entrega
al comprador una URL que ese mismo host responde con 404. Las opciones son el sandbox de
Mercado Pago, o mapear `/dev/checkout` fuera de Development bajo una condición explícita.

**5 · ¿Qué hosting estático?** El documento de infraestructura recomienda DigitalOcean App
Platform. El único rastro en la máquina es un proyecto de Vercel llamado `forever-spot`
(`reservas/.vercel/project.json`, no versionado), linkeado el 14/08/2026 cuando el portal era
todavía el mockup con datos falsos: si se reusa, hay que decidirlo a sabiendas — y el nombre
ya no describe lo que es, porque deja de ser el portal de un club. **F5 no depende de esta
respuesta**, porque versiona los dos formatos de rewrite. Lo que sí depende es el paso final de
subida: con Vercel, subir un `dist/` compilado localmente exige
`vercel build && vercel deploy --prebuilt`, que trabaja sobre `.vercel/output`, no sobre
`dist/`.

**6 · ¿Quién crea el primer club en producción?** Hoy el único que crea clubes es `DevSeeder`,
y sólo corre en Development. Sin esa fila en `clubs`, el portal desplegado responde *no
encontramos ese club* desde el primer día, para cualquier slug. Es lo que hay que resolver
antes de poder verificar nada de punta a punta.

**7 · ¿Se aceptan deploys de preview con URL aleatoria?** `ConfirmScreen.tsx:51` manda
`window.location.origin + window.location.pathname` como `returnUrl`, y el backend lo rechaza
con 422 si el origen no está en `AllowedReturnOrigins`. Una URL distinta por deploy no se puede
listar por anticipado: en un preview, **reservar sin pago funciona y reservar con pago no**, con
el mensaje genérico de reintento, indistinguible de "la API está caída".

## 7. Fases

### F1 — Las dos reglas quedan escritas antes de ejecutarlas

Van primero, no al final: toda regla acordada se persiste en el repo **antes** de ejecutarla.

- **ADR-0019 — Configuración de los frontends horneada en el build**: contexto (los dos `??`,
  los dos `dist/` con `localhost:5037` adentro), decisión, consecuencias —incluida la de que no
  hay promoción de artefacto en el frontend— y alternativas descartadas con su razón:
  `config.json` o `window.__ENV__` en runtime · mismo origen con prefijo de path · dejar el `??`.
  Deja explícito que la variable es **por entorno, no por club**.
- **ADR-0020 — El portal resuelve el club por el primer segmento del path**: contexto (el slug
  horneado convertía un producto multi-club en un build por club), decisión, consecuencias —la
  URL de desarrollo cambia, el `localStorage` se separa por club, el rewrite de SPA pasa a ser
  obligatorio— y alternativas descartadas: subdominio con certificado comodín · dominio propio
  por club, que **no se descarta para siempre**: se puede agregar encima sin recompilar · seguir
  horneando el slug, vetado por el usuario.
- Fila en `docs/adr/README.md` y la convención en `AGENTS.md` §6. La fila del plan en §2 ya
  está: se escribió junto con este documento.

**Archivos:** `docs/adr/0019-configuracion-de-los-frontends-horneada-en-el-build.md` ·
`docs/adr/0020-el-portal-resuelve-el-club-por-el-path.md` · `docs/adr/README.md` · `AGENTS.md`.

**Verificación:** los dos ADR siguen el formato del índice (contexto → decisión → consecuencias
→ alternativas descartadas) y están listados · `git status` no muestra ni un archivo de código.

### F2 — El portal deja de saber de un solo club

El corazón del plan.

- **El slug sale del primer segmento del path**, leído una vez al arrancar. `CLUB_SLUG` deja de
  ser una constante de build y pasa a ser un valor resuelto en runtime, disponible para los
  mismos seis llamados que hoy lo usan en `portalApi.ts`.
- **`VITE_CLUB_SLUG` se elimina** de `config.ts`, de `vite-env.d.ts` y del `.env.development`.
- **Sin segmento, o con un slug que la API no reconoce, se muestra una pantalla de club no
  encontrado.** El catálogo ya responde 404 para un slug inexistente: la pantalla es la
  traducción de ese 404, no una validación nueva. La raíz `/` entra por el mismo camino.
- **Las dos claves de `localStorage` se separan por club**, llevando el slug adentro. Las claves
  viejas quedan huérfanas en el navegador del developer; no se migran, porque hoy sólo hay datos
  de prueba.
- **El `<title>` se escribe con `catalog.club.name`** cuando el catálogo responde.
  `index.html` queda con un título neutro, sin el nombre de ningún club.
- **El `returnUrl` no cambia**: `window.location.origin + window.location.pathname` ya incluye
  el segmento del club, así que el comprador vuelve al portal de su club solo. Se verifica, no
  se toca.

**Archivos:** `reservas/src/api/config.ts` · `reservas/src/api/portalApi.ts` ·
`reservas/src/vite-env.d.ts` · `reservas/index.html` · `reservas/src/state/myBookings.ts` ·
`reservas/src/state/bookingTokens.ts` · `reservas/src/App.tsx` y la pantalla nueva ·
`reservas/.env.development` (local, no versionado).

**Verificación:** `npm run typecheck` limpio · `http://localhost:5183/chaco-for-ever` abre el
portal y el `<title>` dice el nombre que devuelve el catálogo · `http://localhost:5183/` y
`http://localhost:5183/no-existe` muestran la pantalla de club no encontrado, sin llamadas en
loop · reservar de punta a punta sigue funcionando, incluida la vuelta del pago con
`?retorno=<guid>` · sembrando un segundo club en desarrollo, los dos portales conviven en el
mismo navegador y **"Mis reservas" de uno no muestra las del otro** — que es lo que hoy no se
cumple · buscar `VITE_CLUB_SLUG` en `src/frontend` no devuelve nada.

### F3 — El build de producción falla si le falta su configuración

Los dos `vite.config.ts` pasan a `defineConfig(({ mode }) => …)` con `loadEnv`. Con
`mode === 'production'` se lanza —mensaje en inglés (ADR-0006), nombrando la variable y el
`.env.example`— si `VITE_API_URL` falta o queda vacía después de recortarla · no parsea como URL
absoluta `http`/`https` · termina en `/` · contiene `localhost` o `127.0.0.1` **y** no está
seteada `VITE_ALLOW_LOCAL_API=1`. **Fuera de `production` no se exige nada**, que es lo que deja
`npm run dev` y `scripts/dev-up.ps1` intactos. Ya no hay ninguna validación de slug: no queda
slug que validar.

`loadEnv` necesita el directorio del proyecto, y ahí hay una trampa medida: los dos
`tsconfig.json` declaran `"types": ["vite/client"]` e **incluyen `vite.config.ts`** en la
compilación, `npm run build` es `tsc -b && vite build`, y **`@types/node` no está instalado en
ninguna de las dos apps**. Usar `process` sin más rompe `tsc -b` antes de que Vite arranque. La
fase agrega `@types/node` a `devDependencies` y `"node"` a `types` en los dos `tsconfig.json`.

Los dos `config.ts` normalizan igual: recortan, atrapan la cadena vacía —que es lo que el `??`
deja pasar— y sacan la barra final. El fallback se conserva, marcado como sólo de desarrollo.

`backoffice/src/vite-env.d.ts` pierde las tres `VITE_DEV_*`; `reservas/src/vite-env.d.ts` gana
el `/// <reference types="vite/client" />` que le falta.

**Archivos:** los dos `vite.config.ts` · los dos `src/api/config.ts` · los dos
`src/vite-env.d.ts` · los dos `tsconfig.json` · los dos `package.json` y sus
`package-lock.json`.

**Verificación:** `npm run typecheck` limpio en las dos apps **antes** de dar la fase por buena
—es lo que prueba que `@types/node` quedó bien y que borrar las `VITE_DEV_*` no dejó
referencias— · `npm run build` sin variables falla con exit ≠ 0 nombrando `VITE_API_URL` y no
deja `dist/` nuevo · `VITE_API_URL=` vacía falla igual, que es el caso que el `??` no atrapaba ·
sin esquema falla · con barra final falla · con `localhost` falla, y con `VITE_ALLOW_LOCAL_API=1`
compila · `npm run dev` sigue levantando en 5184 y 5183 sin ningún `.env`, y `dev-up.ps1` abre
las cinco ventanas como siempre.

### F4 — Un solo comando produce los dos `dist/` de producción

`scripts/build-frontends.ps1`, al lado de `db-sql.ps1` y `db-reset.ps1`. Un solo parámetro
obligatorio, `-ApiUrl`; **ya no hay `-ClubSlug`**, porque el bundle sirve a todos los clubes.
Valida `https://` y ausencia de barra final **antes de instalar nada**; setea la variable, y para
cada frontend corre `npm ci` —no `npm i`: `orval` está declarado como `^8.24.0` y un rango
abierto puede regenerar clientes distintos de los versionados sin que nadie lo note— y
`npm run build`, chequeando el código de salida en cada paso.

El orden importa y queda escrito en el script: **primero `dotnet build`**, que reescribe
`docs/api/clubspot.openapi.json`, y se confirma que ese archivo **no cambió**; recién después se
compilan los frontends. Un frontend compilado contra un contrato más nuevo que la API desplegada
llama endpoints que no existen, y como el build del frontend no necesita el SDK de .NET, por sí
solo no puede detectarlo.

Después de cada build, el chequeo que convierte el defecto en error: si aparece `localhost` en
`dist/assets/*.js`, borra el `dist/` y aborta. Es redundante con F3 **a propósito** —F3 mira lo
que entra, esto mira lo que sale, y lo que se sube es lo que sale—. Al cerrar imprime las dos
rutas y recuerda qué orígenes hay que dar de alta en `Cors:AllowedOrigins` y
`Payments:AllowedReturnOrigins`.

`.env.example` versionado en las dos carpetas de frontend. La excepción `!.env.example` ya
existe en el `.gitignore` y **ya se usa en la raíz** —`./.env.example` alimenta `compose.yaml`—;
lo que no existe es ninguno bajo `src/frontend/`. **No se crea ningún `.env.production`.**
`.nvmrc` en la raíz y `engines` en los dos `package.json`, alineados con lo que exige Vite 7.3.6
(`^20.19.0 || >=22.12.0`) y con lo que corre la máquina del usuario (v24.11.1).

**Archivos:** `scripts/build-frontends.ps1` · los dos `.env.example` · `.nvmrc` · los dos
`package.json`.

**Verificación:** sin `-ApiUrl` pide el parámetro y no corre ningún `npm` · con `http://` aborta
antes de instalar · con barra final aborta · con una URL válida deja los dos `dist/` · buscar
`localhost:5037` en `src/frontend/*/dist/assets/*.js` **no devuelve nada**, contra las dos
coincidencias medidas hoy · la URL nueva sí aparece en los dos bundles ·
`git status --porcelain src/frontend/*/src/api/generated` vacío después de correrlo · `git status`
no muestra ningún `.env` fuera de los dos `.env.example`, y `git check-ignore -v` sobre ellos no
devuelve nada.

### F5 — El hosting sirve la SPA sin romperse en un F5

Con el club en el path, el rewrite deja de ser un detalle de comodidad: **sin él,
`/<slug>` es 404 del hosting y el portal no abre para nadie**. El backoffice lo necesita por su
propio motivo: monta `BrowserRouter` y rutea por path.

- `backoffice/vercel.json` nuevo y `reservas/vercel.json` corregido: `outputDirectory: dist`,
  rewrite de `/(.*)` a `/index.html`, y encabezados de caché —`no-cache` en `/index.html`,
  `immutable` en `/assets/*`—. Es la única mitigación del riesgo que introduce hornear: un
  `index.html` cacheado apuntando al bundle viejo.
- `public/_redirects` en las dos apps, con la regla equivalente de Netlify y Cloudflare Pages;
  Vite copia `public/` al `dist/` tal cual.
- **Se resuelve la contradicción del defecto 9**, coherente con la decisión 12: los dos
  `vercel.json` pierden `framework`, `installCommand` y `buildCommand` —el host no construye— y
  los dos `.vercelignore` dejan de excluir `dist`, que es lo único que hay que subir.

Los equivalentes de DigitalOcean App Platform (`catchall_document`) y de Caddy
(`try_files {path} /index.html`, ya escrito en `infraestructura-mvp.html` §11) se transcriben en
una tabla de este plan; no se crea archivo para ellos.

**Archivos:** `backoffice/vercel.json` · `reservas/vercel.json` · los dos `public/_redirects` ·
los dos `.vercelignore`.

**Verificación:** con los `dist/` de F4 y `npm run preview`, `/<slug>` en el portal devuelve la
app y no un 404, y un F5 parado ahí sigue en el mismo club · `/canchas` y `/personas` en el
backoffice abren directo, y un F5 en `/horarios` sigue en `/horarios` · `dist/_redirects` existe
en los dos después del build · los dos `vercel.json` validan contra su `$schema`.

### F6 — El borde del backend deja de mentir en producción

Tres defectos que hacen que un frontend desplegado no sirva, y ninguno más.

1. **`appsettings.json` pierde `Payments:AllowedReturnOrigins`.** Hoy trae los dos `localhost`
   en el archivo versionado y común a todos los entornos, así que en producción el guard pasa
   con valores de desarrollo: la API arranca en verde y después responde **422 a toda reserva
   online**, que el portal colapsa en *"No se pudo confirmar la reserva"*. Es exactamente el
   modo de falla que el comentario del guard de CORS dice querer evitar. Con el club por path,
   la lista sigue siendo corta: son **orígenes**, no URLs, así que todos los clubes comparten
   una sola entrada.
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
  arrancar en dev.** Ese `.example` **no es plantilla de producción** —trae
  `RequireValidSignature: false`—, y este plan no crea el de producción: es del plan de
  despliegue.

**Tests** nuevos en `DeploymentSurfaceTests`, con el estilo del que ya está: producción se niega
a arrancar con un `PortalBaseUrl` de loopback · ídem con un origen de retorno de loopback ·
`Staging` se niega a arrancar sin sus propias origins de CORS. Cada uno se comprueba **fallando
contra el código anterior** antes de darlo por bueno, como pide la convención que dejó escrita la
auditoría del 20/08.

Los guards son de la Api y no del JobService **a propósito**: el JobService no conoce ninguna URL
de frontend.

**Archivos:** `Api/appsettings.json` · `Api/appsettings.OpenApi.json` ·
`Api/appsettings.Development.json.example` · `Api/Program.cs` ·
`IntegrationTests/Auth/ApiFactory.cs` · `IntegrationTests/Auth/DeploymentSurfaceTests.cs`.

**Verificación:** `dotnet build` verde con `TreatWarningsAsErrors` y reescribe
`docs/api/clubspot.openapi.json` **sin diff**, porque el contrato no cambia · `dotnet test`
verde, con el número exacto comparado contra el último registrado —**184 verdes, 92 unitarios +
92 de integración**, auditoría del 20/08— más los tres nuevos · con el
`appsettings.Development.json` local actualizado, crear una reserva online desde el portal sigue
devolviendo el checkout, no 422 · en `Production`, con CORS puesto, proveedor cableado y **sin**
origen de retorno, la API **falla al arrancar**, cosa que hoy no pasa.

### F7 — Cierre: la documentación deja de mentir

- Los dos `README.md` de frontend se reescriben cortos: qué es la app, cómo se levanta en dev
  copiando el `.env.example`, la tabla de variables, y una sección de deploy que remite al
  script, al requisito de fallback de SPA y a la advertencia de que el `dist/` no se promueve
  entre entornos. El del portal explica además **la URL con el club adentro**. Hoy los dos
  describen un mock que no existe y el del portal da la instrucción que produce el defecto 2.
- **La URL de desarrollo del portal cambia en los tres lugares que la nombran**:
  `scripts/dev-up.ps1:70`, `reservas/README.md:14` y el catálogo de 16 casos E2E de
  [`plan-disponibilidad-e2e.md`](plan-disponibilidad-e2e.md).
- El `README.md` de la raíz corrige `pnpm` por `npm` y alinea el número de Node con `.nvmrc` y
  `engines`.
- Se corrigen las dos afirmaciones **medidas como falsas** de `infraestructura-mvp.html`: la
  variable del proveedor de pagos está escrita `Payments__Gateway`, clave que no existe —con
  ella el proveedor queda en `none` y el portal ofrece sólo *pagar en el club*, sin error en
  ninguna parte; la real es `Payments:Provider`—, y la fila que dice que el CORS está fijo en
  `Program.cs:73` dejó de ser cierta. Se marca cerrado su bloqueante #6.
- `docs/auditoria-codigo-vs-reglas.md` suma una entrada: su cierre del 20/08 dice que el build
  por entorno *"sigue faltando"*, y deja de ser verdad.
- `TODO.md`: se marca el ítem *"necesitamos urls fijas"*.
- `AGENTS.md`: §6 suma el comando, aclarando que **sí lo puede correr un agente**, a diferencia
  de `dev-up.ps1`; §8 y §9 reflejan el estado; §10 pierde nada y gana la deuda que quede.
  ADR-0018 se referencia desde ADR-0020: el argumento que usó para el backoffice es el mismo que
  ahora se aplica al portal.

**Archivos:** los dos `README.md` de frontend · `README.md` · `scripts/dev-up.ps1` ·
`docs/plan-disponibilidad-e2e.md` · `docs/infraestructura-mvp.html` ·
`docs/auditoria-codigo-vs-reglas.md` · `TODO.md` · `AGENTS.md` · la bitácora de este plan.

**Verificación:** buscar `mock`, `store.ts`, `mockApi` en los dos README no devuelve nada ·
buscar `localhost:5183` sin slug en `dev-up.ps1`, el README del portal y el plan E2E no devuelve
nada · buscar `pnpm` en el README de la raíz no devuelve nada · las dos afirmaciones falsas ya no
están en el documento de infraestructura · seguir el README de cero, en un clon limpio, produce
un `dist/` sin `localhost` adentro · recorrida en el navegador contra el `dist/` de producción
servido de verdad: login del backoffice → agenda → panel de venta, y el portal de **dos clubes
distintos** hasta confirmar · la bitácora cierra con los números medidos.

## 8. Bloqueantes externos a este plan

Se ejecutan las siete fases sin ellos, pero **el resultado no se puede verificar de punta a
punta** hasta que estén. No son alcance de este plan:

| Qué falta | Por qué bloquea |
|---|---|
| **En producción no hay migración ni seed** | `Program.cs:148-155` migra y siembra sólo en Development, y `DevSeeder` es el único que crea clubes. Sin una fila en `clubs`, el portal responde *no encontramos ese club* para cualquier slug. Es la decisión 6 |
| **Los dos Dockerfiles y quién corre las migraciones** | `infraestructura-mvp.html` §9. Sin API desplegada no hay contra qué apuntar el `VITE_API_URL` |
| **El reemplazo de ngrok** | Hoy `Payments:PublicBaseUrl` es el túnel. Sin URL pública estable, Mercado Pago no notifica |
| **Los nombres de DNS y sus certificados** | Son los valores que el script de F4 necesita como argumento |

## 9. Lo que queda anotado para después

- **Dominio propio por club.** El club por path no lo cierra: se puede agregar encima con una
  tabla host → slug, sin recompilar el portal. Se hace el día que un club lo pida.
- **Branding por club.** El portal es uno solo y hoy tiene un solo aspecto. Con el `<title>` ya
  saliendo del catálogo queda abierto el camino, pero colores y logo son otro plan.
- **Cambiar el dominio de la API obliga a recompilar y republicar los dos frontends**, y el
  `index.html` cacheado sigue apuntando al bundle viejo hasta que expire. Los encabezados de F5
  lo acotan; no lo eliminan.
- **Rotar `Jwt:SigningKey` invalida todos los tokens de reserva del portal en vuelo**, no sólo
  las sesiones del backoffice: `PortalBookingToken.cs:30` firma con la misma clave. Un comprador
  con un checkout abierto pierde la prueba de propiedad de su turno, y "Mis reservas" se vacía en
  silencio. Rotarla es una decisión, no un trámite de checklist.
- **Probar el portal desde el celular necesita su propio valor de `VITE_API_URL`.** Vite expone
  el server en la red local (`host: true`), pero el teléfono no tiene `localhost:5037`: el
  `.env.development` del portal tiene que apuntar a la IP de la máquina. Hoy no está escrito en
  ningún lado.
- **La auditoría del `dist/` vive dentro del script de PowerShell.** Vuelve a hacer falta como
  paso independiente el día que un host corra `vite build` por su cuenta — o sea, junto con CI.
- **`scripts/dev-down.ps1` no existe**, aunque el encabezado de `dev-up.ps1` lo promete.
