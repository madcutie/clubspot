# Cómo usar esta carpeta — instrucciones para agentes

## Qué es

`docs/referencia-ourclub/` es el **relevamiento de OurClub**, el SaaS de gestión de clubes que
Chaco For Ever usa hoy. Sistema de terceros, versión V1.44, relevado el **12/08/2026** en modo
**solo lectura** sobre el tenant `chacoforever`: 26 módulos, ~70 pantallas, campos, estados,
combos, políticas de acceso y volúmenes reales.

**Describe cómo funciona ese sistema, no cómo funciona ClubSpot.**

## Qué NO es

- **No es una especificación.** Nada entra al producto por el solo hecho de estar acá.
- **No es un backlog.** Que OurClub tenga 26 módulos no significa que ClubSpot deba tenerlos.
- **No es el modelo de datos a implementar.** `10-modelo-de-datos-inferido.md` es una
  inferencia desde la UI, no un esquema ni un dump de base.
- **No es un compromiso con el club.** Lo comprometido está en el documento de alcance.

## Precedencia

Cuando dos documentos dicen cosas distintas, el orden es:

1. `alcance-socios-mvp.html` (en esta carpeta) — qué entra al MVP.
2. `diseno-detallado-socios.html` (en esta carpeta) — cómo se resuelve.
3. **Esta carpeta** — cómo lo resuelve hoy el sistema en uso.

El relevamiento nunca gana. Si el diseño decide algo distinto de OurClub, la diferencia es
deliberada: buena parte del diseño existe justamente para no repetir lo que acá está relevado.

⚠️ **Desfasaje conocido:** el relevamiento se hizo cuando el alcance todavía incluía la venta
de entradas. Hoy **la boletería está fuera del producto** (`AGENTS.md` §1). `07-eventos-y-boleteria.md`
y buena parte de `08-politicas-de-acceso.md` quedan como contexto del negocio del club, **no**
como trabajo pendiente. El `README.md` de esta carpeta todavía menciona los "dos dolores" con
la redacción de esa época.

## Para qué sirve, entonces

Es material de consulta al implementar. Usos legítimos:

- **Vocabulario real del club** — cómo se llaman las cosas en el mostrador (padrón, concepto,
  liquidación, canje, protocolo). El dominio se escribe en español y conviene que coincida.
- **Campos, estados y casos borde** que el diseño puede no haber previsto: qué datos se cargan
  realmente en una ficha, qué combos existen, qué mensajes ve el operador.
- **Volúmenes para dimensionar**: 3.047 socios activos, 38 % de morosidad, 13 años de historia
  a migrar. Números para decidir índices, lotes y estrategia de migración.
- **Qué usa el operador todos los días** — el buscador de personas y la caja son las pantallas
  calientes; conviene que se note en las prioridades.
- **Precios y conceptos reales** (`03-caja.md`) para armar datos de ejemplo verosímiles —
  inventados, no copiados del padrón.
- **Las inconsistencias detectadas son requisitos invertidos**: cada dato roto que se encontró
  (grupos familiares de un integrante, categorías huérfanas, validaciones que son sólo un
  cartel en pantalla) marca una invariante que ClubSpot **sí** debe imponer en el agregado y
  en la base.

### Dónde buscar cada tema

| Si se está implementando… | Leer |
|---|---|
| Personas, socios, grupo familiar, categorías | `06-personas-y-socios.md` |
| Caja, cobro de mostrador, deuda, cierre | `03-caja.md` |
| Recibos, numeración, anulación, envíos | `04-recibos.md` |
| Conceptos, liquidación, cobradores, deportes, reservas, ecommerce | `09-modulos-operativos.md` |
| Alta de socios, empadronamiento, trámites | `02-tramites-y-alta-de-socios.md` |
| Entidades, volúmenes, inconsistencias, migración del padrón | `10-modelo-de-datos-inferido.md` |
| Reglas de elegibilidad y habilitación | `08-politicas-de-acceso.md` |
| Listados y métricas que el club mira hoy | `05-reportes-y-dashboard.md` |
| Mapa general antes de bucear | `01-mapa-navegacion.md` · `99-INFORME.md` |
| Qué significa un término | `glosario.html` |

`glosario.html` marca aparte los **términos que introdujo el análisis** (evaluador de
elegibilidad, núcleo compartido, Grupo A / Grupo B): no existen en ninguna pantalla de OurClub
y no deben citarse como si fueran del sistema.

## Reglas al usar esta carpeta

- **PII: material interno.** `00-datos-de-prueba.md` y los ejemplos con nombres, DNI, emails o
  domicilios son datos reales de personas. No se copian a tests, seeds, fixtures, mocks,
  documentos entregables ni capturas para mostrar. Los datos de ejemplo se inventan.
- **No se replica lo que está roto.** Ver arriba: lo relevado incluye el problema, no sólo la
  solución.
- **No se copia el esquema inferido** como diseño de tablas. Sirve para entender qué existe y
  cuánto pesa, no para generar migraciones.
- **Lo "pendiente de relevar" es un hueco conocido**, no una invitación a inferir: protocolos
  por evento, control de acceso móvil, administración/migración, precios por sector. Si algo
  depende de eso, es **pregunta al usuario**, no una decisión a tomar sola.
- **No se accede al sistema en vivo.** El relevamiento fue solo lectura y explícitamente no
  ejecutó ninguna escritura ni intentó saltear un control por API. Ese límite sigue vigente.
- **Los puntajes de `presentacion.html` son juicio informado**, no medición ni estimación de
  plazos. No se citan como números duros (`AGENTS.md` §3: no inventar números).
- **Estos documentos no se editan para "corregir" a OurClub.** Son un registro fechado del
  sistema tal como estaba el 12/08/2026. Lo que cambie después se anota aparte, con fecha.
- **Al citarlo en un entregable, decirlo**: "en el sistema actual (OurClub)…". Un lector que
  encuentra una función ajena descrita sin aclaración la lee como funcionalidad propia.
