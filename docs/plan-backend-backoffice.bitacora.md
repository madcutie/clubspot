# Bitácora — Plan backend del backoffice

Registro de avance del plan [`plan-backend-backoffice.md`](plan-backend-backoffice.md).

**Regla de uso:** el agente que trabaje sobre el plan actualiza este archivo **al terminar cada
bloque de trabajo**, no al final de la sesión. Cada entrada va arriba de las anteriores, con
fecha, qué se hizo, qué decisiones se tomaron sobre la marcha, y un cierre explícito de
**"dónde quedó / próximo paso"**. La tabla de estado se mantiene al día.

## Estado por fase

Corte de fases del 15/08/2026: F0 se dividió en A1–A4; B = F1+F2; C = F3+F4 (ver la nota de
actualización en el plan).

| Fase | Contenido | Estado |
|---|---|---|
| Plan | Diseño del plan + documentos movidos + links arreglados | ✅ 14/08/2026 |
| A1 | Renombres a inglés (`Period`, ids de módulo, tests, docs) + reestructura por capas con Application (ADR-0005) | 🚧 código listo 15/08/2026 — **falta build+tests** (el usuario pidió no compilar aún) |
| A2 | Persistencia (EF Core + PostgreSQL, `CoreDbContext`, tabla `club`) + tenancy (`AsyncLocal`, filtro global, guardia en `SaveChanges`) + infra Testcontainers | ⬜ |
| A3 | Auth: tablas `user`/`user_role`, hash, `POST /api/auth/session` → JWT, roles y políticas | ⬜ |
| A4 | Módulos por club (`club_module`), `GET /api/context`, gating 404, ProblemDetails, CORS, seed | ⬜ |
| B | Schedules, Courts y People: agregados, GET/PUT masivos con xmin, búsqueda y ficha, endpoints y tests | ⬜ |
| C | Agenda y Bookings (exclusion constraint, servicios de dominio, 6 endpoints) + conexión del frontend (`http.ts` reemplaza `mockApi.ts`, se borra `store.ts`, login mínimo) | ⬜ |

Leyenda: ⬜ pendiente · 🚧 en curso · ✅ terminada (build + tests verdes).

---

## Entradas

### 15/08/2026 (2) — Reestructura por capas (ADR-0005) y creación de los ADRs.

**Decisión del usuario:** la estructura tiene que tener una capa **Application** explícita,
como su proyecto `anubis` (`C:\Users\dario\source\repos\anubis`), con los módulos separados
por carpetas y no por proyectos. Además pidió que las decisiones de arquitectura queden
**escritas en piedra en ADRs**.

**Qué se hizo:**

- Creado `docs/adr/` con índice y 5 ADRs: 0001 monolito modular con modularidad comercial por
  tenant (no plugins) · 0002 agenda calculada en lectura + exclusion constraint · 0003 auth
  tablas propias + JWT · 0004 identificadores en inglés · 0005 capas con Application, módulos
  como carpetas.
- Reestructura espejo de anubis: `src/backend/src/{Core,Infrastructure,Api,Tests}/`. Nuevos
  proyectos `ClubSpot.Domain` (vacío aún) y `ClubSpot.Application`; **eliminados** los 5
  proyectos `ClubSpot.Modules.*` y `ClubSpot.Jobs` (vacío). Manifiestos del catálogo movidos a
  `Application/Modularity/ProductModules.cs`. Referencias: Api → Application + Infrastructure;
  Infrastructure → Application; Application → Domain; Domain → SharedKernel. `ClubSpot.slnx`
  con carpetas de solución por capa. `ModuleCatalogTests` apunta a
  `ClubSpot.Application.Modularity`.
- Actualizados `AGENTS.md` (§2 tabla de docs, §4 árbol y reglas de frontera, §8 estado) y
  `README.md` (árbol y tabla de docs); nota 3 agregada al recuadro de actualización del plan
  (cómo leer el plan con la estructura nueva).
- Nota operativa: hubo que cerrar VS Code para mover las carpetas (el C# Dev Kit retiene
  handles sobre los proyectos).

**Dónde quedó / próximo paso:** A1 + reestructura con el código completo y **sin verificar**
(sigue en pie el pedido de no compilar). Cuando el usuario pida la verificación: `dotnet build`
+ `dotnet test` en `src/backend`; en verde, marcar A1 ✅ y continuar con A2 (persistencia +
tenancy), creando las carpetas de módulo `Core/` y `Bookings/` dentro de Domain y Application
según el plan §3 y la nota 3.

### 15/08/2026 — Arranca la implementación. A1 (renombres a inglés) con el código listo, sin verificar.

**Decisiones tomadas (por el usuario):**

1. **Identificadores en inglés, textos en español.** Clases, tablas, endpoints, proyectos e ids
   de módulo en inglés; comentarios, mensajes de error, nombres de tests y nombres comerciales
   en español. El mapa de traducción quedó en la nota de actualización del plan.
2. **Renombrar lo existente ahora** (ids de módulo incluidos): es el único momento barato, no
   hay nada persistido ni commits.
3. **Fases más chicas**: F0 se dividió en A1–A4; B = F1+F2; C = F3+F4.
4. **No compilar ni correr nada pesado sin pedirlo**: la verificación (build + tests) se hace
   completa cuando el usuario lo pida, no por cada bloque.

**Qué se hizo (A1):**

- Proyectos renombrados: `Modules.Clubes`→`Modules.Core`, `Modules.Finanzas`→`Modules.Finance`,
  `Modules.Reservas`→`Modules.Bookings`, `Modules.Futbol`→`Modules.Football` (carpetas y
  csproj; se borraron los `obj/` viejos). Actualizados `ClubSpot.slnx` y todas las referencias
  entre proyectos.
- `Periodo`→`Period` (archivo y struct). Ids de módulo: `nucleo`→`core`, `socios`→`members`,
  `finanzas`→`finance`, `reservas`→`bookings`, `futbol`→`football`; estáticos de `ModuleId` y
  manifiestos renombrados (`CoreModule`, `MembersModule`, `FinanceModule`, `BookingsModule`,
  `FootballModule`; `DisplayName` sigue en español). `ModuleCatalogTests` actualizado con los
  nombres nuevos, tests con nombres en español.
- Docs: regla de idioma nueva en `AGENTS.md` (§3 y §6), árbol y grafo de módulos en inglés en
  `AGENTS.md` §4/§5/§8/§9 y `README.md`, nota de actualización con el mapa de nombres y el
  corte de fases nuevo en el plan.

**Dónde quedó / próximo paso:** A1 tiene el código completo pero **sin verificar**: falta
`dotnet build` + `dotnet test` (el usuario pidió explícitamente no compilar todavía; se hará
una verificación completa cuando lo indique). Al verificar en verde, marcar A1 ✅ y seguir con
A2 (persistencia + tenancy) según el plan §3, previa confirmación del usuario.

### 14/08/2026 — Plan creado. La implementación NO arranca todavía.

**Qué se hizo:**

- Relevamiento completo de tres fuentes: el contrato del mock del frontend
  (`src/frontend/backoffice/src/api/mockApi.ts` + `domain/`), el esqueleto del backend
  (`src/backend`, SharedKernel completo, resto vacío) y los documentos de alcance/diseño.
- Escrito el plan completo en [`plan-backend-backoffice.md`](plan-backend-backoffice.md):
  arquitectura, modelos por módulo, los 19 endpoints, handlers/servicios/repositorios archivo
  por archivo, tests de unidad e integración, y el orden F0→F4.
- Movidos `alcance-socios-mvp.html` y `diseno-detallado-socios.html` de `docs/` a
  `docs/referencia-ourclub/` (pasan a ser material de consulta, no especificación que compita
  con el prototipo). Arregladas todas las referencias: `README.md`, `AGENTS.md` raíz (tabla §2
  y §7) y `docs/referencia-ourclub/AGENTS.md` (sección Precedencia).

**Decisiones tomadas (por el usuario):**

1. **Manda el mockup** sobre el diseño detallado donde divergen (Horario compartido + tarifa y
   reglas por cancha, en vez de Tarifa por tipo de espacio × franja × audiencia).
2. **Sin módulo finanzas**: cobro en la reserva, `deuda` como campo llano de Persona — stubs
   provisionales marcados.
3. **Auth con tablas propias + JWT** (ni Identity ni proveedor externo).
4. **La implementación no arranca todavía** — decisión explícita. Este plan queda escrito y se
   ejecuta cuando el usuario lo pida.

**Contexto útil para el que retome:**

- El backoffice frontend está terminado como cascarón contra mock (ver `AGENTS.md` §10) y fue
  recorrido entero en el navegador en esta misma fecha. Corre en `:5184`.
- El backend sólo tiene SharedKernel + manifiestos de módulo. No hay ni un paquete NuGet de
  EF/auth todavía.
- Los tests de integración van a necesitar Docker (Testcontainers con PostgreSQL real, por la
  exclusion constraint).

**Dónde quedó / próximo paso:** el plan está completo y aprobado en su contenido; **no empezar
a implementar sin pedido explícito del usuario**. Cuando lo pida, arrancar por la fase F0
(plataforma) siguiendo `plan-backend-backoffice.md` §3, y marcar F0 como 🚧 acá antes de tocar
código.
