# ADR-0001 — Monolito modular; la modularidad es comercial por tenant, no plugins

**Fecha:** 14/08/2026 · **Estado:** Aceptada

## Contexto

ClubSpot es un producto configurable por módulos: cada club contrata los que usa (núcleo,
socios, finanzas, reservas, pádel, fútbol). Hacía falta decidir qué significa "módulo" a nivel
técnico: ¿unidades desplegables por separado, plugins cargables, o un solo sistema con acceso
gateado?

## Decisión

**Un solo monolito: todos los módulos compilados en el mismo binario, un solo host, un solo
despliegue.** No hay carga dinámica ni assemblies opcionales. Agregar un módulo al producto es
un cambio de código (el catálogo en `ModuleId` es la única puerta de entrada).

La modularidad es **comercial y por tenant**, resuelta en runtime contra datos:

- Un catálogo único del producto (`ModuleCatalog`) declara los módulos y su grafo de
  dependencias, validado al arrancar (ciclos o dependencias inexistentes impiden el arranque).
- Qué contrató cada club se persiste (tabla `club_module`), guardando el **cierre transitivo**:
  contratar `padel` trae `bookings`, `finance` y `core` solos.
- El borde HTTP responde **404 —no 403— en módulo no contratado**: quien no contrató un módulo
  no tiene por qué enterarse de que existe. Los jobs de un módulo apagado no se encolan. El
  frontend arma su menú con el endpoint de capacidades.
- **La lógica de dominio jamás pregunta si un módulo está habilitado.** Si un agregado se está
  ejecutando es porque el club ya pasó el filtro del borde.
- Apagar un módulo corta el acceso; **no borra datos**.

## Consecuencias

- Despliegue y operación simples (un binario, una base), a costa de que todos los clientes
  corren la misma versión de todo.
- El gating es un punto único (filtro de endpoint + despachador de jobs), fácil de testear:
  el test canónico es "módulo apagado ⇒ 404".
- Los manifiestos (`IClubModule`) pueden parecer infraestructura de plugins; no lo son: son el
  catálogo de venta con su grafo, para validar contrataciones y apagados.

## Alternativas descartadas

- **Plugins / assemblies cargables:** complejidad de carga dinámica y versionado sin ningún
  beneficio para un SaaS multi-tenant donde el binario es uno solo.
- **Microservicios por módulo:** costo operativo injustificable para el tamaño del producto y
  del equipo.
- **403 en módulo no contratado:** filtra información de qué existe; se eligió 404.
