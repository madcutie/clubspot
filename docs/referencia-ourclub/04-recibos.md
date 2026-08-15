# Módulo Recibos

El **recibo** es el comprobante de cobro. Es una entidad de primer orden, con estado propio,
ciclo de anulación y canal de entrega.

---

## 4.1 Consulta Recibos — `#/caja/consultarecibos`

**H:** Recibos · **BC:** Inicio / Consulta Recibos

### Campos

| Label | Tipo | name | Opciones / placeholder |
|---|---|---|---|
| Nro. Recibo | text | — | ph: `Numero de Recibo` |
| Estado Recibo | select2 | — | `PENDIENTE PAGO` · `PAGADO` · `CANCELADO` |
| Cobrador | select2 | `cobradorid` | `GONZALEZ, ENZO DE JESUS` (único cobrador cargado) |
| Estado Rendicion | select2 | `rendicionestadoid` | vacío (cascada desde Cobrador) |
| Fecha Desde | date | — | dd/mm/aaaa |
| Fecha Hasta | date | — | dd/mm/aaaa |
| Filtrar Fechas por | radio | — | **`Pago Caja`** (default) / `Emision` |
| Nivel | select2 | `colegionivelid` | vacío |
| Grado | select2 | `colegiogradoid` | vacío |
| Division | select2 | `colegiocursogradoid` | vacío |

Botones: `Buscar` · `Limpiar`

> 🔎 **`Fecha de emisión` ≠ `Fecha de pago en caja`.** Son dos fechas distintas del mismo recibo
> y el usuario elige por cuál filtrar. En contabilidad de clubes esto importa: el recibo se
> emite en un período y puede cobrarse en otro. Nuestro modelo necesita ambas.

> 🔎 Los filtros de **Nivel / Grado / División** (colegio) están en la consulta general de
> recibos: la deuda escolar comparte la misma tabla de recibos que la cuota social.
> Un único libro de cobranzas para todos los negocios del club.

---

## 4.2 Envío Masivo de Recibos — `#/recibos/envioMasivo`

**H:** Envio Masivo de Recibos · **BC:** Inicio / Envio Masivo Recibos

Pantalla de **generación y presentación de lotes** (débito automático + envío por email).
Tiene 4 secciones.

### Sección "Nuevo Envío Masivo"

| Label | Tipo | name | Opciones |
|---|---|---|---|
| (modo presentación) | radio | `optionsRadios_pres` | 2 opciones |
| (modo búsqueda) | radio | `optionsRadios_busq` | 2 opciones |
| Fecha Presentación Desde | date | `fechadesde` | |
| Fecha Presentación Hasta | date | `fechahasta` | |
| **Tipo** | select | `tipo` | **VISA · AMEX · BANCOS · PAGOS EXTERNOS · MASTERCARD · PAYPERTIC · VISA DEBITO · VISA CREDITO MACRO · LINE DEBITOS** |

Botones: `Nuevo Envio Masivo` · `Buscar` · `Cancelar` · `Generar` · `Anular` ·
`Actualizar Info.` · `Cancelar Envio!`

### Sección "Envios Pendientes"

Columnas: `F. Generacion ; F. Presentacion ; Tipo ; Cantidad ; Estado`

### Sección "Proceso de Envio en Curso"

Columnas: `F. Generacion ; Tipo ; Cantidad ; Estado`

### Sección "Detalle de Envío"

Columnas: `Email ; Pagador/Destinatario ; Recibo/Mensaje ; Estado ; Fecha Envio ; Mensaje Error`

Export DataTables: `Copy` `CSV` `Excel` `PDF` `Print`

> ⚠️ Al abrir la pantalla apareció el aviso **"Error general. Comuniquese con el area de soporte."**
> Registrado como observación del estado del sistema (no se ejecutó ninguna acción).

> 🔎🔎 **Los 9 "Tipos" son los canales de cobro recurrente** y son el corazón de la cobranza:
> tarjetas (VISA/MASTERCARD/AMEX), débito bancario (BANCOS, LINE DEBITOS, VISA DEBITO,
> VISA CREDITO MACRO) y agregadores (PAGOS EXTERNOS, PAYPERTIC).
> Cada uno implica **generar un archivo de lote, presentarlo, y conciliar la respuesta** —
> por eso hay `F. Generacion` y `F. Presentacion` separadas, y un `Mensaje Error` por ítem.
> Es un patrón batch, no API en tiempo real. Cualquier reconstrucción tiene que decidir
> conscientemente si mantiene el modelo de lote o migra a cobro por API.

---

## 4.3 Administrar Recibos — `#/caja/gestionrecibos`

**H:** Recibos · **BC:** Inicio / Gestion Recibos

Pantalla mínima: un único campo.

| Label | Tipo | name |
|---|---|---|
| Nro. Recibo | text `reciboid` | ph: `Numero de Recibo` |

Botones: `Buscar` · `Limpiar`

> Entrada puntual por número de recibo para operar sobre uno solo (reimprimir / anular).
> No permite operación masiva: **la anulación es un acto individual y trazable.**

---

## 4.4 Recibos Anulados — `#/caja/consulta_anulados`

**H:** Recibos · **BC:** Inicio / Consulta Recibos anulados

| Label | Tipo | name | Nota |
|---|---|---|---|
| Nro. Recibo | text | `nrosocio` | ph: `Numero de Recibo` — ⚠️ el `name` dice `nrosocio`: **bug de copy/paste en el form** |
| Fecha | date | `fechadesde` | |
| Busqeda Avanzada | checkbox | — | ⚠️ typo en el label: *"Busqeda"* |

Botones: `Buscar` · `Limpiar`

## Estados del recibo

```
[PENDIENTE PAGO] ──cobro──→ [PAGADO] ──anulación──→ [CANCELADO]
```

Sólo 3 estados. La anulación no borra: pasa a `CANCELADO` y queda consultable en su propia pantalla.
