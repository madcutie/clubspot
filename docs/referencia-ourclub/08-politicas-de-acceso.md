# Políticas de Acceso — el motor de reglas

Es el componente más importante y menos evidente del sistema. Define **quién puede
comprar una entrada y quién puede ingresar**. Se configura por evento, por ventana de venta,
por sector y por pack.

Origen: select `accespoliticaid` / `VentanaObj_accespoliticaid` / `selectedPackPoliticaid`
en el ABM de eventos. **35 políticas configuradas.**

## Formato

La UI las muestra como `NOMBRE (predicado - predicado - … )`. El nombre es libre; los
predicados entre paréntesis son la regla real, en conjunción (AND).

## Predicados detectados (la gramática del motor)

| Predicado | Parámetro | Semántica |
|---|---|---|
| `Socio Activo` | — | el socio debe estar en estado activo |
| `Socio Empadronado` | — | debe tener el empadronamiento finalizado |
| `CONCEPTO <X> Tolerancia Deuda en Meses N` | concepto + N | admite hasta N meses de deuda de ese concepto |
| `CATEGORIA SOCIO <X> Acceso Permitido` | categoría | restringe a una categoría de socio |
| `Edad Desde A Edad Hasta B` | A, B | rango etario |
| `Sexo` | valor | filtro por sexo (sin valor en las configuradas) |
| `Fecha Alta Desde / Fecha Alta Hasta` | fechas | antigüedad mínima/máxima como socio |
| `Acceso Denegado Menos de N dias desde el ult. evento` | N | **frecuencia máxima de asistencia** |
| *(vacío)* `()` | — | sin condición: elegibilidad manual/por lista |

> 🔎🔎 Los dos predicados más interesantes:
> - **`Tolerancia Deuda en Meses`** — la morosidad no es binaria. El club tiene *grados* de
>   tolerancia (0, 1, 5 meses) y elige cuál aplicar según el evento. Con 38–58 % de morosidad
>   (ver Dashboard), exigir cuota al día vaciaría el estadio. **Es una perilla comercial.**
> - **`Acceso Denegado Menos de 10 dias desde el ult. evento`** — límite de frecuencia.
>   Evita que la misma persona acapare la promoción partido tras partido. Es control
>   anti-abuso construido dentro del motor de elegibilidad.

## Catálogo completo (35)

### Grupo A — Socio vigente, por tolerancia de deuda

| # | Política | Predicados |
|---|---|---|
| 1 | **SOCIO VIGENTE** | Socio Activo · Empadronado · CUOTA SOCIAL **tol. 5 meses** |
| 2 | **SOCIO VIGENTE CUOTA AL DIA** | Socio Activo · Empadronado · CUOTA SOCIAL **tol. 0** |
| 3 | **SOCIO VIGENTE TOLERANCIA 1 MES** | Socio Activo · Empadronado · CUOTA SOCIAL **tol. 1** |

### Grupo B — Deportistas

| # | Política | Predicados |
|---|---|---|
| 4 | DEPORTISTA VIGENTE | CUOTA SOCIAL tol. 0 |
| 5 | CONTROL DE INGRESOS DEPORTISTAS (0) | CUOTA SOCIAL tol. 0 · CUOTA ACTIVIDAD tol. 0 |
| 6 | CONTROL DE INGRESO DEPORTISTA (1)) *(sic)* | Socio Activo · Empadronado · CUOTA SOCIAL tol. 1 · CUOTA ACTIVIDAD tol. 1 |

### Grupo C — Protocolos (sin predicados: lista nominada)

| # | Política |
|---|---|
| 7 | **PROTOCOLO PRENSA** `()` |
| 8 | **PROTOCOLO PERSONAS CON DISCAPACIDAD** `()` |
| 9 | **PROTOCOLO SPONSOR** `()` |
| 24 | **BONO** `()` |
| 28 | **DAMAS NO SOCIA** `()` |
| 31 | **ABONOS DE EEVENTO** `()` ⚠️ typo en producción |

> 🔎 Los protocolos tienen paréntesis vacío: **no hay regla evaluable**, la pertenencia es por
> lista explícita. Coherente con el mecanismo de `Importar Entradas` / `Incluir Protocolos`.

### Grupo D — Socio por edad

| # | Política | Predicados |
|---|---|---|
| 10 | SOCIO VIGENTE MAYOR | Socio Activo · Empadronado · CUOTA SOCIAL tol. 1 · **Edad 12–64** |
| 22 | SOCIO MAYOR | Socio Activo · Empadronado · CUOTA SOCIAL tol. 0 · Edad 12–64 |
| 26 | SOCIO MENOR DE 11 AÑOS | Socio Activo · Empadronado · CUOTA SOCIAL tol. 0 · **Edad 0–11** |
| 35 | SOCIO MENOR 11 AÑOS | Socio Activo · Empadronado · CUOTA SOCIAL tol. **1** · Edad 0–11 |
| 27 | SOCIO JUBILADO | Socio Activo · Empadronado · CUOTA SOCIAL tol. 1 · **Edad ≥ 65** |
| 34 | SOCIO JUBILADO/MENOR | CUOTA SOCIAL tol. 0 · Edad ≥ 65 · Edad 0–11 |
| 29 | DAMAS SOCIAS | Socio Activo · Empadronado · CUOTA SOCIAL tol. 0 · Edad ≥ 12 |

> ⚠️ **#26 y #35 son la misma regla con distinta tolerancia (0 vs 1) y nombres casi idénticos**
> (`SOCIO MENOR DE 11 AÑOS` vs `SOCIO MENOR 11 AÑOS`). Igual #10 vs #22. Es deuda de
> configuración: nadie puede saber cuál usar sin abrir cada una.

### Grupo E — Venta por categoría de socio

| # | Política | Categoría exigida (todas con CUOTA SOCIAL tol. 0) |
|---|---|---|
| 11 | VENTA EVENTOS SOCIOS ACTIVOS | ACTIVO |
| 15 | VENTA SOCIOS ACTIVOS (GF) | ACTIVO (GRUPO FAMILIAR) |
| 12 | VENTA SOCIOS CADETES | CADETE INFANTIL |
| 17 | VENTA SOCIOS INFANTILES | CADETE INFANTIL |
| 16 | VENTA SOCIOS INFANTILES (GF) | CADETE INFANTIL (GRUPO FAMILIAR) |
| 13 | VENTA SOCIOS MENORES | CADETE MENOR |
| 14 | VENTA SOCIOS MENORES (GF) | CADETE MENOR (GRUPO FAMILIAR) |

> ⚠️ **#12 y #17 son idénticas** (misma categoría, misma tolerancia), con nombres distintos.
> 🔎 Hay una política por cada una de las **6 categorías de socio**. Cuando se agregue una
> categoría nueva habrá que crear su política a mano. **Debería derivarse, no duplicarse.**

### Grupo F — Público general (no socios)

| # | Política | Predicados |
|---|---|---|
| 18 | VENTA PUBLICO GENERAL MAYOR | Edad 12–64 |
| 19 | MENORES NO SOCIOS | Edad 4–12 · Edad ≥ 65 |
| 20 | JUBILADO NO SOCIO | Edad ≥ 65 |
| 23 | VENTA PUBLICO GENERAL MENOR/JUBILADO | Edad 4–12 · Edad ≥ 65 |
| 25 | **PUBLICO VISITANTE** | Edad ≥ 4 |
| 33 | PLAN FAMILIAR | Edad 0–11 |

> 🔎 **Edad mínima 4 años** para entrar al estadio (menores de 4 no pagan / no se registran).
> ⚠️ #19 y #23 son idénticas.
> ⚠️ #19/#23/#34 combinan `Edad 4–12` **y** `Edad ≥ 65` en la misma política: dos rangos en un
> AND es imposible de cumplir literalmente. O el motor los evalúa como **OR** (menores *o*
> jubilados — que es la intención obvia: la tarifa reducida), o la config está mal.
> **Hay que averiguar la semántica real antes de replicar el motor.**

### Grupo G — Casos especiales

| # | Política | Predicados | Lectura |
|---|---|---|---|
| 21 | SOCIO X PARTIDO | CUOTA SOCIAL tol. 1 · CUOTA ACTIVIDAD tol. 0 | socio por partido |
| 30 | **PADRON ASAMBLEA 2025** | CUOTA SOCIAL tol. 1 · Socio Activo · **Fecha Alta Desde/Hasta** · **Edad ≥ 18** | 🗳️ **padrón electoral** |
| 32 | **PROMO PARA SOCIOS** | Socio Activo · Empadronado · CUOTA SOCIAL tol. 0 · **Acceso Denegado Menos de 10 días desde el últ. evento** | promo con tope de frecuencia |

> 🔎🔎 **#30 es el padrón de la asamblea.** El mismo motor que decide quién entra a la cancha
> decide **quién vota**: mayor de 18, socio activo, con antigüedad entre dos fechas y hasta
> 1 mes de deuda. Es el uso institucionalmente más sensible del sistema y está modelado como
> una política de acceso más.
> El nombre lleva el año (`2025`) porque **el padrón se congela por elección**.

---

## Cómo se usa una política

Una política se engancha en **cuatro lugares distintos**:

1. **`Ventana por Politica`** → quién puede comprar en esa ventana de venta.
2. **`Control por Política`** en un pack → validación del pack/protocolo/abono.
3. **`Generación Entradas Automáticas`** en un sector → a quién se le emite entrada sola,
   sin venta ni acreditación (con opción `NO DESCUENTA CAPACIDAD`).
4. **Política de acceso del evento** → validación general en el molinete.

## Qué copiar y qué no

**Copiar:**
- La idea de **política como entidad configurable y reutilizable**, no como código.
- **`Tolerancia de deuda` parametrizable** — es la perilla que hace viable el negocio real.
- **Límite de frecuencia** (`N días desde el último evento`) como predicado de primera clase.
- El mismo motor para venta, acceso, generación automática y padrón de asamblea.

**No copiar:**
- 35 políticas con **al menos 4 pares duplicados** y nombres casi iguales. Sin descripción,
  sin owner, sin fecha. Nadie puede elegir con confianza.
- **Versionado por nombre** (`PADRON ASAMBLEA 2025`, `PROTOCOLOS 2024/2025/2026`).
- **Semántica ambigua del AND/OR** en rangos etarios múltiples.
- Predicados que se muestran vacíos (`Sexo -`, `Fecha Alta Desde -`) sin indicar si es
  "sin filtro" o "sin configurar".

**Propuesta:** política = `{ nombre, descripción, vigencia, predicados[] }` con predicados
tipados y composición explícita `ALL`/`ANY`, más un evaluador puro y testeable.
