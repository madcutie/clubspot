import { TORNEO_DIA_IDX, courtList, sportLabel } from '../domain/catalog';
import { freeCourts, freeCount, hash, openHours, price } from '../domain/availability';
import { dayChip, dayLabel, hhmm } from '../domain/dates';
import { senaOf } from '../domain/pricing';
import type {
  Booking,
  CourtFilter,
  Duration,
  PayMethod,
  PayMode,
  Selection,
  Sport,
} from '../domain/types';
import { addBooking, readBookings, removeBooking } from './store';

/**
 * Backend simulado. Todas las funciones son async y devuelven DTOs planos, con
 * la misma forma que tendría el API real: cuando exista, se reemplaza este
 * archivo por llamadas HTTP y las pantallas no cambian.
 */
const LATENCY = 220;
const PAY_LATENCY = 1500;

function delay<T>(value: T, ms = LATENCY): Promise<T> {
  return new Promise((resolve) => setTimeout(() => resolve(value), ms));
}

// ── Días ─────────────────────────────────────────────────────────────────────

export interface DayDto {
  i: number;
  top: string;
  num: string;
  mon: string;
  /** Horarios con al menos una cancha libre (turnos de 1 h). */
  free: number;
}

export async function fetchDays(sport: Sport, dias: number): Promise<DayDto[]> {
  const out = Array.from({ length: dias }, (_, i) => ({
    i,
    ...dayChip(i),
    free: freeCount(sport, i),
  }));
  return delay(out);
}

export async function fetchSportCounts(dateIdx: number): Promise<Record<Sport, number>> {
  return delay({ padel: freeCount('padel', dateIdx), futbol: freeCount('futbol', dateIdx) });
}

// ── Disponibilidad ───────────────────────────────────────────────────────────

export interface HourDto {
  h: number;
  label: string;
  free: number;
}

export interface CourtDto {
  i: number;
  n: string;
  d: string;
  free: boolean;
  price: number;
}

export interface SuggestionDto {
  dateIdx: number;
  hour: number;
  courtIdx: number;
  when: string;
  court: string;
  price: number;
}

export interface AvailabilityDto {
  hours: HourDto[];
  anyFree: boolean;
  /** Canchas para la hora elegida; vacío si todavía no se eligió hora. */
  courts: CourtDto[];
  freeCourts: number;
  suggestions: SuggestionDto[];
  /** Por qué el día quedó sin cupo, cuando corresponde. */
  reason: 'torneo' | 'filtro' | null;
}

export interface AvailabilityQuery {
  sport: Sport;
  dateIdx: number;
  dur: Duration;
  ctype: CourtFilter;
  hour: number | null;
}

export async function fetchAvailability(q: AvailabilityQuery): Promise<AvailabilityDto> {
  const { sport, dateIdx, dur, ctype, hour } = q;

  const hours: HourDto[] = openHours(sport, dateIdx, dur, ctype).map((o) => ({
    h: o.h,
    label: hhmm(o.h * 60),
    free: o.n,
  }));
  const anyFree = hours.some((o) => o.free > 0);

  const cFree = hour == null ? [] : freeCourts(sport, dateIdx, hour, dur, ctype);
  const courts: CourtDto[] =
    hour == null
      ? []
      : courtList(sport)
          .map((c, i) => ({ c, i }))
          .filter((o) => ctype === 'todas' || o.c.t === ctype)
          .map((o) => ({
            i: o.i,
            n: o.c.n,
            d: o.c.d,
            free: cFree.some((x) => x.i === o.i),
            price: price(sport, o.i, hour, dur),
          }));

  // Si el día quedó sin nada, el API sugiere lo más cercano hacia adelante.
  const suggestions: SuggestionDto[] = [];
  if (!anyFree) {
    for (let i = dateIdx + 1; i < dateIdx + 6 && suggestions.length < 2; i++) {
      const hs = openHours(sport, i, dur, ctype).filter((o) => o.n > 0);
      for (const o of hs.slice(0, 2 - suggestions.length)) {
        const c = freeCourts(sport, i, o.h, dur, ctype)[0];
        suggestions.push({
          dateIdx: i,
          hour: o.h,
          courtIdx: c.i,
          when: `${dayLabel(i, false)} · ${hhmm(o.h * 60)} – ${hhmm(o.h * 60 + dur)}`,
          court: `${c.n} · ${c.d}`,
          price: price(sport, c.i, o.h, dur),
        });
      }
    }
  }

  const reason: AvailabilityDto['reason'] = anyFree
    ? null
    : dateIdx === TORNEO_DIA_IDX
      ? 'torneo'
      : 'filtro';

  return delay({ hours, anyFree, courts, freeCourts: cFree.length, suggestions, reason });
}

// ── Reservas ─────────────────────────────────────────────────────────────────

export async function fetchBookings(): Promise<Booking[]> {
  return delay(readBookings().slice());
}

export async function cancelBooking(id: string): Promise<void> {
  removeBooking(id);
  return delay(undefined);
}

// ── Pago ─────────────────────────────────────────────────────────────────────

export class PaymentRejectedError extends Error {
  constructor() {
    super('La tarjeta fue rechazada por el banco (fondos insuficientes).');
    this.name = 'PaymentRejectedError';
  }
}

export interface PayInput {
  sel: Selection;
  sport: Sport;
  dateIdx: number;
  pago: PayMode;
  method: PayMethod;
  tel: string;
  /** Intento número N para este turno. El primero con tarjeta se rechaza. */
  attempt: number;
}

export interface PayResult {
  code: string;
  booking: Booking;
}

export async function payReservation(input: PayInput): Promise<PayResult> {
  await delay(undefined, PAY_LATENCY);

  if (input.method === 'tarjeta' && input.attempt === 0) throw new PaymentRejectedError();

  const total = input.sel.price;
  const saldo = input.pago === 'total' ? 0 : total - senaOf(total);
  const code = 'FVR-' + ((hash(input.sel.key + input.tel) % 9000) + 1000);
  const booking: Booking = {
    id: code,
    sport: sportLabel(input.sport),
    when: `${dayLabel(input.dateIdx, false).toLowerCase()} · ${input.sel.label}`,
    court: input.sel.court,
    pay: input.pago,
    saldo,
    past: false,
  };
  addBooking(booking);
  return { code, booking };
}
