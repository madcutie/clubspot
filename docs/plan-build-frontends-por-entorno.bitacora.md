# Bitácora — plan de frontends listos para producción, multi-club

Registro de avance del [plan](plan-build-frontends-por-entorno.md). La entrada más nueva arriba.

## 21/08/2026 — F2 ejecutada: el portal dejó de estar atado a un club

`VITE_CLUB_SLUG` ya no existe. El club sale del primer segmento del path, resuelto en runtime.

- **`src/api/club.ts`** nuevo: lee el primer segmento de `location.pathname`, lo valida contra
  `^[a-z0-9]+(-[a-z0-9]+)*$` y contra los 60 caracteres de `clubs.slug`, y expone `CLUB_SLUG`
  (`string | null`) más `requireClubSlug()`. **No hay club por defecto**: un default haría que un
  error de ruteo se viera como el portal andando, con el club equivocado.
- **`config.ts`** queda con una sola línea; `vite-env.d.ts` pierde `VITE_CLUB_SLUG` y gana el
  `/// <reference types="vite/client" />` que le faltaba. `portalApi.ts` cambió sus **6** usos.
- **Las dos claves de `localStorage` llevan el club adentro.** Es el bug de datos cruzados que no
  estaba en el radar: `clubspot.misReservas` y `clubspot.tokensReserva` eran globales al origen.
  Pasaron a `clubspot.<slug>.misReservas` y `clubspot.<slug>.tokensReserva`, calculadas por
  función y no por constante de módulo, para no lanzar al importar sin club.
- **`ClubNotFoundScreen`** nueva. Entra por dos caminos: la URL no trae segmento con forma de
  slug, o el catálogo responde 404. `App` quedó partido en dos —el gate afuera, los hooks
  adentro— para no romper las reglas de hooks.
- **El `<title>` sale del catálogo** (`PortalCatalogResponse.club.name`, que ya viajaba en el
  contrato). El `index.html` quedó con `Reserva de canchas`, sin el nombre de ningún club.
- **El `returnUrl` no se tocó**: `origin + pathname` ya incluye el segmento del club.

**Verificado corriendo, no sólo con tests.** Con la API en `:5037` y el portal en `:5183`:

- `/chaco-for-ever` abre el portal y la pestaña dice *"Club Atlético Chaco For Ever · Reserva de
  canchas"* — el nombre lo puso el catálogo.
- `/` y `/no-existe` muestran *"No encontramos ese club"*, sin llamadas en loop.
- **Reserva de punta a punta**: pádel, sábado 22, 19:00–20:00, Cancha 1, $18.000 → la fila quedó
  en `bookings` como `PendingPayment` bajo `chaco-for-ever`, y el token en
  `clubspot.chaco-for-ever.tokensReserva`.
- **Dos clubes a la vez.** Se sembró `club-de-prueba` en la base de desarrollo: `/club-de-prueba`
  abrió el portal con su propio nombre y sus 0 canchas, contra el mismo dev server y el mismo
  bundle. Con una reserva sembrada en cada club, **"Mis reservas" de uno mostró Cancha 1 y la del
  otro Cancha 2** — que es exactamente lo que hoy no se cumplía. El club de prueba se borró
  después; la base quedó como estaba.
- La vuelta del pago con `?retorno=<guid>` limpia la query y **conserva el segmento del club**.
- `npm run typecheck` limpio (`tsc -b --force`), `npm run build` verde, y **`chaco-for-ever` ya no
  aparece en `dist/assets/*.js`**. Los clientes de Orval quedaron idénticos: el `git status` que
  aparece después del build es sólo fin de línea, con `git diff` vacío.

**Actualizado además**: `scripts/dev-up.ps1` y el README del portal ahora imprimen la URL con el
club adentro.

**Lo que no entra en esta fase**, y sigue como estaba: el guard de `vite.config.ts` que hace
fallar el build sin `VITE_API_URL` (F3), el script de build (F4) y el fallback de SPA (F5). Hasta
que exista F5, un F5 del navegador sobre `/<slug>` depende de que el hosting reescriba a
`index.html`; el dev server de Vite ya lo hace solo.

## 21/08/2026 — El escape del guard queda fijado, y el plan deja de estar bloqueado

El plan listaba como *decisión que bloquea la ejecución* si el guard de `vite.config.ts` acepta
un escape para compilar en modo producción contra la API local. El usuario pidió que se lo
explicara y quedó claro que **estaba mal catalogado**: es un detalle de implementación de una
sola fase, no una decisión de producto, y elevarlo a bloqueante frenaba el plan entero por algo
que tiene una respuesta obvia.

Queda fijado en el plan (sección 5, decisión 9): el guard acepta `VITE_ALLOW_LOCAL_API=1`.
`npm run build && npm run preview` contra `localhost:5037` —ver el bundle real, minificado,
antes de publicarlo— es un uso legítimo y frecuente. Sin escape hay que comentar el guard a
mano, y un guard comentado a mano no se descomenta nunca. Sacar el chequeo de `localhost` no es
opción: es el defecto que este plan arregla.

Con eso, **ninguna de las decisiones que quedan bloquea la ejecución**. Las seis restantes son
de despliegue —dominio, entorno de test, proveedor de pagos del test, hosting, quién crea el
primer club, previews— y lo que bloquean es compilar y verificar el `dist/` real, no escribir
ni ejecutar las fases. El estado del plan pasa a **listo para ejecutar**.

## 21/08/2026 — Corrección del usuario: no existe un build por club

La primera versión del plan proponía dejar `VITE_CLUB_SLUG` horneado en el build y marcarlo como
deuda, con el argumento de que hoy hay un solo club sembrado y que resolverlo en runtime no
acercaba el primer deploy. El usuario lo vetó, y es la decisión de fondo de este plan:

> *"la aplicación es multiclub, es un frontend para todos los clubes, no entiendo esto de
> vite_club_slug… ¿como que voy a compilar el fronte para cada club? esto es una locura, la
> aplicación es multi club, ahora vamos a instalar todo para un club, pero después en otro y
> así"*

Tiene razón. Hornear el slug convierte un producto que se vende a muchos clubes en un artefacto
por cliente: cada alta de club pasaría a ser un build, un despliegue y un sitio más. El
argumento de "hoy hay un solo club" mide el costo del día uno e ignora el del día dos, que es
justamente el que define si el producto escala.

Lo llamativo es que **el argumento ya estaba escrito en el repo**:
[ADR-0018](adr/0018-sesion-del-backoffice-token-en-sessionstorage-y-rol-en-la-claim.md) descartó
`VITE_CLUB_SLUG` para el backoffice con las palabras *"ata el frontend a un club por build y no
aporta nada que `users.tenantId` no diga mejor"*. El plan lo citó como precedente y **igual
recomendó lo contrario para el portal**. La regla estaba; faltó aplicarla donde correspondía.

**Qué cambia en el plan:**

- `VITE_CLUB_SLUG` **se elimina**, no queda como deuda. Un default de club es lo que hace que un
  error de ruteo se vea como "el portal anda", mostrando el club equivocado.
- **El club sale del primer segmento del path** (decisión del usuario del 21/08/2026):
  `reservas.<dominio>/<slug>`. Un dominio, un certificado, y dar de alta un club nuevo no toca
  infraestructura ni recompila nada. Va a **ADR-0020**.
- Se descartaron el subdominio con certificado comodín y el dominio propio por club. El segundo
  **no se descarta para siempre**: se puede agregar encima del club por path, con una tabla
  host → slug, sin recompilar el portal.
- El plan pasa de seis fases a siete: entra **F2 — el portal deja de saber de un solo club**, y
  el resto se corre un lugar.
- El script de build pierde el parámetro `-ClubSlug`: el bundle sirve a todos los clubes.
- El fallback de SPA deja de ser comodidad y pasa a ser requisito: sin rewrite, `/<slug>` es 404
  del hosting y el portal no abre para nadie.
- Lo que **no** cambia es `VITE_API_URL`: sigue horneándose, porque es una variable **por
  entorno, no por club**. Esa es toda la diferencia entre lo que se mantiene y lo que se tira.

**Qué se midió al revisar el alcance real del cambio.** La buena noticia es que el problema
estaba acotado al frontend del portal:

- **La API ya es multi-club**: `/api/portal/{clubSlug}/…` recibe el slug como segmento de ruta y
  `clubs.slug` tiene índice único.
- **El backoffice ya es multi-club**: el club sale de `users.tenantId`, firmado en el token.
- **Fuera del portal, `chaco-for-ever` sólo aparece en `DevSeeder.cs` y en los tests de
  integración**, que es donde corresponde.
- Lo que ata el portal son **tres** cosas, no una: el slug horneado (`config.ts:2`), el `<title>`
  con el nombre de un club adentro (`index.html:7`) y **las dos claves de `localStorage` sin
  separar por club** (`myBookings.ts:8`, `bookingTokens.ts:8`) — con dos clubes en el mismo
  origen, las reservas de uno aparecen en el otro. Esa tercera no estaba en el radar y es la que
  habría dado un bug de datos cruzados el día del segundo club.
- **`PortalCatalogResponse.club.name` ya viaja en el contrato**, así que el `<title>` sale del
  catálogo sin tocar el backend ni regenerar clientes.
- **El `returnUrl` no necesita cambios**: `window.location.origin + window.location.pathname` ya
  incluye el segmento del club, así que el comprador vuelve solo al portal de su club, y
  `AllowedReturnOrigins` compara esquema y host, no el path.

## 21/08/2026 — Plan escrito, sin ejecutar

El plan queda **escrito y esperando decisiones**. No se tocó ni una línea de código: las fases
están sin arrancar.

**Qué se relevó**, todo verificado contra el árbol en `b5f047d`:

- **Los dos `dist/` que hay en disco tienen `localhost:5037` adentro.** Medido sobre
  `backoffice/dist/assets/index-BYbusAEb.js` y `reservas/dist/assets/index-BLKyrLZD.js`. No es
  un riesgo teórico: es el estado actual de los dos artefactos.
- **El `??` de `config.ts` no atrapa la cadena vacía**, que es exactamente lo que deja una
  variable recién creada y sin valor en el panel de un hosting.
- **El acoplamiento con el backend son cinco claves, no una**: `Cors:AllowedOrigins`,
  `Payments:AllowedReturnOrigins`, `Payments:PortalBaseUrl`, `Payments:PublicBaseUrl` y
  `Payments:ApiBaseUrl`. Cada una rompe algo distinto y ninguna avisa.
- **`Payments:AllowedReturnOrigins` viaja con los dos `localhost` en el `appsettings.json`
  versionado**, así que el guard de arranque *pasa* en producción con valores de desarrollo y el
  síntoma aparece recién al confirmar una reserva: 422, que el portal muestra como *"No se pudo
  confirmar la reserva"*.
- **`Payments:PortalBaseUrl` no está en ningún `appsettings`**: sólo como default de código
  apuntando a `http://localhost:5183`. Es de donde salen el QR y el link de WhatsApp del cobro
  en mostrador.
- **El guard de CORS pregunta `IsProduction()`**, así que un `Staging` hereda los puertos de
  desarrollo.
- **`reservas/vercel.json` y `reservas/.vercelignore` se contradicen**: el primero declara
  `installCommand`/`buildCommand` —el host construye— y el segundo excluye `dist`, que es lo
  único que habría que subir si el host no construye.
- **El build de los frontends no necesita el SDK de .NET**: `docs/api/clubspot.openapi.json` y
  los dos clientes generados por Orval están versionados. Lo que sí necesita es **el repo entero
  clonado**, porque `orval.config.ts` lee tres niveles para arriba con `clean: true`.
- **`@types/node` no está instalado en ninguna de las dos apps**, y los dos `tsconfig.json`
  declaran `"types": ["vite/client"]` e incluyen `vite.config.ts` en la compilación. Como
  `npm run build` es `tsc -b && vite build`, un guard que use `process` rompe el build antes de
  que Vite arranque. Es la trampa que hizo falta medir para que la fase no naciera rota.
- **Los dos README de frontend describen un mock que se borró el 19/08/2026**, y el del portal
  recomienda literalmente servir `dist/` en cualquier hosting estático — que es la instrucción
  que produce el defecto principal. El README de la raíz enseña `pnpm` en un repo con
  `package-lock.json`, y pide "Node 20+" cuando Vite 7.3.6 exige `^20.19.0 || >=22.12.0` (la
  máquina corre v24.11.1).
- **`Jwt:SigningKey` firma también los tokens de reserva del portal**
  (`PortalBookingToken.cs:30`), no sólo las sesiones del backoffice: rotarla vacía "Mis
  reservas" de todos los clientes.
- **`FakePaymentProvider` arma el checkout como `{ApiBaseUrl}/dev/checkout`, y esa ruta se mapea
  sólo en Development.** Un entorno de test con `Provider=fake` entrega una URL que el propio
  host responde con 404.
- **En producción no hay migración ni seed**, así que no existe ningún club. Es bloqueante
  externo, no de este plan.

**Cómo se armó.** Cinco relevamientos en paralelo (documento de infraestructura, los dos
frontends, el borde del backend, el estilo de los planes del repo y un pase de abogado del
diablo), tres propuestas de diseño independientes con lentes distintas —mínimo, a prueba de
fallos silenciosos, y producto multi-club—, y dos pases finales de síntesis y de crítica de
completitud.

**Vale anotar el error de método**, porque es el que corrigió el usuario al día siguiente: de
las tres propuestas, la de lente *producto multi-club* era la que tenía razón, y la síntesis la
descartó por cara aplicando la regla de "implementar lo mínimo". Esa regla es para recortar lo
especulativo, no para postergar un requisito del producto que ya estaba escrito en `AGENTS.md`
§5. Un requisito no es alcance opcional aunque hoy haya un solo cliente.

**Lo que se descartó y sigue descartado:**

- **Configuración en runtime de la URL de la API** (`config.json` al lado del `dist/`). Sólo
  paga cuando haya que promover un mismo `dist/` entre entornos; hoy recompilar desde el mismo
  commit alcanza. Va como alternativa descartada dentro de ADR-0019, con su razón escrita, para
  que el próximo agente no lo lea como un descuido.
- **Un auditor del `dist/` en Node, aparte del script.** Sería la tercera capa sobre el mismo
  defecto. Vuelve a hacer falta el día que un host corra `vite build` por su cuenta, o sea junto
  con CI.
- **Un chequeo post-deploy contra los dominios reales.** No hay dominio contra el cual correrlo.
