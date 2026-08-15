import { dayLabel } from '../domain/dates';
import type { Booking } from '../domain/types';

/**
 * Reservas de ejemplo para que "Mis reservas" tenga contenido desde el primer
 * arranque, en ambas pestañas. Las fechas se derivan de la grilla real (hoy + n)
 * para que nunca queden desfasadas.
 */
export const SEED: Booking[] = [
  {
    id: 'FVR-4182',
    sport: 'Fútbol 5',
    when: `${dayLabel(2, false).toLowerCase()} · 21:00 – 22:00`,
    court: 'Cancha 1 · Sintético techado · con luces',
    pay: 'sena',
    saldo: 21500,
    past: false,
  },
  {
    id: 'FVR-3907',
    sport: 'Pádel',
    when: `${dayLabel(5, false).toLowerCase()} · 19:00 – 21:00`,
    court: 'Cancha 2 · Blindex techada · con luces',
    pay: 'total',
    saldo: 0,
    past: false,
  },
  {
    id: 'FVR-2260',
    sport: 'Pádel',
    when: `${dayLabel(-7, false).toLowerCase()} · 20:00 – 21:00`,
    court: 'Cancha 1 · Blindex techada · con luces',
    pay: 'total',
    saldo: 0,
    past: true,
  },
  {
    id: 'FVR-1934',
    sport: 'Fútbol 5',
    when: `${dayLabel(-11, false).toLowerCase()} · 11:00 – 12:00`,
    court: 'Cancha 2 · Sintético al aire libre',
    pay: 'total',
    saldo: 0,
    past: true,
  },
];
