# ADR-0010 — Un solo `DbContext` y una sola tabla de migraciones

**Fecha:** 16/08/2026 · **Estado:** Aceptada ·
**Reemplaza parcialmente a** [ADR-0007](0007-esquema-postgresql-unico.md) (la parte que
conservaba dos contextos con dos historiales)

## Contexto

El plan original preveía **un `DbContext` por módulo** y ADR-0007, al unificar el esquema en
`public`, conservó esa separación técnica. La consecuencia directa fue tener **dos tablas de
historial de migraciones** —`__EFMigrationsHistoryCore` y `__EFMigrationsHistoryBookings`—
conviviendo en la misma base: no era una decisión propia, sino el precio obligado de tener dos
contextos apuntando a un mismo esquema (dos contextos no pueden compartir un historial sin que
cada uno vea las migraciones del otro como ajenas; EF desaconseja explícitamente compartirlo).

Al revisarlo, el usuario lo señaló como incorrecto: **una base, una cadena de migraciones**.
Dos historiales obligan a mantener dos secuencias que hay que ordenar a mano cuando un cambio
toca ambos lados, duplican el comando de migración y la lógica de arranque, y no aportan
ninguna frontera real: las tablas ya conviven en `public` y el compilador nunca impuso el
límite entre módulos.

## Decisión

**Existe un único `DbContext`: `ClubSpotDbContext`**, con una única cadena de migraciones y la
tabla de historial estándar de EF, `__EFMigrationsHistory`.

- `CoreDbContext` y `BookingsDbContext` desaparecen; sus `DbSet` y configuraciones se unifican.
- Las configuraciones de entidad siguen **separadas por módulo en archivos y carpetas**
  (`Persistence/Configurations/…`): la frontera modular es de código, como manda ADR-0005.
- Una sola fábrica de diseño para `dotnet ef`, una sola carpeta `Persistence/Migrations/`, un
  solo snapshot.
- Ya no se declara ni se configura el nombre de la tabla de historial: se usa el default.

## Consecuencias

- Un solo comando para crear migraciones y un solo `MigrateAsync` al arrancar.
- Un cambio que toca dos módulos entra en **una** migración, ordenada por sí misma.
- Los repositorios, queries, stores, el seed y el fixture de tests inyectan un único contexto.
- **Riesgo asumido:** con un solo contexto, nada impide técnicamente que una consulta de un
  módulo lea tablas de otro. La regla de frontera de `AGENTS.md` §4 pasa a cuidarse
  íntegramente en revisión de código, como ya ocurría con las carpetas.
- Las migraciones de desarrollo se regeneran una vez más; toda base local existente debe
  recrearse.

## Alternativas descartadas

- **Dos contextos compartiendo una sola tabla de historial:** EF lo desaconseja y es frágil —
  cada contexto sólo reconoce sus propias migraciones, los ids pueden colisionar y el estado
  real de la base deja de ser legible desde una sola secuencia.
- **Mantener dos historiales** (lo que había): rechazado por el usuario; era una consecuencia
  no querida de una decisión técnica, no un beneficio.
- **Un contexto por módulo con una base por módulo:** contradice el monolito modular y
  ADR-0001; nunca estuvo sobre la mesa.
