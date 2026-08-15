# Módulos Personas y Socios — el núcleo del sistema

Es el pain #1. Todo el resto del sistema cuelga de acá.

---

# 7. Personas

## 7.1 Gestión de Personas — `#/PersonasBuscador`

**H:** Personas · **BC:** Inicio / Personas / Gestion de Personas

| Label | Tipo | name | Placeholder |
|---|---|---|---|
| Nro. Socio | text | `nrosocio` | Numero de socio |
| Apellido | text | `apellido` | Apellido |
| Nombre | text | `nombre` | Nombre |
| Documento | text | `documento` | Numero de documento |
| **Codigo Usos Multiples** | text | `codigoum` | Codigo UM |

Botones: **`Alta Personas`** · `Buscar` · `Limpiar`

> 🔎 **`Codigo Usos Multiples` (Codigo UM)** es un identificador alternativo buscable de la
> persona — el número que lleva impreso/codificado la credencial. Es la llave con la que un
> lector de acceso en la puerta del estadio identifica a alguien sin tipear el DNI.
> **Es el puente entre el padrón y el control de acceso.** Nuestro modelo lo necesita:
> `Persona` tiene ≥ 3 identificadores (DNI, Nro Socio, Código UM) y los tres son buscables.

## 7.2 Consulta Migración — `#/persona/migracion_consulta`

**H:** Migracion · **BC:** Inicio / Gestion

| Label | Tipo | name | Opciones |
|---|---|---|---|
| (toggle avanzada) | checkbox | — | |
| Documento | text | `documento` | |
| Apellido | text | `apellido` | |
| Nombre | text | `nombre` | |
| **Nro. Socio (SAS)** | text | `nrosocio` | |
| Migrable | radio | `optionsRadios_migrable` | `Si` / `No` / **`Todos`** (default) |

> 🔎 **`SAS` es el sistema legado** del que migraron. Sigue existiendo una pantalla viva de
> conciliación con un flag **`migrable` Sí/No**: hay registros históricos que **nunca se
> pudieron migrar**. La migración no terminó — quedó como estado permanente.
> Confirmado en la ficha del socio con el campo `Nro Sistema Anterior Socios` (`nrosocio_sas`).

---

# 8. Socios

## 8.1 Gestión de Socios — `#/socios` → "Socios Individuales"

**H:** Socios · **BC:** Inicio / Socios / Socios Individuales

Buscador idéntico al de Personas: `Nro. Socio` · `Apellido` · `Nombre` · `Documento` ·
`Codigo Usos Multiples`. Botones `Buscar` / `Limpiar`.

### Resultados

Búsqueda por apellido `lopez` → **100 resultados** (tope de página).
Filtros por columna en línea: `filtrar documento…` · `filtrar apellido…` · `filtrar nombre…` · `filtrar socio…`
Botón de toggle de vista tabla (ícono arriba a la derecha).

Cada fila muestra: **Documento** (link) · **Apellido y Nombre** · **Estado** · **Email** ·
**Edad** · botón **`Ingresar`**.

Formato del estado observado:
- `BAJA` (en rojo)
- `VIGENTE (ACTIVO)` + línea `Socio: 00000193`
- `VIGENTE (ACTIVO (GRUPO FAMILIAR))` + `Socio: 00011730`

> 🔎 El estado se renderiza como **`VIGENTE (<categoría>)`**: estado y categoría son campos
> distintos que la UI concatena. Un socio de baja **no muestra número de socio** — el número
> se libera/oculta al dar de baja.

## 8.2 Ficha del Socio — `#/socios/ficha/{personaId}`

Ejemplo relevado: `#/socios/ficha/126` (socio `00000193`).
**H:** Gestion de Socios · **BC:** Inicio / Socios / Ficha del Socio

Es la pantalla más densa del sistema. **14 secciones.**

### Cabecera (siempre visible)

| Elemento | Ejemplo |
|---|---|
| Foto | imagen del socio (placeholder si no hay) |
| Nombre completo | IGNACIO OSCAR LOPEZ |
| Email | yoni2018lopez@gmail.com |
| Edad | 47 Años. |
| Estado de la **persona** | ACTIVO |
| Link | **`e-Carnet`** (credencial digital) |
| Bloque Socio | `Socio` / **00000193** / **"4 Años y 5 Meses De Antigüedad"** / `Vigente` |
| Bloque deuda | **`$25.000,00 Deuda al 08/2026`** · **`$25.000,00 Deuda Total`** + link **`Ir a Caja`** · **`07/2026 - Ult. Cuota Social Paga`** |

> 🔎 La cabecera responde de un vistazo las 4 preguntas del mostrador: **quién es, si está
> vigente, cuánto debe y hasta cuándo pagó.** Es el diseño correcto para atención presencial.
> La **antigüedad** se muestra calculada en años y meses — es un dato con peso social/estatutario.

### Barra de roles (tabs)

`Ficha Persona` · `Ficha Socio` · `Alta Alumno` · `Alta Deportista` · `Alta Profesor` · `Alta Cobrador`
(cuando el rol ya existe, el botón dice `Ficha …` en vez de `Alta …`; se observaron ambos: `Alta Socio`/`Ficha Socio`, `Alta Alumno`/`Ficha Alumno`)

> 🔎🔎 **Modelo de roles confirmado.** Una `Persona` puede acumular los roles
> **Socio · Alumno · Deportista · Profesor · Cobrador**, y la ficha es un contenedor con una
> pestaña por rol. No hay herencia: son agregados separados que comparten `personaId`.
> El `personaId` (126) es la clave de la URL, **no** el número de socio.

### Sección "Datos de Socio"

Banner de trazabilidad del origen:
`EMPADRONAMIENTO (Tramite: 00001370 - Estado: FINALIZADO)`
(otro literal detectado: *"El socio posee un registro web nro. \_ en estado \_."*)

| Label | Tipo | name | Ejemplo / notas |
|---|---|---|---|
| Nro. Socio | text (readonly) | `nrosocio_text` | `00000193` |
| Nro Sistema Anterior Socios | text | `nrosocio_sas` | `193` — ph "Nro Sistema Anterior" |
| Fecha Alta: | **date** | `fechaAlta` | `2022-03-02` |
| Descuento Antiguedad (Meses) | text | `descuento_antiguedad_meses` | `0` — ph "Cantidad de descuento de antiguedad, expresado en meses" |
| Filial | select2 | `filialid` | vacío ("No results match") |
| Legajo | text | `legajo` | vacío |
| **socio al minuto** | checkbox | — | destildado |

Botones: `Modificar` · `Historia del Socio` · `Cambiar Nro Socio` · `Generar Nro Socio` ·
**`Suspender`** (rojo) · **`Baja`** (rojo)

> 🔎 **Tres estados de socio, no dos:** `Vigente` / `Suspendido` / `Baja`. La suspensión es un
> botón propio, distinto de la baja — se usa para inhabilitar sin perder la antigüedad.
> 🔎 **`Descuento Antigüedad (Meses)`** permite acreditar antigüedad manualmente: un socio que
> vuelve puede recuperar años. La antigüedad es un activo negociable, no un cálculo puro de fechas.
> 🔎 **`socio al minuto`** = alta express en el mostrador (sin el trámite completo).
> 🔎 `Cambiar Nro Socio` + `Generar Nro Socio`: **el número de socio es mutable**. No sirve como
> clave primaria; es un identificador de negocio reasignable.

### Sección "Categorias de Socio y Grupo Familiar (N Resultado/s)"

Encabezado del grupo: categoría del grupo (`ACTIVO`) + botón `Modificar` + badge
**`3 Integrantes`** + fecha de vigencia (`5/1/2023`).

Columnas: `Nro Socio ; Apellido y Nombre ; Categoria ; Estado ; Titular ; Fecha Desde`

Ejemplo real:

| Nro Socio | Categoría | Estado | Titular | Fecha Desde |
|---|---|---|---|---|
| 00000193 | **ACTIVO (De Titular)** | `VIGENTE` | ✔ | 5/1/2023 |
| 00010371 | CADETE INFANTIL (GRUPO FAMILIAR) | `BAJA` | — | 5/1/2023 |
| 00012518 | CADETE MENOR (GRUPO FAMILIAR) | `BAJA` | — | 5/1/2023 |

Sub-bloque **"Excepción a la Preliquidación"** → botón `Administrar Excepción`.
Botones del bloque: `Cambio Titularidad` · `Cancelar Titularidad` · `Nuevo Titular`

Tipos de excepción (select `tipoexcepcionid`):
`DISCAPACIDAD` · `ADHERENTE - NO VITALICIA` · `VITALICIO VOLUNTARIO` ·
`CATEGORIA HIJA MAYOR A 24 AÑOS (ESTATUTO 2011)`

> 🔎🔎 **El grupo familiar es una entidad con titular, fecha de vigencia y categoría propia.**
> La categoría del integrante *deriva* de la del titular (`ACTIVO` → `CADETE … (GRUPO FAMILIAR)`).
> Cada integrante mantiene **su propio estado**: el titular vigente con dos hijos de baja.
> `Cambio de Titularidad` es una operación de negocio de primer orden (fallecimiento, divorcio).
> 🔎 Las **excepciones citan el estatuto** (`ESTATUTO 2011`). Las reglas de categorización son
> normativa del club, versionada por año. Hay que modelarlas como datos, no como `if`s.

### Sección "Cuenta Corriente (N Resultado/s)" — 75 registros en el caso relevado

Columnas: `Registro ; Periodo ; Concepto ; Monto ; Estado ; Recibo ; Fecha Pago`
Filtros en línea: `periodo…` · `concepto…` · `estado…` · `cobrado_por…` (cobrador)
Botones: `Factura` · `Plan Pago` · `Deuda Por Mes` · `Accion` · `Historia Cuenta Corriente` ·
`Seleccionar Todos` · `Deseleccionar Todos` · `Agregar` · `Eliminar`
Campos de factura: `Número *` (`numero`, ph "Nro Factura") · `Fecha` (date, ph "Fecha Factura")

> 🔎 **`Plan Pago`** existe como acción sobre la cuenta corriente → refinanciación de deuda
> en cuotas (coherente con el concepto `INTERESES PAGO EN CUOTAS`).

### Sección "Frecuencias Pago por Tipo de Concepto"

Campos: `Tipo Concepto*` (`tipoConcepto`) · `Frecuencia de Pago *` (`frecpago`)

### Sección "Formas de Pago por Tipo de Concepto (N Resultado/s)"

Columnas: `Tipo Concepto ; Forma de Pago ; Fecha Desde ; Fecha Hasta`
Campos: `Tipo Concepto *` · `Forma de Pago *` (`medioPago`) ·
`Cobradores *` (`cobradorpersona`) → `GONZALEZ, ENZO DE JESUS` ·
`Domicilio de Cobro` (`cobradordomicilio`) → domicilios reales del socio para cobro puerta a puerta

Adhesiones a débito:
`Medio de Pago adhesión *` (ph *"No se registran adhesiones"*) ·
`Tarjeta de Pago *` (ph *"No se registra tarjeta"*) ·
`Cuenta Bancaria Pago *` (ph *"No se registra cuenta"*)

Botones: `Adherir Medio de Pago` · `Quitar Adhesion` · `Agregar Tarjeta` · `Quitar Tarjeta` ·
`Agregar Cuenta` · `Quitar Cuenta` · `Baja Adhersion` (sic) · `Historial Formas de Pago` ·
`Volver a Formas Pago Vigentes`

> 🔎 **La forma de pago se configura por tipo de concepto y con vigencia (desde/hasta).**
> Un socio puede pagar la cuota social por débito y la actividad deportiva por cobrador.
> Es una matriz `socio × tipoConcepto → medioPago`, historizada. Mucho más fino que un
> "método de pago por defecto".
> 🔎 El **cobrador domiciliario sigue vivo** en 2026 y tiene domicilio de cobro asociado.

### Sección "Promociones Vigentes"

Campos: `Concepto *` · **`Beca (Opcion 1)`** · **`Beca (Opcion 2)`** (ambos select `concepto`)

Catálogo de becas/descuentos:

| Beca | Descuento |
|---|---|
| DESCUENTO X HERMANOS | 20 % |
| DESCUENTO ESPECIAL SOCIOECONOMICO | 50 % |
| BECADOS X CLUB 50% | 50 % |
| BECADOS X CLUB 100% | 100 % |

Botones: `Acceder al Descuento` · `Adherirse al Pago Voluntario`

> 🔎 **Se acumulan hasta 2 becas** (Opción 1 + Opción 2) — hay que definir si suman o si
> aplica la mayor. Y existe **`Pago Voluntario`**: socios que pagan de más por voluntad propia.

### Otras secciones

| Sección | Contenido |
|---|---|
| **Socios Referidos (N)** | 0 en el caso relevado — programa de referidos |
| **Conceptos asociados (N)** | 1 resultado |
| **Personas con Discapacidad Vigente (Sin Documento de Respaldo)** | control de excepciones sin respaldo documental |
| **Documentación Adjunta** | `Upload Archivo` · `Eliminar Archivo` |
| **Carnet Socio** | emisión de credencial (ligado al `e-Carnet` de la cabecera) |
| **Observaciones** | textarea ph *"Escribir Observacion de la Persona…"* |
| **Otras Acciones** | `Ingresar` · `Volver a Otras Acciones` · `Recuperar` · `Restauración manual` · `Enviar Correo` |

### Mensajes de estado / validaciones capturados (literales)

| Mensaje | Lectura |
|---|---|
| **`El socio NO se encuentra Habilitado.`** | 🎟️ **habilitación** — bandera distinta de "vigente" |
| `El socio tiene la excepcion a la preliquidacion Baja Excepcion.` | excepción activa al proceso de liquidación |
| `El socio tiene asignado una categoria de grupo famililar, pero no se encuentra asignado a ninguna.` (sic) | inconsistencia de datos detectada en runtime |
| `El socio se encuentra dentro de un grupo familiar, pero de un solo integrante, puede ser que le falten integrantes o modifiquelo como socio individual.` | idem |
| `Atencion! El socio posee un registro web nro. \_ en estado \_.` | trámite web abierto sin resolver |

> 🔎🔎 **Esto es lo más valioso de la pantalla.** El sistema **no puede garantizar la
> consistencia de sus propios datos**, así que la valida en pantalla y se la avisa al operador.
> Grupos familiares de un solo integrante, categorías de grupo sin grupo, trámites web
> huérfanos. Son **invariantes de dominio que la base no impone**.
> En la reconstrucción, cada uno de estos mensajes debe convertirse en una **invariante del
> agregado** (imposible de violar) en vez de una advertencia cosmética.

> 🔎 **`El socio NO se encuentra Habilitado`** es la llave del pain #2: la habilitación
> —que depende de la deuda— es lo que decide si el socio puede comprar entrada / entrar al
> estadio. Es el acoplamiento real entre gestión del club y boletería.
