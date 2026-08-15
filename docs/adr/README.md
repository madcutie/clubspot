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
