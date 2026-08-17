# ADR-0007 - Esquema PostgreSQL unico

**Fecha:** 16/08/2026 · **Estado:** Aceptada

## Contexto

La configuracion inicial separaba las tablas fisicas en los esquemas `core` y `bookings`.
Aunque los modulos son una frontera de dominio y de producto, esa separacion agrega navegacion
innecesaria a la base y no es la organizacion esperada para la operacion del producto.

## Decision

Todas las tablas de ClubSpot viven en el esquema PostgreSQL estandar `public`.

Los contextos de EF Core se mantienen separados mientras corresponda a sus responsabilidades,
pero no determinan un esquema. Cada contexto mantiene su propia tabla de historial de
migraciones, con nombre distinto, dentro de `public`.

La frontera entre modulos sigue siendo el codigo por carpetas, los contratos y el catalogo de
modulos; no el esquema fisico de PostgreSQL.

## Consecuencias

- DBeaver y las herramientas operativas muestran todas las tablas de la aplicacion bajo
  `public`.
- Las migraciones y los contextos deben referenciar el esquema unico y dos historiales de EF
  con nombres distintos para evitar colisiones.
- Las migraciones iniciales de desarrollo se regeneran para crear las tablas directamente en
  `public`. Las bases locales creadas con los esquemas anteriores deben recrearse.

## Alternativas descartadas

- **Un esquema por modulo:** separa visualmente las tablas pero no aporta una frontera de
  compilacion ni de seguridad y complica la inspeccion operativa.
- **Un unico DbContext:** no es necesario para usar un esquema unico; se conserva la separacion
  tecnica de los contextos actuales.
