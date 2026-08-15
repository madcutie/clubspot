import { CLUB } from './catalog';
import type { Duration } from './types';

export function fmt(n: number): string {
  return '$' + Math.round(n).toLocaleString('es-AR');
}

/** Seña redondeada a los $100 más cercanos. */
export function senaOf(total: number): number {
  return Math.round((total * CLUB.senaPct) / 100 / 100) * 100;
}

export function durLabel(d: Duration): string {
  return d === 60 ? '1 h' : d === 90 ? '1 h 30' : '2 h';
}
