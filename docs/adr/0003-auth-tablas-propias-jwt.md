# ADR-0003 — Autenticación con tablas propias + JWT

**Fecha:** 14/08/2026 · **Estado:** Aceptada

## Contexto

El backoffice necesita usuarios de sistema por club, con roles (los 7 del catálogo del diseño).
Había que elegir entre ASP.NET Identity completo, un proveedor externo (Auth0, Entra) o un
esquema propio mínimo.

## Decisión

**Tablas propias (`user`, `user_role`) + JWT emitido por la propia API.**

- Hash de contraseñas con `PasswordHasher<T>` de `Microsoft.Extensions.Identity.Core` (sólo el
  hasher, no Identity completo), detrás de una interfaz propia.
- Login `club (slug) + email + password` → JWT con claims `sub`, `tenant`, `name`, `roles`.
  TTL 12 horas, sin refresh tokens en el MVP.
- El `tenant` viaja en el claim: de ahí sale el ámbito de tenancy de cada request.
- Roles como enum persistido como texto; autorización por políticas
  (`people.view`, `people.manage`, `agenda.operate`, `configuration.edit`).

## Consecuencias

- Cero dependencia externa y control total del modelo (email único por tenant, no global).
- Sin refresh: una sesión de mostrador dura el turno de trabajo; renovar = volver a loguear.
- Recuperación de contraseña, MFA y bloqueo por intentos quedan como trabajo futuro explícito.

## Alternativas descartadas

- **ASP.NET Identity completo:** arrastra su esquema y su modelo de usuario; sobra para 7
  roles y login de mostrador.
- **Proveedor externo:** costo, dependencia de terceros y fricción para un producto que se
  vende a clubes chicos.
