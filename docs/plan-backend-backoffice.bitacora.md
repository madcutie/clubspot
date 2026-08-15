# Bitácora — Plan backend del backoffice

Registro de avance del plan [`plan-backend-backoffice.md`](plan-backend-backoffice.md).

**Regla de uso:** el agente que trabaje sobre el plan actualiza este archivo **al terminar cada
bloque de trabajo**, no al final de la sesión. Cada entrada va arriba de las anteriores, con
fecha, qué se hizo, qué decisiones se tomaron sobre la marcha, y un cierre explícito de
**"dónde quedó / próximo paso"**. La tabla de estado se mantiene al día.

## Estado por fase

| Fase | Contenido | Estado |
|---|---|---|
| Plan | Diseño del plan + documentos movidos + links arreglados | ✅ 14/08/2026 |
| F0 | Plataforma: persistencia, tenancy, módulos contratados + gating 404, auth JWT, errores, seed | ⬜ |
| F1 | Canchas y Horarios: agregados, GET/PUT masivos, xmin, `idsAsignados` | ⬜ |
| F2 | Personas: agregado, queries con contadores, 6 endpoints | ⬜ |
| F3 | Agenda y Reservas: exclusion constraint, servicios de dominio, 6 endpoints, historial real | ⬜ |
| F4 | Conexión del frontend: `http.ts` reemplaza `mockApi.ts`, se borra `store.ts`, login mínimo | ⬜ |

Leyenda: ⬜ pendiente · 🚧 en curso · ✅ terminada (build + tests verdes).

---

## Entradas

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
