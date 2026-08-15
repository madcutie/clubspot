import type { Court, Sport } from './types';

export const CLUB = {
  nombre: 'Chaco Forever Spot',
  direccion: 'Av. Sarmiento 2450 · Resistencia',
  /** Porcentaje del total que se cobra online cuando se paga con seña. */
  senaPct: 50,
  /** Horas antes del turno hasta las que se puede cancelar sin cargo. */
  cancelHoras: 12,
  /** Grilla de turnos: de 8 a 24 h. */
  apertura: 8,
  cierre: 24,
  /** Días hacia adelante que se pueden reservar. */
  diasVisibles: 14,
};

export const PADEL: Court[] = [
  { n: 'Cancha 1', d: 'Blindex techada · con luces', t: 'techada', extra: 0 },
  { n: 'Cancha 2', d: 'Blindex techada · con luces', t: 'techada', extra: 0 },
  { n: 'Cancha 3', d: 'Muro descubierta', t: 'descubierta', extra: -1000 },
  { n: 'Cancha 4', d: 'Panorámica techada', t: 'techada', extra: 2000 },
];

export const FUTBOL: Court[] = [
  { n: 'Cancha 1', d: 'Sintético techado · con luces', t: 'techada', extra: 3000 },
  { n: 'Cancha 2', d: 'Sintético al aire libre', t: 'descubierta', extra: 0 },
  { n: 'Cancha 3', d: 'Sintético al aire libre', t: 'descubierta', extra: 0 },
];

export function courtList(sport: Sport): Court[] {
  return sport === 'padel' ? PADEL : FUTBOL;
}

export function sportLabel(sport: Sport): string {
  return sport === 'padel' ? 'Pádel' : 'Fútbol 5';
}

/** Día (índice relativo a hoy) en el que el club tiene torneo interno y no hay cupo. */
export const TORNEO_DIA_IDX = 3;
