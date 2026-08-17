# Registros de decisión de arquitectura (ADR)

Decisiones de arquitectura que quedan **escritas en piedra**: qué se decidió, cuándo, por qué
y qué se descartó. Una decisión registrada acá no se rediscute en cada sesión; si cambia, no
se edita el ADR original — se escribe uno nuevo que lo reemplaza y el viejo pasa a estado
*Reemplazada por ADR-XXXX*.

Formato: contexto → decisión → consecuencias → alternativas descartadas. Un archivo por
decisión, numerado, en español y en voz impersonal.

## Índice

| ADR | Decisión | Fecha | Estado |
|---|---|---|---|
| [0001](0001-monolito-modular-con-modularidad-comercial.md) | Monolito modular; la modularidad es comercial por tenant, no plugins | 14/08/2026 | Aceptada |
| [0002](0002-agenda-calculada-en-lectura.md) | Agenda calculada en lectura; exclusion constraint contra la doble venta | 14/08/2026 | Aceptada |
| [0003](0003-auth-tablas-propias-jwt.md) | Autenticación con tablas propias + JWT | 14/08/2026 | Aceptada |
| [0004](0004-identificadores-en-ingles.md) | Identificadores en inglés, textos en español | 15/08/2026 | Reemplazada por 0006 |
| [0005](0005-capas-con-application-modulos-como-carpetas.md) | Arquitectura por capas con Application explícita; módulos como carpetas | 15/08/2026 | Aceptada |
| [0006](0006-codigo-entero-en-ingles-casi-sin-comentarios.md) | Código entero en inglés (comentarios y tests incluidos) y casi sin comentarios | 15/08/2026 | Aceptada |
| [0007](0007-esquema-postgresql-unico.md) | Un único esquema PostgreSQL `public`; módulos separados por código, no por esquema | 16/08/2026 | Aceptada · dos contextos/historiales reemplazado por 0010 |
| [0008](0008-deporte-como-configuracion-no-modulo.md) | El deporte es configuración de la cancha; se eliminan los módulos `padel` y `football` | 16/08/2026 | Aceptada |
| [0009](0009-club-module-guarda-lo-contratado.md) | `club_module` guarda lo contratado; la habilitación es el cierre resuelto en lectura | 16/08/2026 | Aceptada |
| [0010](0010-un-solo-dbcontext-y-una-sola-tabla-de-migraciones.md) | Un solo `DbContext` y una sola tabla de migraciones (`__EFMigrationsHistory`) | 16/08/2026 | Aceptada |
| [0011](0011-convenciones-fisicas-de-postgresql.md) | Convenciones físicas de PostgreSQL (camelCase, plural, `pk`/`ix`/`ux`/`fk`) resueltas por convención en el contexto | 16/08/2026 | Aceptada |
| [0012](0012-composicion-de-modulos-por-tenant.md) | Composición de módulos: el módulo es lo más chico que se vende; la persona es de `core` y ningún módulo le agrega columnas | 16/08/2026 | Aceptada, con capacidades pendientes |
| [0013](0013-disponibilidad-patron-semanal-mas-excepciones.md) | Disponibilidad: patrón semanal pisado por excepciones con fecha y alcance; gana la más específica | 16/08/2026 | Aceptada |
