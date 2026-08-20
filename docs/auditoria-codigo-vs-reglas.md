# Auditoría — el código contra sus propias reglas

Cruce de lo que dicen los 18 ADR y las convenciones de AGENTS.md §6 contra lo que hace el código.
La entrada más nueva arriba.

## 20/08/2026 — Primera pasada sobre `main` (`9733fdb`)

**Qué se auditó:** las reglas **mecánicamente verificables** — las que se pueden chequear con una
búsqueda sobre el árbol, sin ejecutar nada. Eso cubre 5 de los 18 ADR (0005, 0006, 0010, 0011,
0016) más las convenciones de AGENTS.md §6. Los otros 13 son de dominio y no se auditan así: se
auditan con tests (ver *lo que queda abierto*).

### Lo que estaba mal, y se arregló

| | Regla | Qué pasaba | Arreglo |
|---|---|---|---|
| 1 | *Nunca `DateTime.Now`: se inyecta `IClock`* | `JwtIssuer` fijaba el vencimiento del token con `DateTime.UtcNow`. La vida de la sesión es una decisión de diseño (ADR-0018) y **no se podía testear con reloj falso** | `IClock` inyectado; las 12 horas pasaron a la constante `Lifetime` |
| 2 | idem | `DevSeeder` usaba `DateTimeOffset.UtcNow` en 4 lugares | un solo `clock.UtcNow` al entrar, reusado en los 4 |

Ambos verificados: **build sin warnings, 82 unitarios + 76 de integración verdes** (antes 75).

El test que respalda el arreglo 1 es
`The_token_expires_twelve_hours_after_the_clock_says_it_was_issued`. Se comprobó que **falla contra
el código anterior** —`Expected 2026-03-04T21:15:00Z, Actual 2026-08-21T08:32:38Z`— y pasa contra el
nuevo. Un arreglo sin esa comprobación no está respaldado por nada.

Detalle lateral que vale anotar: al intentar reproducir la regresión dejando el parámetro `clock`
sin usar, `TreatWarningsAsErrors` la convirtió en **error de compilación** (`CS9113`). La
convención se defiende sola una vez que la dependencia está inyectada.

### Lo que parecía mal y no lo estaba

Se anota con su razón para que nadie lo "arregle" en la próxima pasada. **Los tres eran hallazgos
propios de esta auditoría, descartados al verificarlos contra el código.**

- **Los 10 `decimal` para plata de la capa Application.** No son descuido. Son tres bordes
  legítimos —lo que reporta un proveedor externo, las agregaciones en SQL, y los DTO de respuesta—
  y colapsarlos a `Money` **empeoraría el sistema**: haría indetectable `wrongCurrency` y obligaría
  a los adaptadores a inventar una moneda. La regla de AGENTS.md §6 estaba escrita más absoluta de
  lo que el código puede honrar; se corrigió **la regla**, no el código.
- **Los dos endpoints "sin contrato declarado"** (`/api/payments/return` y `/dev/checkout`). Los
  dos llevan `ExcludeFromDescription()`, que **es** la declaración explícita de que no van al
  contrato, más un comentario que explica por qué. Se verificó además que ningún frontend los llama
  a mano, que sería la violación real de ADR-0016.
- **Los textos en español del backend.** Son datos de seed (`"Cancha 1"`, `"Canchero"`) y nombres
  comerciales de módulo (`"Membresías…"`), permitidos por ADR-0006.

### Lo que está limpio

Sin desvíos en: fronteras de módulo (`Bookings` sólo toca `Core`) · cero `HasDatabaseName` /
`HasConstraintName` en las configuraciones · la Api no usa `DbContext` fuera del arranque y el seed
· los 5 enums del contrato viajan en camelCase, todos con converter registrado ·
`ITenantContext.Current` lanza sin tenant, con el comentario que explica por qué · check constraint
`ckClubsDepositPercent IN (50, 100)` · una sola cadena de 11 migraciones en orden ·
`people.debtAmount` marcada como provisional citando ADR-0012 · `Directory.Build.props` con
`nullable`, `TreatWarningsAsErrors` e `InvariantGlobalization=false` · frontend: cero `fetch` fuera
del mutator, cero glifos de texto como íconos, el cliente generado en su lugar.

### Lo que queda abierto

**No se sabe cuáles de los 13 ADR de dominio tienen un test que los cuida.** Un ADR cuya regla no
tiene test es una regla que nadie hace cumplir: el documento no se entera si alguien la rompe. Los
152 tests existen y sus nombres son frases (`The_most_specific_override_wins`), así que el cruce es
posible; falta hacerlo.

Ése es el chequeo que atrapa las vueltas en círculo, y es el próximo paso natural de este
documento.
