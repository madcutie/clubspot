# Módulo Eventos y Boletería 🎟️

**El pain #2.** Este es el documento más importante del relevamiento.

---

## 15.1 Gestión de Eventos — `#/EventosV2`

**H:** Eventos y Torneos · **BC:** Inicio / Eventos / Gestion de Eventos
**Encabezado:** *GESTION EVENTOS*
**Bajada literal:** *"Esta sección permite administrar los eventos, configurando **ventanas de venta, protocolos, tickets** y demás parámetros necesarios para su gestión."*

### Filtros

| Label | Tipo | name |
|---|---|---|
| Tipo Evento | select2 | `tipoeventoid` |
| Escenario | select2 | `escenarioid` |

Filtros en línea sobre el listado: `Nombre` (text) · `Codigo` (**number**) · `Escenario` (text) ·
`Tipo evento` (text) · fecha (date) · `Limpiar`

Botones: **`Agregar`** · `Buscar` · `Limpiar`

### Catálogo `Tipo Evento` (12)

| Tipo | Lectura |
|---|---|
| **FUTBOL** | partido de local — el caso principal |
| **FUTBOL VISITANTE** | partido de visitante (venta para hinchada visitante) |
| **PROTOCOLOS** | evento contenedor de accesos gratuitos |
| **PACK PROTOCOLOS** | agrupador de protocolos |
| **PACK DE EVENTOS** | abono / paquete de varios partidos |
| **CONTROL SOCIO HABILITADO X PARTIDO** | evento cuyo único fin es validar habilitación |
| **CONTROL ACCESO DEPORTES** | ingreso a entrenamientos/actividades |
| **CONTROL ACCESO SEDE** | ingreso a la sede social |
| **CONTROL PERIMETRAL** | anillo exterior del estadio |
| **ASAMBLEA** | asamblea de socios (padrón + acreditación) |
| **BINGO** | evento recaudatorio |
| **CLUB DE BENEFICIOS CHACO FOR EVER** | programa de beneficios |

> 🔎🔎 **"Evento" es una abstracción mucho más amplia que "partido".** El mismo motor resuelve
> venta de entradas, control de acceso a la sede, asambleas y bingos. Lo común es:
> *un conjunto de personas habilitadas a atravesar un punto de control en una ventana de tiempo*.
> Esa es la abstracción correcta y conviene conservarla.

### Catálogo `Escenario` (5)

`SEDE` · `PREDIO` · `MICROESTADIO` · **`JUAN ALBERTO GARCIA`** (el estadio) · `COMERCIOS`

### Listado de eventos vigentes (al 12/08/2026)

| Nombre | Tipo | Código | Fecha | Escenario |
|---|---|---|---|---|
| CHACO FOR EVER - DEPORTIVO MORON | FUTBOL | **244** | 02/08/2026 15:30 | JUAN ALBERTO GARCIA |
| PROTOCOLO CHACO FOR EVER-SAN MIGUEL | FUTBOL | **243** | 02/08/2026 15:30 | JUAN ALBERTO GARCIA |

Acciones por evento: `Gestion del Evento` · `Consulta de Ticket` · `Dashboard` ·
`Protocolos` · `Sectores Habilitados`

> 🔎 **El protocolo se modela como un EVENTO PARALELO al partido**, con el mismo escenario,
> misma fecha y misma hora, pero nombre prefijado `PROTOCOLO …`. Es un *evento sombra*:
> el partido real vende entradas, el evento protocolo acredita a los que entran gratis.
> Filtrando por Tipo Evento = `PROTOCOLOS` no aparece ninguno vigente, así que en la práctica
> el evento sombra se crea con tipo `FUTBOL` — **convención de nombre, no de tipo.**
> Es frágil y vale la pena modelarlo explícitamente en la reconstrucción.

---

## 15.2 ABM del Evento — `#/GestionEventosV2/{codigoEvento}`

Relevado sobre el evento **244**. Wizard de **5 pasos**:
`Datos Generales` → `Tickets` → `Sectores` → `Ventanas` → `Sorteo`

Cabecera: badge **`ESTADO DISPONIBLE`** + botón `Cerrar` + botón `Siguiente`
Barra superior: `Listado Eventos` · `Consulta de Ticket` · `Dashboard`

### Paso 1 — Datos Generales

*"Permite crear eventos de Futbol, definiendo sectores, ventanas y un rival."*

| Label | Tipo | name | Valor observado |
|---|---|---|---|
| Nombre * | text | `nombre` | CHACO FOR EVER - DEPORTIVO MORON |
| Fecha * | text/date | `fecha` | 02/08/2026 |
| Hora * | time | — | 15:30 |
| Torneo * | select2 | `torneoid` | **TORNEO NACIONAL 2026** |
| Escenario * | select2 | `escenarioid` | JUAN ALBERTO GARCIA |
| **Fecha/Fase *** | text | `nrofecha` | **23** (número de fecha del torneo) |
| Nombre del Rival y Escudo * | text | `rival` | DEPORTIVO MORON |
| Escudo del rival | **file** | `inputFile` | imagen |
| Nombre/Escudo propio (override) | text/file | `rival` / `inputFilelocal` | *"Completar en el caso que se desee utilizar Nombre del Club y/o Escudo diferentes a los que se utilizan en el portal"* |
| Información Publicitaria | text | `info_publicitaria_html` | ph *"URL de la imagen publicitaria (opcional)"* — *"Si no se completa, el portal mostrará una imagen genérica por defecto"* |

#### Control de Acceso

| Label | Tipo | name | Opciones |
|---|---|---|---|
| Empresa Control de Acceso | select2 | `empresacontrolaccesoid` | **AXCESS** (seleccionada) · GRUPO ECSA · ARSE · ARCHIVO GENERICO |

> 🔎🔎 **El control de acceso está TERCERIZADO** y es intercambiable por evento (4 proveedores).
> `ARCHIVO GENERICO` = exportar un archivo plano para cualquier molinete.
> Esto explica el botón **`Descarga Molinete`** de la boletería.
> **Implicancia de arquitectura:** el sistema no valida el ingreso en tiempo real; **exporta el
> padrón de habilitados al proveedor del molinete.** Es integración por archivo, no por API.

#### Parámetros temporales

| Label | name | Valor | Lectura |
|---|---|---|---|
| Fecha/hora límite de cancelación | `fechaLimiteCancelacion` | 02/08/2026 18:00 | hasta cuándo se puede cancelar |
| Fecha de bloqueo | `fecha_bloqueo` | 02/08/2026 | corte de operaciones |
| **Tiempo Vigencia Reservas (Minutos)** | `tiemporeserva` | **40** | ⏱️ **TTL del hold** |
| Fecha en que se muestra el QR | `fecha_show_qr_disponible` | `Invalid date` ⚠️ | cuándo se libera el QR |
| Ver en el portal | checkbox | ✅ | visibilidad pública |
| Fecha Visible Hasta | `visible_portal_hasta` (date) | — | |

> 🔎🔎 **`Tiempo Vigencia Reservas = 40 minutos`** es exactamente nuestro patrón `hold`.
> Confirma que el modelo `reserva con TTL → confirmación` es el estándar del rubro.
> 40 min es mucho más largo que un e-commerce típico (10–15) porque el comprador puede ir
> a pagar a la caja física.
> 🔎 **`fecha_show_qr_disponible` está en `Invalid date`** — bug real en producción: el campo
> no se inicializa. El QR se libera en un momento controlado (para evitar reventa anticipada)
> y ese control hoy está roto en este evento.

#### Usuarios habilitados (checkboxes)

Dos grupos:
- **14 usuarios administrativos** (por apellido + DNI, ej. `ACEVEDO, DANIEL ALBERTO (28510698)`,
  `ADMINISTRADOR, OURCLUB (ADMIN)`) — **5 tildados** para este evento.
- **8 usuarios de control de acceso**: `SEGURIDAD, CONTROL 1 (01)` … `CONTROL 8 (08)` —
  **6 de 8 tildados** (los controles 7 y 8 fuera de servicio para este partido).

> 🔎 Los operadores se habilitan **por evento**, no globalmente. Y los puestos de control
> físicos (molinetes/puertas) son **usuarios del sistema** con credenciales numeradas 01–08.
> El sector `PUERTA 4 ACCESO (TRIBUNA POPULAR)` mapea contra estos controles.

#### Editor de contenido

Editor WYSIWYG (Summernote): `Insert Image` · `Insert Link` · `Insert Video` ·
tamaños `100% / 50% / 25%` · fuente `Arial` · campos `Text to display`, `To what URL should this
link go?`, `Open in new window` ✅, `Video URL?`

### Paso 2 — Tickets

| Label | Tipo | name | Opciones |
|---|---|---|---|
| Ticket | select | `ticketid` | `CENA ANUAL DE SOCIOS` · `ENTRADA` · `TICKET` · `Ticket General` · `TICKET PARTIDO` |
| **Identificador QR** | select | `eventoticketidentificadorid` | **`QR CON DOCUMENTO`** · **`QR CON HASH NUMERICO`** |
| Ticket Impresion | select | `ticketid` | mismas 5 opciones |
| Concepto (Para Ventanas con Costo) | select | `conceptoid_canje` | `ENTRADAS PARTIDO` · `SOCIOS + 2DA ENTRADA 50%` |

Botones: `Guardar Cambios` · `Guardar Cambios APP` · `Ocultar QR` · `Enviar Ticket`

> 🔎🔎 **Dos estrategias de QR y la elección es por evento:**
> - `QR CON DOCUMENTO` → el QR codifica el DNI. El ticket es **nominativo e intransferible**;
>   el molinete puede cruzar contra el documento físico. Imposible de revender.
> - `QR CON HASH NUMERICO` → el QR es un token opaco. El ticket es **al portador**, transferible.
>
> Es la decisión central del diseño anti-reventa y el sistema la deja **configurable por partido**
> (clásico vs. amistoso). En nuestro modelo: `TicketIdentityStrategy` como propiedad del evento.
> 🔎 `Ticket` vs `Ticket Impresion` separados: **plantilla digital ≠ plantilla impresa**.
> 🔎 `Guardar Cambios` vs `Guardar Cambios APP`: la app móvil tiene su propio ciclo de publicación.

### Paso 3 — Sectores — `#/Eventos/sectores/{id}`

**BC:** Inicio / Eventos y Torneos / Import Entradas
*"Listado de sectores habilitados para el evento."* + filtro `filtrar sectores...`

Catálogo de sectores del estadio (select `sectorid`):
`TRIBUNA POPULAR` · `PLATEA VIP` · `TRIBUNA CALLE 15` · `PLATEA CALLE 14` · `TRIBUNA VISITANTE`

Configuración real del evento 244:

| ✔ | Sector | Puerta / Acceso | Numeración | Cap. Total | Cap. VENTA GENERAL | Cap. GANADORES SORTEO | Canal de Venta |
|---|---|---|---|---|---|---|---|
| ✅ | **PLATEA CALLE 14** | PUERTA 2 ACCESO PLATEA CALLE 14 | NO NUMERADAS | 9.000 | 9.000 | 0 | Online y Presencial |
| ✅ | **PLATEA VIP** | PUERTA 1 ACCESO PLATEA VIP | NO NUMERADAS | 200 | 200 | 0 | Online y Presencial |
| ✅ | **TRIBUNA CALLE 15** | PUERTA 3 ACCESO CALLE 15 | NO NUMERADAS | 13.000 | 13.000 | 0 | Online y Presencial |
| ✅ | **TRIBUNA POPULAR** | PUERTA 4 ACCESO (TRIBUNA POPULAR) CALLE 15 | NO NUMERADAS | 5.000 | 5.000 | 0 | Online y Presencial |
| ✅ | **TRIBUNA VISITANTE** | PUERTA 3 ACCESO CALLE 15 | NO NUMERADAS | 5.000 | 5.000 | 0 | Online y Presencial |

**Capacidad total del estadio configurada: 32.200.**

Por sector, además:
- Tabla `# ; Sector ; Precio`
- **"Generación Entradas Automáticas"** — *"En este punto se pueden seleccionando una o mas
  políticas generando entradas de manera automática sin ventas ni acreditaciones."*
  → política asignada en el ejemplo: `SOCIO MENOR 11 AÑOS`, marcada **`NO DESCUENTA CAPACIDAD`**
- **"Ingreso Masivo Entradas"** — *"se pueden ingresar un listado de personas en estado
  reservado o confirmado"* → botón **`Importar Entradas`**

> 🔎🔎 Tres mecanismos distintos para que alguien tenga entrada:
> 1. **Venta** (online o presencial)
> 2. **Generación automática por política** — p. ej. todos los socios menores de 11 entran
>    solos, y con **`NO DESCUENTA CAPACIDAD`** ⇒ no ocupan cupo (over-booking deliberado y controlado).
> 3. **Importación masiva** — carga de listado en estado reservado o confirmado.
>    **Éste es el mecanismo real por el que entran los protocolos** (prensa, sponsors, discapacidad).
>
> 🔎 `TRIBUNA VISITANTE` y `TRIBUNA CALLE 15` **comparten la PUERTA 3**. El mapeo
> sector→puerta es N:1, con la complicación operativa de segregar hinchadas por la misma puerta.
> 🔎 **Todo es NO NUMERADO.** No hay asignación de asiento: sólo cupo por sector.
> Simplifica enormemente el modelo respecto de un teatro (no hace falta `Seat`, sólo contador).

### Paso 4 — Ventanas de venta

| Label | Tipo | name | Opciones |
|---|---|---|---|
| Nombre * | text | `nombre` | ph *"Nombre de la Ventana"* |
| Fecha inicio | text | `fechainicio` | |
| Fecha fin | text | `fechafin` | |
| **Tipo Ventana *** | select | `tipoventanaid` | **`ABONADOS` · `GANADORES SORTEO` · `SOCIOS` · `NO SOCIOS` · `LIBERADA`** |
| Ventana por Politica | select | `VentanaObj_accespoliticaid` | las 35 políticas de acceso |
| **Tipo Localidades *** | select | `tipoventacantidadid` | **`SOLO LA PROPIA O MIEMBROS DEL GRUPO FAMILIAR`** · **`CANTIDAD LIBRE DE ENTRADAS`** |
| Actualizar Precio según el beneficiario | checkbox | — | |
| Control por Política (No Obligatorio) | select | `selectedPackPoliticaid` | las 35 políticas |

Filtro: `ventanas_search` (ph *"filtrar por…"*). Estado observado: **"No se registran ventanas"**.

> 🔎🔎 **Las ventanas son el mecanismo de prioridad de compra**, y su orden es la secuencia
> comercial del club:
> `ABONADOS` → `GANADORES SORTEO` → `SOCIOS` → `NO SOCIOS` → `LIBERADA`
> Cada una con su rango de fechas y su política de elegibilidad.
> 🔎 **`Tipo Localidades`** es el límite anti-reventa: o comprás **sólo para vos y tu grupo
> familiar** (cada entrada nominada a una persona del padrón) o **cantidad libre**.
> El primer modo es imposible sin el padrón de socios: **acá se acopla el pain #1 con el #2.**
> 🔎 `Actualizar Precio según el beneficiario` → el precio se recalcula según **quién** es el
> beneficiario, no quién compra. Precio dinámico por atributos de persona.

### Paso 5 — Sorteo

| Label | name |
|---|---|
| Fecha límite de inscripción | `fecha_limite_inscripcion` |
| Fecha del sorteo | `fecha_sorteo` |

Consulta asociada: botón **`Consulta Sorteo`** en Consulta de Ticket.

> 🔎 Cuando la demanda supera la capacidad, el club **sortea** el derecho a comprar.
> Hay una capacidad reservada por sector (`Capacidad GANADORES SORTEO`, hoy en 0) y un tipo
> de ventana dedicado. Es un mecanismo de racionamiento de primer orden en el modelo.

### Packs y Protocolos del evento

Tabla: `Status ; Pack ; Control por Politica ; Incluido Desde - Hasta ; Actualiza Aut.`

Packs incluidos en el evento 244:

| # | Status | Pack | Control por Política |
|---|---|---|---|
| 1 | **IMPORTADO (0)** | PERSONAS CON DISCAPACIDAD 2026 | Sin Control por Politica |
| 2 | **IMPORTADO (0)** | PRENSA 2026 | Sin Control por Politica |

Botones: `Incluir Entradas` · `Incluir Packs` · `Incluir Protocolos` ·
`Incluir Protocolos Seleccionados` · `Actualizar` · `Cancelar`
Campos: `Fecha Desde Pack` (`fechadesdepackname`) · `Fecha Limite Pack` (`fechalimitepackname`) ·
`Puede eliminar este pack` (checkbox) · filtro `filtrar Protocolos...`

#### Catálogo completo de PACKS (16)

| Pack | Categoría |
|---|---|
| ABONOS 1° RONDA (INCLUYE 9 PARTIDOS 2026 | abono |
| ABONOS 1° RONDA (INCLUYE 9 PARTIDOS). | abono |
| ABONOS 2° RONDA (INCLUYE 10 PARTIDOS). | abono |
| ABONOS 2° RONDA (INCLUYE 8 PARTIDOS).TEMPORADA 2025 | abono |
| ANULADO ERROR | ⚠️ dato sucio |
| PERSONAS CON DISCAPACIDAD | protocolo |
| PERSONAS CON DISCAPACIDAD 2026 | protocolo |
| PRENSA 2026 | protocolo |
| PRENSA ACREDITADA 2025 | protocolo |
| PROTOCOLO PLATEA 14 2026 | protocolo |
| PROTOCOLO VIP 2 2026 | protocolo |
| PROTOCOLO VIP1 2026 | protocolo |
| PROTOCOLOS 2025 | protocolo |
| PROTOCOLOS CALLE 15 2026 | protocolo |
| PROTOCOLOS TORNEO 2024 | protocolo |
| SOCIOS 2DA ENTRADA 50% BONIFICACION | promoción |

#### Catálogo de PROTOCOLOS asignables (7)

`PERSONAS CON DISCAPACIDAD 2026` · `PRENSA 2026` · `PROTOCOLOS 2026 VIP 1` ·
`PROTOCOLOS CALLE 15` · `PROTOCOLOS VIP 2 2026` · `SPONSOR 2026 PLATEA 14` · `SPONSOR VIP1`

> 🔎🔎 **Respuesta a "quiénes entran gratis":** los **protocolos** son listas nominadas de
> invitados por categoría — prensa, personas con discapacidad, sponsors, VIP —, cada una
> **anclada a un sector** (`SPONSOR 2026 PLATEA 14`, `PROTOCOLOS CALLE 15`, `VIP 1`, `VIP 2`)
> y **con año de vigencia** en el nombre. Se cargan al evento con `Incluir Protocolos` y
> quedan en estado `IMPORTADO`, con `Fecha Desde` / `Fecha Límite` propias.
> Los **abonos son el mismo mecanismo** (un pack que incluye N partidos): abonado y periodista
> entran por la misma cañería, sólo cambia si hubo plata de por medio.
> 🔎 Los abonos se venden **por ronda** (1° ronda = 9 partidos, 2° ronda = 8 ó 10), no por
> temporada completa. Refleja el fixture del ascenso argentino.
> 🔎 El versionado por año está **en el nombre del pack**, no en un campo. Mala práctica que
> ya generó basura (`ANULADO ERROR`, duplicados 2024/2025/2026).

---

## 15.3 Boletería — `#/BoleteriaV2`

**H:** Eventos y Torneos · **BC:** Inicio / Eventos / **Eventos Vigentes**
**Encabezado:** *BOLETERIA*
**Bajada literal:** *"Esta sección permite operar sobre los eventos vigentes. Desde aquí se
pueden vender y gestionar tickets, asignar protocolos y consultar el dashboard con información
y estadísticas en tiempo real."*

Acciones por evento (6):

| Botón | Función |
|---|---|
| **`Venta de Ticket`** | POS de entradas |
| `Consulta de Ticket` | consulta de reservas/ventas/cancelaciones |
| `Protocolos` | gestión de invitados |
| `Dashboard` | estadísticas en tiempo real |
| `Sectores Habilitados` | capacidades y carga masiva |
| **`Descarga Molinete`** | exportación del padrón al control de acceso |

> 🔎 Boletería lista **sólo eventos vigentes**; Gestión Eventos lista todos. Misma entidad,
> dos vistas según el rol (operador de venta vs. administrador).

## 15.4 Consulta de Ticket — `#/EventosV2/consulta_entradas/{id}/`

**H:** Gestion Eventos · **BC:** Inicio / Eventos / Consulta Entradas
**Encabezado:** *Consulta Reservas, Cancelaciones y Venta de Entradas*
Muestra `Nombre Evento` + el nombre del evento.

| Label | Tipo | name | Opciones |
|---|---|---|---|
| Documento Beneficiario (Ticket) | text | `documento` | ph *Numero de documento* |
| Beneficiario Denominacion | text | `apellido` | ph *Apellido* |
| **Estado** | select2 | `estadoingresoid` | **`CANCELADO` · `CONFIRMADO` · `PENDIENTE` · `RESERVADO`** (default CONFIRMADO) |
| Búsqueda Avanzada | toggle | — | OFF |

Botones: `Buscar` · `Buscar y descargar Excel` · `Limpiar` ·
**`Cancelar Reservas`** (rojo, destructivo — no ejecutado) · `Consulta Sorteo`

### 🎟️ Máquina de estados del TICKET

```
        ┌──────────────┐
        │  PENDIENTE   │  (iniciado, sin pago)
        └──────┬───────┘
               ↓
        ┌──────────────┐   TTL 40 min
        │  RESERVADO   │ ─────────────→ vence
        └──────┬───────┘
               ↓ pago
        ┌──────────────┐
        │  CONFIRMADO  │ ──→ QR → molinete
        └──────┬───────┘
               ↓
        ┌──────────────┐
        │  CANCELADO   │  (hasta fechaLimiteCancelacion)
        └──────────────┘
```

> 🔎 El campo se llama **`estadoingresoid`** (estado de *ingreso*), no "estado de ticket".
> El modelo mental del sistema no es "vendí una entrada" sino **"habilité un ingreso"**.
> Es la abstracción correcta y unifica venta con protocolo: en ambos casos el resultado es
> un derecho de ingreso, con o sin pago detrás.
> 🔎 El buscador es por **beneficiario**, no por comprador: la entrada está nominada a una
> persona distinta de quien pagó.

## 15.5 Protocolos del evento — `#/GestionProtocolosInvitadosEventoV2/{id}/{nombre}`

**H:** Gestion Protocolos · **BC:** Inicio / Eventos y Torneos / **Gestion Invitados**
Encabezado: *PROTOCOLOS DEL EVENTO <nombre>*

⛔ **Con la sesión relevada (ALDANA SOFÍA) devuelve: `"No sos gestor de protocolos para este evento"`**
(probado en los eventos 244 y 243).

> 🔎 Existe un rol **"gestor de protocolos" asignado por evento**, separado del rol
> administrativo general. Quien administra socios NO puede ver ni tocar la lista de invitados.
> Es una segregación de funciones deliberada: la lista de quién entra gratis es información
> sensible y políticamente delicada en un club.
> **Para completar este relevamiento hace falta una sesión con ese rol.**
> La ruta pasa el nombre del evento por URL además del id (`/244/CHACO%20FOR%20EVER%20-%20DEPORTIVO%20MORON`).

## 15.6 Derecho de Admisión — `#/DerechoAdmision`

Lista de personas con ingreso prohibido. (Ver documento 08.)

## 15.7 Torneos — `#/GestionTorneos/false`

Alta y gestión de torneos. `Torneo` es el contenedor de los eventos de fútbol
(`TORNEO NACIONAL 2026`) y aporta el campo `Fecha/Fase`.

---

## Síntesis del dominio de ticketing

```
Torneo ──< Evento ──< Sector (capacidad, puerta, canal, precio)
                │         └──< Entrada/Ingreso {PENDIENTE→RESERVADO→CONFIRMADO→CANCELADO}
                │                    ├─ beneficiario: Persona (DNI)
                │                    └─ QR: {documento | hash}
                ├──< Ventana de venta {ABONADOS|SORTEO|SOCIOS|NO SOCIOS|LIBERADA}
                │         ├─ rango de fechas
                │         ├─ Política de Acceso (elegibilidad)
                │         └─ Tipo Localidades (límite de compra)
                ├──< Pack {abono | protocolo | promoción}
                ├──< Política de Acceso (35 reglas)
                ├──  Empresa Control de Acceso {AXCESS|ECSA|ARSE|ARCHIVO}
                └──  Sorteo (fecha límite inscripción, fecha sorteo)
```

**El acoplamiento crítico:** casi todas las políticas de acceso consultan el estado de socio,
la categoría y la deuda. **El sistema de ticketing no puede funcionar sin el padrón en línea.**
Si se reconstruyen como dos sistemas, el contrato entre ellos es:
`¿esta persona, hoy, cumple la política P?` → y debe responder en milisegundos, en la puerta
del estadio, para 32.200 personas.
