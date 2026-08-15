# ADR-0004 — Identificadores en inglés, textos en español

**Fecha:** 15/08/2026 · **Estado:** Aceptada

## Contexto

La convención original del repo era "todo en español", incluidos los nombres de dominio
(`Socio`, `Cancha`, `Reserva`). Al arrancar la implementación del backend, el usuario decidió
que las implementaciones fueran en inglés.

## Decisión

**Todo identificador de código va en inglés; todo texto que lee una persona va en español.**

- En inglés: clases, métodos, tablas, columnas, endpoints, proyectos, ids de módulo
  (`Person`, `Court`, `Booking`, `Schedule`, `Period`; `/api/people`; módulos `core`,
  `members`, `finance`, `bookings`, `padel`, `football`).
- En español: documentación, comentarios, mensajes de error, nombres de tests
  (`El_catalogo_del_producto_es_valido`) y nombres comerciales (`DisplayName = "Socios"`).
- El vocabulario del club (glosario del relevamiento) alimenta la UI y los textos, no los
  identificadores.

Se renombró también **todo lo ya existente** (proyectos, `Periodo`→`Period`, ids de módulo):
era el único momento barato, sin nada persistido ni commits. Los ids de módulo se persisten a
partir de la fase A2 y desde entonces no cambian más.

El mapa de traducción completo del plan (español del documento → inglés del código) está en la
nota de actualización de `docs/plan-backend-backoffice.md`.

## Consecuencias

- El código queda alineado con el ecosistema .NET y con cualquier desarrollador futuro.
- Hay una traducción mental entre el vocabulario del mostrador ("cancha", "seña") y el código
  (`Court`, `deposit`); el glosario y los comentarios en español la achican.
- `AGENTS.md` §6 quedó actualizado; la regla vieja ("todo en español") ya no aplica al código.

## Alternativas descartadas

- **Dominio en español (convención original):** el usuario la descartó explícitamente.
- **Inglés también en comentarios y tests:** se prefirió mantener en español lo que lee una
  persona del equipo.
