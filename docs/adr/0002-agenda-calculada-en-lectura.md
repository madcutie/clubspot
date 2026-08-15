# ADR-0002 — Agenda calculada en lectura; exclusion constraint contra la doble venta

**Fecha:** 14/08/2026 · **Estado:** Aceptada

## Contexto

La pantalla de agenda muestra una grilla cancha × media hora con turnos vendibles. El diseño
detallado (`docs/referencia-ourclub/diseno-detallado-socios.html`) proponía materializar los
turnos con un job diario de apertura de agenda (J5), pensado para el flujo del portal con
hold+TTL. El backoffice del MVP no necesita holds: el operador vende en el momento.

## Decisión

**La agenda no se materializa: se computa al leer** a partir de los tramos del `Schedule`
(la fecha especial pisa el día semanal entero), la configuración del `Court` y las reservas
confirmadas del día.

**La doble venta la impide la base**, con una exclusion constraint de PostgreSQL
(`btree_gist`) sobre `(tenant_id, court_id, fecha, rango de minutos)` aplicada a reservas
confirmadas. Dos ventas simultáneas del mismo hueco: una obtiene 201, la otra 409 — sin locks
de aplicación ni fila de turno como punto de serialización.

## Consecuencias

- No existe el job J5 ni la tabla de turnos en este alcance; menos piezas móviles.
- Los tests de integración necesitan **PostgreSQL real** (Testcontainers): la constraint no
  existe en SQLite ni en el provider InMemory.
- Cuando llegue el portal del socio con hold+TTL, se agrega el estado de retención sobre esta
  base sin romper nada: la constraint sigue siendo la última línea de defensa.

## Alternativas descartadas

- **Turnos materializados (J5) ya:** infraestructura de jobs y una tabla más para un flujo
  (hold del portal) que no está en el alcance.
- **Serialización en la aplicación (lock por cancha):** frágil ante múltiples instancias;
  la base es el único árbitro confiable.
