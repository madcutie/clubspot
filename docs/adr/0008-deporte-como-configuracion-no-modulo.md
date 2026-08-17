# ADR-0008 — El deporte es configuración de la cancha, no un módulo contratable

**Fecha:** 16/08/2026 · **Estado:** Aceptada

## Contexto

El catálogo de producto tenía dos módulos contratables por deporte —`padel` y `football`—
colgando de `bookings`. La revisión de modelado del 16/08/2026 mostró que el deporte estaba
representado tres veces sin conexión entre sí:

1. Dos enums `Sport` idénticos pero de tipos distintos (uno en `SharedKernel/Primitives`,
   otro en `Domain/Bookings`), con riesgo de divergencia silenciosa.
2. Los ids de módulo `padel`/`football`, sin ningún mapeo hacia el enum. El gating prometido
   ("agenda de un deporte cuyo módulo está apagado ⇒ 404") era inimplementable: los módulos
   por deporte eran entradas de catálogo sin un solo comportamiento asociado.
3. `Person.PreferredSport` metía un concepto de reservas dentro del módulo `core`, sin que el
   negocio lo pidiera: la relación real entre una persona y un deporte surge de sus reservas.

Además, la etiqueta "FÚTBOL 5" de la pantalla de canchas se había leído como si fuera otro
deporte, cuando en los datos del propio mock el valor es siempre el genérico `futbol`; el "5"
es formato/presentación (F5/F7/F11 son formatos del mismo deporte, pregunta abierta del
relevamiento).

## Decisión

**El módulo `bookings` se contrata una sola vez y cubre reservas de cualquier deporte.**
Los módulos `padel` y `football` se eliminan del catálogo de producto.

- El deporte es **configuración de la cancha** (`Court.Sport`), no una unidad comercial.
- Cómo se configuran las canchas y a qué deporte pertenecen —catálogo de deportes
  administrable, formatos F5/F7/F11, tipos de espacio— **queda como decisión futura
  explícita**, a diseñar con las pantallas. No se infiere desde la base ni se anticipa.
- `Person` **no tiene deporte preferido**. La relación persona↔deporte se deriva de sus
  reservas cuando exista el agregado de reserva.
- Queda **un solo enum `Sport`**, en el módulo `bookings` (`Domain/Bookings/Sport.cs`); el
  duplicado de SharedKernel se elimina. El valor de dominio para fútbol sigue siendo
  `Football`: "Fútbol 5" es rótulo de presentación, no otro deporte.

## Consecuencias

- El grafo de módulos queda: `core` ← `finance` ← { `members`, `bookings` }.
- El gating de las pantallas de reservas se reduce a `RequireModule(bookings)`; desaparece la
  necesidad de un mapeo deporte→módulo.
- `ModuleId.Padel`/`ModuleId.Football`, `PadelModule`/`FootballModule`, su registro en el
  arranque y el seed se eliminan; los tests de catálogo se reescriben sobre el grafo nuevo.
- `Person` pierde `PreferredSport` (agregado, handlers, contrato HTTP, tabla). El mock del
  backoffice todavía muestra "deporte" en la base de personas y en el alta: ese ajuste de
  frontend queda **pendiente registrado**, se hace al conectar las pantallas.
- Las migraciones iniciales de desarrollo se regeneran.

## Alternativas descartadas

- **Un módulo contratable por deporte:** duplica unidades comerciales sin comportamiento
  propio y exige un mapeo deporte→módulo que nadie poseía. Si algún día un deporte tiene
  reglas propias de verdad, se evaluará entonces con contenido concreto, no como cascarón.
- **Renombrar `Football` a `Football5`:** confunde deporte con formato de cancha. Los datos
  del mock usan el valor genérico `futbol`; con F7/F11 en el horizonte, el enum explotaría en
  falsos deportes y arrastraría todo lo que cuelga de él.
- **Catálogo de deportes administrable ahora:** las pantallas actuales no lo piden; sería
  inventar UI y flujo que no existen (regla del plan: manda el mock).
