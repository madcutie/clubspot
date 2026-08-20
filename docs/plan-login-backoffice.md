# Plan — Login del backoffice, empezando por el canchero

**Fecha:** 20/08/2026 · **Estado:** escrito, **esperando aprobación** · Avance en la
[bitácora](plan-login-backoffice.bitacora.md)

Cierra el ítem *"Login de verdad"* de `AGENTS.md` §10 e implementa
[ADR-0018](adr/0018-sesion-del-backoffice-token-en-sessionstorage-y-rol-en-la-claim.md). Se trabaja
en el worktree `login-backoffice`, por pedido del usuario: la rama `main` está ocupada con otra cosa.

## 1. El defecto, medido

`src/frontend/backoffice/src/api/http.ts` hace auto-login contra `/api/auth/session` con las
credenciales que están escritas en `src/api/config.ts`:

```ts
export const DEV_EMAIL = import.meta.env.VITE_DEV_EMAIL ?? 'admin@chacoforever.test';
export const DEV_PASSWORD = import.meta.env.VITE_DEV_PASSWORD ?? 'clubspot-dev';
```

De ahí salen cuatro problemas concretos:

1. **Cualquiera que abra `:5184` entra como administrador del club.** No hay pantalla de login ni
   forma de salir.
2. **La sesión no sobrevive la recarga**: el token vive en una variable de módulo.
3. **Los cuatro módulos se montan siempre.** Un canchero vería Canchas y Horarios en la
   navegación, entraría, y la API le devolvería 403 en cada request: pantalla rota en vez de
   pantalla ausente.
4. **Un 401 se reintenta con las credenciales de desarrollo**, así que un token vencido se renueva
   solo como administrador.

Lo que **ya está** y no hay que construir: `POST /api/auth/session` (slug de club + email +
contraseña → JWT de 12 h), las cuatro políticas por rol aplicadas endpoint por endpoint, y el enum
`Role` con los siete roles operativos.

## 2. Qué tiene que poder hacer cada uno

Las políticas de `AuthorizationPolicies.cs` ya definen el reparto. Traducido a pantallas:

| Pantalla | Política de la API | Administrator | CourtReception (*canchero*) |
|---|---|---|---|
| Reservas (agenda del día) | `agenda.operate` | ✅ | ✅ |
| Personas — buscar y ver ficha | `people.view` | ✅ | ✅ |
| Personas — alta, bloqueo, nota, pago | `people.manage` | ✅ | ❌ |
| Canchas | `configuration.edit` | ✅ | ❌ |
| Horarios | `configuration.edit` | ✅ | ❌ |

El canchero es entonces una consola de **dos módulos**: Reservas y Personas en lectura. Los otros
cinco roles del enum (`MemberDesk`, `Treasury`, `AccessControl`, `Coach`, `Member`) no tienen
pantalla propia todavía; el mapa los contempla pero hoy sólo derivan de las políticas que ya existen.

**El club no se pregunta en el login** (definición del usuario, 20/08/2026): cada usuario del
backoffice pertenece a un club y ya lo lleva encima en `users.tenantId`. La pantalla pide email y
contraseña; `POST /api/auth/session` pierde el campo `club`. Detalle y consecuencias en
[ADR-0018](adr/0018-sesion-del-backoffice-token-en-sessionstorage-y-rol-en-la-claim.md) §1 — de acá
salen dos cambios que no son de UI: **el email pasa a ser único en toda la instalación** y **la
búsqueda del usuario al loguearse ignora el filtro de tenant**, porque todavía no hay tenant.

## 3. Fases

Fases chicas, cada una verificable sola. **Ninguna arranca sin el OK del usuario.**

---

### F1 — Backend: el login es sólo email y contraseña

El club sale del usuario. Es la fase que toca la base, así que va sola.

**Cambios**

| Archivo | Qué |
|---|---|
| `Api/Endpoints/AuthEndpoints.cs` | `SignInRequest` pierde `Club`. Ya no se resuelve el club por slug: se busca el usuario por email y el tenant sale de `user.TenantId` |
| `Application/Core/Users/IUserRepository.cs` | `FindByEmailAsync` → `FindForSignInAsync(email)`: **cruza tenants a propósito**, con el comentario de una línea que lo explica |
| `Infrastructure/Repositories/UserRepository.cs` | `IgnoreQueryFilters()` — el único lugar del sistema que lo hace |
| `Infrastructure/Persistence/Configurations/UserConfiguration.cs` | El índice único pasa de `(tenantId, email)` a `(email)`: `uxUsersTenantIdEmail` → `uxUsersEmail` |
| `Infrastructure/Persistence/Migrations/` | Migración del índice |

**Tests**

- Se entra sin mandar `club` y el token sale con el `tenant` del usuario.
- Dos clubes, un email en cada uno: cada uno entra al suyo. Un tercer usuario con el email de otro
  club **no se puede insertar** (lo impide la base, no el código).
- Email inexistente y contraseña mala dan el mismo 401 genérico.

**Criterio de aceptación:** tests verdes y el documento OpenAPI regenerado ya sin `club` en
`SignInRequest`.

---

### F2 — Backend: claims de sesión y el usuario del canchero

Que el token sea legible por el frontend y que haya un canchero con quién entrar.

**Cambios**

| Archivo | Qué |
|---|---|
| `Api/Auth/RoleNames.cs` *(nuevo)* | Nombre de cable del rol en camelCase (`courtReception`). Único lugar que lo produce |
| `Api/Auth/JwtIssuer.cs` | Emite `sub`, `tenant`, `name` y una claim `role` por rol, con nombres cortos en vez de las URIs de `ClaimTypes` |
| `Api/Auth/AuthorizationPolicies.cs` | `RequireRole` pasa a usar `RoleNames`, no `Role.ToString()` |
| `Api/Program.cs` | `MapInboundClaims = false`, `NameClaimType = "name"`, `RoleClaimType = "role"`. Se quita el converter JSON de `Role`, que queda sin uso |
| `Api/Endpoints/ContextEndpoints.cs` | Se elimina `Operator` de `ContextResponse` (ADR-0018 §3) |
| `Api/Seed/DevSeeder.cs` | Segundo usuario: `reception@chacoforever.test` / `clubspot-dev`, rol `CourtReception`, nombre visible "Canchero" |

Los lectores de `sub` ya tienen el fallback `?? user.FindFirstValue("sub")`, así que apagar el mapeo
de entrada no los rompe. `TenantResolutionMiddleware` lee `"tenant"` literal: tampoco.

**Tests** (`ClubSpot.IntegrationTests/Auth/AuthenticationTests.cs`)

- El token del canchero trae `name` y `role = courtReception`.
- El canchero opera la agenda (200) y **no** configura canchas ni horarios (403).
- El administrador hace las dos cosas (200).

**Criterio de aceptación:** `dotnet build` sin warnings, tests verdes, y el documento OpenAPI
regenerado ya sin `OperatorResponse`.

---

### F3 — Frontend: la sesión y la pantalla de login

**Archivos nuevos** — `src/frontend/backoffice/src/auth/`

| Archivo | Qué |
|---|---|
| `sesion.ts` | Lee y escribe el token en `sessionStorage`, decodifica el payload (`name`, `role`, `exp`) y descarta el token vencido al leerlo. Sin verificar firma, a propósito (ADR-0018 §6) |
| `SesionContexto.tsx` | Provider de React: `sesion`, `entrar(email, clave)`, `salir()`. Es lo único que escribe la sesión |
| `../modulos/sesion/LoginScreen.tsx` | Pantalla: email, contraseña, error, estado cargando. Con los tokens de `ui/theme.ts` |

**Archivos que cambian**

| Archivo | Qué |
|---|---|
| `api/config.ts` | Fuera `DEV_CLUB`, `DEV_EMAIL` y `DEV_PASSWORD`. Queda sólo `API_URL` |
| `api/http.ts` | Deja de auto-loguearse. Adjunta el token si hay; si no, manda la request sin `Authorization` (el login es anónimo). Un 401 borra la sesión y avisa; no reintenta. La firma `api<T>(path, init)` no cambia: es el mutator de Orval (ADR-0016) |
| `api/personasHttp.ts` | `fetchClub` deja de leer `ctx.operator`; el nombre y el rol del operador salen de la sesión |
| `domain/types.ts` | `Club` pierde `operador`, `operadorIniciales` y `rol` |
| `main.tsx` / `App.tsx` | `SesionProvider` envuelve la app; sin sesión se renderiza `LoginScreen` |

**Criterio de aceptación:** se entra con los dos usuarios; F5 mantiene la sesión; cerrar el navegador
la pierde; un 401 devuelve al login; credenciales malas muestran *"Email o contraseña incorrectos"*
sin revelar si el email existe.

---

### F4 — La consola del canchero: navegación y rutas por rol

| Archivo | Qué |
|---|---|
| `auth/permisos.ts` *(nuevo)* | Mapa rol → capacidades de UI (`operarAgenda`, `verPersonas`, `gestionarPersonas`, `configurar`). Comentario de una línea: **es de presentación, no autoriza nada** |
| `App.tsx` | La navegación se arma desde los permisos; las rutas prohibidas redirigen a la primera pantalla permitida |
| `ui/Navegacion.tsx` | El botón del pie deja de ser un aviso: muestra nombre y rol del operador (de la sesión) y **Salir**, con ícono `LogOut` de lucide |

**Criterio de aceptación:** el canchero ve Reservas y Personas y nada más; escribir `/canchas` a mano
lo devuelve a Reservas; el administrador sigue viendo las cuatro.

---

### F5 — Acciones por permiso dentro de las pantallas

El canchero entra a Personas pero no puede gestionar. Se apagan —no se ocultan a medias— las acciones
de `people.manage`: alta de mostrador, bloquear, agregar nota, registrar pago, importar.

**Criterio de aceptación:** ninguna acción que el canchero pueda tocar termina en 403.

---

### F6 — Verificación y documentación

- Recorrida en el navegador con los dos usuarios, de punta a punta.
- `npm run typecheck` en el backoffice, `dotnet build` y `dotnet test` en el backend.
- `AGENTS.md`: sale *"Login de verdad"* de §10 *Lo que falta*, entra la sesión en §10 *Cómo está
  armado*, y ADR-0018 al índice de `docs/adr/README.md`.
- Bitácora al día.

> Por pedido del usuario, **el build y los tests no se corren hasta que él lo indique**.

## 4. Lo que este plan deja afuera

| Fuera de alcance | Por qué |
|---|---|
| Recuperar contraseña, cambiar contraseña, invitar usuarios | Es ABM de usuarios; no hay pantalla ni endpoints todavía |
| Refresh token y revocación | ADR-0018: sesión de 12 h, logout local. Si hace falta, es un ADR nuevo |
| Gating por **módulo contratado** (el otro ítem de §10) | Es otra regla —404, no 403— y otro origen: `/api/context` ya devuelve los módulos. Va después, y encaja en el mismo `App.tsx` |
| Login del portal de reservas | Es la etapa 3 de [`plan-reserva-online.md`](plan-reserva-online.md), con otro tipo de usuario (el socio) |
| Los cinco roles sin pantalla propia | `MemberDesk`, `Treasury`, `AccessControl`, `Coach` y `Member` entran cuando tengan qué operar |
