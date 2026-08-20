# Bitácora — plan del registro de actividad

Registro de avance del [plan](plan-activity-log.md). La entrada más nueva arriba.

## 19/08/2026 — ADR y plan escritos, sin arrancar

- El tema apareció al preguntar qué quedaba pendiente después de conectar la base de personas.
  El pedido original fue "el traffic log", y al desarmarlo quedó claro que no era tráfico HTTP
  sino la crónica del negocio: entra un pago y hay que asentarlo, se cancela un turno y hay que
  asentarlo.
- **El usuario amplió el alcance sobre lo que decía AGENTS.md §9.1**: no es sólo auditoría, el
  canchero también tiene que poder ver qué pasó y cómo. Y no son sólo acciones de usuarios:
  también eventos que llegan solos, como la entrada de un webhook, para saber cuándo entró.
- Se evaluó el nombre. `trafficLog` se descartó porque "traffic" en software significa tráfico
  de red — de hecho, al pedirlo así, lo primero que se entregó fue un log de requests HTTP.
  **El usuario eligió `activityLog`**, y con ese nombre quedó.
- Se escribió [ADR-0017](adr/0017-registro-de-actividad-activitylog.md) con las decisiones de
  fondo (un solo registro para operador y auditoría · actor persona o sistema · nunca la frase
  en castellano · tipos inmutables · misma transacción que el hecho · append-only · motivo en
  lo destructivo · sin relleno hacia atrás) y el plan con el esquema, el catálogo de tipos, los
  endpoints, los roles y siete fases.
- **Sin implementar.** El plan espera aprobación.

Dos cosas quedaron anotadas para confirmar con el usuario antes de F1:

1. **La retención de 24 meses es un número puesto**, no averiguado. Conviene confirmarlo con el
   club antes de que empiece a borrar.
2. **`bookingNoShow` depende de un estado que no existe**: `BookingStatus` no modela la
   ausencia. Se detectó el mismo día, al sacar de la ficha de una persona el dato de ausencias
   que el mock inventaba.
