# ADR-0017 — Registro de actividad (`activityLog`): un solo registro para el operador y para la auditoría

**Fecha:** 19/08/2026 · **Estado:** Aceptada

## Contexto

La sección 9.1 de AGENTS.md tiene pendiente una parte llamada **Auditoría**: *"quién, cuándo y
por qué en cada transición de estado. Es requisito, no un extra"*. Al ir a diseñarla apareció
que el requisito real es más ancho que una auditoría:

- **El canchero también necesita ver qué pasó.** No es un registro que mira un auditor una vez
  al año: es la respuesta a "¿quién canceló este turno?", parado en el mostrador, con el
  cliente enfrente. Un registro pensado sólo para auditoría no se muestra, y uno pensado sólo
  para la pantalla no sirve para rendir cuentas.
- **Hay dos clases de origen.** Están las **acciones de usuarios del sistema** (el canchero
  canceló, el tesorero anuló un recibo) y los **eventos que llegan solos**: un webhook de
  Mercado Pago que entra, un hold que vence, el job J2 que concilia. Saber *cuándo entró el
  webhook* es tan operativo como saber quién canceló.

Estado al momento de decidir: no existe ningún registro. Los hechos se pierden. Un pago
huérfano (`ApprovedOrphan`) queda en `payments` sin ninguna traza de por qué llegó tarde, una
reserva cancelada no dice quién la canceló, y la expiración perezosa de un hold no deja rastro
de cuándo ocurrió porque no la ejecuta nadie: se descubre al leer.

El sistema de referencia (OurClub) no tiene nada equivalente, y es una de las razones por las
que su operación depende de la memoria de las personas.

## Decisión

Se construye un **registro de actividad**, `activityLog`: una crónica append-only de hechos del
negocio, multi-tenant, escrita por todos los módulos y leída tanto por el operador como por la
auditoría.

### 1. Un solo registro, no dos

El registro del canchero y el de auditoría son **la misma tabla con dos vistas**: el operador
ve lo suyo filtrado y escrito en castellano; la auditoría es el mismo registro sin filtrar. Dos
registros separados se desincronizan y entonces ninguno de los dos sirve para rendir cuentas.

Es la misma regla que ya rige en el resto del sistema: **una definición, una respuesta**.

### 2. El actor puede ser una persona o el sistema

Lo que distingue "el canchero canceló un turno" de "entró un webhook" no es la naturaleza del
hecho: es quién lo causó. Cada entrada lleva:

- `actorUserId` — el usuario, **nulo cuando el actor es el sistema**.
- `actorName` — **una foto del nombre al momento del hecho**, no un join. El registro tiene que
  leerse igual dentro de cinco años, aunque el usuario se haya renombrado o dado de baja.
- `source` — por dónde entró el hecho: `counter`, `portal`, `webhook`, `job`, `system`.

`source` no es decoración: es lo que permite responder "esto lo hizo alguien o pasó solo", que
es la primera pregunta cuando algo salió mal.

### 3. Nunca se guarda la frase en castellano

Cada entrada guarda un **`type`** (un código estable, en inglés como todo el código) más los
datos estructurados que ese tipo necesita. **El texto en castellano lo arma el frontend**, igual
que ya se hace con los errores de la API (AGENTS.md §6).

Una frase guardada en español no se puede volver a dibujar si cambia el diseño, no se puede
filtrar por tipo y no se puede traducir. El código sí.

### 4. Los `type` son inmutables

Un `type` ya emitido **nunca cambia de significado**. Si el hecho cambia, se agrega un tipo
nuevo. Cambiarle el sentido a uno viejo reescribe el pasado sin tocar una sola fila, que es la
única forma de corromper un registro append-only.

Cada módulo declara **su propio catálogo** de tipos; la tabla guarda un string. Así `core` no
necesita conocer el vocabulario de `bookings`, y un módulo nuevo no obliga a tocar el núcleo.

### 5. La entrada se escribe en la misma transacción que el hecho

Igual que la fila del outbox. Si el hecho no se confirmó, no hay entrada; si el hecho se
confirmó, hay entrada. No existe el estado "pasó pero no quedó registrado".

Consecuencia directa: el puerto de escritura **no hace `SaveChanges` propio**, exactamente como
`IPeopleLink`.

### 6. Append-only: una corrección es una entrada nueva

No se edita ni se borra una entrada. Si algo se registró mal, se registra la corrección. Misma
regla que los movimientos de dinero.

La única salida de una entrada es la **purga por retención** (J11), que borra por antigüedad,
nunca por contenido.

### 7. El motivo es obligatorio en lo destructivo

Cancelar un turno, anular un recibo y bloquear una ficha **exigen motivo**; el resto de los
hechos no lo piden. El sistema de referencia no lo pide en ningún lado, y por eso nadie sabe
nunca por qué se canceló nada.

El motivo es texto libre del operador y viaja en su propia columna, no dentro de los datos:
es lo primero que se lee.

### 8. Pertenece a `core`, y todos los módulos escriben en él

`activityLog` es del núcleo, que no se puede apagar. El puerto `IActivityLog` se declara en
`Application/Core/Activity/` y lo consume cualquier módulo — dirección permitida, porque el
grafo de módulos ya dice que todos dependen de `core`.

**No es la contabilidad.** `payments` sigue siendo el asiento del dinero (ADR-0014); el registro
de actividad lo referencia y cuenta la historia alrededor. Tampoco es el outbox: el outbox es
*qué hay que mandar*, esto es *qué pasó*.

### 9. Referencias tipadas además de los datos

Cada entrada lleva `bookingId`, `personId` y `paymentId` nullables, además del payload. Sin
ellas, "mostrame la historia de este turno" obliga a buscar dentro de un `jsonb`, que no se
puede indexar razonablemente ni garantizar. Con ellas es un índice.

### 10. El registro arranca el día que se despliega

**No se rellena hacia atrás.** Las reservas y los pagos que ya existen no tienen historia y no
se les va a inventar una. Un registro con filas fabricadas deja de ser un registro.

## Consecuencias

- El canchero puede responder "quién y cuándo" sin llamar a nadie, y la operación puede ver
  cuándo entró un webhook — hoy invisible salvo leyendo el log de la aplicación.
- Todo hecho nuevo obliga a decidir su `type` y qué datos guarda. Es trabajo por hecho, a
  propósito: si registrar fuera gratis, se registraría cualquier cosa y el registro sería ruido.
- **Volumen.** Es la tabla que más crece. Por eso la retención se define desde el principio
  (J11) en vez de descubrirse cuando la base duela.
- El payload `jsonb` **no lo verifica el compilador**. Un tipo que cambia sus datos sin cambiar
  su `type` rompe la pantalla en runtime. Se mitiga con la regla 4 y con que el frontend
  degrade a mostrar el código crudo ante un tipo que no conoce, en vez de romperse.
- Escribir en la misma transacción **agranda las transacciones de escritura**. Es el precio de
  no tener hechos sin registro; medible, y aceptado.
- La lectura del registro necesita permisos: quien puede ver el sujeto puede ver su actividad;
  la pantalla general pide rol administrativo. Se define en el plan.

## Alternativas descartadas

- **Dos registros separados, uno para el operador y otro para auditoría.** Es la alternativa
  que más se propone sola, porque las audiencias parecen distintas. Se descarta porque en seis
  meses no coinciden, y entonces el de auditoría deja de ser confiable justo cuando hace falta.
- **Guardar la frase ya escrita en castellano.** Más simple de dibujar, imposible de filtrar,
  de rediseñar y de traducir. Además congela el idioma dentro de la base, contra ADR-0006.
- **Usar el log de la aplicación** (el que hoy escribe `ILogger`). No es un registro del
  negocio: rota, no está particionado por tenant, no se puede mostrar a un operador y nadie
  garantiza que sobreviva. Sirve para diagnosticar, no para rendir cuentas.
- **Triggers de base o tablas temporales de PostgreSQL.** Capturan el *qué* con exactitud, pero
  nunca el *quién* en términos de negocio ni el *por qué*, y atan el registro al esquema físico:
  un `ALTER TABLE` cambiaría la forma del pasado.
- **Event sourcing** (derivar el estado de los eventos). Es un cambio de arquitectura entero,
  no un registro. Acá el estado sigue siendo la fuente de verdad y el registro va al lado.
- **Llamarlo `trafficLog`.** "Traffic" en software significa tráfico de red: pedidos, latencia,
  códigos de estado. El nombre confunde en el primer minuto — de hecho lo hizo. Queda
  `activityLog`.
