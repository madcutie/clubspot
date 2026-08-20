# Bitácora — plan de las reglas de la plata huérfana

Registro de avance del [plan](plan-reglas-de-plata-huerfana.md). La entrada más nueva arriba.

## 20/08/2026 — Escrito, sin arrancar

- Salió de una pregunta del usuario al ver la columna `orphanReason` recién agregada: si el
  monto lo fija la preferencia de Mercado Pago, cómo puede entrar un pago corto. La duda era
  válida y la respuesta obligó a medir los cinco motivos contra el código en vez de darlos por
  buenos.
- **De los cinco, dos no eran lo que parecían.** `wrongCurrency` no puede pasar con una cuenta
  argentina, y `short` no lo puede provocar el cliente —del webhook se lee `TransactionAmount`,
  que es el bruto, así que ni las comisiones lo achican—: lo provoca el sistema al recalcular lo
  esperado con el `depositPercent` vivo en vez del acordado.
- **El hallazgo grande no era ninguno de los cinco**, sino la asimetría entre liberar un hold y
  dejarlo vencer: la misma situación resuelta de dos maneras, y la peor le toca al cliente que
  pagó. Se verificó que el arreglo no toca la restricción de exclusión, ni la lista de
  inactivas, y que además corrige una etiqueta que hoy miente en la agenda y un agujero de la
  conciliación J2.
- **Sin implementar.** El plan espera las decisiones del usuario, en particular el TTL del hold,
  que es un intercambio de negocio y no una decisión técnica.
