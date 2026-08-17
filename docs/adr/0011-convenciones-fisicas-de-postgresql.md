# ADR-0011 — Convenciones físicas de PostgreSQL, resueltas por convención y no a mano

**Fecha:** 16/08/2026 · **Estado:** Aceptada ·
**Consolida** decisiones sueltas que hasta hoy sólo vivían en `AGENTS.md` §6 y en la bitácora

## Contexto

Las reglas de nomenclatura física se habían ido tomando de a una y quedaron repartidas entre
`AGENTS.md` y entradas de bitácora. La revisión de modelado del 16/08/2026 mostró el problema
de fondo: cuando la regla se aplica **nombre por nombre en cada configuración**, se cumple sólo
donde alguien se acordó de escribirla. Los índices y claves foráneas que EF nombraba por su
cuenta —`IX_courts_tenantId`, `FK_clubModules_clubs_clubId`— quedaban con guiones bajos y
prefijos en mayúscula, violando la convención camelCase mientras los nombrados a mano sí la
cumplían. El caso más claro: el índice por `tenantId` lo agrega código genérico para toda
entidad `ITenantOwned`, así que no hay ninguna configuración donde ponerle el nombre.

## Decisión

Las convenciones físicas quedan fijadas y se aplican **por convención en el modelo**, no
declarándolas caso por caso.

1. **Un único esquema `public`** (ADR-0007) y una **única cadena de migraciones** con la tabla
   de historial estándar `__EFMigrationsHistory` (ADR-0010).
2. **camelCase** en todo nombre físico: tablas, columnas, índices y constraints.
3. **Tablas en plural**, plural inglés real, incluidos los irregulares: `people`, no `persons`.
   Las **columnas van en singular**, salvo que el dato sea una colección.
4. **Los nombres de claves, índices y foráneas los asigna `ClubSpotDbContext`** en una pasada
   final sobre el modelo terminado, con este esquema:

   | Objeto | Patrón | Ejemplo |
   |---|---|---|
   | Clave primaria | `pk<Tabla>` | `pkPeople` |
   | Índice | `ix<Tabla><Columnas>` | `ixPeopleTenantIdSearchName` |
   | Índice único | `ux<Tabla><Columnas>` | `uxUsersTenantIdEmail` |
   | Clave foránea | `fk<Tabla><Columnas>` | `fkCourtsScheduleId` |

   Las columnas entran con su nombre físico completo y la primera letra en mayúscula: el
   nombre es mecánico y predecible, sin abreviaturas ni excepciones.
5. **No se escriben `HasDatabaseName` ni `HasConstraintName` en las configuraciones.** Un
   nombre explícito sería una segunda fuente de verdad que la convención pisa igual.
   Excepción: los **check constraints**, que se nombran donde se declaran (`ckClubsDepositPercent`),
   porque expresan una regla puntual y no se derivan de columnas.

## Consecuencias

- Toda entidad nueva cumple la convención sin que nadie se acuerde de nada, incluidos los
  índices que agrega código genérico.
- Los nombres son deterministas: dado tabla y columnas, el nombre se puede predecir y
  greppear.
- Cambiar el esquema de nombres es un solo lugar, no una recorrida por todas las
  configuraciones.
- Único nombre del sistema que queda fuera de la convención: `PK___EFMigrationsHistory`, que
  crea EF para su propia tabla de historial. No es una tabla de dominio y no se toca.
- La base de desarrollo es descartable: ante un cambio de nombres se tira y se recrea
  (`docker compose down -v && docker compose up -d postgres`), no se migra.

## Alternativas descartadas

- **Nombrar cada índice y foránea a mano en su configuración:** es lo que se venía haciendo y
  falló exactamente donde no había configuración dónde escribirlo. Depende de que nadie se
  olvide.
- **Aceptar los defaults de EF** (`IX_tabla_columna`): contradice la convención camelCase y
  mezcla dos estilos en la misma base.
- **Un plugin de convenciones de nombres de terceros:** dependencia nueva para veinte líneas
  de metadatos propios.
