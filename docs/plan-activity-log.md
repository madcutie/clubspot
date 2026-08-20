# Plan — Registro de actividad (`activityLog`)

**Fecha:** 19/08/2026 · **Estado:** **F1 cerrada y verificada**; F2–F7 pendientes · Avance en la
[bitácora](plan-activity-log.bitacora.md)

Implementa [ADR-0017](adr/0017-registro-de-actividad-activitylog.md). El ADR fija el *qué* y el
*por qué*; este plan fija el esquema concreto, el catálogo de tipos, los endpoints, los roles y
el orden de ejecución.

## 1. Objetivo

Que todo hecho relevante del negocio deje una entrada, y que el canchero pueda leerla.

Al terminar, parado en el mostrador, se puede responder sin llamar a nadie:

- ¿Quién canceló este turno, cuándo y por qué?
- ¿Cuándo entró el pago? ¿Por webhook o lo levantó la conciliación?
- ¿Este hold se liberó porque el cliente abandonó, o venció solo?
- ¿Por qué esta plata quedó huérfana?
- ¿Quién bloqueó esta ficha?

## 2. Fuera de alcance (explícito)

| Qué | Por qué queda afuera |
|---|---|
| **Relleno hacia atrás** | Decisión 10 del ADR-0017: el registro arranca el día que se despliega. Lo que ya pasó no tiene historia y no se le inventa una |
| Exportar el registro | Sale después, cuando exista el exportador general (§9.4 de AGENTS.md) |
| Firmado o encadenado de entradas (hash chain) | Protege contra un administrador de base malicioso. No es el riesgo de este producto hoy; si aparece, es un ADR nuevo |
| Retención configurable por club | La retención arranca como un valor único del sistema. Volverla comercial es una decisión de producto que hoy no está tomada |
| Notificar a alguien ante un hecho | Eso es el outbox (J4), otra cosa |
| Registrar lecturas | Sólo transiciones de estado. Auditar consultas multiplica el volumen y no responde ninguna de las preguntas de §1 |

## 3. Decisiones que este plan fija

Se listan para que queden a la vista; cualquiera se puede vetar antes de aprobar.

1. **Nombres físicos.** El concepto se llama `activityLog`. La entidad es `ActivityLogEntry` y
   la tabla `activityLogEntries` — plural de la entidad, como manda ADR-0011. Una fila es una
   *entrada* del registro, no un registro: `activityLogs` diría otra cosa.
2. **`occurredAt` es uno solo.** No se guardan dos tiempos ("cuándo pasó" y "cuándo se anotó"):
   como la entrada se escribe en la misma transacción que el hecho, son el mismo instante. El
   tiempo que reporta un tercero —el `date_approved` de Mercado Pago, por ejemplo— es un dato
   del hecho y va en el payload, donde no se confunde con el del registro.
3. **El actor nunca se resuelve solo.** Un `IActivityActor` por ámbito: en HTTP sale del
   `ClaimsPrincipal`; en un job o un webhook **se setea explícitamente**. Si nadie lo setea,
   lanza — misma regla que `ITenantContext.Current` en background.
4. **El payload es `jsonb` plano**, claves en camelCase, sin anidar más de un nivel. Lo que hay
   que poder buscar no va ahí: va en columna.
5. **Roles.** Quien puede ver el sujeto ve su actividad: el registro de un turno reusa
   `AgendaOperate`, el de una persona reusa `PeopleView`. La **pantalla general** pide rol
   administrativo, porque cruza módulos y muestra la operación entera.
6. **Retención: 24 meses**, purga por J11. Es un número puesto para no dejarlo abierto, no una
   obligación legal averiguada — **conviene confirmarlo con el club**.
7. **Los endpoints nuevos nacen cumpliendo [ADR-0016](adr/0016-contrato-de-api-generado-desde-el-codigo.md)**:
   `TypedResults`, uniones `Results<Ok<T>, NotFound>`, DTO nombrados y públicos, `WithName` y
   `WithTags`. No se suman endpoints a la deuda de contrato que ese ADR vino a saldar.
8. **El frontend degrada, no rompe.** Un `type` que el frontend no conoce se muestra con su
   código crudo y su fecha. Nunca una pantalla en blanco por un tipo nuevo.

## 4. Modelo

### Entidad y tabla

`Domain/Core/Activity/ActivityLogEntry.cs` — `ITenantOwned`, sin comportamiento más allá de sus
invariantes: se construye y no se toca nunca más.

| Columna | Tipo | Notas |
|---|---|---|
| `id` | uuid | pk |
| `tenantId` | uuid | filtro global |
| `occurredAt` | timestamptz | de `IClock`; índice descendente |
| `type` | text(60) | código estable del catálogo del módulo |
| `source` | text | `counter` · `portal` · `webhook` · `job` · `system` |
| `actorUserId` | uuid null | nulo si el actor es el sistema |
| `actorName` | text(120) | **foto** del nombre al momento del hecho; `"sistema"` si no hay usuario |
| `reason` | text(300) null | obligatorio en los tipos destructivos |
| `bookingId` | uuid null | referencia tipada, indexada |
| `personId` | uuid null | referencia tipada, indexada |
| `paymentId` | uuid null | referencia tipada |
| `data` | jsonb | el resto, estructurado |

Índices: `(tenantId, occurredAt desc)` para la pantalla general, `bookingId` y `personId` para
las historias por sujeto. Los nombres los pone la convención del contexto (ADR-0011), no se
escriben a mano.

**Sin foreign keys** hacia `bookings`, `people` ni `payments`: una entrada tiene que sobrevivir
al borrado de su sujeto — si no, la purga de un dato borra su propia historia. Las columnas son
referencias, no relaciones.

### Puerto

```csharp
// Application/Core/Activity/IActivityLog.cs
public interface IActivityLog
{
    // No hace SaveChanges: la entrada se confirma con el hecho o no se confirma (ADR-0017 §5).
    void Record(ActivityRecord record);
}
```

`ActivityRecord` lleva `type`, `source`, las referencias, el `reason` y el payload. El actor no
viaja en el record: lo pone la implementación desde `IActivityActor`, para que ningún llamador
pueda mentir sobre quién fue.

**Invariante impuesta en el puerto**: un tipo marcado destructivo sin `reason` lanza. No se
confía en que cada llamador se acuerde.

## 5. Catálogo inicial de tipos

Cada módulo declara los suyos como constantes. Los marcados con **⚠** exigen motivo.

**`bookings`**

| `type` | Cuándo | Datos |
|---|---|---|
| `bookingCreated` | se vende un turno | cancha, fecha, hora, duración, precio, origen |
| `bookingCancelled` **⚠** | se cancela | quién lo tenía, cuánto faltaba para el turno |
| `bookingNoShow` **⚠** | se marca ausente | *(depende de que exista el estado; ver §6, F5)* |
| `holdCreated` | el portal toma un turno contra un pago | vencimiento |
| `holdReleased` | el comprador abandona el checkout | — |
| `holdExpired` | vence el TTL (expiración perezosa) | cuánto tardó en detectarse |
| `checkoutIssued` | se emite un link o QR de cobro | monto, proveedor, canal |

**`finance`** (hoy dentro de `bookings`, ADR-0012)

| `type` | Cuándo | Datos |
|---|---|---|
| `paymentApproved` | entra un pago aprobado | monto, moneda, proveedor, canal, id externo, tipo (seña/total/saldo) |
| `paymentRejected` | entra un pago rechazado | ídem |
| `paymentOrphaned` | plata que el club no acordó | **por qué**: duplicada, corta, moneda ajena, turno perdido |
| `reconciliationRan` | corre J2 | cuántos pagos levantó |

**`core`**

| `type` | Cuándo | Datos |
|---|---|---|
| `personCreated` | alta de una persona | origen (mostrador/app) |
| `personBlocked` **⚠** | se bloquea una ficha | — |
| `personUnblocked` | se desbloquea | — |
| `personNoteAdded` | se agrega una nota | — |
| `personPaymentRegistered` | se cancela el saldo de una ficha | monto |

`paymentOrphaned` con su motivo es, de todo el catálogo, la entrada que más falta hace hoy: es
plata que entró, que el club tiene, y que nadie sabe por qué está marcada.

## 6. Fases

Cada fase deja algo utilizable y se verifica antes de seguir.

### F1 — Núcleo y camino de la plata

- Entidad, configuración EF, migración, índices.
- `IActivityLog` + implementación en Infrastructure sobre el mismo `DbContext`, **sin flush
  propio**.
- `IActivityActor`: implementación HTTP desde el `ClaimsPrincipal`; ámbito explícito para
  webhook y job; lanza si nadie lo abrió.
- Tipos de `bookings` y de pagos cableados en `BookingsStore` y en los handlers.
- Tests: la entrada se confirma con el hecho · una transacción que revienta no deja entrada ·
  un tipo destructivo sin motivo lanza · un webhook registra actor sistema y `source=webhook`.

**Verificación:** vender un turno del portal, pagarlo con el gateway fake, cancelarlo desde el
backoffice, y leer con SQL las cinco entradas en orden.

### F2 — Motivo al cancelar

- La API de cancelación **exige motivo**; el panel del backoffice lo pide antes de cancelar.
- Mismo tratamiento para bloquear una ficha.

**Verificación:** cancelar sin motivo da 422; con motivo, la entrada lo guarda y la pantalla lo
muestra.

### F3 — Lectura

- `GET /api/bookings/{id}/activity` — la historia de un turno.
- `GET /api/people/{id}/activity` — la historia de una persona.
- `GET /api/activity?from=&to=&type=&source=` — la general, paginada, rol administrativo.
- Los tres cumpliendo ADR-0016 desde el primer commit.

### F4 — La historia dentro del panel del turno

Es donde el canchero tiene la pregunta. `ReservaPanel` gana una sección con la línea de tiempo:
hora, qué pasó, quién, y el motivo cuando lo hay. El castellano lo arma el frontend desde el
`type`; un tipo desconocido se muestra crudo.

**Verificación:** el ciclo completo de F1 leído desde la pantalla, no desde SQL.

### F5 — Cobertura ancha

- Tipos de `core` (personas) y de configuración (horarios, canchas, excepciones).
- **`bookingNoShow` depende de que exista el estado**: hoy `BookingStatus` no tiene ausencia.
  Es trabajo previo, no de este plan — se anota acá porque la ficha de una persona ya mostró
  ese dato inventado y se sacó el 19/08/2026.

### F6 — Pantalla general y ficha de persona

La actividad del día para la operación, y la línea de tiempo dentro de la ficha. Salen de la
misma tabla que F3, sin trabajo de backend nuevo.

### F7 — Retención y purga

J11 con las reglas de AGENTS.md §7: idempotente, por lotes, reanudable, con lock por
(job, tenant), en hora local del club. Borra por antigüedad, jamás por contenido.

## 7. Orden y valor

| Después de | El club tiene |
|---|---|
| F1 | el registro existe y es confiable, aunque sólo se lea por SQL |
| F2 | se sabe **por qué** se cancelan las cosas — el dato que el sistema de referencia nunca tuvo |
| F3+F4 | el canchero responde solo "¿quién canceló esto?" |
| F5+F6 | la operación entera, en una pantalla |
| F7 | la tabla no crece para siempre |

F1 a F4 son el hito con valor real; F5 en adelante es ensanchar.
