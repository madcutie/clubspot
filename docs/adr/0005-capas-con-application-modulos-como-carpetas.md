# ADR-0005 — Arquitectura por capas con Application explícita; módulos como carpetas

**Fecha:** 15/08/2026 · **Estado:** Aceptada

## Contexto

La estructura inicial del backend tenía **un proyecto por módulo** (`ClubSpot.Modules.Core`,
`ClubSpot.Modules.Bookings`, …) para que la frontera entre módulos la impusiera el compilador
vía referencias de proyecto. El usuario pidió la forma de su proyecto **anubis**: capas
explícitas —en particular una capa **Application**— con los módulos separados por carpetas,
no por proyectos.

## Decisión

**Capas como proyectos, módulos como carpetas.** Estructura espejo de anubis:

```
src/backend/src/
├─ Core/
│  ├─ ClubSpot.SharedKernel/     primitivas: Money, TenantId, IClock, ModuleId, ModuleCatalog
│  ├─ ClubSpot.Domain/           agregados y servicios de dominio puros, una carpeta por módulo
│  └─ ClubSpot.Application/      casos de uso (handlers) y puertos, una carpeta por módulo
├─ Infrastructure/
│  └─ ClubSpot.Infrastructure/   EF Core, repositorios, tenancy, gateways
├─ Api/
│  └─ ClubSpot.Api/              host: endpoints, JWT, middleware, DI
└─ Tests/
   ├─ ClubSpot.UnitTests/
   └─ ClubSpot.IntegrationTests/
```

Referencias: `Api → Application + Infrastructure` · `Infrastructure → Application` ·
`Application → Domain` · `Domain → SharedKernel`.

- Los seis proyectos `ClubSpot.Modules.*` y `ClubSpot.Jobs` (vacío) se eliminaron. Los
  manifiestos del catálogo viven en `Application/Modularity/ProductModules.cs`.
- Dentro de `Domain` y `Application`, la separación por módulo es por carpetas
  (`Core/`, `Bookings/`, …); los puertos (interfaces de repositorio) van con la carpeta del
  módulo que los define.
- `Jobs` se recreará como proyecto cuando existan los jobs.

**Esto no cambia el ADR-0001:** la modularidad comercial por tenant (catálogo, `club_module`,
gating 404) es runtime y datos, e queda intacta.

## Consecuencias

- Estructura familiar (clean architecture clásica) y menos proyectos (7 en vez de 11).
- **La frontera entre módulos ya no la impone el compilador**: pasa a ser convención de
  carpetas cuidada en revisión. La regla se mantiene: una carpeta de módulo no usa tipos de
  otra; lo compartido va por contratos (interfaces) o por SharedKernel.
- La regla "el dominio jamás pregunta por módulos habilitados" no depende de la estructura y
  sigue vigente.

## Alternativas descartadas

- **Un proyecto por módulo (estructura inicial):** frontera impuesta por el compilador, pero
  estructura que al usuario le resultó rara y sin capa Application explícita.
- **Application por módulo (`Modules.X/Application/`):** conserva ambas cosas pero multiplica
  proyectos y no es la forma pedida.
