# Bitácora — plan de logging estructurado

Registro de avance del [plan](plan-logging.md). La entrada más nueva arriba.

## 21/08/2026 — Revisión de código: seis correcciones, cinco de ellas por hallazgos verificados

Se corrió el pipeline de `code-reviewer` sobre el PR (4 finders + verificadores). El verificador de
comportamiento del logging **confirmó 6 de 7** hallazgos y refutó uno. Lo corregido:

- **El 500 salía sin `tenant` ni `userId`** (confirmado). El middleware que los empujaba está *debajo*
  del manejador de excepciones, así que sus ámbitos de `LogContext` ya estaban cerrados cuando se
  escribía la línea del error — justo la que más necesita nombrar el club. Se cambió el mecanismo:
  los dos valores van a `HttpContext.Items` y los lee un `HttpContextEnricher` al escribir el evento.
  Se descartó mover `app.UseExceptionHandler()` más abajo, que era la otra salida: los errores de
  CORS, del rate limiter y de JwtBearer dejarían de pasar por ProblemDetails.
- **Ninguna línea del portal ni de los webhooks llevaba `tenant`** (confirmado, y probado contra el
  `.jsonl` que dejó la propia corrida de tests: de 5 avisos de pago huérfano, **4 sin club**). Esas
  superficies resuelven el club en `ClubScope`, un filtro de endpoint que ningún middleware ve. El
  mismo enricher lo cubre, con una línea en `ClubScope`. Era el agujero más caro: el aviso de plata
  huérfana es la línea de más valor que agrega el PR y llegaba sin poder filtrarse por club.
- **Las dos líneas de falla de J2 quedaban fuera del ámbito de `tenant`** (confirmado). El push estaba
  dentro de `RunForTenantAsync`, así que "Reconciliation failed" salía sólo con `Tenant` (PascalCase,
  de la plantilla). Se movió al `foreach` y se sacó `{Tenant}` de las plantillas: un dato, un nombre.
- **Una falla de arranque no dejaba línea** (confirmado la mitad de arranque; la de flush en apagado
  fue refutada, porque `dispose: true` ya cubre el apagado ordenado y los sinks no bufferean). Se
  instala un manejador de `AppDomain.UnhandledException` que escribe `Fatal` y hace flush. **No** se
  envolvieron los `Program.cs` en `try/catch`: el host de tests aborta el arranque con una excepción
  centinela que espera tragarse, y un `catch` ahí registraría una caída falsa por cada clase de test.
- **Un sink que no puede escribir fallaba en silencio** (confirmado). `SelfLog` encendido en
  Development.
- **La suite escribía sus líneas en el archivo que el ADR designa para diagnosticar** (confirmado, y
  medido: 89 pares de arranque/apagado de host en el `.jsonl` de la corrida). `ApiFactory` apunta
  `Diagnostics:LogDirectory` al temporal.

**Refutado y no tocado:** que el override `Microsoft → Warning` silencie los rechazos del rate limiter
y los fallos de autenticación. El verificador mostró que `main` ya tenía `Microsoft.AspNetCore` en
`Warning`, así que los dos casos citados ya estaban en silencio antes del PR: no es una regresión.
Queda anotado igual, porque **la API no tiene access log** y eso es una decisión, no un olvido.

**Dónde quedó / próximo paso:** build sin warnings, 92 unitarios y 95 de integración en verde. Sigue
faltando la verificación en vivo.

## Estado por fase

| Fase | Contenido | Estado |
|---|---|---|
| F1 | Serilog en los dos hosts, destinos por entorno | ✅ 21/08/2026 |
| F2 | Contexto en cada línea: `tenant`, `requestId`, `userId` | ✅ 21/08/2026 |
| F3 | Los tres caminos que fallaban en silencio | ✅ 21/08/2026 |
| F4 | ADR-0019, AGENTS.md, plan y bitácora | ✅ 21/08/2026 |

Leyenda: ⬜ pendiente · 🚧 en curso · ✅ terminada.

---

## 21/08/2026 — Las cuatro fases, en el mismo worktree que la plata huérfana

**Decisión del usuario:** sólo Serilog, sin rastreador de errores por ahora. Se le plantearon tres
opciones (sólo Serilog, + Sentry, + GlitchTip autohospedado) y eligió la primera. Queda anotado en el
ADR como pendiente y no como descartado: es lo único de la lista que **avisa**, y sin él un 500 con
el proceso vivo no le llega a nadie.

**Lo que se verificó antes de escribir el ADR**, contra la documentación de Render, porque el usuario
preguntó qué se ve si el hosting es ése: retención de logs de 7/14/30 días según plan, búsqueda por
texto y regex con filtros de la plataforma —nivel, método, status, ruta—, notificaciones por mail,
Slack o webhook ante deploy fallido o servicio caído, y **ninguna alerta por error de aplicación**.
También que el filesystem de un servicio es efímero salvo disco persistente, que es lo que decidió
que el sink de archivo fuera **sólo de Development**.

**Cómo quedó**

- **Un solo archivo decide el logging**: `ClubSpotLogging.cs` en Infrastructure, con
  `AddClubSpotLogging(application)` llamado en la primera línea de los dos `Program.cs`. Antes de leer
  las cadenas de conexión, a propósito: si el arranque falla ahí, la falla deja una línea.
- **Serilog no fue a un proyecto propio.** Se evaluó contra la regla de vendors de AGENTS.md §6 y se
  descartó con razón escrita en el ADR: esa regla es para gateways y servicios externos, que traen un
  contrato de negocio ajeno. Serilog no habla con nadie y no expone un tipo suyo fuera de ese archivo
  y de los tres `PushProperty`.
- **`Logging:LogLevel` se sacó de los `appsettings.json`.** Al reemplazar los proveedores, esa sección
  deja de tener efecto, y configuración muerta engaña a quien la lee. Entró `Serilog:MinimumLevel` en
  los dos hosts y en el `.example`.
- **La clave del directorio de logs no vive en la sección `Serilog`** sino en `Diagnostics:LogDirectory`,
  para no meter una clave nuestra dentro de una sección que parsea otra biblioteca.
- **`tenant` se llama igual en la Api y en el JobService.** Es lo que hace que un solo filtro lea los
  dos procesos, y por eso el JobService lo empuja en el despachador aunque ya lo tuviera en el texto
  del mensaje.
- **De la persona va el id y nada más.** Nombre, email y teléfono no entran a un log: para diagnosticar
  alcanza el identificador, y lo que no está escrito no se filtra.

**Los tres silencios.** Los dos de concurrencia quedaron en `Information` —no está pasando nada malo,
pero es la única explicación de un 409 que el llamador no puede reconstruir—. El de plata huérfana
quedó en `Warning` y **en un solo lugar**: dentro del `RecordPayment` local, por donde pasan los cinco
motivos, en vez de repetir la línea en las cinco ramas que marcan huérfano.

**Verificación:** `dotnet build` sin warnings, 92 unitarios y 95 de integración en verde. El documento
OpenAPI salió idéntico, así que no hubo clientes que regenerar.

**Dónde quedó / próximo paso:** las cuatro fases cerradas. Falta **la verificación en vivo** —levantar
los dos hosts y confirmar que el `.jsonl` aparece con `tenant` y `requestId` en las líneas—, que
necesita el entorno levantado. Y queda anotado el rastreador de errores, para decidir junto con el
hosting.
