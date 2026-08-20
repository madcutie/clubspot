# Bitácora — Login del backoffice

Registro de avance del plan [`plan-login-backoffice.md`](plan-login-backoffice.md).

**Regla de uso:** el agente que trabaje sobre el plan actualiza este archivo **al terminar cada
bloque de trabajo**, no al final de la sesión. Cada entrada va arriba de las anteriores, con fecha,
qué se hizo, qué decisiones se tomaron sobre la marcha, y un cierre explícito de **"dónde quedó /
próximo paso"**. La tabla de estado se mantiene al día.

## Estado por fase

| Fase | Contenido | Estado |
|---|---|---|
| Plan | ADR-0018 + este plan + bitácora | ✅ 20/08/2026 |
| F1 | Backend: login sólo con email y contraseña; email único global + migración; búsqueda cruzando tenants | ✅ 20/08/2026 |
| F2 | Backend: claims cortas en camelCase, `RoleNames`, contexto sin `operator`, usuario canchero en el seed | ✅ 20/08/2026 |
| F3 | Frontend: `auth/sesion.ts`, `LoginScreen`, `http.ts` sin auto-login | ✅ 20/08/2026 |
| F4 | Navegación y rutas por rol; Salir en la barra lateral | ✅ 20/08/2026 |
| F5 | Acciones de `people.manage` apagadas para el canchero | ✅ 20/08/2026 |
| F6 | Verificación en navegador + build/tests + documentación | 🚧 build y tests verdes, doc al día; falta el navegador |

Leyenda: ⬜ pendiente · 🚧 en curso · ✅ terminada.

---

## Entradas

### 20/08/2026 — F3 a F5: la consola se dibuja según el token

**Frontend**

- `auth/sesion.ts`: store del token en `sessionStorage` + decodificación del payload sin verificar
  firma. Expuesto a React con `useSyncExternalStore`, así que **no hace falta un provider**: el
  snapshot es puro y la limpieza del token vencido pasa en `tokenActual()`, que corre fuera de todo
  render.
- `auth/permisos.ts`: espejo de `AuthorizationPolicies.cs`, con el comentario que dice que es de
  presentación. `rutaInicial()` resuelve dónde cae cada rol.
- `modulos/sesion/LoginScreen.tsx` y `SinAcceso.tsx`. El segundo cubre el caso que rompía todo si no
  existía: un usuario válido cuyo rol no opera nada —un socio, un profesor— entraría a un redirect
  infinito. Ahora ve un cartel y Salir.
- `api/http.ts` dejó de loguearse solo: adjunta el token si hay y, ante un 401 **con** token, cierra
  la sesión. Sin token el 401 es el login que falló y lo maneja la pantalla.
- `api/config.ts` quedó en una línea: se fueron `DEV_CLUB`, `DEV_EMAIL` y `DEV_PASSWORD`.
- `App.tsx` monta sólo las rutas del rol; la ruta que no toca redirige a la inicial. `Navegacion`
  muestra nombre y rol del token, con **Salir** que además hace `queryClient.clear()` — la máquina
  del mostrador pasa de un turno al otro y el cache no puede quedar del operador anterior. Lo mismo
  al entrar.
- `useAgenda`, `useCanchas`, `useHorarios` y `usePersonas` tomaron un `habilitado`: sin permiso no
  se pide, así los contadores de la barra lateral no disparan requests que van a dar 403.
- Personas: alta, importación, bloqueo (masivo e individual), nota y registro de pago sólo aparecen
  con `people.manage`. El pie de la ficha se esconde entero, para no dejar una barra vacía.

**Verificación hecha:** `dotnet build` sin warnings · 79 tests unitarios y 72 de integración verdes,
con los 7 de auth confirmados por nombre · documento OpenAPI regenerado (sin `club` en
`SignInRequest`, sin `OperatorResponse` ni `Role`) · clientes de los dos frontends regenerados ·
`tsc --noEmit` limpio en backoffice y en el portal.

**Dónde quedó / próximo paso:** falta **la recorrida en el navegador** con los dos usuarios, que es
lo único de F6 pendiente. El puerto `:5184` estaba ocupado por el dev server del backoffice de
`main`, así que hay que liberarlo (o correr este en otro puerto, pero la política de CORS de la Api
tiene `5184` y `5183` fijos en `Program.cs`).

**Rebase sobre `main` (20/08/2026):** el usuario pusheó `main` mientras tanto, que quedó en
`098c83a` con el trabajo de cancelación con motivo —y **dos migraciones nuevas**—. El rebase salió
sin conflictos, pero el snapshot del modelo y el documento OpenAPI son archivos generados y un
auto-merge textual ahí no prueba nada. Se verificó así: el snapshot se restauró desde `origin/main`,
se borró la migración propia y se **regeneró encima**, de modo que `UserEmailGlobalUnique` queda
última en la cadena (`20260820030551`) y su Designer refleja el modelo completo. El OpenAPI
regenerado resultó idéntico al auto-mergeado, y los clientes de los dos frontends se regeneraron
otra vez. Build sin warnings, **82 unitarios y 75 de integración verdes**, `tsc` limpio en ambos.

---

### 20/08/2026 — F1 y F2 escritas (backend), sin verificar

El usuario confirmó **email único global** después de plantearle el trade-off: el caso que lo puede
morder no es un canchero en dos clubes sino él mismo necesitando un usuario en cada club que
soporte, y ahí la salida es `dario+club@…` o el paso de elegir club, que ya está anotado como ADR
futuro en las consecuencias de ADR-0018.

Las dos fases se escribieron juntas porque las dos son backend y comparten el build.

**F1 — el login es sólo email y contraseña**

- `AuthEndpoints`: `SignInRequest` perdió `Club`; ya no se inyectan `IClubDirectory` ni
  `ITenantScopeFactory`. Se hashea igual cuando el email no existe, y la contraseña se verifica
  antes de mirar `IsActive`, para que el endpoint no sirva de oráculo de emails ni de cuentas
  desactivadas.
- `IUserRepository.FindByEmailAsync` → `FindForSignInAsync`, con `IgnoreQueryFilters()` en la
  implementación y el comentario que declara por qué.
- `UserConfiguration`: índice único `(tenantId, email)` → `(email)`.

**F2 — claims y el canchero**

- `ClubSpotClaims` (`sub`, `tenant`, `name`, `role`) y `RoleNames.Wire()` (camelCase) como únicos
  lugares que producen la forma del token; `JwtIssuer` y `AuthorizationPolicies` los usan a los dos.
- `Program.cs`: `MapInboundClaims = false`, `NameClaimType`, `RoleClaimType`; se quitó el
  `JsonStringEnumConverter<Role>`, que ya no tiene qué serializar.
- `ContextEndpoints`: fuera `Operator`. El contexto queda en club + módulos.
- Los cuatro lectores de `sub` dejaron de tener la rama `ClaimTypes.NameIdentifier`, que con el
  mapeo apagado no se ejecuta nunca; ahora leen `ClubSpotClaims.Subject`.
- `DevSeeder`: usuario `reception@chacoforever.test` / `clubspot-dev`, rol `CourtReception`.
- Tests reescritos: token del canchero con `name` y `role = courtReception`; el club sale del
  usuario y no del request; el mismo email en dos clubes lo rechaza la base; email inexistente y
  contraseña mala devuelven exactamente la misma respuesta; el canchero opera agenda y personas
  (200) y no toca canchas ni horarios (403).

**Dónde quedó / próximo paso:** falta **generar la migración del índice** (`dotnet ef migrations
add`), que necesita compilar, y correr build + tests. El usuario pidió que nada de eso corra sin que
lo indique. Después de eso se regenera el OpenAPI y los clientes, y recién ahí arranca F3 (frontend),
que depende de los tipos nuevos.

---

### 20/08/2026 — Se decide de dónde sale el rol, y se escribe el plan

**Disparador:** el usuario pidió la implementación del login del backoffice, *"empecemos por el
canchero"*.

**Decisiones del usuario, en el orden en que las tomó:**

1. **La sesión vive en `sessionStorage`** — sobrevive el F5 y la jornada, se pierde al cerrar el
   navegador. La máquina del mostrador es compartida.
2. **El rol sale de la claim del JWT, no de un endpoint.** Rechazó la propuesta de que
   `GET /api/context` devolviera los permisos efectivos: *"el frontend se entera sacando info de la
   claim, no puedo preguntar a un contexto qué rol tiene"*. De ahí se desprendió sacarle `operator`
   al contexto: la información ya viaja firmada en el token.
3. **Lo que el rol no puede usar no existe para él** — no aparece en la navegación y la URL redirige.
4. **Si hay canchero, hay administrador**: son dos consolas sobre el mismo código, no una sola con
   un caso especial.
5. **El rol no se llama "canchero" en el código** — `Role.CourtReception` en código, claim y base
   (ADR-0006); *canchero* es la etiqueta en español de la UI.
6. **Se trabaja en un worktree** (`login-backoffice`, rama del mismo nombre, creada desde el HEAD
   local `06bea50`): `main` está ocupada con otra cosa. Al terminar se borra.

Todo eso quedó escrito en
[ADR-0018](adr/0018-sesion-del-backoffice-token-en-sessionstorage-y-rol-en-la-claim.md) antes de
tocar código.

7. **El club no se pregunta en el login** (respuesta a la única pregunta que había quedado abierta):
   *"el login se elige con un campo en el admin user, va a haber un admin por tenant, entonces ese
   va a tener info del club al que pertenece"*. El club sale de `users.tenantId`, que ya existe. Se
   descartó tanto el campo en pantalla como el slug por configuración del despliegue, que era el
   supuesto con el que se había escrito el plan.

   Eso arrastra dos cambios que no son de UI y por eso F1 pasó a ser una fase sola:
   **el email es único en toda la instalación** (índice `(tenantId, email)` → `(email)`, con
   migración) y **la búsqueda del usuario al loguearse ignora el filtro global de tenant**, porque
   en ese momento todavía no hay tenant. Es el único lugar del sistema que lo hace.

**Hallazgos del relevamiento previo, que condicionan F1:**

- Los tres lectores de `sub` del backend ya tienen el fallback `?? user.FindFirstValue("sub")`, así
  que apagar `MapInboundClaims` no los rompe. `TenantResolutionMiddleware` lee `"tenant"` literal.
- El único consumidor de `ContextResponse.Operator` es `fetchClub` en `personasHttp.ts`, que lo usa
  para el pie de la barra lateral. Sacarlo toca ese archivo, `domain/types.ts` y `Navegacion.tsx`.
- Ya hay un `JsonStringEnumConverter<Role>` registrado en `Program.cs`; al salir `operator` del
  contexto, `Role` deja de viajar como JSON y el converter queda sin uso.
- `Club` no está alcanzado por el filtro global de tenant: `ClubDirectory` lo consulta sin ámbito
  abierto y funciona. Por eso el login de hoy podía resolver el slug antes de tener tenant.
- Nadie llama a `signIn` a mano: en los dos frontends existe sólo en el cliente generado, y el
  portal de reservas no lo usa. Sacarle el campo `club` no rompe código escrito.

**Dónde quedó / próximo paso:** el plan está escrito y **esperando el OK del usuario**. Cuando lo
dé, arrancar por **F1** y marcarla 🚧 acá antes de tocar código. No correr `dotnet build` ni
`dotnet test` hasta que el usuario lo pida.
