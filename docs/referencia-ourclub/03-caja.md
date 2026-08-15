# Módulo Caja

Punto de venta / cobranza presencial del club. 6 pantallas.

---

## 3.1 Gestión de Caja — `#/caja/gestion/`

**Breadcrumb:** Inicio / Gestion · **Título:** Caja

Es el **POS**. Flujo: identificar a la persona → cargar conceptos a un carrito → confirmar → cobrar.

### Bloque 1 — Identificación de la persona

| Campo | Tipo | Placeholder |
|---|---|---|
| Nro Socio | text | — |
| Documento | text | — |
| Apellido | text | Apellido |
| Nombre | text | Nombre |

Botones: **`Verificar`** (lupa) · **`Limpiar`** (X)

> El botón se llama *Verificar*, no *Buscar*: además de localizar a la persona valida su
> **habilitación para operar** (estado de socio, deuda, derecho de admisión).

### Bloque 2 — Carrito

- Panel **"Listado de Conceptos (N) items"** con ilustración de carrito vacío cuando N=0.
- Botón **`Confirmar`** al pie del listado.

### Bloque 3 — Totalizador (columna derecha)

- Panel **"Total Conceptos"** → etiqueta `Total` → importe grande (`$0,00`).
- Leyenda: *"Una vez confirmado no se podran modificar los conceptos"*.
- Botón **`Confirmar`** (duplicado del anterior).

### Acción global

- Botón **`Nuevo Pago`** arriba a la derecha.

> 🔎 **Confirmar es irreversible.** El sistema avisa explícitamente que tras confirmar no se
> pueden modificar los conceptos. La corrección posterior no es "editar" sino **anular**
> (existe una pantalla entera de *Recibos Anulados*). Modelo de **asiento inmutable +
> contra-asiento**, no de registro mutable. Vale replicarlo.

---

## 3.2 Consulta Deuda Total — `#/caja/consulta_total_ctacte`

**Breadcrumb:** Inicio / Deuda Total Mensual
**Encabezado:** *CONSULTA DEUDA TOTAL AGRUPADA POR MES*
**Bajada literal:** "Desde esta opcion se pueden consultar la deuda de **SOCIOS, DEPORTES y COLEGIO**. Tanto individual como total por grupo familiar."

### Filtros

| Campo | Tipo | Placeholder |
|---|---|---|
| Documento | text | Numero de documento |
| Apellido | text | Apellido |
| Nombre | text | Nombre |
| Nro Socio | text | Nro Socio |

Botones: **`Buscar Persona`** · **`Limpiar`**
Nota al pie: *"\* Complete algún campo para realizar la búsqueda."* (no permite listar todo)

### Resultado — "Personas Encontradas"

Lista con columnas: `Nro Socio (8 díg.) | Documento | Apellido y Nombre | [Seleccionar]`

### Ficha de la persona seleccionada (tras `Seleccionar`)

- Avatar (placeholder genérico si no hay foto)
- Nombre completo
- Email — o la leyenda **`No Registra email`**
- Estado de la **persona**: `ACTIVO`
- Bloque derecho: etiqueta `Socio` + **Nro. de socio** + estado del **socio**: `BAJA`
- Detalle de deuda o el mensaje *"No posee deuda para socios, deportes y colegio."*

> 🔎 **Tres fuentes de deuda unificadas en una sola consulta** (socios / deportes / colegio)
> y agrupables **por grupo familiar**. La cuenta corriente no cuelga de la persona sino que se
> consolida a nivel de **grupo familiar** — el titular ve y paga la deuda de todo el grupo.

---

## 3.3 Cierre de Caja — `#/caja/cierre_caja`

**Breadcrumb:** Inicio / Cierre de Caja

| Campo | Tipo | Valor por defecto |
|---|---|---|
| Fecha | date | fecha del día (`12/08/2026`) |
| Pagos web → `Incluir pagos ON LINE` | checkbox | **✅ tildado** |
| Deb. Autom. → `Incluir pagos DEBITO AUTOMATICO` | checkbox | ☐ destildado |
| Busqueda Avanzada | checkbox | ☐ destildado |

Botones: **`Buscar`** · **`Limpiar`** · Mensaje inicial: *"Realice una busqueda"*

> 🔎 El cierre diario **consolida tres orígenes de cobro**: caja física, pagos online y débito
> automático. Que online venga tildado por defecto y débito automático no, sugiere que el
> débito se concilia en un proceso aparte (ver módulo *Presentaciones*).

---

## 3.4 Consulta Caja — `#/caja/consulta_caja`

**Breadcrumb:** Inicio / Consulta Caja

| Campo | Tipo | Placeholder |
|---|---|---|
| Nro. Recibo | text | Numero de Recibo |
| Fecha | date | dd/mm/aaaa |
| Nro. Documento | text | Numero de documento |
| Nro. Socio | text | Numero de socio |
| Pagos web → `Incluir pagos DECIDIR` | checkbox | **✅ tildado** |
| Busqueda Avanzada | checkbox | ☐ |

Botones: **`Buscar`** · **`Limpiar`**

> 🔎🔎 **DATO TÉCNICO CLAVE: el gateway de pago es DECIDIR** (Prisma Medios de Pago, Argentina).
> Aparece nombrado en la UI, lo que significa que la integración **no está abstraída**: el
> nombre del proveedor se filtró hasta la pantalla del operador. En nuestro sistema esto debe
> ser `IPaymentGateway` con el proveedor como detalle de infraestructura.

---

## 3.5 Recibos Anulados — `#/caja/consulta_anulados`

Consulta de recibos anulados. (Ver módulo Recibos para el detalle de estados.)

---

## 3.6 Consulta Conceptos — `#/caja/consulta_conceptos`

**Breadcrumb:** Inicio / Consulta Conceptos

Es el **catálogo de precios** del club. Pantalla crítica para entender el modelo de facturación.

### Filtros

| Campo | Tipo | Opciones |
|---|---|---|
| Tipo Concepto | select2 `tipoconceptoid` | ESCUELA · VENTAS VARIAS · OTROS · SOCIOS · ACTIVIDADES · EGRESOS CAJAS · SEGURO DEPORTISTA · **ENTRADA** · **EVENTOS** · **ABONOS** |
| Concepto | select2 `conceptoid` | dependiente del tipo (cascada) |
| Genera Deuda | radio | Si / No |
| Por Periodo | radio | Si / No |

Botones: **`Buscar`** · **`Limpiar`**

### Columnas de resultado

`TipoConcepto | Concepto | Liquidacion | Periodos | Monto`

DataTable con `Show [10/25/50/100] entries`, `Search:`, paginación `Previous / 1 / Next`.

### Catálogo real — Tipo `SOCIOS` (7 conceptos)

| Concepto | Liquidación | Períodos | Categoría | Monto |
|---|---|---|---|---|
| BAJA | — | — | — | $10,00 |
| **CUOTA SOCIAL** | Genera Deuda | Ene…Dic (los 12) | ACTIVO | **$25.000,00** |
| | | | ACTIVO (GRUPO FAMILIAR) | $22.000,00 |
| | | | CADETE INFANTIL | $20.000,00 |
| | | | CADETE INFANTIL (GRUPO FAMILIAR) | $18.000,00 |
| | | | CADETE MENOR | $22.000,00 |
| | | | CADETE MENOR (GRUPO FAMILIAR) | $20.000,00 |
| GASTO ADMINISTRACION | — | — | — | (sin monto) |
| INSCRIPCION POR DISCIPLINA | Genera Deuda | Ene…Dic | — | $15.000,00 |
| PAGO INSCRIPCIÓN | Genera Deuda | Ene…Dic | — | $2.000,00 |
| REACTIVACION SOCIO | Genera Deuda | — | — | $30.000,00 |
| REACTIVACION SOCIO GRUPO FAMILIAR | Genera Deuda | — | — | $40.000,00 |

### Catálogos vacíos

`ENTRADA` → 0 conceptos · `ABONOS` → 0 conceptos.

> 🔎 **Las entradas y abonos NO se tarifan desde Caja**, aunque el tipo de concepto existe.
> El precio de las entradas vive dentro del módulo *Eventos/Boletería*. Los tipos
> `ENTRADA`/`EVENTOS`/`ABONOS` existen sólo como **clasificación contable** para que la
> recaudación de boletería impacte en la caja del club.

### Reglas de negocio que se leen del catálogo

1. **El precio depende de la categoría de socio**, no del concepto. Un concepto tiene *N* precios.
2. **Existe descuento por grupo familiar** de forma sistemática: −$3.000 en ACTIVO,
   −$2.000 en los CADETE. Es una dimensión de precio, no una promoción puntual.
3. **`Genera Deuda` es un flag del concepto.** Hay conceptos que se cobran sin generar
   cuenta corriente (ej. `BAJA` a $10, que es simbólico/administrativo).
4. **`Períodos` define en qué meses del año aplica** la liquidación automática. La cuota
   social corre los 12 meses; la reactivación no tiene período (es un evento puntual).
5. **Dar de baja cuesta $10 y reactivar cuesta $30.000** ($40.000 en grupo familiar).
   Penalización deliberada a la intermitencia: es 1,2 cuotas de castigo por volver.
