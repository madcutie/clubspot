# ADR-0019 — Logging estructurado con Serilog: JSON a la consola, contexto en cada línea

**Fecha:** 21/08/2026 · **Estado:** Aceptada

## Contexto

El sistema tiene hoy el logging que trae ASP.NET de fábrica: `ILogger` de Microsoft con el proveedor
de consola, niveles en `Logging:LogLevel`, y **siete llamadas explícitas en todo el backend** —cuatro
`LogWarning`, un `LogError` y un `LogInformation`—. Las excepciones no manejadas sí quedan
registradas, porque `UseExceptionHandler` las escribe antes de devolver el 500.

Eso alcanza mientras todo corre en una máquina y hay una ventana por servicio a la vista. Deja de
alcanzar en el primer despliegue, por tres razones concretas:

1. **La línea no dice de quién es.** Un mensaje como `Reconciliation failed for tenant …` es el único
   que nombra su club; el resto no dice ni el club, ni el usuario, ni el request. Con un club es
   ruido; con dos, es la diferencia entre leer un log y adivinar.
2. **El texto no se puede filtrar por campo.** Un log de consola en texto plano se busca con grep. El
   explorador de logs de cualquier proveedor de hosting filtra por sus propios metadatos —nivel, ruta,
   status— y no por los datos del sistema.
3. **Hay caminos que fallan en silencio.** Tres `catch` del backend traducen un error de PostgreSQL a
   un resultado de negocio y no dejan rastro técnico: la violación de exclusión que responde "el turno
   ya se vendió", la de unicidad que descarta una notificación repetida, y la que convierte un pago
   confirmado en un huérfano por `slotLost`. La última es plata cobrada que el club tiene sin turno.

También hace falta separar dos cosas que se confunden. El **registro de actividad** (ADR-0017) es la
crónica del negocio: se escribe siempre, se lee para operar y es fuente de verdad de nada, pero se
consulta. El **log** es diagnóstico: se lee para averiguar por qué algo se rompió, y puede
desaparecer sin que el negocio pierda nada. Mezclarlos termina de las dos maneras malas — o el log se
vuelve una tabla que nadie borra, o un dato que la operación necesita vive en un archivo rotativo.

## Decisión

**1. Serilog debajo de `ILogger`, no en lugar de él.**

El código sigue escribiendo `logger.LogWarning(...)` con las abstracciones de Microsoft. Serilog es el
proveedor: decide formato y destino. Ningún archivo del dominio ni de Application conoce a Serilog.

**2. El destino depende del entorno, y sólo hay dos.**

| Entorno | Destinos |
|---|---|
| Development | consola legible para una persona **+ archivo `logs/<app>-<fecha>.jsonl`**, rotación diaria, 7 archivos |
| Cualquier otro | consola en JSON compacto, y nada más |

El archivo existe **sólo** en Development. En cualquier hosting de contenedores el filesystem es
efímero —lo que se escribe se pierde en el próximo reinicio o deploy—, así que un sink de archivo ahí
sería un archivo que nadie va a leer. Lo que recoge los logs en producción es el proveedor, leyendo
la salida estándar del contenedor; por eso ahí va JSON, que es lo que se puede filtrar por campo.

El archivo de Development tiene un segundo motivo: es lo que un agente puede abrir y filtrar para
diagnosticar sin que nadie le copie y pegue una ventana de consola.

**3. Cada línea lleva el contexto de quién la produjo.**

Empujado con `LogContext` donde cada dato ya se resuelve, para que ningún llamador tenga que
acordarse de pasarlo:

| Campo | Dónde se empuja |
|---|---|
| `application` | fijo por host: `api` o `jobs` |
| `requestId`, `method`, `path` | `RequestLogContextMiddleware`, primero en el pipeline |
| `tenant` | `TenantResolutionMiddleware` en la Api, el despachador de J2 en el JobService |
| `userId` | `ActivityActorMiddleware`, sólo el id |

`tenant` se llama igual en los dos procesos a propósito: un solo filtro lee la Api y los jobs.

**4. Lo que nunca va a un log.**

Contraseñas y hashes · tokens, JWT y claves de firma · el access token o el secreto del webhook de
cualquier proveedor de pago · el cuerpo completo de un webhook · datos personales más allá de un id
—nombre, email, teléfono, documento—. Un log es diagnóstico: para saber qué pasó alcanza con los
identificadores, y lo que no está escrito no se filtra.

**5. El log no es el registro de actividad, y no lo reemplaza.**

Un hecho de negocio va al `activityLog` (ADR-0017) aunque además se loguee. Una falla técnica va al
log y no al `activityLog`. Cuando los dos corresponden —un pago que queda huérfano es un hecho de
negocio *y* algo que alguien tiene que encontrar mientras diagnostica— se escriben los dos, y la
fuente para operar sigue siendo la crónica.

**6. Los niveles se configuran, no se compilan.**

Los valores por defecto están en código; la sección `Serilog` de la configuración los pisa
(`Serilog.Settings.Configuration`). Así un entorno puede bajar el ruido de un namespace o subir el
detalle de otro sin volver a desplegar, que es lo que evita que una ventana de retención se llene de
líneas que a nadie le importan. La sección `Logging:LogLevel` de Microsoft **deja de tener efecto** y
por eso se sacó de los `appsettings.json`: configuración muerta es peor que configuración ausente.

**7. Sin servicio de rastreo de errores, por ahora.**

Un log responde "¿qué pasó?" cuando alguien va a mirarlo. No responde "¿alguien me avisa?": si la API
devuelve 500 con el proceso vivo, el health check pasa, el deploy sigue verde y nadie se entera. Eso
lo resuelve un rastreador de errores (Sentry, GlitchTip), y **queda anotado como pendiente**, no
descartado. Se posterga porque es una cuenta más y un secreto más, y porque hasta que el club dependa
del sistema para cobrar, el log alcanza. La decisión de contratarlo o autohospedarlo se toma cuando
se elija el hosting.

## Consecuencias

- **Un archivo nuevo decide todo el logging**: `ClubSpot.Infrastructure/Observability/ClubSpotLogging.cs`,
  con la extensión `AddClubSpotLogging(application)` que llaman los dos hosts en su primera línea
  —antes de leer una cadena de conexión, para que una falla de arranque también deje una línea—.
- Serilog vive en `ClubSpot.Infrastructure` y no en un proyecto propio. La regla de aislar SDKs de
  vendor (AGENTS.md §6) es para **gateways y servicios externos**, que traen un contrato de negocio
  ajeno; Serilog no habla con nadie: se enchufa debajo de una abstracción que ya se usa, y no expone
  ni un tipo suyo fuera de ese archivo y de los tres `LogContext.PushProperty`.
- `Logging:LogLevel` desaparece de los `appsettings.json` de los dos hosts y de
  `appsettings.Development.json.example`, reemplazada por `Serilog:MinimumLevel`.
- `logs/` y `*.jsonl` van al `.gitignore`.
- Los tres caminos silenciosos dejan de serlo, con el nivel que les corresponde: `Information` para
  los dos que son concurrencia normal, `Warning` para todo pago que queda huérfano —una sola línea,
  puesta donde pasan los cinco motivos, en vez de cinco líneas repartidas—.

## Alternativas descartadas

**Dejar el `ILogger` de fábrica y sólo agregar llamadas.** No resuelve ninguno de los tres problemas:
las líneas siguen sin contexto y sin campos, y el proveedor de hosting sigue viendo texto.

**Un sink de archivo también en producción.** Requiere un disco persistente —que en la mayoría de los
hostings se paga— para guardar algo que el proveedor ya está guardando, y que nadie va a leer por
`ssh` cuando hay un explorador de logs con búsqueda.

**Serilog en su propio proyecto de Infrastructure.** Se evaluó por la regla de vendors y se descartó:
esa regla existe para que el SDK de un servicio externo no se filtre por toda la solución. Acá el
"vendor" es un proveedor de una abstracción que ya está en uso, y el aislamiento que da un proyecto
aparte ya lo da un archivo.

**Mercado Pago, EF Core y las consultas SQL en el log.** `Microsoft.EntityFrameworkCore` queda en
`Warning`. El log de cada consulta es útil una tarde y carísimo el resto del tiempo, y en producción
es además una vía por la que datos personales terminan en un archivo que nadie revisó.
