import type { Duration } from './types';

export function fmt(n: number): string {
  return '$' + Math.round(n).toLocaleString('es-AR');
}

/** Seña redondeada a los $100 más cercanos; `pct` viene del catálogo del club. */
export function senaOf(total: number, pct: number): number {
  return Math.round((total * pct) / 100 / 100) * 100;
}

export function durLabel(d: Duration): string {
  const h = Math.floor(d / 60);
  const m = d % 60;
  return m === 0 ? `${h} h` : `${h} h ${m}`;
}
