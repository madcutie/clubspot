# ClubSpot

Sistema de **gestión de clubes**, configurable por módulos: cada club contrata los que usa.

Cubre dos dominios: la **gestión del socio** (padrón, cuota, cobro) y la **reserva de canchas**
de pádel y fútbol. La venta de entradas para partidos no forma parte de este producto.

## Arranque rápido

```bash
cd src/backend && dotnet build && dotnet test

cd src/frontend/reservas && pnpm install && pnpm dev
```

Requiere el SDK de .NET fijado en `src/backend/global.json` (10.0.3xx) y Node 20+.

## Estructura

Todo el código fuente vive en `src/`, separado en backend y frontend.

```
src/
  backend/                       la solución .NET (ClubSpot.slnx, global.json, Directory.Build.props)
    src/
      Core/
        ClubSpot.SharedKernel/   primitivas: Money, TenantId, IClock y el sistema de modularidad
        ClubSpot.Domain/         agregados y servicios de dominio puros, una carpeta por módulo
        ClubSpot.Application/    casos de uso y puertos, una carpeta por módulo
      Infrastructure/
        ClubSpot.Infrastructure/ EF Core, persistencia, tenancy, gateways
      Api/
        ClubSpot.Api/            host: endpoints, JWT, DI, arranque
      Tests/                     ClubSpot.UnitTests y ClubSpot.IntegrationTests
  frontend/
    reservas/                    prototipo del portal de reservas (React + Vite)
docs/                            alcance, diseño detallado y relevamiento del sistema de referencia
```

## Módulos

```
core (siempre activo)
 ├─ finance ───► core
 ├─ members ───► core, finance
 └─ bookings ──► core, finance
      ├─ padel ────► bookings
      └─ football ─► bookings
```

Contratar `padel` habilita `bookings`, `finance` y `core` automáticamente. Un módulo apagado
responde 404, no 403, y apagarlo no borra datos.

## Documentación

| Documento | Qué es |
|---|---|
| [`docs/referencia-ourclub/alcance-socios-mvp.html`](docs/referencia-ourclub/alcance-socios-mvp.html) | Alcance del MVP: qué entra, qué no, y las preguntas abiertas |
| [`docs/referencia-ourclub/diseno-detallado-socios.html`](docs/referencia-ourclub/diseno-detallado-socios.html) | Modelo de dominio, máquinas de estado, los 11 jobs, concurrencia y migración |
| [`docs/referencia-ourclub/`](docs/referencia-ourclub/README.md) | Relevamiento del sistema que usa hoy el club |
| [`docs/adr/`](docs/adr/README.md) | Decisiones de arquitectura escritas en piedra (ADRs) |
| [`AGENTS.md`](AGENTS.md) | Contexto, convenciones y **el desglose de lo que falta desarrollar** |

Los HTML se abren con doble clic; no dependen de internet.

## Estado

Recién arrancado: existe la estructura, el sistema de modularidad y las primitivas compartidas.
El desglose de lo pendiente está en la sección 9 de [`AGENTS.md`](AGENTS.md).
