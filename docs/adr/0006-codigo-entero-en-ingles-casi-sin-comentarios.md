# ADR-0006 — Código entero en inglés y casi sin comentarios

**Fecha:** 15/08/2026 · **Estado:** Aceptada · **Reemplaza:** ADR-0004

## Contexto

El ADR-0004 dejaba los identificadores en inglés pero los comentarios, mensajes de excepción y
nombres de tests en español. En la práctica el código quedó cargado de doc-comments largos.
El usuario pidió dos ajustes: casi cero comentarios, y que el código —comentarios incluidos—
sea siempre en inglés.

## Decisión

**1. El código va entero en inglés.** Identificadores, comentarios, mensajes de excepción y
nombres de tests (`The_product_catalog_is_valid`). Es código todo lo que vive en un `.cs`.

**2. Comentarios: casi cero.** Sólo se comenta lo **muy importante** que el código no puede
decir por sí mismo — una invariante no obvia, una lista blanca, un orden obligatorio, un
"a propósito" que sin nota parecería un error. Una o dos líneas, nunca doc-comments
decorativos ni resúmenes de lo que ya se lee en la firma.

**Sigue en español** lo que no es código: la documentación del repo (ADRs, plan, bitácora,
AGENTS), los textos que ve el usuario final en la UI y los nombres comerciales
(`DisplayName = "Socios"`). Los errores de la API viajan con código de regla; el texto que ve
el operador lo pone el frontend en español.

## Consecuencias

- Todo el backend existente se reescribió bajo esta regla el mismo 15/08/2026.
- Los tests que afirmaban sobre mensajes en español se ajustaron a los mensajes en inglés.
- Un archivo lleno de comentarios es señal de revisión: o el código no se explica solo, o
  sobran comentarios.

## Alternativas descartadas

- **Comentarios en español (ADR-0004):** generaba código bilingüe y verboso.
- **Doc-comments XML en todo lo público:** ruido; el nombre y la firma tienen que alcanzar.
