import type { Duration } from './types';

export function fmt(n: number): string {
  return '$' + Math.round(n).toLocaleString('es-AR');
}

export function durLabel(d: Duration): string {
  const h = Math.floor(d / 60);
  const m = d % 60;
  return m === 0 ? `${h} h` : `${h} h ${m}`;
}
