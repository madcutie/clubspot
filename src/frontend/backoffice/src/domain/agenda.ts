/**
 * Lecturas sobre una agenda ya armada. La grilla llega del backend con los
 * turnos vendidos y los huecos; acá sólo se responde "¿entra un turno de tanto
 * acá?", que es lo que necesita el operador para vender.
 */

import type { ColumnaAgenda } from './types';

/** Minutos tomados por cancha: turno vendido o franja cerrada. */
export type Ocupacion = Set<number>[];

export function ocupacion(columnas: ColumnaAgenda[]): Ocupacion {
  return columnas.map((col) => {
    const tomados = new Set<number>();
    col.items.forEach((it) => {
      if (!it.libre) {
        for (let m = it.t; m < it.t + it.dur; m += 30) tomados.add(m);
      } else if (it.cerrado) {
        for (let k = 0; k < (it.span || 1); k++) tomados.add(it.t + k * 30);
      }
    });
    return tomados;
  });
}

/** ¿Está libre la cancha `ci` desde `t` por `dur` minutos? */
export function libreEn(
  ocup: Ocupacion,
  ci: number,
  t: number | null,
  dur: number,
): boolean {
  if (t == null || t + dur > 24 * 60) return false;
  const tomados = ocup[ci];
  if (!tomados) return false;
  for (let m = t; m < t + dur; m += 30) if (tomados.has(m)) return false;
  return true;
}

/** Primera cancha libre a esa hora, o -1. Sirve para ofrecer un cambio de cancha. */
export function primeraLibre(ocup: Ocupacion, t: number, dur: number): number {
  for (let ci = 0; ci < ocup.length; ci++) if (libreEn(ocup, ci, t, dur)) return ci;
  return -1;
}
