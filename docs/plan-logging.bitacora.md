# Bitácora — plan de logging estructurado

Registro de avance del [plan](plan-logging.md). La entrada más nueva arriba.

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
