# Plan — Logging estructurado

**Fecha:** 21/08/2026 · **Estado:** ejecutado y verificado · Avance en la
[bitácora](plan-logging.bitacora.md)

Decisiones de fondo en [ADR-0019](adr/0019-logging-estructurado-y-diagnostico.md). Este documento
es el alcance y las fases.

## 1. De dónde salió

Al listar los pendientes para salir a producción, el usuario preguntó: *"pero si podemos agregar
logging, tenemos? es decir, logging de errores"*. La respuesta al medirlo contra el código fue: hay
lo que trae ASP.NET de fábrica y **siete llamadas explícitas en todo el backend**. Las excepciones no
manejadas quedan registradas; nada más lo está.

De ahí salieron dos preguntas más del usuario, que también quedaron respondidas en el ADR:

- **Por qué Sentry** — porque un log y un rastreador de errores no resuelven lo mismo: el log dice
  qué pasó cuando alguien va a mirarlo; el rastreador avisa. Se pospuso.
- **Qué se ve si el hosting es Render** — logs con búsqueda y filtros (7/14/30 días según plan),
  notificación si el servicio se cae o falla el deploy, y **ninguna alerta por error de aplicación**.
  Ahí un 500 con el proceso sano no le llega a nadie.

También quedó anotado el ítem viejo de `TODO.md`: *"necesitamos logs de las cosas que van pasando
fácilmente accesible por llm, entonces es fácil el troubleshooting"*. El archivo `.jsonl` de
Development es eso.

## 2. Alcance

**Entra**: Serilog debajo de `ILogger` en los dos hosts · destinos por entorno · contexto
(`application`, `requestId`, `method`, `path`, `tenant`, `userId`) · los tres caminos que hoy fallan
en silencio · el ADR y las reglas en AGENTS.md.

**No entra, a propósito**:

| Qué | Por qué |
|---|---|
| Sentry / GlitchTip | Decisión del usuario (21/08/2026): sólo Serilog por ahora. Es lo único que avisa solo, y se retoma al elegir hosting |
| Logging en los dos frontends | Un error de React muere en la consola del navegador del canchero. Es un frente aparte y va con el rastreador de errores |
| Métricas y pantalla de operación | Es la parte de observabilidad de AGENTS.md §9.1, que sigue pendiente |
| Log de tráfico HTTP request por request | Ya se descartó una vez (bitácora del activity log, 19/08): lo que hacía falta era la crónica del negocio, no un log de requests |
| Retención y purga del archivo local | Lo resuelve la rotación: 7 archivos diarios y 64 MB por archivo |

## 3. Fases

### F1 — Serilog en los dos hosts

`ClubSpot.Infrastructure/Observability/ClubSpotLogging.cs` con `AddClubSpotLogging(application)`,
llamado en la primera línea de cada `Program.cs` —antes de leer una cadena de conexión, para que una
falla de arranque también deje una línea—. Consola legible + archivo `.jsonl` en Development, consola
JSON en cualquier otro entorno. `Logging:LogLevel` sale de los `appsettings.json` y entra
`Serilog:MinimumLevel`, para no dejar configuración muerta. `logs/` y `*.jsonl` al `.gitignore`.

### F2 — Contexto en cada línea

`RequestLogContextMiddleware` (primero en el pipeline, antes del manejador de excepciones) empuja
`requestId`, `method` y `path`. `TenantResolutionMiddleware` empuja `tenant`; `ActivityActorMiddleware`
empuja `userId`, sólo el id. En el JobService, el despachador de J2 empuja `tenant` **con el mismo
nombre de campo**, para que un solo filtro lea los dos procesos.

### F3 — Los caminos que fallan en silencio

| Dónde | Qué pasaba | Nivel |
|---|---|---|
| `CreateAsync`, violación de exclusión | Dos personas compraron el mismo turno a la vez y la respuesta es un 409 sin explicación | `Information` |
| `ApplyPaymentAsync`, violación de unicidad | Dos notificaciones del mismo pago en carrera; una se descarta | `Information` |
| Todo pago que queda huérfano | Plata que el club tiene sin turno, y hasta ahora sólo en la crónica | `Warning` |

La tercera se puso **en un solo lugar** —dentro de `RecordPayment`, por donde pasan los cinco
motivos— en vez de en las cinco ramas que marcan huérfano.

### F4 — Documentación

ADR-0019 con las decisiones, incluida la lista de lo que nunca va a un log y la frontera contra el
`activityLog` de ADR-0017. Reglas nuevas en AGENTS.md §6. Este plan y su bitácora.

## 4. Verificación

- `dotnet build` sin warnings · 92 unitarios y 95 de integración en verde.
- El documento OpenAPI **no cambia**: nada de esto toca el contrato, así que no hay clientes que
  regenerar.
- Falta la verificación en vivo: levantar los dos hosts y confirmar que el archivo `.jsonl` aparece
  con `tenant` y `requestId` en las líneas. Necesita el entorno levantado.

## 5. Lo que queda anotado

- **El rastreador de errores.** Sin él, un 500 con el proceso sano no le llega a nadie. Se decide al
  elegir el hosting.
- **Los frontends no reportan nada.**
- **El nivel de EF Core queda en `Warning`.** Si alguna vez hace falta ver las consultas, se sube por
  configuración y se vuelve a bajar — no se deja puesto.
