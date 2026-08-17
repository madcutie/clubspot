# ADR-0013 — Disponibilidad: patrón semanal pisado por excepciones con fecha y alcance

**Fecha:** 16/08/2026 · **Estado:** Aceptada, con dos puntos abiertos anotados al final

## Contexto

El modelo de horarios no podía expresar los casos reales de operación de un club. La forma que
tenía —`schedules` con `weeklyRanges` y `specialDates` en jsonb, y `courts` con un FK al
horario— falla así:

- `specialDates` guarda **una fecha por entrada**: "del 19 al 25 de junio, la cancha 1 de 12 a
  17" son siete entradas dentro de un array, y deshacerlo es sacar siete elementos del jsonb.
- La excepción vive en el **horario, que varias canchas comparten**. Cerrar una sola cancha
  obliga a darle horario propio, y a partir de ahí todo lo que valga para el club hay que
  cargarlo en dos lugares que se desincronizan solos.
- El jsonb **crece sin techo y no se consulta por fecha**: para dibujar la semana que viene hay
  que abrir y expandir el blob de cada horario. Las excepciones viejas se siguen leyendo para
  siempre.
- Cargar una excepción es reescribir el horario entero con su token de versión, así que el
  canchero que cierra una cancha y el operador que edita las horas del martes **se pisan entre
  ellos** sin tener nada que ver.

El usuario planteó el problema con la analogía de Calendly, que resultó estructural: la cancha
es el recurso reservable, el horario semanal son las *weekly hours*, las excepciones son las
*date-specific hours*, y el usuario final elige entre los huecos que quedan.

Dos definiciones del usuario simplifican el modelo más que cualquier otra cosa:

1. **"Siempre dibujo hacia adelante; lo que pasó, ya pasó."**
2. **Los feriados se cargan a mano**, y por lo tanto no son un concepto: son una excepción más.

## Decisión

La disponibilidad de una cancha en una fecha se **calcula**, y sale de dos fuentes:

**1. El patrón semanal (`schedules`).** Reusable y con nombre, compartido por varias canchas,
con varios rangos por día; un día sin rangos es un día cerrado. `weeklyRanges` **sigue siendo
jsonb**: es chico, acotado a siete días, se lee y se escribe entero y nunca se consulta por
tramo. Se le quitan dos columnas: `specialDates`, que pasa a la tabla nueva, y `timeZone`, que
pertenece al club.

**2. Las excepciones (`availabilityOverrides`), que pisan al patrón** en las fechas que
alcanzan. Una excepción dice: *para este recurso, en estas fechas, las ventanas abiertas son
éstas.*

```
availabilityOverrides
  id · tenantId
  courtId uuid NULL      -- NULL = todas las canchas del club
  windows jsonb          -- [[720,1020]] = 12 a 17 · [] = cerrado
  reason text NULL
  createdAt · createdBy

availabilityOverrideDates
  overrideId · tenantId · date
  PK (overrideId, date) · índice (tenantId, date)
```

Las reglas que la gobiernan:

- **Es un conjunto de fechas, no un rango.** Calendly selecciona *date(s)* en un calendario, y
  un conjunto expresa tanto "del 19 al 25" como "el 16, el 18 y el 22"; un rango no. Un rango
  es una excepción con sus fechas cargadas.
- **Cerrar no es un concepto aparte: es una excepción sin ventanas.** No existe una entidad
  "bloqueo" ni un tipo "feriado" ni un flag de cerrado.
- **La excepción reemplaza al patrón** de esa fecha, no lo interseca.
- **Gana la más específica**: la excepción de cancha le gana a la de club. Si empatan en
  alcance, gana la más reciente.
- **El patrón no se versiona.** Como sólo se dibuja hacia adelante, cambiar las horas es editar
  el patrón: que una fecha pasada se dibuje distinta no le importa a nadie, porque no se
  dibuja.
- **`courts.scheduleId` se queda como está.** El problema nunca fue que el FK fuera uno solo,
  sino que el único lugar donde poner una excepción de una cancha era el horario compartido.
  Con la excepción apuntando a la cancha, un FK alcanza; y sin versionado del patrón, tampoco
  hace falta fechar la asignación.

### La lectura

Dibujar un día o una semana son la misma cuenta con distinto rango, y son **cuatro consultas
para la semana entera**, no cuatro por día ni una por cancha:

1. Las canchas con su configuración y su `scheduleId`.
2. Los patrones de esos horarios.
3. Las excepciones: `date BETWEEN inicio AND fin`, de esas canchas o de alcance club.
4. Las reservas confirmadas del rango.

Y después, en memoria, por cada par (cancha, fecha): ventanas efectivas → arranques según
incremento y duración → menos las reservas → menos lo que no llega al aviso mínimo. Eso es lo
que el usuario final ve como huecos disponibles.

### La zona horaria

Vive donde vive el recurso. En Calendly está en el horario porque el recurso es una persona que
se mueve; acá la cancha está en un lugar físico, así que la zona es la del club —o la de la
sede, si algún día un club tiene varias en husos distintos—. Nunca en el horario: serían dos
fuentes que pueden contradecirse. Lo que ve el usuario final se convierte a **su** huso al
mostrar.

## Consecuencias

- Cada excepción es un `INSERT` y se borra con un `DELETE`. No reescribe el horario, así que
  dejan de competir dos ediciones que no tienen nada que ver.
- La consulta de la semana es un índice por fecha en vez de abrir y expandir jsonb.
- Las excepciones con todas sus fechas vencidas **se pueden purgar**, porque nunca se dibuja
  hacia atrás. La tabla se mantiene chica sola; el array jsonb no lo hacía.
- Queda auditado quién cerró qué y por qué (`reason`, `createdBy`), cosa que hoy no existe.
- El botón "bloquear horario" del backoffice, que hoy es sólo un aviso, pasa a tener modelo
  abajo.
- La agenda se sigue calculando en lectura y la exclusion constraint sigue siendo la que impide
  la doble venta: [ADR-0002](0002-agenda-calculada-en-lectura.md) no cambia en eso.

## Lo que queda abierto

- **Hold con TTL.** ADR-0002 descartó los holds con el argumento de que "el operador vende en
  el momento" y difirió el tema "cuando llegue el portal del socio". Si el usuario final elige
  huecos por su cuenta, el portal llegó y esa premisa se cae. **No se decide acá**: se escribe
  cuando el flujo del usuario final entre en alcance.
- **La pantalla de excepciones**: si permite elegir fechas sueltas en un calendario como
  Calendly o alcanza con rangos. El usuario todavía no lo definió, y **no bloquea**: el modelo
  aguanta las dos formas.

## Alternativas descartadas

- **Versionar el patrón con `validFrom`/`validTo`:** era la propuesta inicial y la regla de
  dibujar sólo hacia adelante la volvió innecesaria. Habría sido la parte más cara del cambio.
- **Excepciones colgadas del horario, como Calendly:** a ellos les alcanza porque el recurso es
  uno solo. Acá obliga a duplicar horarios por cancha, que es el problema que se está
  resolviendo.
- **Rango de fechas (`dateFrom`/`dateTo`) en vez de conjunto:** no expresa fechas sueltas.
- **Fechar la asignación cancha↔horario (`courtSchedules`):** sólo hace falta si se redibuja el
  pasado.
- **Entidades separadas para bloqueo, feriado y fecha especial:** son el mismo dato con
  distintas ventanas.
