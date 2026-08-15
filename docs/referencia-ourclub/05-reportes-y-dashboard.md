# Módulos Reportes y Dashboard

---

# 5. Reportes

Todos los reportes comparten un patrón: un panel **"Datos para Búsqueda"** colapsable
(el checkbox suelto de cada pantalla es el toggle de *Búsqueda Avanzada*) + botones `Buscar` / `Limpiar`,
y resultados en DataTable con export `Copy / CSV / Excel / PDF / Print`.

## 5.1 General — `#/consulta_general`

**H:** Reportes · **BC:** Inicio / Consulta General

| Label | Tipo | name | Placeholder |
|---|---|---|---|
| (toggle avanzada) | checkbox | — | — |
| Documento | text | `documento` | Numero de documento |
| Apellido | text | `apellido` | Apellido |
| Nombre | text | `nombre` | Nombre |
| Nro. Socio | text | `nrosocio` | Numero de socio |

## 5.2 Cuenta Corriente — `#/consulta_ctacte`

**H:** Reportes · **BC:** Inicio / Consulta Cuenta Corriente

El reporte más rico del módulo. Es la **cuenta corriente unificada** de todo el club.

| Label | Tipo | name | Detalle |
|---|---|---|---|
| (toggle avanzada) | checkbox | — | |
| (multi-select) | text | — | val: `Select Some Options` → control **multi-selección** |
| Período Desde | text | `periodoDesde` | ph `MM/AAAA` |
| Período Hasta | text | `periodoHasta` | ph `MM/AAAA` |
| Importe Desde | **number** | `importe_desde` | |
| Importe Hasta | **number** | `importe_hasta` | |
| Documento | text | `documento` | |
| Apellido | text | `apellido` | |
| Nombre | text | `nombre` | |
| Nro. Socio | text | `nrosocio` | |
| Tipo Concepto | select2 | `tipoconceptoid` | 10 opciones (ver 5.2.1) |
| Conceptos | select2 | `conceptoid` | 15 opciones (ver 5.2.2) — cascada desde Tipo |

### 5.2.1 Catálogo `TipoConcepto` (10)

`ESCUELA` · `VENTAS VARIAS` · `OTROS` · `SOCIOS` · `ACTIVIDADES` · `EGRESOS CAJAS` ·
`SEGURO DEPORTISTA` · `ENTRADA` · `EVENTOS` · `ABONOS`

### 5.2.2 Catálogo `Concepto` completo (15 entradas)

| # | Concepto | Observación |
|---|---|---|
| 1 | AJUSTE ADMINISTRATIVOS | corrección manual de saldo |
| 2 | BAJA | |
| 3 | CARNET DE SOCIO | el plástico se cobra aparte |
| 4 | CUOTA ACTIVIDAD | cuota de disciplina deportiva |
| 5 | CUOTA SOCIAL | cuota societaria |
| 6 | **ENTRADAS PARTIDO** | 🎟️ recaudación de boletería impacta en cta. cte. |
| 7 | GASTO ADMINISTRACION | |
| 8 | INSCRIPCION POR DISCIPLINA | |
| 9 | INSCRIPCION POR DISCIPLINA | ⚠️ **duplicado exacto** — dato sucio en el catálogo |
| 10 | INTERESES PAGO EN CUOTAS | financiación con interés |
| 11 | PAGO INSCRIPCIÓN | |
| 12 | PAGO PROVEEDORES | egreso: la cta. cte. también registra salidas |
| 13 | REACTIVACION SOCIO | |
| 14 | REACTIVACION SOCIO GRUPO FAMILIAR | |
| 15 | **SOCIOS + 2DA ENTRADA 50%** | 🎟️ beneficio: 2ª entrada al 50 % para socios |

> 🔎🔎 **Los conceptos 6 y 15 son la bisagra entre los dos pains.**
> La venta de entradas no es un sistema aparte: **desemboca en la misma cuenta corriente**
> que la cuota social, y existe un beneficio (`SOCIOS + 2DA ENTRADA 50%`) que sólo se puede
> calcular conociendo el **estado de socio del comprador**. Cualquier arquitectura que separe
> "club" de "ticketing" en dos sistemas sin un contrato fuerte entre ellos va a romper acá.

> 🔎 El catálogo tiene un **duplicado exacto** (`INSCRIPCION POR DISCIPLINA` ×2). Falta una
> constraint de unicidad. Anotarlo para no replicar el problema.

## 5.3 Recibos — `#/consulta_recibos`

**H:** Reportes · **BC:** Inicio / Consulta Recibos

| Label | Tipo | name |
|---|---|---|
| Recibo | **number** | `reciboid` |
| Estado | select2 | `estadoid` → `PENDIENTE PAGO` / `PAGADO` / `CANCELADO` |
| Documento | text | `pagador_documento` |
| Apellido | text | `pagador_apellido` |
| Nombre | text | `pagador_nombre` |
| Nro. Socio | text | `pagador_nrosocio` |

> 🔎 Prefijo **`pagador_`** en todos los campos de persona. El **pagador es un rol distinto
> del socio**: quien paga puede no ser el titular de la deuda (grupo familiar, empresa, tutor).
> Modelar `Pagador` explícitamente.

## 5.4 Cuenta Corriente Anulados — `#/ConsultaCuentaCorrienteAnulados`

**H:** Consulta · **BC:** Inicio / Socios / Consulta Cta Cte Anulados

Sin formulario. Tres botones de rango predefinido:
`Bajas Mes Actual` · `Bajas Año Actual` · `Bajas Entre Fechas`

## 5.5 Socios Altas-Bajas — `#/consulta_socios_altas_bajas`

**H:** Socios · **BC:** Inicio / Socios / Consulta Bajas

| Grupo | Tipo | name | Opciones |
|---|---|---|---|
| Movimiento | radio | `optionsRadios_100` | `Altas` / `Bajas` / **`Todas`** (default) |
| Presentación | radio | `optionsagrupados_100` | `Agrupado` / **`Detalle`** (default) |

Botones de rango: `Mes Actual` · `Año Actual` · `Entre Fechas`

---

# 6. Dashboard — `#/dashboard_gral`

**H:** Dashboard · **BC:** Análisis y métricas
Título interno de la vista: `demoTableros`

Dos grandes bloques (**SOCIOS** y **DEPORTES**), cada uno con un botón `Info`.

## 6.1 Bloque SOCIOS

> *"Evolución histórica de los socios, representada mediante gráficos, y análisis de los datos actuales de la masa societaria."*

### Widget "Altas y Bajas" (torta)
- Subtítulo: *"Según auditoría en los últimos 12 meses."*
- **Altas: 669 (23,3 %) — Bajas: 2.202 (76,7 %)**

### Widget "Socios Vigentes" (área temporal)
- *"Crecimiento Societario año 2026."*
- Eje X: meses 1–12. Selector de año: `2019 2020 2021 2022 2023 2024 2025 2026` + `Totales por Año`
- Definición literal de la métrica: *"Se considera por cada mes, la mayor cantidad de socios
  que abonaron la cuota, para evaluar la variación en la participación societaria a lo largo del año."*

### Widget "Socios Vigentes — En números"

| Métrica | Valor |
|---|---|
| **Estado Activos (Sin CS)** | **3.047** |
| Pago cuota AGOSTO | 1.278 |
| Pago cuota JULIO | 1.882 |

### Widget "Número de socios activos por período"
- Selectores `Desde:` / `Hasta:` (`periodo_desde` / `periodo_hasta`) — **164 opciones, de `01/2013` a `08/2026`**
- Rango por defecto `01/2026` → `08/2026`, **Promedio: 1.917,75**
- Link `descargar csv`

## 6.2 Bloque DEPORTES

> *"Infromación histórica y actual de actividades deportivas y deportistas."* (sic — typo en el original)

### Widget "Deportistas" (torta)
- **Socios: 786 (31,4 %) — No Socios: 1.719 (68,6 %)**

### Widget "Deportistas Activos" (área temporal)
- *"Crecimiento año 2026."* Mismo selector de años 2019–2026.

### Widget "Deportistas — En números"

| Métrica | Valor |
|---|---|
| Estado Activos (Sin CS) | 2.505 |
| Pago cuota AGOSTO | 334 |
| Pago cuota JULIO | 501 |

- Período `01/2026`–`08/2026` (selectores con **55 opciones**, desde `02/2022`) → **Promedio: 437,625**

### Widget "Totales por Disciplinas" (`descargar csv`)

| Disciplina | Deportistas | % |
|---|---|---|
| FUTBOL | 888 | 36 % |
| VOLEY | 515 | 21 % |
| GIMNASIA ARTISTICA | 262 | 11 % |
| BASQUET | 213 | 9 % |
| HANDBALL | 191 | 8 % |
| KARATE | 164 | 7 % |
| PATIN | 128 | 6 % |
| HOCKEY | 122 | 5 % |
| ZUMBA | 21 | 1 % |
| TAEKWONDO | 11 | 1 % |
| RITMOS | 5 | 1 % |

**Total: 2.520 inscripciones** en 11 disciplinas.

### Widget "Totales por Categoria" (`descargar csv`)
Selector de disciplina (tabs): VOLEY · FUTBOL · KARATE · GIMNASIA ARTISTICA · PATIN · HOCKEY ·
BASQUET · HANDBALL · ZUMBA · TAEKWONDO · RITMOS

Ejemplo con **VOLEY** (505 deportistas):

| Categoría | Cant. | % |
|---|---|---|
| JUVENIL FEMENINO | 223 | 44 % |
| MAYORES FEMENINO | 114 | 23 % |
| JUVENIL MASCULINO | 66 | 13 % |
| MAYORES MASCULINO | 61 | 12 % |
| MINI | 51 | 10 % |

> 🔎 Las categorías combinan **edad × género** (`JUVENIL FEMENINO`) y son **por disciplina**,
> no globales. `MINI` no tiene género. El modelo de categorías deportivas es libre por disciplina.

---

## Lecturas de negocio del Dashboard

1. **Rotación brutal: 669 altas vs 2.202 bajas en 12 meses.** El club pierde socios 3,3 a 1.
   La retención —no la captación— es el problema real.
2. **"Socio activo" ≠ "socio que paga".** 3.047 activos, pero sólo 1.882 pagaron julio y
   1.278 agosto. La **morosidad ronda el 38–58 %**. Por eso la métrica oficial de socios
   vigentes se define como *"la mayor cantidad de socios que abonaron la cuota"* en el mes:
   el club desconfía del propio estado `ACTIVO` y usa el pago como prueba de vida.
   El sufijo **"(Sin CS)"** = *sin cuota social*, refuerza lo mismo.
3. **La caída de agosto (1.882 → 1.278)** es de mes en curso: el corte se tomó el 12/08/2026,
   con la cuota aún abierta. No es fuga.
4. **Los deportistas son mayormente NO socios (68,6 %).** El negocio deportivo es más grande
   (2.505) que la masa societaria pagadora, y funciona con gente ajena al club.
   *Deportista* no es un subtipo de *Socio*: son roles independientes sobre *Persona*.
5. **Hay datos desde 01/2013** — 13 años de historia a migrar.
