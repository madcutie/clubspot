# ClubSpot — instrucciones para agentes

Sistema de **gestión de clubes** con backend .NET, pensado como producto **configurable por
módulos**: cada club contrata los que usa.

Este repo arranca el 14/08/2026. Reemplaza el enfoque del repo `Ticketing` (venta de entradas),
que **no se toca ni se migra acá** — se encara de otra manera.

---

## 1. Qué se está construyendo

Un club real —Club Atlético Chaco For Ever— usa hoy un SaaS llamado **OurClub**. Se relevó
entero y ese relevamiento es la referencia. Los dolores a resolver:

1. **Gestión del socio** — padrón, cuota, cobro.
2. **Reservas de canchas** de pádel y fútbol — funcionalidad nueva, sin equivalente real en
   el sistema actual.

**La venta de entradas para partidos NO es parte de este producto.**

## 2. Documentos — fuente de verdad

Leer antes de proponer cualquier cosa de dominio. Están en `docs/`:

| Documento | Qué contiene |
|---|---|
| `docs/referencia-ourclub/alcance-socios-mvp.html` | **Alcance del MVP aprobado.** Qué entra, qué no, y por qué. Incluye la decisión sobre arqueo de caja y las 7 preguntas abiertas |
| `docs/referencia-ourclub/diseno-detallado-socios.html` | **Diseño detallado.** Modelo de dominio con campos y tipos, 8 máquinas de estado, los 11 jobs, concurrencia, roles, migración del padrón |
| `docs/referencia-ourclub/` | **Relevamiento de OurClub — el sistema que usa el club hoy, no este producto.** 26 módulos, ~70 pantallas, campos y tipos, políticas de acceso, inconsistencias a no replicar. Es material de consulta, no especificación: leer primero [`docs/referencia-ourclub/AGENTS.md`](docs/referencia-ourclub/AGENTS.md) |
| [`docs/adr/`](docs/adr/README.md) | **Decisiones de arquitectura escritas en piedra** (ADRs): monolito modular, agenda en lectura, auth propia, idioma, capas. No se rediscuten; si una cambia, se escribe un ADR nuevo |

Ante una duda de dominio: **primero buscar en esos documentos**, no improvisar. Si la respuesta
no está, es una pregunta para el usuario, no una decisión a tomar sola.

Los dos primeros mandan sobre el tercero: el alcance define qué entra, el diseño cómo se
resuelve, y el relevamiento sólo muestra **cómo lo hace el sistema ajeno**. Nada se implementa
por estar relevado. Ojo con el desfasaje: el relevamiento se hizo cuando el alcance todavía
incluía boletería.

⚠️ `docs/referencia-ourclub/00-datos-de-prueba.md` tiene datos personales reales. Material
interno: no publicar, no copiar a ejemplos, no usar en tests.

## 3. Reglas de trabajo

- **Los commits los hace el usuario.** No hacer `git commit` ni `git push` salvo pedido
  explícito. (Regla heredada de cómo trabaja en sus otros repos — confirmar si cambia.)
- **Idioma** (ADR-0006, 15/08/2026, reemplaza al "todo en español" original): **el código va
  entero en inglés** —identificadores, comentarios, mensajes de excepción y nombres de tests—.
  En español queda lo que no es código: la documentación del repo y los textos que ve el
  usuario final. Detalle en la sección 6.
- **Comentarios: casi cero** (ADR-0006). Sólo lo muy importante que el código no puede decir
  por sí mismo; nada de doc-comments decorativos.
- **Sin primera persona** en documentos entregables. Voz impersonal.
- **No inventar números.** Si algo es una estimación, decirlo. El usuario lleva estos
  documentos a reuniones con el club.
- Antes de borrar o pisar algo, mirarlo. Preferir copiar y avisar antes que mover y perder.

### Lo que está esperando definición del usuario

- ✅ **Frontend del backoffice** — el diseño llegó (14/08/2026) y el cascarón está implementado
  en `src/frontend/backoffice/`. Ya no está bloqueado. Ver sección 10.
- Las 7 preguntas abiertas del documento de alcance (facturación electrónica, cobrador
  domiciliario, débito automático, estrategia de migración, tolerancia de deuda para reservar,
  alquiler a no socios, acumulación de becas).

## 4. Arquitectura

**Monolito modular** en .NET 10. Un solo host, un solo despliegue. Las decisiones grandes de
arquitectura están escritas en piedra en [`docs/adr/`](docs/adr/README.md) — **leerlas antes de
proponer un cambio estructural**; si una decisión cambia, se escribe un ADR nuevo, no se edita
el viejo.

La estructura es **por capas** (ADR-0005, espejo del proyecto anubis del usuario): las capas
son proyectos, los módulos son **carpetas** dentro de cada capa. Todo el código fuente cuelga
de `src/`, con backend y frontend separados. La solución .NET vive entera dentro de
`src/backend/` —incluidos `global.json` y `Directory.Build.props`—, así que esa carpeta se
puede abrir sola y compila.

```
src/
├─ backend/
│  ├─ ClubSpot.slnx
│  ├─ global.json
│  ├─ Directory.Build.props
│  └─ src/
│     ├─ Core/
│     │  ├─ ClubSpot.SharedKernel/     primitivas: Money, TenantId, IClock, ModuleId, ModuleCatalog
│     │  ├─ ClubSpot.Domain/           agregados y servicios de dominio puros — carpeta por módulo
│     │  └─ ClubSpot.Application/      casos de uso (handlers) y puertos — carpeta por módulo
│     ├─ Infrastructure/
│     │  └─ ClubSpot.Infrastructure/   EF Core, repositorios, tenancy, gateways
│     ├─ Api/
│     │  └─ ClubSpot.Api/              host: endpoints, JWT, middleware, DI, arranque
│     └─ Tests/
│        ├─ ClubSpot.UnitTests/
│        └─ ClubSpot.IntegrationTests/
└─ frontend/
   ├─ backoffice/                    consola del club (React+Vite) — ver sección 10
   └─ reservas/                      prototipo React+Vite del portal de reservas (ya existía)

docs/                                alcance, diseño detallado, relevamiento y ADRs
```

Referencias entre capas: `Api → Application + Infrastructure` · `Infrastructure → Application`
· `Application → Domain` · `Domain → SharedKernel`. Los manifiestos del catálogo de módulos
viven en `Application/Modularity/ProductModules.cs`. `Jobs` se recreará como proyecto cuando
existan los jobs.

### Grafo de módulos

```
core (núcleo, no se puede apagar)
 ├─ finance ───────────► core
 ├─ members ───────────► core, finance
 └─ bookings ──────────► core, finance
      ├─ padel ────────► bookings
      └─ football ─────► bookings
```

**Por qué existe `bookings` si el usuario pidió "pádel" y "fútbol" como módulos separados:**
el motor —espacio, grilla, turno, reserva, cobro— es el mismo, y duplicarlo sería duplicar la
parte más delicada del sistema. `padel` y `football` existen como módulos contratables y
contienen lo que sí difiere entre deportes.

**Por qué `members` y `bookings` dependen de `finance`:** ambos generan cargos y cobran. Sin
módulo de dinero no hay nada que hacer con una cuota ni con un turno vendido.

### Reglas de frontera entre módulos

Los módulos ya no son proyectos, así que la frontera **no la impone el compilador**: es
convención de carpetas, cuidada en revisión (ADR-0005).

- Una carpeta de módulo (`Domain/Bookings/`, `Application/Bookings/`) **no usa tipos** de la
  carpeta de otro módulo, salvo lo propio de `padel`/`football` sobre `bookings`.
- Lo que dos módulos necesitan compartir va como **contrato** (interfaz), implementado por el
  módulo dueño y cableado por DI. Ejemplo: la habilitación del socio la define `members` y la
  consume `bookings` sin conocerlo.
- La lógica de dominio **nunca pregunta si un módulo está habilitado**. Eso se resuelve en el
  borde: el endpoint responde 404 y el job no se encola.

## 5. Configurabilidad por módulos

Es requisito del producto, no una feature futura.

- Cada módulo se declara a sí mismo implementando `IClubModule` (id estable, nombre comercial,
  dependencias, si es núcleo).
- `ModuleCatalog` valida el grafo al arrancar: dependencias inexistentes o ciclos hacen fallar
  el arranque, no producen comportamiento raro en runtime.
- `ModuleCatalog.Resolve` expande al cierre transitivo: contratar `padel` trae `bookings`,
  `finance` y `core` solos.
- `ITenantModules` dice qué tiene contratado el club en curso.
- **Módulo apagado ⇒ 404, no 403.** Quien no contrató un módulo no tiene por qué enterarse de
  que existe.
- **Apagar un módulo no borra datos.** Corta el acceso; los datos quedan.

## 6. Convenciones de código

- **.NET 10**, `nullable` habilitado, `TreatWarningsAsErrors=true`, `InvariantGlobalization=false`
  (el club opera en es-AR y las fechas y montos dependen de la cultura).
- **Idioma** (ADR-0006): **el código va entero en inglés** — clases, métodos, tablas,
  columnas, endpoints, proyectos, ids de módulo, comentarios, mensajes de excepción y nombres
  de tests (`The_product_catalog_is_valid`). En español queda lo que no es código: la
  documentación del repo (ADRs, plan, bitácora, este archivo), los textos de la UI y los
  nombres comerciales (`DisplayName = "Socios"`). Los errores de la API viajan con código de
  regla; el texto en español que ve el operador lo pone el frontend.
- **Comentarios: casi cero** (ADR-0006). Se comenta únicamente lo muy importante que el código
  no puede decir solo —una invariante no obvia, una lista blanca, un orden obligatorio, un
  "a propósito" que sin nota parecería un error—, en una o dos líneas y en inglés. Prohibidos
  los doc-comments decorativos y los resúmenes de lo que ya dice la firma.
- **Nunca un `decimal` suelto para plata**: se usa `Money`, que lleva la moneda.
- **Nunca `DateTime.Now`**: se inyecta `IClock`. Todo lo que el negocio llama "día" se resuelve
  con `ClubCalendar` en la zona del club, no en UTC.
- **Nunca un `TenantId` implícito en background**: `ITenantContext.Current` lanza si no hay
  tenant, a propósito.
- Movimientos de dinero **append-only**: no se editan, se anulan con contra-asiento.
- Las invariantes del dominio se imponen en el agregado y en la base. El sistema de referencia
  las "valida" con carteles en pantalla y por eso tiene datos rotos: grupos familiares de un
  integrante, categorías huérfanas. **Eso no se replica.**

### Comandos

```bash
cd src/backend && dotnet build      # compilar la solución
cd src/backend && dotnet test       # correr los tests
cd src/frontend/backoffice && npm i && npm run dev   # consola del club — :5184
cd src/frontend/reservas && npm i && npm run dev     # portal de reservas — :5183
```

## 7. Los procesos de background

El diseño identifica **11 jobs para el MVP** (detalle completo en `docs/referencia-ourclub/diseno-detallado-socios.html`):

| | Job | Cadencia |
|---|---|---|
| J1 | Expiración de reservas | 30 s |
| J2 | Conciliación de pagos con el proveedor | 5 min |
| J3 | Reproceso de la bandeja de webhooks | 1 min |
| J4 | Despachador de notificaciones (outbox) | 30 s |
| J5 | Apertura de agenda de canchas | diario |
| J6 | Snapshot de habilitación | diario |
| J7 | Preliquidación | bajo demanda |
| J8 | Aplicación de liquidación | bajo demanda |
| J9 | Avisos de cobranza | diario |
| J10 | Recordatorio de reservas | cada hora |
| J11 | Retención y purga | diario |

Reglas que cumple **todo** job, sin excepción: idempotente · lock distribuido por (job, tenant) ·
acotado y por lotes · reanudable · en hora local del club · sin efectos externos dentro de la
transacción · emite métrica de resultado · **recibe el tenant como parámetro explícito**.

Y dos que no son jobs aunque lo parezcan: marcar cargos vencidos (es una comparación de fecha)
y recalcular saldos (se actualizan en la misma transacción del movimiento).

## 8. Estado actual

| | Qué |
|---|---|
| ✅ | Solución por capas (7 proyectos: SharedKernel, Domain, Application, Infrastructure, Api y 2 de tests) |
| ✅ | `SharedKernel`: `TenantId`, `ITenantContext`, `IClock` + `ClubCalendar`, `Money`, `Periodo` |
| ✅ | Modularidad: `ModuleId`, `IClubModule`, `ModuleCatalog` (valida grafo y cierre transitivo), `ITenantModules` |
| ✅ | Manifiestos de módulo de `core` y `members` |
| ✅ | Documentos en `docs/` y prototipo de reservas en `src/frontend/reservas/` |
| ✅ | Cascarón del backoffice en `src/frontend/backoffice/` — 4 pantallas contra un mock (sección 10) |
| ⬜ | Todo lo demás — ver abajo |

**No hay todavía**: persistencia, autenticación, endpoints, jobs, ni un solo agregado de
dominio implementado. Los dos frontends corren contra mocks en memoria: no hay una sola
llamada HTTP real todavía.

---

## 9. Partes a desarrollar

> 📋 Existe un **plan aprobado para el backend de las 4 pantallas del backoffice** (plataforma,
> tenancy, auth, personas, reservas, canchas, horarios y sus tests):
> [`docs/plan-backend-backoffice.md`](docs/plan-backend-backoffice.md). Su avance se registra en
> [`docs/plan-backend-backoffice.bitacora.md`](docs/plan-backend-backoffice.bitacora.md) —
> **leer la bitácora antes de retomar**: dice qué fase está en curso y dónde quedó.
> La implementación **no arranca sin pedido explícito del usuario**.

Leyenda: ✅ hecho · 🚧 bloqueado · ⬜ pendiente

### 9.1 Plataforma (transversal, habilita todo lo demás)

| | Parte | Notas |
|---|---|---|
| ⬜ | **Persistencia** | EF Core + PostgreSQL. Un esquema por módulo. Filtro global por tenant, con lista blanca auditada de los lugares que lo ignoran |
| ⬜ | **Migraciones** | Una por módulo, para que el grafo de módulos se refleje en la base |
| ⬜ | **Tenancy** | Resolución por token/host en HTTP + **ámbito explícito en background**. Test que verifique que un job sin tenant lanza en vez de procesar |
| ⬜ | **Autenticación y roles** | Usuarios, JWT, y los 7 roles operativos de la sección 6 del diseño. Incluye **separación de funciones**: quien calcula la liquidación no puede aprobarla |
| ⬜ | **Configuración de módulos por club** | Persistir qué contrató cada tenant · endpoint de capacidades para el frontend · filtro que devuelve **404** en módulo apagado · gating del despachador de jobs |
| ⬜ | **Infraestructura de jobs** | Hangfire sobre PostgreSQL · lock distribuido por (job, tenant) · despachador que encola una ejecución por tenant y aísla el fallo de uno · registro de resultado por corrida |
| ⬜ | **Outbox de notificaciones** | Tabla + despachador (J4) + proveedor de email. La fila se escribe en la misma transacción que el hecho que la origina |
| ⬜ | **Auditoría** | Quién, cuándo y por qué en cada transición de estado. Es requisito, no un extra |
| ⬜ | **Observabilidad** | Métricas por job y **pantalla de operación** dentro del sistema: última corrida, pagos en revisión manual, outbox fallido, divergencias de habilitación |
| ⬜ | **Contrato de API** | Decidir si se sigue el enfoque contract-first del repo anterior (OpenAPI escrito a mano, frontend generado desde ahí) |

### 9.2 Módulo `core`

| | Parte |
|---|---|
| ⬜ | Agregado **Persona** con sus invariantes y la unicidad de documento impuesta en base |
| ⬜ | Los **tres identificadores buscables**: documento, número de socio, código de credencial |
| ⬜ | Buscador de personas — es la pantalla más usada del sistema |
| ⬜ | Domicilio y datos de contacto |
| ⬜ | Usuarios, roles y asignación |
| ⬜ | Configuración del club: zona horaria, moneda, datos institucionales |

### 9.3 Módulo `members`

| | Parte |
|---|---|
| ⬜ | **Membresía**: alta, baja, suspensión, reactivación, cambio de categoría, cambio de número |
| ⬜ | Catálogo de **categorías** |
| ⬜ | **Grupo familiar**: titular, integrantes, cambio de titularidad, disolución — con las invariantes que el sistema actual no impone |
| ⬜ | Antigüedad derivada + descuento de antigüedad acreditable |
| ⬜ | **Excepciones a la recategorización** como dato versionado por estatuto, no como `if` |
| ⬜ | **Habilitación**: proyección materializada + recálculo por evento + contrato que consumen reservas y, a futuro, el control de acceso |
| ⬜ | Alta express de mostrador ("socio al minuto") |
| ⬜ | **Alta online**: pago → alta, sin que pueda quedar plata cobrada sin socio creado |

### 9.4 Módulo `finance`

| | Parte |
|---|---|
| ⬜ | **Conceptos** y precios por categoría y audiencia, **historizados** |
| ⬜ | Descuentos y becas (uno vigente por membresía en el MVP) |
| ⬜ | **Cuenta corriente**: cargos, imputaciones, pagos, saldo. Append-only |
| ⬜ | **Liquidación**: lote, preliquidación (J7), aplicación (J8), reversión por contra-asientos |
| ⬜ | **Recategorización por edad** dentro de la preliquidación, con previsualización obligatoria |
| ⬜ | **Recibos**: numeración, emisión, anulación individual con motivo |
| ⬜ | **Caja**: sesión por operador, cobro de mostrador, cierre con efectivo declarado y diferencia |
| ⬜ | **Pagos**: gateway abstraído, checkout, webhook idempotente, conciliación (J2), bandeja de revisión manual |
| ⬜ | Listados exportables: deudores, cobranza del período, altas y bajas. **Sin dashboards** |

### 9.5 Módulo `bookings`

| | Parte |
|---|---|
| ⬜ | **Espacio**, grilla horaria y bloqueos |
| ⬜ | **Tarifas** por tipo de espacio × franja horaria × socio/no socio |
| ⬜ | **Materialización de turnos** (J5) — la fila del turno es el punto de serialización |
| ⬜ | **Reserva**: hold con TTL → pago → confirmada, con el `UPDATE` condicional atómico |
| ⬜ | Cancelación con ventana · marcar ausente |
| ⬜ | **Series recurrentes** (turno fijo), creadas por el operador |
| ⬜ | API de agenda día/semana — la UI espera diseño |
| ⬜ | Elegibilidad vía el contrato de habilitación |

### 9.6 Módulos `padel` y `football`

| | Parte |
|---|---|
| ⬜ | Tipos de espacio y duración de turno propios de cada deporte |
| ⬜ | **Definir con el usuario qué difiere realmente** entre ambos más allá de la configuración. Candidatos a discutir: partido abierto para completar jugadores, alquiler de paletas, seña, F5/F7/F11 |

### 9.7 Frontend

| | Parte |
|---|---|
| ✅ | **Backoffice del club** — cascarón implementado en `src/frontend/backoffice/`. Detalle y pendientes en la sección 10 |
| ⬜ | **Portal del socio**: mi cuenta, deuda, pagar, credencial, mis reservas |
| ⬜ | Conectar el prototipo `src/frontend/reservas/` a la API real (hoy corre contra un mock) |

### 9.8 Fase cero — migración del padrón

| | Parte |
|---|---|
| ⬜ | Importador **idempotente y reejecutable**, con **informe de rechazos** por registro |
| ⬜ | Orden: personas → membresías → grupos familiares → saldos → becas → códigos de credencial |
| ⬜ | Decidir: ¿histórico completo de cuenta corriente o sólo saldo de apertura? (recomendación del diseño: saldo de apertura) |
| ⬜ | Resolver **antes de migrar** los datos que violan las invariantes nuevas |

> Sin padrón migrado no hay sistema utilizable. Define la fecha real de puesta en marcha.

### 9.9 Orden sugerido

Cada fase deja algo utilizable. Del documento de diseño:

| Fase | Contenido | Al terminar |
|---|---|---|
| 0 | Plataforma (9.1) + migración del padrón (9.8) | hay socios reales en el sistema |
| 1 | `core` + `members` sin dinero: buscador y ficha | el mostrador puede consultar y dar de alta |
| 2 | `finance`: conceptos, liquidación, cuenta corriente | existe la deuda |
| 3 | Cobro de mostrador, recibos, cierre de caja | el club cobra |
| 4 | Portal del socio + pago online | el socio se autogestiona |
| 5 | `bookings` + `padel` + `football` | las canchas se venden |
| 6 | Habilitación como servicio consumible desde afuera | queda listo para integrar con lo que venga |

La habilitación se **define** en la fase 1 aunque se **integre** en la 6: es la bisagra del
producto y no puede quedar improvisada.

---

## 10. Backoffice — `src/frontend/backoffice/`

Consola de operación del club. Traducción a React del diseño **"Backoffice Consola"** del
proyecto de Claude Design *Diseño Chaco Forever en blanco y negro*, importado el 14/08/2026.

**Corre entero contra un mock en memoria.** No hay una llamada HTTP: es para mostrar, discutir
y validar el flujo con el club antes de que exista la API.

```bash
cd src/frontend/backoffice && npm i && npm run dev   # http://localhost:5184
```

### Qué hay

| Ruta | Pantalla | Qué resuelve |
|---|---|---|
| `/reservas` | Agenda del día | Grilla cancha × media hora. Vender, cobrar, marcar ausencia, cancelar |
| `/canchas` | Editor de cancha | Horario que usa, duraciones, incremento, aviso mínimo, precios, vista previa |
| `/horarios` | Editor de horario | Horas semanales, fechas específicas que pisan la semana, vista de calendario |
| `/personas` | Base de personas | Búsqueda, filtros, ficha, alta de mostrador, importación (sólo la pantalla) |

### Cómo está armado

```
src/
├─ domain/    tipos y lógica pura: horarios, agenda, fechas, dinero
├─ api/       store.ts (estado del mock) · mockApi.ts (funciones async) · queries.ts (React Query)
├─ ui/        theme.ts (paleta y controles) · Panel · Navegación · Tostadas · estados
└─ modulos/   una carpeta por módulo: reservas, canchas, horarios, personas
```

- **React Query es la única fuente de datos.** Nada de estado servidor en `useState`.
- **`api/mockApi.ts` es el contrato.** Cuando exista la API se reemplaza ese archivo por
  llamadas HTTP y las pantallas no cambian. `store.ts` desaparece.
- **Lo que se está mirando vive en la URL** (`rutas.ts`): módulo, deporte, día, filtro,
  búsqueda, ficha abierta. Lo transitorio —qué panel está abierto, un borrador sin guardar—
  se queda en el componente.
- **Los editores trabajan sobre un borrador.** Canchas y Horarios acumulan cambios en estado
  local y recién persisten al Guardar; Descartar vuelve a lo guardado.
- **Estilos inline con tokens** en `ui/theme.ts`, como el diseño. Lo único en CSS es el reset,
  las animaciones y los `:hover` / `:focus`, que un objeto de estilo no puede expresar.

### Lo que falta

| | Parte |
|---|---|
| ⬜ | Conectar contra la API real y borrar `api/store.ts` |
| ⬜ | **Gating por módulo contratado**: hoy los cuatro módulos se montan siempre; falta el endpoint de capacidades y que una ruta de módulo apagado dé 404 |
| ⬜ | Autenticación, roles y las acciones que hoy son sólo un aviso: bloquear horario, reprogramar, WhatsApp, exportar, elegir archivo de importación |
| ⬜ | Accesibilidad: foco visible, navegación por teclado en la grilla, atajo ⌘K que hoy es sólo el cartel |
| ⬜ | Responsive: está pensado para un monitor de mostrador, abajo de ~1000 px no se acomoda |

### Decisiones tomadas sobre el diseño

Van acá porque no se deducen del HTML y conviene revisarlas con el usuario:

- **Ruteo por módulo con react-router** en vez del `module` en estado. El diseño es una sola
  pantalla con un switch; un backoffice necesita URLs.
- **El precio de un turno sale de la tarifa de la cancha** (`precioDia` / `precioNoche` /
  `noche`), no de una constante por deporte. Con la configuración de fábrica da exactamente lo
  mismo que el diseño, pero además responde si el club cambia un precio.
- **"Descartar" vuelve a lo guardado**, no a los valores de fábrica.
- **Las duraciones que ofrece el panel de venta siguen siendo por deporte** (pádel 1 h / 1 h 30
  / 2 h, fútbol 1 h / 2 h), como en el diseño, aunque cada cancha ya tenga las suyas
  configurables. Es la inconsistencia que quedó: hay que decidir cuál manda.
