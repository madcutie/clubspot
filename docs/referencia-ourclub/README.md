# Relevamiento OurClub — Chaco For Ever

> 📌 **Antes de usar esta carpeta como insumo de trabajo, leer [`AGENTS.md`](AGENTS.md)**: qué
> es esto, qué no es, qué precedencia tiene frente al alcance y al diseño, y qué no se copia de
> acá (empezando por los datos personales).

Investigación del sistema **OurClub** (`https://gestion.ourclub.io/chacoforever/`) usado por
el Club Atlético Chaco For Ever, como base para reconstruir los dos dolores principales:
**gestión del club** y **venta de entradas para los partidos**.

- **Fecha del relevamiento:** 12/08/2026
- **Versión del sistema:** Sistema de Gestión de Socios **V1.44** — © OURCLUB 2020
- **Sesión utilizada:** ALDANA SOFÍA (perfil administrativo)
- **Modo:** **solo lectura**. Ver [Alcance y método](#alcance-y-método).

## Documentos

| # | Documento | Contenido |
|---|---|---|
| 00 | [Datos de prueba](00-datos-de-prueba.md) | identificadores para reproducir consultas ⚠️ PII |
| 01 | [Mapa de navegación](01-mapa-navegacion.md) | los 26 módulos y ~70 rutas |
| 02 | [Trámites y alta de socios](02-tramites-y-alta-de-socios.md) | onboarding, empadronamiento, RENAPER |
| 03 | [Caja](03-caja.md) | POS, deuda, cierre, **catálogo de precios** |
| 04 | [Recibos](04-recibos.md) | comprobantes, envíos masivos, anulación |
| 05 | [Reportes y Dashboard](05-reportes-y-dashboard.md) | consultas + **métricas del club** |
| 06 | [Personas y Socios](06-personas-y-socios.md) | ⭐ **pain #1** — padrón, ficha del socio, grupo familiar |
| 07 | [Eventos y Boletería](07-eventos-y-boleteria.md) | ⭐ **pain #2** — evento, sectores, ventanas, packs, protocolos |
| 08 | [Políticas de acceso](08-politicas-de-acceso.md) | ⭐ el motor de reglas (35 políticas) |
| 09 | [Módulos operativos](09-modulos-operativos.md) | cobradores, deportes, colegio, ecommerce, reservas, liquidación, etc. |
| 10 | [Modelo de datos inferido](10-modelo-de-datos-inferido.md) | entidades, volúmenes, inconsistencias |
| 99 | [Informe](99-INFORME.md) | **conclusiones y recomendaciones** |

## Glosario

**[`glosario.html`](glosario.html)** — 72 entradas: qué es cada módulo, qué hace y con qué se
relaciona, más el vocabulario del sistema. Tiene buscador y filtros.

Distingue dos cosas que conviene no mezclar: los **términos del propio OurClub** (padrón,
concepto, liquidación, protocolo, canje…) y los **términos que introduce este análisis** y que
no existen en ninguna pantalla del sistema — *evaluador de elegibilidad*, *núcleo compartido*,
*dominio*, *peso relativo*, *Grupo A / Grupo B*. Estos últimos van marcados con una etiqueta
violeta y agrupados al principio.

## Presentación de dimensionamiento

**[`presentacion.html`](presentacion.html)** — deck de 14 diapositivas (abrir con doble clic,
navegar con ← →). Dimensiona los dos sistemas para decidir cómo encarar la reconstrucción:
inventario de módulos y pantallas por grupo, puntaje de complejidad en seis dimensiones,
comparación A vs B, zonas sin relevar como riesgo, y una secuencia propuesta.

> Los puntajes son un juicio informado sobre lo relevado, **no una medición ni una estimación
> de plazos**. Sirven para comparar dominios entre sí, no para presupuestar.

## Resumen visual

**[`resumen-reunion.html`](resumen-reunion.html)** — página HTML autónoma (abrir con doble clic).
Recaudación de los dos eventos vigentes, masa societaria y deportes, con la captura de la
pantalla de origen debajo de cada cifra. Las imágenes van embebidas en el archivo: no depende
de internet ni de la carpeta `capturas/`.

- `capturas/` — las 8 capturas sueltas en JPG, por si hacen falta para una presentación.
- `resumen-reunion.template.html` — fuente de la página, con tokens `__IMG_nn__` en lugar de
  las imágenes. Para regenerar el HTML hay que reemplazar cada token por el JPG correspondiente
  de `capturas/` codificado en base64 como `data:image/jpeg;base64,…`.

## Lectura mínima

Si vas a leer sólo tres: **06**, **07** y **08**. Ahí está el 80 % de lo que hay que reconstruir.
El **99** resume todo.

## Alcance y método

Recorrido de los menús de arriba hacia abajo con Chrome, extrayendo de cada pantalla:
ruta, título, breadcrumb, todos los campos (label, tipo, `name`, placeholder, valor),
opciones de cada combo, botones, columnas de tabla y mensajes del sistema.

### Acciones realizadas (todas de lectura)

- Navegación por URL y por menú.
- `Buscar` / `Filtrar` / `Verificar` en formularios de consulta.
- `Seleccionar` / `Ingresar` para abrir fichas de detalle.
- Escritura **únicamente** en campos de búsqueda: el apellido `lopez` y el filtro
  de tipo de concepto.
- Extracción del DOM vía JavaScript (solo lectura).

### Acciones NO realizadas

**No se ejecutó ninguna operación de escritura.** En particular NO se tocó:
`Confirmar` (caja) · `Importar Entradas` · `Cancelar Reservas` · `Guardar Cambios` ·
`Generar` / `Anular` (envíos masivos) · `Comenzar` / `Re-Iniciar Liquidación` ·
`Iniciar Calculo` (scoring) · `Suspender` / `Baja` (socio) · `Agregar` / `Alta` de nada ·
`Iniciar Control` / `Guardar Arqueo` · ningún `Upload`.

### Pendiente de relevar

| Área | Motivo |
|---|---|
| **Protocolos por evento** (`#/GestionProtocolosInvitadosEventoV2/…`) | `"No sos gestor de protocolos para este evento"` — requiere una sesión con ese rol. **No se intentó saltear el control por API.** |
| **Control Rápido** y **Control Acceso** | ítems de menú sin ruta en esta sesión; corresponden a la app móvil de control |
| **Administración → Sistema / Migración** | sin href en el menú |
| Solapas Tickets/Sectores/Ventanas/Sorteo del wizard de evento | no se activan por click; su **estructura** se extrajo del DOM, pero no los datos renderizados |
| Portal público / app del hincha | fuera del alcance (esto es sólo el backoffice) |
| Detalle de precios por sector | los valores capturados en `sector_detalle` son **capacidades**, no precios |

> ⚠️ Los documentos contienen algunos datos personales reales (nombres, DNI, emails) tomados
> del padrón para ilustrar estructura. Tratar esta carpeta como **material interno**.
