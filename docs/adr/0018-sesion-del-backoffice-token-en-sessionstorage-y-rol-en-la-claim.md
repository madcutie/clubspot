# ADR-0018 — La sesión del backoffice es el JWT: token en `sessionStorage` y rol leído de la claim

**Fecha:** 20/08/2026 · **Estado:** Aceptada

## Contexto

El backoffice nunca tuvo login. `http.ts` inicia sesión solo contra `/api/auth/session` con las
credenciales de desarrollo que están escritas en `config.ts`, guarda el token en una variable de
módulo y, ante un 401, vuelve a loguearse con las mismas credenciales. Es decir: **cualquiera que
abra `:5184` es el administrador del club**, la sesión se pierde en cada recarga y no hay forma de
salir.

Eso alcanzaba mientras el backoffice era un cascarón de demo. Ya no: el primer rol que tiene que
entrar de verdad es el **canchero** (`CourtReception`), que opera la agenda del día pero no
configura canchas ni horarios. Y si hay canchero, hay administrador: son dos consolas distintas
sobre el mismo código.

El backend ya tiene todo lo que hace falta (ADR-0003): tablas propias, `POST /api/auth/session` que
devuelve un JWT de 12 h, y cuatro políticas por rol —`agenda.operate`, `people.view`,
`people.manage`, `configuration.edit`— aplicadas endpoint por endpoint. Lo que falta es del lado del
navegador.

La pregunta que abrió esta decisión fue **de dónde saca el frontend el rol del operador**. Estaba
sobre la mesa agregarle a `GET /api/context` un campo con los permisos efectivos, calculado desde las
mismas políticas de la API. El usuario lo rechazó de plano: *"el frontend se entera sacando info de
la claim, no puedo preguntar a un contexto qué rol tiene"*. Tiene razón —la respuesta ya viajó, está
firmada dentro del token que el frontend acaba de recibir— y esta decisión registra eso y lo que se
desprende.

## Decisión

**1. El login no pide club: lo pide el usuario que se está logueando.**

`POST /api/auth/session` pasa a recibir **email y contraseña, nada más**. El club sale de
`users.tenantId`, que ya existe: cada usuario del backoffice pertenece a un club y lo sabe. No hay
campo "club" en la pantalla ni slug en la configuración del despliegue.

De eso se desprenden dos cosas que hay que imponer, no suponer:

- **El email es único en toda la instalación**, no por club: el índice único de `users` pasa de
  `(tenantId, email)` a `(email)`. Si el mismo email existiera en dos clubes, el login sería
  ambiguo y no habría forma honesta de resolverlo sin volver a preguntar el club. La invariante se
  impone en la base, no con un cartel (AGENTS.md §6).
- **La búsqueda del usuario al loguearse ignora el filtro global de tenant**, porque todavía no hay
  tenant: es el único lugar del sistema que lo hace y entra en la lista blanca auditada. Está
  acotado a un método —`IUserRepository.FindForSignInAsync`— que sólo se usa desde el endpoint de
  sesión, y que devuelve el usuario con su `TenantId` adentro: a partir de ahí todo vuelve a estar
  bajo tenant.

**2. El JWT *es* la sesión. El frontend no le pregunta al servidor quién es.**

`POST /api/auth/session` devuelve un token que lleva el nombre del operador, sus roles y el
vencimiento. El backoffice decodifica el payload y de ahí sale todo lo que necesita saber del
operador. No hay endpoint de "quién soy".

**3. `GET /api/context` deja de devolver el operador.**

Como consecuencia directa, `ContextResponse.Operator` se elimina. El contexto pasa a ser lo que
siempre debió ser: **datos del club** (nombre, sede) y **módulos contratados** —lo que sí es del
servidor y el token no puede saber—. Con eso, `Role` deja de viajar como JSON: su única
representación externa es la claim.

**4. El token vive en `sessionStorage`.**

Sobrevive el F5 y toda la jornada mientras la pestaña siga abierta; se pierde al cerrar el navegador.
La máquina del mostrador es compartida entre turnos y un token de 12 h que sobreviva al cierre del
navegador queda a disposición del turno siguiente.

**5. Las claims son cortas, estables y en camelCase: `name`, `role`, `tenant`, `sub`.**

La claim es **contrato con el frontend**, no un detalle de implementación de `JwtIssuer`. Por eso:

- Se emiten con nombre corto en vez de las URIs de `ClaimTypes` (`http://schemas.microsoft.com/...`),
  que dependen del mapeo implícito de `JwtSecurityTokenHandler`.
- El valor del rol va en **camelCase** (`courtReception`), igual que cualquier otro enum que cruza la
  frontera (AGENTS.md §6). El nombre de cable lo produce un único helper que usan tanto el emisor
  como las políticas de autorización, así que no hay dos formas del mismo rol.
- La validación se configura explícita: `MapInboundClaims = false`, `NameClaimType = "name"`,
  `RoleClaimType = "role"`. Lo que viaja es lo que se lee, sin traducción en el medio.

**6. El frontend decodifica el token sin verificar la firma, a propósito.**

Leer el payload en el navegador **no es un control de seguridad**: es para dibujar la consola. Quien
autoriza es la API, que valida firma y vencimiento en cada request. Un token adulterado en el
navegador cambia lo que se dibuja, no lo que se puede hacer: la API responde 403.

**7. Lo que el rol no puede usar no existe para él.**

Los módulos que el rol no puede operar no aparecen en la navegación, y entrar a la URL a mano
redirige a la primera pantalla que sí puede ver. Mismo criterio que los módulos no contratados
(ADR-0009): no se muestra apagado, no está.

**8. El rol se llama en inglés en el código; "canchero" es la etiqueta de la UI.**

`Role.CourtReception` en el código, la claim y la base (ADR-0006). *Canchero*, *administrador*,
*tesorería* son textos en español que sólo existen en la capa de presentación.

## Consecuencias

- **El mapa rol → pantallas vive en el frontend** y es una segunda escritura de una regla que ya está
  en `AuthorizationPolicies`. Se acota a un único archivo (`src/auth/permisos.ts`) que declara que es
  **de presentación**: apaga botones, no autoriza nada. La autorización sigue siendo del backend, y
  una acción que se escape del mapa termina en 403, no en un permiso de más.
- **Sin refresh token.** A las 12 h la sesión cae; el operador vuelve a entrar. Un turno de mostrador
  entra cómodo.
- **El logout es local.** Se borra el token del navegador; no hay lista de revocación en el servidor.
  Un token robado sigue siendo válido hasta que vence. Aceptable para el MVP; si hace falta
  revocación, es un ADR nuevo.
- **Cambia el contrato de dos endpoints**: `POST /api/auth/session` pierde `club` y `GET /api/context`
  pierde `operator`. Se regenera el documento OpenAPI y los clientes de los dos frontends (ADR-0016).
  El portal de reservas no consume ninguno de los dos.
- **La misma persona no puede ser usuaria de dos clubes.** Es el precio de que el login no pregunte
  club, y hoy no hay ningún caso: el backoffice de un club lo opera gente de ese club. Si algún día
  aparece, la salida no es aflojar el índice sino agregar el paso de elegir club cuando el email
  matchea más de uno — y eso es un ADR nuevo.
- **La migración del índice puede fallar en una base ya poblada** si dos clubes comparten un email.
  Hoy la única instalación es la de desarrollo, con un club; en cualquier otra, hay que mirar antes.
- `IClubDirectory.FindClubIdBySlugAsync` deja de participar del login. Sigue en uso para el portal
  de reservas (`ClubScope`, que sí resuelve el club por slug de ruta) y para el despachador de jobs.
- El backoffice queda con **dos usuarios de desarrollo** en el seed: administrador y canchero.

## Alternativas descartadas

| Alternativa | Por qué no |
|---|---|
| **`/api/context` devuelve los permisos efectivos** | Rechazada explícitamente por el usuario: la información ya viaja firmada en el token que el frontend acaba de recibir. Un round-trip extra para preguntar algo que ya se tiene |
| **Campo "club" en la pantalla de login** | Le pide al operador un dato que el sistema ya tiene. Entra 200 veces por semana al mismo club |
| **Slug del club por configuración del despliegue (`VITE_CLUB_SLUG`)** | Era el supuesto de trabajo hasta que el usuario definió que el club sale del usuario. Ata el frontend a un club por build y no aporta nada que `users.tenantId` no diga mejor |
| **Cookie `httpOnly`** | Es lo correcto desde seguridad —el token queda fuera del alcance de JavaScript—, pero obliga a CORS con credenciales, defensa CSRF y a tocar también el portal de reservas. No se descarta para siempre; hoy no paga el costo |
| **`localStorage`** | Más cómodo (el operador no vuelve a entrar nunca), peor en una PC compartida: el token queda disponible para el turno siguiente |
| **Claims con las URIs de `ClaimTypes`** | Funciona, pero deja el nombre de la claim a merced del mapeo implícito del handler y obliga al frontend a leer `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` |
| **Mostrar las pantallas prohibidas deshabilitadas** | Le enseña al canchero que el sistema hace cosas que él no puede tocar, sin ganancia operativa. Además los GET de canchas y horarios también piden `configuration.edit`: la pantalla quedaría vacía |
