# Modelo de datos inferido

Reconstruido a partir de los `name` de los campos, las rutas, los catálogos y los mensajes
de validación observados. **Es una inferencia**, no el esquema real.

## Convenciones detectadas

| Patrón | Ejemplo | Nota |
|---|---|---|
| PK sufijo `id` | `personaid`, `tipoconceptoid`, `accespoliticaid` | todo en minúsculas, sin separador |
| Identificadores de negocio con padding 8 | `00000193`, `00000344` | distintos de la PK |
| Rutas por `personaid` | `#/socios/ficha/126` | la ficha se direcciona por persona, no por socio |
| Rutas por código de evento | `#/GestionEventosV2/244` | código de evento visible al usuario |
| Sufijo `_sas` | `nrosocio_sas` | referencia al sistema legado |
| Prefijo `pagador_` | `pagador_documento` | rol pagador ≠ socio |

## Entidades

### Núcleo de identidad

```
Persona
  personaid            PK
  documento            string   ⚠️ sin normalizar (8 y 9 dígitos observados)
  tipodocumentoid      FK       (catálogo "DATOS GENERALES")
  apellido, nombre     string
  fechanacimiento      date     → edad, edad calculada
  sexoid               FK
  email                string   nullable ("No Registra email")
  codigoum             string   ← Código Usos Multiples (credencial / control de acceso)
  estado               enum     ACTIVO | ...
  foto                 blob/url
  observaciones        text
```

Roles sobre `Persona` (1:0..1 cada uno, todos independientes):
`Socio` · `Alumno` · `Deportista` · `Profesor` · `Cobrador` · `MiembroFilial` · `Usuario`

```
Socio
  socioid              PK
  personaid            FK
  nrosocio             string(8)  ⚠️ MUTABLE (Cambiar/Generar Nro Socio)
  nrosocio_sas         string     legado
  fechaAlta            date
  descuento_antiguedad_meses  int
  filialid             FK nullable
  legajo               string nullable
  socio_al_minuto      bool
  estado               enum   VIGENTE | SUSPENDIDO | BAJA
  categoriaid          FK
  grupofamiliarid      FK nullable
  habilitado           bool derivado  ← "El socio NO se encuentra Habilitado"
  origen_tramiteid     FK  (EMPADRONAMIENTO / INSCRIPCION WEB)
```

```
GrupoFamiliar
  grupofamiliarid      PK
  categoriaid          FK        categoría del grupo
  titular_socioid      FK
  fechadesde           date
  (integrantes: Socio[] con categoría derivada y estado propio)
```

```
CategoriaSocio
  ACTIVO | CADETE INFANTIL | CADETE MENOR | SOCIO VITALICIO
  ACTIVO (GRUPO FAMILIAR) | CADETE INFANTIL (GRUPO FAMILIAR) | CADETE MENOR (GRUPO FAMILIAR)
  ├─ es_grupo_familiar  bool
  ├─ es_titular         bool
  └─ costos: { inscripcion: [Concepto,monto,cuotas], fijos: [Concepto,monto] }
```

### Facturación

```
TipoConcepto  (10)
  ESCUELA | VENTAS VARIAS | OTROS | SOCIOS | ACTIVIDADES
  EGRESOS CAJAS | SEGURO DEPORTISTA | ENTRADA | EVENTOS | ABONOS

Concepto  (15)
  conceptoid           PK
  tipoconceptoid       FK
  descripcion
  genera_deuda         bool
  periodos             Mes[]      (Ene..Dic)
  montos               [{ categoriasocioid, monto }]   ← precio por categoría
  vigencia             desde/hasta  (ver CategoriaSocioConceptosHistorico: DESDE|HASTA|IMPORTE|DEBITO)
```

```
CuentaCorriente (movimiento)
  registro, periodo (MM/AAAA), conceptoid, monto, estado, reciboid, fecha_pago
  personaid / grupofamiliarid
```

```
Recibo
  reciboid             PK
  estado               enum PENDIENTE PAGO | PAGADO | CANCELADO
  fecha_emision        date
  fecha_pago_caja      date     ⚠️ distinta de la emisión
  fecha_vencimiento    date
  pagador_personaid    FK
  cobradorid           FK nullable
  medio_pago
```

```
FormaPagoPorConcepto        (matriz historizada)
  socioid, tipoconceptoid, medioPago, cobradorpersona, cobradordomicilio,
  fecha_desde, fecha_hasta

Adhesion  { medio_pago, tarjeta, cuenta_bancaria }   0..1 cada una
```

```
Presentacion  (lote de débito automático)
  tipo    enum VISA|AMEX|BANCOS|PAGOS EXTERNOS|MASTERCARD|PAYPERTIC|
               VISA DEBITO|VISA CREDITO MACRO|LINE DEBITOS
  estado  enum GENERADA|PROCESANDO|FINALIZADA|ERROR EN PRESENTACION|RECHAZOS PROCESADOS
  f_generacion, f_presentacion, cantidad
```

```
Beca / Descuento
  descripcion, comentarios, es_porcentaje bool, monto number
  (catálogo socios: HERMANOS 20% | SOCIOECONOMICO 50% | CLUB 50% | CLUB 100%)
  Socio admite hasta 2 becas (Opción 1 y 2)
```

### Ticketing 🎟️

```
Torneo
  torneoid, nombre, tipoeventoid, abonosgestionid, link_escudo,
  fecha_desde, fecha_hasta, descripcion

Escenario   SEDE | PREDIO | MICROESTADIO | JUAN ALBERTO GARCIA | COMERCIOS
Puerta      (catálogo configurable, área EVENTOS)

Evento
  eventoid / codigo     244
  tipoeventoid          FK (12 tipos)
  nombre, fecha, hora
  torneoid              FK
  escenarioid           FK
  nrofecha              int      ← Fecha/Fase del torneo
  rival, escudo_rival
  info_publicitaria_html
  empresacontrolaccesoid  FK  AXCESS|GRUPO ECSA|ARSE|ARCHIVO GENERICO
  accespoliticaid       FK
  ticketid              FK  plantilla digital
  ticketid_impresion    FK  plantilla impresa
  eventoticketidentificadorid  enum QR CON DOCUMENTO | QR CON HASH NUMERICO
  conceptoid_canje      FK  ENTRADAS PARTIDO | SOCIOS + 2DA ENTRADA 50%
  tiemporeserva         int (minutos)  ← 40
  fechaLimiteCancelacion datetime
  fecha_bloqueo         date
  fecha_show_qr_disponible datetime   ⚠️ "Invalid date" en producción
  ver_en_portal         bool
  visible_portal_hasta  date
  estado                DISPONIBLE | CERRADO
  usuarios_habilitados  Usuario[]        (14 admin + 8 puestos de control)
```

```
EventoSector
  eventoid, sectorid
  puerta
  numeradas             bool   (todas NO NUMERADAS)
  capacidad_total       int
  capacidad_venta_general int
  capacidad_ganadores_sorteo int
  precio                money
  canal_venta           enum Online y Presencial | ...
  politicas_generacion_automatica  Politica[]  (+ flag NO DESCUENTA CAPACIDAD)
```

```
Ventana
  eventoid, nombre, fechainicio, fechafin
  tipoventanaid         enum ABONADOS|GANADORES SORTEO|SOCIOS|NO SOCIOS|LIBERADA
  accespoliticaid       FK
  tipoventacantidadid   enum SOLO LA PROPIA O MIEMBROS DEL GRUPO FAMILIAR | CANTIDAD LIBRE
  actualizar_precio_segun_beneficiario  bool
```

```
Pack   (abono | protocolo | promoción)
  packid, nombre, fecha_desde, fecha_limite
  control_por_politica  FK nullable
  status                IMPORTADO(n) | ...
  actualiza_automatico  bool
  puede_eliminar        bool

Protocolo  (7 catalogados; lista nominada de invitados por sector)
```

```
Entrada / Ingreso
  eventoid, sectorid
  beneficiario_personaid  FK   (documento + apellido)
  estadoingresoid  enum PENDIENTE | RESERVADO | CONFIRMADO | CANCELADO
  origen           VENTA | GENERACION AUTOMATICA | IMPORTACION MASIVA
  qr               (documento | hash numérico)
  reciboid         FK nullable
  packid           FK nullable
```

```
PoliticaAcceso   (35)
  accespoliticaid, nombre
  predicados[]:
    - socio_activo            bool
    - socio_empadronado       bool
    - tolerancia_deuda        [{ conceptoid, meses }]
    - categoria_permitida     [categoriasocioid]
    - edad_desde / edad_hasta int
    - sexo                    enum
    - fecha_alta_desde/hasta  date
    - dias_min_desde_ult_evento int
```

```
Sorteo
  eventoid, fecha_limite_inscripcion, fecha_sorteo
```

### Otros dominios

```
ActividadDeportiva  { disciplinaid, categoriaid, frecuenciaid, lugarid,
                      detalle_horario, edad_desde, edad_hasta, cupo_maximo,
                      psicofisico, inscripcion_portal, info_portal, gestion }
Profesor            { personaid, disciplinaid, categoriaid,
                      cargo: COORDINADOR|PROFESOR|DIRECTOR TECNICO|AYUDANTE|
                             PREPARADOR FISICO|DELEGADO }
Alumno              { personaid, nivelid, gradoid, cursoid, turnoid }
ProductoEcommerce   { nombre, descripcion, tipo, genero, uso, activo }
  └─ Variante       { color, talle, sku, precio_socio, precio_no_socio, stock, activo }
  └─ MovStock       { fecha, tipo, cantidad, precio, usuarioid, observacion }
VentaEcommerce      { id, fecha, personaid, estado: Reservado|Entregado|Anulado, pago, total }
Espacio/Reserva     { actividadid, espaciodeporteid, fechahorareserva, personaid,
                      precio, accespoliticaid }
Filial              { descripcion, domicilio, provinciaid, asentamientoid }
Empresa             { nombre, razonsocial, cuit }
DerechoAdmision     { documento, apellido, nombre }
```

## Volúmenes observados (12/08/2026)

| Entidad | Cantidad |
|---|---|
| Socios activos (Sin CS) | 3.047 |
| Socios que pagaron julio 2026 | 1.882 |
| Socios que pagaron agosto 2026 | 1.278 |
| Socios empadronados | 1.277 (41,9 %) |
| Altas últimos 12 meses | 669 |
| Bajas últimos 12 meses | 2.202 |
| Deportistas activos | 2.505 (786 socios / 1.719 no socios) |
| Inscripciones a disciplinas | 2.520 en 11 disciplinas |
| Trámites de inscripción web | 2.131 |
| Movimientos de cta. cte. de un socio ejemplo | 75 |
| Capacidad del estadio configurada | 32.200 en 5 sectores |
| Historia de datos | desde 01/2013 |

## Inconsistencias detectadas (no replicar)

| # | Problema | Dónde |
|---|---|---|
| 1 | `documento` sin normalizar (8 y 9 dígitos) | padrón |
| 2 | Concepto `INSCRIPCION POR DISCIPLINA` **duplicado exacto** | catálogo de conceptos |
| 3 | ≥4 pares de políticas de acceso duplicadas con nombres casi iguales | políticas |
| 4 | Semántica AND/OR ambigua en rangos etarios múltiples | políticas #19, #23, #34 |
| 5 | Versionado por nombre (`… 2024/2025/2026`, `PADRON ASAMBLEA 2025`) | packs, protocolos, políticas |
| 6 | Pack basura `ANULADO ERROR` | packs |
| 7 | `fecha_show_qr_disponible` = **`Invalid date`** | evento 244 |
| 8 | Grupos familiares de 1 integrante / categoría de grupo sin grupo | validado en runtime, no en el modelo |
| 9 | Trámites web huérfanos (`registro web nro. _ en estado _`) | ficha del socio |
| 10 | Estado `Pago Ok. Error Alta` — cobro sin alta | inscripción web |
| 11 | `name` repetidos en un mismo form (`Titulo`/`Link`; `Tipo`/`Estado`) | APPs Noticias, Presentaciones |
| 12 | `name` incoherente con el label (`Nro. Recibo` → `nrosocio`; `Precio` → `sede`) | recibos anulados, reservas |
| 13 | Excepción de EF/ADO.NET cruda en la UI | ventas ecommerce |
| 14 | Typos en producción: *Busqeda, Recuadado, Infromación, ABONOS DE EEVENTO, Baja Adhersion, famililar* | varias |
| 15 | Nombre del gateway (`DECIDIR`) filtrado a la UI del operador | consulta caja |
| 16 | Migración legada nunca terminada (flag `migrable`) | consulta migración |
