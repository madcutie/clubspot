/**
 * La grilla del día se arma acá a partir de lo que manda el backend por
 * cancha: ventanas efectivas, arranques vendibles con precio y reservas
 * confirmadas. La pantalla sólo dibuja las celdas resultantes.
 */

import type { CanchaAgenda, ReservaDia } from './types';

/** Primera y última hora que muestra la grilla. */
export const GRILLA_DESDE = 8 * 60;
export const GRILLA_HASTA = 24 * 60;

export interface CeldaLibre {
  libre: true;
  t: number;
  /** Cantidad de filas de 30 min que ocupa. */
  span: number;
  /** La ventana del día no cubre esta franja. */
  cerrado: boolean;
  /** Hay al menos un turno vendible que arranca acá. */
  vendible: boolean;
  /** Precio del turno más barato que arranca acá, o `null` si no se vende. */
  precio: number | null;
  /** Hay más de un precio posible según la duración elegida. */
  desde: boolean;
}

export interface CeldaReserva {
  libre: false;
  reserva: ReservaDia;
}

export type Celda = CeldaLibre | CeldaReserva;

/** Celdas de la columna de una cancha, de media hora en media hora. */
export function celdasDe(cancha: CanchaAgenda): Celda[] {
  const abiertas = new Set<number>();
  cancha.ventanas.forEach(([apertura, cierre]) => {
    for (let m = Math.max(apertura, GRILLA_DESDE); m < Math.min(cierre, GRILLA_HASTA); m += 30) {
      abiertas.add(m);
    }
  });

  const reservaEn = new Map<number, ReservaDia>();
  cancha.reservas.forEach((r) => reservaEn.set(r.t, r));

  const preciosEn = new Map<number, number[]>();
  cancha.turnos.forEach((s) => {
    const xs = preciosEn.get(s.t);
    if (xs) xs.push(s.precio);
    else preciosEn.set(s.t, [s.precio]);
  });
  const arranques = new Set(preciosEn.keys());

  const out: Celda[] = [];
  let t = GRILLA_DESDE;
  while (t < GRILLA_HASTA) {
    const reserva = reservaEn.get(t);
    if (reserva) {
      out.push({ libre: false, reserva });
      t += Math.max(30, reserva.dur);
      continue;
    }

    const cerrado = !abiertas.has(t);
    const vendible = !cerrado && arranques.has(t);
    let span = 1;
    while (t + span * 30 < GRILLA_HASTA) {
      const m = t + span * 30;
      if (reservaEn.has(m) || arranques.has(m)) break;
      if (!abiertas.has(m) !== cerrado) break;
      span++;
    }
    const precios = vendible ? preciosEn.get(t) : undefined;
    out.push({
      libre: true,
      t,
      span,
      cerrado,
      vendible,
      precio: precios ? Math.min(...precios) : null,
      desde: precios ? new Set(precios).size > 1 : false,
    });
    t += span * 30;
  }
  return out;
}

/** Turnos del día y ocupación: filas reservadas sobre filas abiertas. */
export function resumenAgenda(canchas: CanchaAgenda[]): { turnos: number; ocupacion: number } {
  let abiertas = 0;
  let reservadas = 0;
  let turnos = 0;
  canchas.forEach((cancha) => {
    cancha.ventanas.forEach(([apertura, cierre]) => {
      abiertas +=
        Math.max(0, Math.min(cierre, GRILLA_HASTA) - Math.max(apertura, GRILLA_DESDE)) / 30;
    });
    cancha.reservas.forEach((r) => {
      reservadas += r.dur / 30;
      turnos += 1;
    });
  });
  return { turnos, ocupacion: abiertas > 0 ? Math.round((100 * reservadas) / abiertas) : 0 };
}
