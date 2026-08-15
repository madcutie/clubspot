# Informe — Relevamiento OurClub / Chaco For Ever

Fecha: 12/08/2026 · Sistema: OurClub V1.44 · Alcance: backoffice completo, solo lectura.

---

## 1. Qué es OurClub

Un **SaaS multi-tenant de gestión integral de clubes** (el tenant va en el path:
`/chacoforever/`). No es un sistema de ticketing con un módulo de socios, ni al revés:
es un **ERP de club** con 26 módulos, donde la venta de entradas es una de las salidas.

Stack inferido: **.NET + Entity Framework** (por la excepción de ADO.NET filtrada a la UI),
frontend **AngularJS** con routing por hash, UI **Inspinia/AdminLTE + Bootstrap 3**,
DataTables, select2, Summernote. API REST por tenant: `/{tenant}/api/{Recurso}/{accion}`.
Gateway de pago: **Decidir (Prisma)**. Control de acceso tercerizado: **AXCESS / Grupo ECSA /
ARSE / Archivo genérico**.

## 2. La foto del negocio

| Métrica | Valor | Lectura |
|---|---|---|
| Socios activos | 3.047 | escala media |
| Pagaron la cuota de julio 2026 | 1.882 | **38 % de morosidad** |
| Altas / Bajas últimos 12 meses | 669 / 2.202 | **pierde socios 3,3 a 1** |
| Empadronados (identidad digital) | 1.277 (41,9 %) | menos de la mitad |
| Deportistas | 2.505, **68,6 % no socios** | el deporte es más grande que el club social |
| Capacidad del estadio | 32.200 en 5 sectores | ~10× la masa societaria |
| Cuota social ACTIVO | $25.000/mes | −$3.000 en grupo familiar |
| Historia | desde 01/2013 | 13 años a migrar |

**Los tres hechos que deberían ordenar cualquier rediseño:**

1. **La retención es el problema, no la captación.** 2.202 bajas contra 669 altas.
2. **"Socio activo" no significa "socio que paga".** El propio sistema desconfía de su
   estado `ACTIVO` y define la métrica oficial como *"la mayor cantidad de socios que
   abonaron la cuota"* en el mes.
3. **El estadio es 10× el padrón.** El negocio de entradas no puede depender sólo de socios;
   pero casi todas las reglas de acceso están escritas en términos de socio.

## 3. Los dos pains, y por qué no son dos sistemas

### Pain #1 — Gestión del club

El corazón es **`Persona`** con roles acumulables: `Socio`, `Alumno`, `Deportista`,
`Profesor`, `Cobrador`, `MiembroFilial`. La ficha del socio tiene **14 secciones** y resuelve
en una pantalla: identidad, antigüedad, deuda, grupo familiar, cuenta corriente, formas de
pago por concepto, becas, documentación y credencial.

Lo mejor del diseño actual:
- **Grupo familiar como entidad** con titular, vigencia y categoría propia; cada integrante
  con su estado. `Cambio de Titularidad` es una operación de negocio de primer orden.
- **Forma de pago por tipo de concepto, historizada** (`socio × tipoConcepto → medioPago`
  con desde/hasta). Mucho más fino que un "medio de pago por defecto".
- **Liquidación con recategorización automática por edad** y etapa de previsualización,
  más un sistema de **excepciones** para los casos que no deben recategorizarse
  (discapacidad, vitalicio, `CATEGORIA HIJA MAYOR A 24 AÑOS (ESTATUTO 2011)`).
- **Anulación en vez de edición**: el recibo confirmado es inmutable.

Lo peor:
- **El sistema no puede garantizar sus propias invariantes** y se lo avisa al operador por
  pantalla: *"grupo familiar de un solo integrante"*, *"categoría de grupo familiar pero no
  asignado a ninguno"*, *"posee un registro web en estado \_"*. Son reglas de dominio que la
  base no impone.
- Estado `Pago Ok. Error Alta`: **cobra primero, da de alta después**, y hay un tablero para
  contar cuándo falla.
- 1.020 altas **"pendientes de impresión de plástico"**: el proceso digital no termina.
- Migración del sistema legado (`SAS`) **nunca terminada**, con flag `migrable` permanente.

### Pain #2 — Venta de entradas

El modelo es mejor de lo esperado. Cinco piezas:

1. **Evento** — abstracción amplia: el mismo motor cubre partido, control de acceso a la sede,
   asamblea y bingo. Lo común es *un conjunto de personas habilitadas a atravesar un punto de
   control en una ventana de tiempo*. Vale conservarla.
2. **Sector** — capacidad, puerta, canal de venta, precio. **Todo no numerado**: no hace falta
   modelar asientos, sólo cupos.
3. **Ventana de venta** — `ABONADOS → GANADORES SORTEO → SOCIOS → NO SOCIOS → LIBERADA`,
   con fechas y política de elegibilidad. Es el mecanismo de prioridad.
4. **Pack** — abonos (por ronda: 9 ó 10 partidos) y **protocolos** (prensa, discapacidad,
   sponsors, VIP) por la misma cañería.
5. **Política de acceso** — 35 reglas parametrizadas.

Estados del ticket: `PENDIENTE → RESERVADO → CONFIRMADO → CANCELADO`, con
**TTL de reserva de 40 minutos** (`tiemporeserva`) — el mismo patrón `hold` que ya usamos.
El campo se llama `estadoingresoid`: el modelo mental no es "vendí una entrada" sino
**"habilité un ingreso"**, lo que unifica venta y protocolo.

### Por qué están acoplados

Casi todas las políticas de acceso consultan **estado de socio, categoría y deuda**:

- `SOCIO VIGENTE (… CUOTA SOCIAL Tolerancia Deuda en Meses 5)`
- `VENTA SOCIOS ACTIVOS (GF) (… CATEGORIA SOCIO ACTIVO (GRUPO FAMILIAR) Acceso Permitido)`
- `Tipo Localidades: SOLO LA PROPIA O MIEMBROS DEL GRUPO FAMILIAR`
- Concepto `SOCIOS + 2DA ENTRADA 50%`
- Mensaje `"El socio NO se encuentra Habilitado."`

**No se pueden construir como dos sistemas independientes.** Si se separan, el contrato es:
*¿esta persona, hoy, cumple la política P?* — y tiene que responder en milisegundos, en la
puerta, para 32.200 personas.

## 4. Los cinco hallazgos que más valen

### 4.1 `Tolerancia Deuda en Meses` es una perilla comercial

Con 38 % de morosidad, exigir cuota al día vaciaría el estadio. El sistema tiene políticas
con tolerancia **0, 1 y 5 meses** y el club elige cuál aplicar según el partido.
La morosidad no es binaria: es un parámetro de negocio. **Copiarlo.**

### 4.2 Dos estrategias de QR, configurables por evento

- `QR CON DOCUMENTO` → nominativo e intransferible, cruzable contra el DNI en la puerta.
- `QR CON HASH NUMERICO` → token opaco, al portador, transferible.

Es la decisión anti-reventa central y se toma **por partido** (clásico vs. amistoso).
En nuestro modelo: `TicketIdentityStrategy` como propiedad del evento. **Copiarlo.**

### 4.3 Tres orígenes distintos de un derecho de ingreso

`Venta` · `Generación automática por política` (con flag **`NO DESCUENTA CAPACIDAD`**, o sea
over-booking deliberado) · `Importación masiva` (el mecanismo real de los protocolos).
Los tres desembocan en la misma entidad `Ingreso`. **Copiarlo.**

### 4.4 El mismo motor de políticas decide quién vota

`PADRON ASAMBLEA 2025 (CUOTA SOCIAL tol. 1 · Socio Activo · Fecha Alta Desde/Hasta · Edad ≥ 18)`.
El componente que habilita el molinete habilita también el padrón electoral y la reserva de
una cancha (`Reservas Club+` usa el mismo `accespoliticaid`).
**Es un servicio transversal, no una feature de ticketing.** Diseñarlo así desde el día uno.

### 4.5 La dualidad socio / no socio atraviesa TODO el modelo de precios

Cuota (por categoría), arancel deportivo (columna `Socios`), entrada (`SOCIOS + 2DA ENTRADA 50%`)
y merchandising (`Precio Socio` / `Precio No Socio` en cada SKU).
No es un `if socio`: es una **lista de precios por audiencia**. Modelarla como tal.

## 5. Lo que NO hay que replicar

| # | Problema | Costo real |
|---|---|---|
| 1 | **Versionado por nombre** (`PROTOCOLOS 2024/2025/2026`, `PADRON ASAMBLEA 2025`) | basura acumulada, imposible de limpiar |
| 2 | **35 políticas con ≥4 pares duplicados** y nombres casi iguales, sin descripción ni owner | nadie puede elegir con confianza |
| 3 | **Semántica AND/OR ambigua** en rangos etarios múltiples (#19, #23, #34) | reglas que probablemente no hacen lo que dicen |
| 4 | **Invariantes validadas en la UI, no en el modelo** | datos inconsistentes en producción, hoy |
| 5 | **Cobrar antes de dar de alta** (`Pago Ok. Error Alta`) | plata cobrada sin contraprestación |
| 6 | **Nombre del gateway en la UI** (`Incluir pagos DECIDIR`) | proveedor no abstraído |
| 7 | **Excepciones de EF crudas en pantalla** | fuga de implementación |
| 8 | **`fecha_show_qr_disponible = Invalid date`** | control anti-reventa roto en producción |
| 9 | **Migración legada permanente** (flag `migrable`) | dos fuentes de verdad, para siempre |
| 10 | **Un cupo por concepto duplicado** (`INSCRIPCION POR DISCIPLINA` ×2) | falta constraint de unicidad |

## 6. Recomendaciones para la reconstrucción

1. **Un solo sistema, dos bounded contexts** — `Club` (padrón, cuotas, cobranzas) y
   `Ticketing` (eventos, ingresos), con un tercero compartido: **`Elegibilidad`**
   (el motor de políticas). No dos productos.
2. **`Politica` como agregado tipado**: `{ nombre, descripción, vigencia, ALL/ANY, predicados[] }`
   con evaluador puro y testeable. Predicados de primera clase, incluido el de frecuencia
   (`N días desde el último evento`). Nada de versionar por nombre.
3. **`Ingreso` como agregado, no `Entrada`.** Estados `PENDIENTE → RESERVADO → CONFIRMADO →
   CANCELADO`, TTL configurable por evento, y `origen ∈ {VENTA, AUTOMATICA, IMPORTACION}`.
   Esto unifica venta, abono y protocolo sin ramas especiales.
4. **Nunca cobrar sin poder revertir.** El estado `Pago Ok. Error Alta` es la prueba de que
   el flujo actual no es transaccional. Usar `hold → payment → confirm` con idempotencia y
   compensación (ya lo tenemos en el proyecto: aprovecharlo).
5. **Convertir cada mensaje de advertencia en una invariante.** Los seis mensajes de la ficha
   del socio (documento 06) son la especificación gratuita de qué imponer en el modelo.
6. **`PriceList` por audiencia** como concepto de primer orden (socio / categoría / grupo
   familiar / no socio / edad), no condicionales dispersos.
7. **Abstraer el gateway y el control de acceso.** `IPaymentGateway` ya existe; hace falta
   el equivalente para molinetes (`IAccessControlExport`), porque hoy hay 4 proveedores y la
   integración es **por archivo, no por API**.
8. **Normalizar identidad desde el día uno**: `tipo_documento` + `numero_documento` validados
   por tipo. El padrón actual tiene documentos de 8 y 9 dígitos conviviendo.
9. **`nrosocio` no es clave.** Es mutable por diseño (`Cambiar Nro Socio`). La PK es interna.
10. **Priorizar retención sobre alta.** Con 2.202 bajas anuales, la funcionalidad de más valor
    no es vender más rápido sino detectar y frenar la fuga. Hoy no hay ninguna pantalla
    orientada a eso — sólo a contarla.

## 7. Qué falta relevar

| Área | Cómo destrabarlo |
|---|---|
| **Protocolos por evento** (quiénes entran gratis, nominalmente) | conseguir una sesión con rol **"gestor de protocolos"**. La estructura ya está documentada (7 protocolos, 16 packs, mecanismo de importación); falta el contenido de las listas. |
| **Control Acceso / Control Rápido** | la app móvil de los usuarios `SEGURIDAD, CONTROL 1–8` |
| **Precios por sector** | activar las solapas del wizard con un usuario con permiso de edición |
| **Scoring / Ranking de socios** | pantalla con un solo botón; los parámetros están en Configuraciones |
| **Portal público y app del hincha** | es la otra mitad del producto; esto fue sólo el backoffice |
| **Administración → Sistema / Migración** | sin ruta accesible |
