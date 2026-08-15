# Módulos operativos (9–14, 16–26)

Relevamiento de los módulos restantes. Menos centrales para los dos pains, pero necesarios
para el inventario completo.

---

## 9. Cobradores

Cobranza domiciliaria con talonario físico. **Un solo cobrador activo: `GONZALEZ, ENZO DE JESUS`.**

### 9.1 Gestión de Cobradores — `#/CobradoresBuscador`
Buscador estándar de persona: `Nro. Socio` · `Apellido` · `Nombre` · `Documento` · `Codigo Usos Multiples`.

### 9.2 Rendiciones — `#/CobradoresRendiciones/consulta_rendiciones`
**H:** Caja · **BC:** Inicio / Consulta Rendiciones

| Label | Tipo | name | Opciones |
|---|---|---|---|
| Nro. Recibo | text | `reciboid` | |
| Fecha Desde / Hasta | date | `fechadesde` / `fechahasta` | |
| Estado | radio | `optionsRadios_cerrada` | `Cerradas` / **`Abiertas`** (default) |
| Cobrador | select2 | `cobradorid` | GONZALEZ, ENZO DE JESUS |

### 9.3 Control — `#/CobradoresControl`
**BC:** Inicio / Control de Recibos de un Cobrador — **arqueo de talonario**.

Campos: `Cobrador` (select) · `Numero Recibo` (text `reciboid`)
Botones: `Iniciar Control` · `Agregar Recibo` · `Guardar Arqueo` · `Finalizar Control`

Clasificación de recibos durante el arqueo (cada una con su listado):
**Rendidos** · **Pago en Caja** · **Anulados** · **Pendientes** ·
**Recibos Perdidos** · **No Encontrados** · **Total Recibos Ingresados**

Columnas: `Recibo ; Fecha Emision ; Fecha Vencimiento ; Pagador ; Importe`

> 🔎 Existe la categoría **"Recibos Perdidos"**. El talonario es papel numerado y a veces
> se pierde. Es un proceso de auditoría física dentro del software.

### 9.4 Cierre — `#/CobradoresRendicionesCierre`
`Cobrador` + `Fecha Desde` / `Fecha Hasta`.

### 9.5 Otras
`Rendir` (`/CobradoresRendiciones/-1`) · `Reasignaciones`
(`/CobradoresRendicionesCambioAsignacionRecibo`) · `Consulta Recibos`
(`/CobradoresConsultaRecibo`) · `Arqueos` (`/CobradoresRendiciones/consulta_arqueos`)

---

## 10. Deportes

### 10.1 Actividades Deportivas — `#/ActividadesDeportivas/false`

| Label | Tipo | name | Opciones |
|---|---|---|---|
| Disciplina * | select2 | `actividaddisciplinaid` | cascada |
| Categoria * | select2 | `actividadcategoriaid` | cascada |
| **Frecuencia *** | select2 | `actividadfrecuenciaid` | `MENSUAL` · `1 VEZ X SEMANA` · `2 VECES X SEMANA` · `3 VECES X SEMANA` · `5 VECES X SEMANA` |
| **Lugar *** | select2 | `actividadlugarid` | `SEDE SOCIAL` · `ESTADIO` · `MICROESTADIO` · `PLAYON 1` · `PLAYON 2` · `PREDIO` · `SALON` |
| Detalle Horario | textarea | `detalle_horario` | ph *"Detalle de Horas de la Actividad…"* |
| Observaciones | textarea | — | |
| Edad Desde / Edad Hasta | number | — | ph `Ej: 6` / `Ej: 12` |
| **Cupo Máximo** | number | — | ph `Ej: 12` |
| Psicofísico | select | — | `Todos` / `Sí` / `No` |
| Inscripción Portal | select | — | `Todos` / `Sí` / `No` |
| Info Portal | select | — | `Todos` / `Sí` / `No` |
| Gestion | select | — | `Todos` / `Sí` / `No` |

> 🔎 **`Cupo Máximo` + `Psicofísico`** — la actividad deportiva tiene control de cupo y
> requisito de apto físico, igual que un evento tiene capacidad y política de acceso.
> Es el mismo patrón aplicado a otro dominio.

### 10.2 Deportistas — `#/deportistas`
Buscador estándar + `Disciplina` + `Categoria`.

### 10.3 Profesores — `#/profesores`
Buscador estándar + `Disciplina` + `Categoria` +
**`Cargo`** (`profesortipoactividadids`): `COORDINADOR` · `PROFESOR` · `DIRECTOR TECNICO` ·
`AYUDANTE` · `PREPARADOR FISICO` · `DELEGADO`

### 10.4 Aranceles — `#/deportes/consulta`
**H:** Deportistas · **BC:** Inicio / Deportes / Aranceles de Actividades Deportivas
Sección *"Conceptos de Actividades"*. Requiere seleccionar disciplina.

Columnas: `Disciplina ; Categoria ; Frecuencia ; Detalle Horario ; Concepto ; Socios ;
Inscripcion ; Cambio Cat. ; Genera Deuda ; Monto`

> 🔎 La columna **`Socios`** en la grilla de aranceles ⇒ **precio diferencial socio / no socio**
> también en deportes. Consistente con el 68,6 % de deportistas no socios.

### 10.5 Becas — `#/ActividadesBecas`
**H:** ABM - Becas · **BC:** Referencias / Becas de Actividades

| Label | Tipo | name |
|---|---|---|
| Descripcion | text | `descripcion` |
| Comentarios | text | `comentarios` |
| *"Chequear en el caso que el descuento sea un porcentaje. Caso contrario es un monto fijo"* | checkbox | — |
| Descuento * | **number** | `monto` |

Columnas: `Descripcion ; Comentarios ; Descuento ; Acciones` — **3 becas cargadas**.

> 🔎 El descuento es **porcentaje o monto fijo**, discriminado por un booleano. Modelar como
> `Discount { type: PERCENT|AMOUNT, value }`.

### 10.6 Otras
`Gestion Aranceles` (`/ActividadesDeportivasArancelMasivo`) ·
`Liquidación Deportistas` (`/liquidacion_deportes`)

---

## 11. Colegio / Escuela

El club opera un colegio. Jerarquía académica: **Nivel → Grado → Curso → División + Turno**.

### 11.1 Alumnos — `#/alumnos`
**H:** Escuela · **BC:** Inicio / Escuela / Gestion de Alumnos
Buscador estándar + `Nivel` (`colegionivelid`) · `Grado` (`colegiogradoid`) ·
`Curso` (`colegiocursoid`) · `Turno` (`colegioturnoid`)

### 11.2 Divisiones — `#/escuela/ColegioDivisionesCons`
**H:** ABM - Divisiones · **BC:** Inicio / Configuraciones Escuela / Divisiones
Columnas: `Nivel ; Grado ; Turno ; Acciones`

### 11.3 Liquidación Manual — `#/ColegioArancelMasivo`

---

## 12. Ecommerce

Tienda de indumentaria/merchandising del club.

### 12.1 Productos — `#/productos_ecommerce`
**H:** Gestión de Productos

| Label | Tipo | Opciones |
|---|---|---|
| Nombre * | text | |
| Descripción | textarea | ph *"Ej: Remera técnica ideal para entrenamiento, tela liviana y respirable…"* |
| Tipo / Género / Uso | select | catálogos de clasificación |
| Estado (filtro) | select | `Todos` / `Activos` / `Inactivos` |
| Imagen | radio `Archivo`/`Url` + file + text (`https://…`) | |

Grilla principal: `Imagen ; Nombre ; Descripcion ; Tipo ; Género ; Uso`
**Grilla de variantes: `Img ; Color ; Talle ; SKU ; Precio Socio ; Precio No Socio ; Stock ; Activo`**
Grilla de movimientos de stock: `Fecha ; Tipo ; Cantidad ; Precio ; Usuario ; Observación`
Botones: `Nuevo Producto` · `+ Agregar fila` · `Guardar variantes` · `Agregar imagen`

> 🔎🔎 **`Precio Socio` / `Precio No Socio` como columnas del SKU.** La dualidad socio/no-socio
> atraviesa todo el sistema: cuota, arancel deportivo, entrada y merchandising.
> **Es LA dimensión transversal del modelo de precios.** En la reconstrucción debe ser un
> concepto de primer orden (`PriceList` por audiencia), no un `if socio`.
> 🔎 Modelo producto→variante con SKU, color, talle y stock propio: e-commerce completo, con
> historial de movimientos de stock auditado por usuario.

### 12.2 Ventas — `#/ventas_ecommerce`
**H:** Gestión de Ventas

| Label | Tipo | Opciones |
|---|---|---|
| Orden ID | number | |
| Búsqueda | text | ph *"Buscar por documento, nombre o producto…"* |
| Estado | select | `Todos` · **`Reservado`** · **`Entregado`** · **`Anulado`** |

Columnas: `ID ; Fecha ; Persona ; Estado ; Pago ; Total ; Acciones`

> ⚠️ **Error observado en producción:**
> `Error al listar órdenes: An error occurred while reading from the store provider's data reader.`
> Excepción de EF/ADO.NET filtrada crudamente a la UI. Confirma stack **.NET + Entity Framework**.

> 🔎 Estado de venta `Reservado → Entregado → Anulado`: **el mismo patrón reserva→confirmación**
> que en boletería. Vale unificar el concepto en la reconstrucción.

### 12.3 Otras
`Vender` (`/vender_ecommerce`) · `Rentabilidad` (`/reportes_ecommerce`) ·
`Configuracion` (`/catalogo_ecommerce`)

---

## 13. Reservas Club+

Reserva de espacios deportivos (canchas, salones).

### 13.1 Gestor de Espacios — `#/actividades`
**H:** Reservas Club+ · **BC:** Inicio / Gestor de Espacios

| Label | Tipo | name |
|---|---|---|
| (nombre actividad) | text | `nombre` — ph *"filtar actividad…"* (sic) |
| Descripcion (Opcional) | text | — |
| **Fecha y Hora de Inicio** | **datetime-local** | — |
| **Fecha y Hora de Fin** | **datetime-local** | — |
| Precio | number | `sede` ⚠️ *name incoherente con el label* |
| **Politica** | select2 | **`accespoliticaid`** |

Botones: `Añadir Actividad` · `Guardar` · `Cancelar`

> 🔎🔎 **`accespoliticaid`: el mismo motor de políticas de acceso que la boletería.**
> Reservar una cancha y comprar una entrada usan el **mismo componente de elegibilidad**.
> Es la confirmación más fuerte de que la política de acceso es un servicio transversal
> del sistema, no una feature de ticketing.

### 13.2 Consulta — `#/reservas_consultas`

| Label | Tipo | name | Nota |
|---|---|---|---|
| Documento / Apellido / Nombre / Socio | text | `documento` `apellido` `nombre` `nrosocio` | |
| Fecha Desde | date | `fechadesde` | default = hoy |
| Fecha Hasta | date | — | |
| **Incluir reservas canceladas** | checkbox | — | |
| Actividad | select2 | `actividadid` | |
| Espacio | select2 | `espaciodeporteid` | |

Filtros de columna: `actividad_nombre` · `espacio_nombre` · `fechahorareserva` · `documento`
Botones: `Buscar` · `Limpiar` · **`Pendientes Hoy`**

### 13.3 Reservar — `#/reservar_espacio`

---

## 14. Empresas — `#/EmpresasGestion`

**H:** Empresas · **BC:** Inicio / Gestión de Empresas

Filtros: `Nombre` (`nombre`) · `razonsocial` · **`CUIT`** (`cuit`)
Botones: `Alta de Empresa` · `Ver` — **2 empresas cargadas**
Consulta asociada: `#/EmpresasConsulta`

> 🔎 Sponsors y proveedores. Se conecta con los protocolos `SPONSOR 2026 PLATEA 14` y
> `SPONSOR VIP1`: la empresa es titular de un cupo de accesos.

---

## 16–17. Control Rápido / Control Acceso

Ítems de menú **sin submenú ni href** en la sesión relevada (no navegables).
La pantalla de Configuraciones confirma que existe el área:
> *"CONTROL DE ACCESOS — Desde esta opción se puede administrar las **políticas y reglas para
> la aplicación de control de acceso**."*

Se corresponden con la **app móvil de control de acceso** (los usuarios `SEGURIDAD, CONTROL 1–8`).
⚠️ **Pendiente de relevar** — requiere permisos o el cliente móvil.

---

## 18. Filiales — `#/Filiales/Gestion`

**H:** Gestion de Filiales · **BC:** Inicio / Filiales / Administracion de Filiales

| Label | Tipo | name | Opciones |
|---|---|---|---|
| Descripcion * | text | `descripcion` | |
| Domicilio * | text | `domicilio` | ⚠️ ph dice "Descripcion" (copy/paste) |
| Provincia | select2 | `provinciaid` | **24 provincias argentinas** |
| Localidad | select2 | `asentamientoid` | cascada desde provincia |

Columnas: `Descripcion ; Domicilio ; Localidad ; Acciones` — **1 filial cargada**
Otras rutas: `/Filiales/Miembros` · `/Filiales/Consulta`

> 🔎 Se enlaza con el campo `Filial` de la ficha del socio y con el filtro
> **`Socio Interior (por region)`** del trámite de inscripción web.

---

## 19. APPs Noticias — `#/AppFanNovedades`

**H:** GESTION - Noticias · **BC:** Inicio / Noticias

| Label | Tipo | name |
|---|---|---|
| Titulo * | text | `titulo` |
| Link | text | `titulo` ⚠️ **mismo `name` que Titulo — bug** |
| Descripcion | textarea | ph *"Descripcion Nota…"* |
| Visible | checkbox | — |
| Fecha desde | text | `fechadesde` (default hoy) |
| *"Ingresar Fecha Hasta de Vigencia?"* | checkbox | — |
| Fecha hasta | text | `fechahasta` |
| Imagen | radio `Archivo`/`Url` + file `inputFileimagen_base64` + text | |

Estado: **"No hay Noticias Vigentes"**. CMS de la app del hincha.
⚠️ La imagen se guarda como **base64** (nombre del input), no como archivo referenciado.

---

## 20. Liquidación — `#/liquidacion/proceso/index`

**H:** Proceso de Liquidación · **BC:** Liquidación / Proceso de Liquidación

Es el **motor de facturación periódica**: genera la deuda de todos los socios cada mes.

| Label | Tipo | name | Opciones |
|---|---|---|---|
| Tipo Concepto | select2 | `tipoconceptoid` | **`SOCIOS` · `ACTIVIDADES`** |
| Mes | select2 | `mesid` | Ene … Dic |
| Año | **number** | `anio` | ph AAAA |
| Fecha Vencimiento | date | `fvencimiento` | |

Botones: `Comenzar` · `Nueva Liquidación` · **`Re-Iniciar Liquidación`** · `Volver` · `Limpiar` ·
`Medio de Pago` · `Tipo Concepto` · `Descargar Detalle`

### Grilla de novedades (pre-liquidación)

`Datos Persona ; Tipo Novedad ; Fecha Nac ; Edad ; **Edad Calc.** ; Sexo ; **Categoria Actual** ; **Categoria Siguiente** ; Estado`

### Grillas de recaudación
`Medio de pago ; Tipo Concepto ; Total Recuadado` *(sic: "Recuadado")* — 3 filas
`Tipo Concepto ; Medio de pago ; Total Recuadado`

### Mensajes capturados
- *"Existe una liquidación en curso pero no es MENSUAL, por favor ingrese en la opcion correspondiente!"*
- *"No hay items para liquidar con el criterio seleccionado"*

> 🔎🔎 **La liquidación RECATEGORIZA automáticamente por edad.** Las columnas
> `Edad` / `Edad Calc.` / `Categoria Actual` / `Categoria Siguiente` muestran que cada mes
> el proceso evalúa si el socio cambió de categoría (CADETE INFANTIL → CADETE MENOR → ACTIVO)
> y por lo tanto de precio. Es el proceso más delicado del sistema:
> **cambia el precio de miles de socios sin intervención humana.**
> Por eso existe la **"Excepción a la Preliquidación"** en la ficha del socio (documento 06)
> y los tipos de excepción `DISCAPACIDAD`, `VITALICIO VOLUNTARIO`,
> `CATEGORIA HIJA MAYOR A 24 AÑOS (ESTATUTO 2011)`: casos que **no deben** recategorizarse.
> 🔎 Hay una **etapa de revisión previa** (grilla de novedades) antes de confirmar. Correcto:
> el proceso es previsualizable. Y `Re-Iniciar Liquidación` implica que puede fallar a mitad.
> 🔎 Sólo liquida `SOCIOS` y `ACTIVIDADES`. Colegio tiene su propia liquidación manual.

### Otras rutas
`Liquidacion Anual` (`/liquidacion/proceso/anual/index`) · `Busqueda / Gestion`
(`/liquidacion/gestion`) · `Actividades Deportivas` (`/ActividadesCategoriasLiquidacion`)

---

## 21. Presentaciones — `#/presentacion/gestion`

**H:** Gestion de Presentaciones

Gestión de los **lotes de débito automático** enviados a bancos y tarjetas.

| Label | Tipo | name | Opciones |
|---|---|---|---|
| Fecha Presentación Desde / Hasta | date | `fechadesde` / `fechahasta` | |
| **Tipo** | select2 | `tipo` | `VISA` · `AMEX` · `BANCOS` · `PAGOS EXTERNOS` · `MASTERCARD` · `PAYPERTIC` · `VISA DEBITO` · `VISA CREDITO MACRO` · `LINE DEBITOS` |
| **Estado** | select2 | `tipo` ⚠️ *mismo name que Tipo* | `RECHAZOS PROCESADOS` · `FINALIZADA` · `ERROR EN PRESENTACION` · `GENERADA` · `PROCESANDO` |

Columnas: `F. Generacion ; Tipo ; Cantidad ; Estado ; F. Presentacion`

### Máquina de estados de la presentación

```
[GENERADA] → [PROCESANDO] → [FINALIZADA]
                 │              │
                 ↓              ↓
      [ERROR EN PRESENTACION]  [RECHAZOS PROCESADOS]
```

> 🔎 **`RECHAZOS PROCESADOS` es un estado terminal distinto de `FINALIZADA`.** El lote volvió
> con rechazos (tarjeta vencida, sin fondos) y esos rechazos ya se imputaron a las cuentas
> corrientes. **El ciclo de cobro recurrente es asincrónico y con vuelta atrás parcial.**
> Cualquier reconstrucción tiene que modelar el rechazo como un evento de negocio de primer
> orden, no como un error técnico.

### Otras rutas
`Consulta` (`/presentacion/pendientes_proceso`) ·
`Pendientes Debito Automatico` (`/presentacion/debito_automatico`)

---

## 22. Ranking — `#/scoring/calculo`

**H:** Proceso de Cálculo de Scoring · **BC:** Scoring / Calculo
Sección: *Ranking*. Único control: botón **`Iniciar Calculo`**.

> 🔎 Hay un **scoring de socios** calculado por lote. Sin parámetros visibles en esta pantalla.
> Probable insumo para prioridad de compra de entradas / sorteos / beneficios.
> **Vale investigarlo:** un ranking de socios es exactamente el mecanismo para asignar
> entradas escasas de forma percibida como justa.

---

## 23–25. Mi Perfil · Notas · Configuraciones

- **Mi Perfil** — `#/perfil_user/miPerfil`
- **Notas** — `#/notas`
- **Configuraciones** — `#/configuracion_general` → **H:** Configuraciones de la Plataforma.
  Buscador `Buscar configuración...` + 13 áreas, cada una con botón `Info`:

| # | Área | Alcance (texto literal resumido) |
|---|---|---|
| 1 | **SOCIOS** | categorías, planes de socios, conceptos por categoría, localidades socio interior |
| 2 | **CONCEPTOS** | conceptos por tipo, tipos de concepto, becas, códigos contables, éticas y clases |
| 3 | **DEPORTES** | disciplinas, categorías, frecuencias, lugares, permisos por disciplina + config de Reservas Club+ |
| 4 | **CTACTE, PAGOS Y LIQUIDACIONES** | tarjetas, bancos, promociones, establecimientos para débito automático, intereses y cuotas, **tipos de excepción a la liquidación**, formas de pago, **bines de tarjeta**, actualización masiva de montos en cta. cte. |
| 5 | **DATOS GENERALES** | tipos de documento, sexo, tipos de documentación por persona |
| 6 | **DATOS CONTACTO** | teléfonos y domicilios |
| 7 | **COLEGIO** | cursos, niveles, grados, turnos, divisiones |
| 8 | **EVENTOS** | escenarios, tipos de evento, tipos de escenario, **puertas** |
| 9 | **APPS SERVICIOS** | servicios disponibles en el portal, configuración del empadronamiento |
| 10 | **CONTROL DE ACCESOS** | **políticas y reglas para la aplicación de control de acceso** |
| 11 | **TEMPLATES** | estilos de mails automáticos/manuales, **diseño de carnet físico y carnet digital** |
| 12 | **DATOS COMPLEMENTARIOS DE LAS PERSONAS** | campos que el socio actualiza desde el portal |
| 13 | **GESTIÓN DE EMPRESAS Y/O PROVEEDORES** | empresas y proveedores |

> 🔎 **Casi todo el sistema es configuración, no código.** 13 áreas de parametrización, con
> catálogos para cada dimensión. Es la razón de que sirva como SaaS multi-club — y también
> de que la configuración esté sucia (duplicados, versionado por nombre).
> 🔎 El área 4 menciona **"bines de tarjeta"**: el sistema clasifica tarjetas por BIN para
> rutear la presentación al procesador correcto.
> 🔎 El área 11 confirma **carnet físico y carnet digital** como templates configurables.

---

## 26. Administración

Ítems `Sistema` y `Migracion`, **sin href directo** en el menú. No relevados.
