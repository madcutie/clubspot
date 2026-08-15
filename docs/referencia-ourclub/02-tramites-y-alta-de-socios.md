# Módulo Trámites — alta y registro de socios

Este módulo cubre el **onboarding**: cómo una persona entra al padrón del club.
Hay tres canales distintos, cada uno con su propia máquina de estados.

## 1. Inscripción Web (`/InscripcionWebBuscador/`)

Alta de socio **nuevo** iniciada por el usuario final desde el portal público.

### Buscador (filtros disponibles)

`Trámite` · `Nro. Documento` · `Apellido` · `Nombre` · `Estado` · `Categoria Socio` ·
`Sexo` · `País` · `Socio Interior (por región)` [Sí/No/Todos] · `Extranjero` [Sí/No/Todos] ·
`Edad Desde` / `Edad Hasta` · `Fecha Desde` / `Fecha Hasta`

**Volumen actual: 2.131 trámites.**

Resultado en tabla con columnas `Trámite | Documento | Apellido y Nombre | Edad | Info | Estado | Fecha`,
donde *Info* concatena la categoría de socio + condición (ej. `CADETE INFANTIL` + `SOCIO INTERIOR`).

Endpoint: `GET /chacoforever/api/InscripcionWeb/search`

### Máquina de estados (del tablero `/InscripcionWebTablero`)

| Estado | Significado | Cantidad |
|---|---|---|
| **Pago Ok. Error Alta** | Inscripción iniciada por el usuario, no confirmada | 0 |
| **Pago Realizado** | Trámite con pago hecho **pero el socio no se registró** | 0 |
| **Finalizadas** | Finalizadas por el usuario, **pendientes de impresión de plástico** | 1.020 |
| **Cerradas** | Trámite cerrado | 114 |
| **Anuladas** | Inscripciones web anuladas | 521 |

> 🔎 **Insight fuerte para nuestro diseño.** Existe un estado dedicado a
> *"pago realizado pero el alta falló"*. Es decir: el sistema **cobra primero y da de alta después**,
> y esa transición puede romperse, dejando plata cobrada sin contraprestación.
> Es exactamente el problema que en nuestro dominio resolvemos con `hold → payment → confirm`
> y compensación/idempotencia. Que tengan un tablero para contarlos confirma que la falla
> es lo bastante frecuente como para necesitar monitoreo.

> 🔎 **"Pendiente de impresión de plástico"** — hay un artefacto físico (credencial) en el flujo.
> El alta digital no termina el proceso; queda una cola operativa offline de 1.020 casos.

## 2. Registro de Socios / Empadronamiento (`/EmpadronamientoBuscador/`)

Regularización de socios **ya existentes** (migrados o históricos) para que tengan
identidad digital verificada. No es un alta nueva.

### Máquina de estados (tablero `/ReempadronamientoTablero`)

| Paso | Estado | Cantidad |
|---|---|---|
| Paso 1 | **Validar Email** — pendientes de validación de correo electrónico | 144 |
| Paso 2 | **Validar Área Socios** — pendientes de validación humana en el club | 13 |
| Final | **Finalizados** — empadronamiento completo | **1.277 (41,9 %)** |

> El 41,9 % permite inferir un padrón total de **≈ 3.050 socios**.
> Es un club de escala media: el sistema no necesita resolver millones de registros,
> pero sí concurrencia alta y puntual en días de partido.

Filtros del buscador: `Trámite` · `Nro. Socio` · `Nro. Documento` · `Apellido` · `Nombre` ·
`Estado` (default `VALIDAR AREA SOCIOS`) · `Fecha Desde` / `Fecha Hasta`.

> 🔎 El patrón **doble validación (automática por email + manual por back-office)** es
> deliberado: el club no confía sólo en el email para asociar una identidad digital
> a un legajo histórico. Hay un humano en el loop.

## 3. Suscripciones Web (`/SuscripcionWebBuscador/`)

Mismo formulario de búsqueda que Inscripción Web. Sin resultados en la consulta por defecto
— aparenta ser un canal habilitado pero **sin uso actual** en este club.

## 4. RENAPER (`/renaper_info`)

Integración con el Registro Nacional de las Personas para **validar identidad contra la fuente oficial**.

La pantalla es de monitoreo, con dos bloques:

1. **Estadísticas de Validación RENAPER** — resumen de validaciones por período
   (sin datos en el rango por defecto).
2. **Calidad de Datos RENAPER** — casos que requieren revisión o ajuste de parametrización.
   Categoría detectada: **"Provincias RENAPER no mapeadas"** (sin casos abiertos hoy).

> 🔎 Dos aprendizajes de arquitectura:
> - La validación de identidad es un **servicio externo con su propio panel de salud**, no una
>   llamada perdida dentro del alta. Tiene métricas y cola de errores.
> - El problema concreto que monitorean es de **mapeo de catálogos** (provincias del proveedor
>   externo ↔ provincias internas). Todo diseño con proveedor externo necesita una tabla de
>   mapeo explícita y un reporte de valores no mapeados.

## Resumen del dominio de onboarding

```
                    ┌─ Inscripción Web ──→ paga ──→ [Pago Realizado] ──→ alta socio ──→ [Finalizada] ──→ plástico ──→ [Cerrada]
                    │                          └──→ [Pago Ok. Error Alta]  ⚠ inconsistencia
  Persona (RENAPER) ┤
                    ├─ Empadronamiento ──→ [Validar Email] ──→ [Validar Área Socios] ──→ [Finalizado]
                    │
                    └─ Suscripción Web (canal inactivo)
```

Las tres rutas desembocan en el mismo agregado **Socio**, pero con historia y garantías distintas.
