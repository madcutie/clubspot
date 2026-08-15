import { CLUB, TORNEO_DIA_IDX, courtList } from './catalog';
import type { CourtFilter, Duration, Sport } from './types';

/**
 * FNV-1a. La ocupación del club es un mock determinístico: la misma combinación
 * deporte/día/cancha/bloque devuelve siempre el mismo estado, así el prototipo
 * se comporta de forma estable entre renders y recargas.
 */
export function hash(s: string): number {
  let h = 2166136261;
  for (let i = 0; i < s.length; i++) {
    h ^= s.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

/** ¿Está libre el bloque de 30 min número `b` (0 = 08:00) de esa cancha? */
function blockFree(sport: Sport, dateIdx: number, ci: number, b: number): boolean {
  if (dateIdx === TORNEO_DIA_IDX) return false;
  const h = CLUB.apertura + Math.floor(b / 2);
  const r = hash(`${sport}|${dateIdx}|${ci}|${b}`) % 100;
  // A la noche el club está más lleno.
  return h >= 19 ? r < 54 : r < 74;
}

/** ¿La cancha está libre durante todo el bloque `hour` → `hour + dur`? */
export function courtFree(
  sport: Sport,
  dateIdx: number,
  ci: number,
  hour: number,
  dur: Duration,
): boolean {
  if (hour + dur / 60 > CLUB.cierre) return false;
  const start = (hour - CLUB.apertura) * 2;
  const n = dur / 30;
  for (let b = start; b < start + n; b++) {
    if (!blockFree(sport, dateIdx, ci, b)) return false;
  }
  return true;
}

export function price(sport: Sport, ci: number, hour: number, dur: Duration): number {
  const night = hour >= 19;
  const base =
    (sport === 'padel' ? (night ? 15000 : 12000) : night ? 40000 : 34000) +
    courtList(sport)[ci].extra;
  return Math.round((base * dur) / 60 / 100) * 100;
}

export interface FreeCourt {
  i: number;
  n: string;
  d: string;
}

export function freeCourts(
  sport: Sport,
  dateIdx: number,
  hour: number,
  dur: Duration,
  filter: CourtFilter,
): FreeCourt[] {
  return courtList(sport)
    .map((c, i) => ({ i, n: c.n, d: c.d, t: c.t }))
    .filter((o) => filter === 'todas' || o.t === filter)
    .filter((o) => courtFree(sport, dateIdx, o.i, hour, dur));
}

export interface OpenHour {
  /** Hora de inicio (8…23). */
  h: number;
  /** Cantidad de canchas libres para todo el bloque. */
  n: number;
}

export function openHours(
  sport: Sport,
  dateIdx: number,
  dur: Duration,
  filter: CourtFilter,
): OpenHour[] {
  const out: OpenHour[] = [];
  for (let h = CLUB.apertura; h + dur / 60 <= CLUB.cierre; h++) {
    out.push({ h, n: freeCourts(sport, dateIdx, h, dur, filter).length });
  }
  return out;
}

/** Cantidad de horarios con al menos una cancha libre (turnos de 1 h, sin filtro). */
export function freeCount(sport: Sport, dateIdx: number): number {
  return openHours(sport, dateIdx, 60, 'todas').filter((o) => o.n > 0).length;
}

/** Primer turno libre del día, priorizando la franja nocturna. */
export function firstFree(
  sport: Sport,
  dateIdx: number,
  dur: Duration,
): { hour: number | null; courtIdx: number | null } {
  const hs = openHours(sport, dateIdx, dur, 'todas').filter((o) => o.n > 0);
  const o = hs.find((x) => x.h >= 19) ?? hs[0];
  if (!o) return { hour: null, courtIdx: null };
  return { hour: o.h, courtIdx: freeCourts(sport, dateIdx, o.h, dur, 'todas')[0].i };
}
