# ADR-0009 — `club_module` guarda lo contratado; la habilitación es el cierre resuelto en lectura

**Fecha:** 16/08/2026 · **Estado:** Aceptada

## Contexto

La tabla `club_module` existía sin semántica definida: no estaba decidido si sus filas
representan **lo que el club contrató comercialmente** o **el conjunto habilitado** (el cierre
transitivo de dependencias). `ModuleCatalog.Resolve` —el cierre transitivo exigido por el
producto— se invocaba en un solo lugar: el seed de desarrollo. El sistema funcionaba porque el
seed escribía el cierre completo; ninguna pieza era dueña de la invariante "el conjunto
persistido está cerrado por dependencias", y `ContractedAt` marcaba como "contratadas" a
dependencias que el club nunca eligió.

## Decisión

**`club_module` persiste únicamente lo contratado comercialmente.** La habilitación se calcula
en lectura: `ITenantModules.Enabled` = `ModuleCatalog.Resolve(contratado)`, cacheado como hasta
ahora.

- Los módulos núcleo (`IsCore`) están siempre habilitados, con o sin fila.
- Ninguna otra parte del sistema lee `club_module` directo: el único camino hacia "qué tiene
  habilitado este club" es `ITenantModules`.
- La futura pantalla de configuración de módulos escribe sólo contrataciones; nunca persiste
  dependencias arrastradas.

## Consecuencias

- `ContractedAt` vuelve a ser un dato honesto: fecha en que el club contrató ese módulo.
- El seed de desarrollo persiste sólo los módulos contratados (hoy: `members` y `bookings`);
  `core` y `finance` llegan por cierre en lectura.
- `/api/context` sigue exponiendo el conjunto habilitado (el cierre), que es lo que el
  frontend necesita para el gating.
- Un dato persistido "incompleto" (por ejemplo, sólo `members`) es válido por definición: la
  expansión es responsabilidad de la lectura, no una invariante de escritura que policiar.

## Alternativas descartadas

- **Persistir el cierre resuelto:** pierde la distinción comercial entre lo que el club compró
  y lo que se arrastró, y obliga a imponer la invariante de cierre en cada punto de escritura
  presente y futuro. La información comercial es la que el producto factura; no se descarta.
- **Resolver en escritura y en lectura "por las dudas":** dos dueños de la misma regla,
  garantía de divergencia.
