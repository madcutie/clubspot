# Bitácora — plan de build de frontends por entorno

Registro de avance del [plan](plan-build-frontends-por-entorno.md). La entrada más nueva arriba.

## 21/08/2026 — Plan escrito, sin ejecutar

El plan queda **escrito y esperando decisiones**. No se tocó ni una línea de código: las seis
fases están sin arrancar y las dos primeras decisiones de la sección 5 bloquean la ejecución.

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
  versionado**, así que el guard de arranque *pasa* en producción con valores de desarrollo y
  el síntoma aparece recién al confirmar una reserva: 422, que el portal muestra como
  *"No se pudo confirmar la reserva"*.
- **`Payments:PortalBaseUrl` no está en ningún `appsettings`**: sólo como default de código
  apuntando a `http://localhost:5183`. Es de donde salen el QR y el link de WhatsApp del cobro
  en mostrador.
- **El guard de CORS pregunta `IsProduction()`**, así que un `Staging` hereda los puertos de
  desarrollo.
- **El backoffice rutea por path con `BrowserRouter`** y no hay ningún fallback de SPA
  versionado: un F5 sobre `/canchas` es 404 del hosting.
- **`reservas/vercel.json` y `reservas/.vercelignore` se contradicen**: el primero declara
  `installCommand`/`buildCommand` —el host construye— y el segundo excluye `dist`, que es lo
  único que habría que subir si el host no construye.
- **El build de los frontends no necesita el SDK de .NET**: `docs/api/clubspot.openapi.json` y
  los dos clientes generados por Orval están versionados. Lo que sí necesita es **el repo
  entero clonado**, porque `orval.config.ts` lee tres niveles para arriba con `clean: true`.
- **`@types/node` no está instalado en ninguna de las dos apps**, y los dos `tsconfig.json`
  declaran `"types": ["vite/client"]` e incluyen `vite.config.ts` en la compilación. Como
  `npm run build` es `tsc -b && vite build`, un guard que use `process` rompe el build antes de
  que Vite arranque. Es la trampa que hizo falta medir para que F2 no naciera rota.
- **Los dos README de frontend describen un mock que se borró el 19/08/2026**, y el del portal
  recomienda literalmente servir `dist/` en cualquier hosting estático — que es la instrucción
  que produce el defecto principal. El README de la raíz enseña `pnpm` en un repo con
  `package-lock.json`, y pide "Node 20+" cuando Vite 7.3.6 exige `^20.19.0 || >=22.12.0` (la
  máquina corre v24.11.1).
- **`Jwt:SigningKey` firma también los tokens de reserva del portal** (`PortalBookingToken.cs:30`),
  no sólo las sesiones del backoffice: rotarla vacía "Mis reservas" de todos los clientes.
- **`FakePaymentProvider` arma el checkout como `{ApiBaseUrl}/dev/checkout`, y esa ruta se mapea
  sólo en Development.** Un entorno de test con `Provider=fake` entrega una URL que el propio
  host responde con 404. Pasó a ser la decisión 5 del plan.
- **En producción no hay migración ni seed**, así que el club no existe y el portal desplegado
  responde 404 desde el primer día. Es bloqueante externo, no de este plan; quedó en la
  sección 7 para que no aparezca como sorpresa.

**Cómo se armó.** Cinco relevamientos en paralelo (documento de infraestructura, los dos
frontends, el borde del backend, el estilo de los planes del repo y un pase de abogado del
diablo), tres propuestas de diseño independientes con lentes distintas —mínimo, a prueba de
fallos silenciosos, y producto multi-club—, y dos pases finales de síntesis y de crítica de
completitud. Gana el esqueleto mínimo, con injertos puntuales de los otros dos: la
normalización que atrapa la cadena vacía, el escape declarado para probar contra la API local,
los dos formatos de rewrite versionados y los guards de loopback del backend.

**Lo que se descartó, y por qué**, para que no se vuelva a proponer:

- **Resolver el club en runtime** (por path o por hostname) en vez de hornearlo. Es la solución
  correcta al problema que va a existir —ADR-0018 ya rechazó la misma variable para el
  backoffice— pero toca más de diez archivos, cambia la URL de desarrollo del portal y arrastra
  el catálogo de 16 casos E2E, todo para un producto que hoy tiene un solo club sembrado. Queda
  como deuda marcada, no como olvido.
- **Configuración en runtime** (`config.json` al lado del `dist/`). Sólo paga cuando haya que
  promover un mismo `dist/` entre entornos; hoy recompilar desde el mismo commit alcanza. Va
  como alternativa descartada dentro de ADR-0019, con su razón escrita, para que el próximo
  agente no lo lea como un descuido.
- **Un auditor del `dist/` en Node, aparte del script.** Sería la tercera capa sobre el mismo
  defecto. Vuelve a hacer falta el día que un host corra `vite build` por su cuenta, o sea junto
  con CI.
- **Un chequeo post-deploy contra los dominios reales.** No hay dominio contra el cual correrlo.

**Lo que queda pendiente antes de poder arrancar:** las decisiones 1 y 2 de la sección 5 del
plan —un club o multi-club, y si se acepta el escape `VITE_ALLOW_LOCAL_API=1`—. Las otras seis
no bloquean la ejecución de las fases, sólo la compilación del `dist/` real.
