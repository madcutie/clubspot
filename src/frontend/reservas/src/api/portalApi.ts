import { addDays, dayChipOf, dayLabelOf, hhmm, isoDate, parseDate } from '../domain/dates';
import type { CourtFilter, CourtType, Duration, Sport } from '../domain/types';
import { API_URL, CLUB_SLUG } from './config';
import { queryClient } from './queryClient';

/**
 * Adaptador del portal contra la API real. Dos fuentes cacheadas por React
 * Query —el catálogo y la disponibilidad de 14 días por deporte— y todo lo
 * demás se deriva de esos payloads.
 */

/** Días hacia adelante que se pueden reservar (constante de UI). */
export const DIAS_VISIBLES = 14;

// ── Contrato de la API (camelCase, deporte en inglés) ────────────────────────

type ApiSport = 'padel' | 'football';

interface ApiCatalog {
  club: { name: string; venue: string | null; currency: string; depositPercent: number };
  sports: { sport: ApiSport; courts: ApiCourt[] }[];
  onlinePayments: boolean;
}

interface ApiCourt {
  id: string;
  name: string;
  detail: string;
  isCovered: boolean;
  durations: number[];
}

interface ApiAvailability {
  currency: string;
  days: { date: string; courts: { courtId: string; slots: ApiSlot[] }[] }[];
}

interface ApiSlot {
  startMinute: number;
  duration: number;
  price: number;
}

const API_SPORT: Record<Sport, ApiSport> = { padel: 'padel', futbol: 'football' };

export class ApiError extends Error {
  constructor(public readonly status: number, path: string) {
    super(`La API respondió ${status} en ${path}`);
  }
}

async function getJson<T>(path: string): Promise<T> {
  const res = await fetch(`${API_URL}/api/portal/${CLUB_SLUG}${path}`);
  if (!res.ok) throw new ApiError(res.status, path);
  return res.json() as Promise<T>;
}

function fetchCatalog(): Promise<ApiCatalog> {
  return queryClient.fetchQuery({
    queryKey: ['portal', 'catalog'],
    queryFn: () => getJson<ApiCatalog>('/catalog'),
  });
}

function fetchRange(sport: Sport): Promise<ApiAvailability> {
  const hoy = new Date();
  const from = isoDate(hoy);
  const to = isoDate(addDays(hoy, DIAS_VISIBLES - 1));
  return queryClient.fetchQuery({
    queryKey: ['portal', 'availability', sport, from],
    queryFn: () =>
      getJson<ApiAvailability>(`/availability?sport=${API_SPORT[sport]}&from=${from}&to=${to}`),
  });
}

// ── Derivaciones ─────────────────────────────────────────────────────────────

interface CatalogCourt {
  id: string;
  n: string;
  d: string;
  t: CourtType;
  durations: number[];
}

function courtsOf(cat: ApiCatalog, sport: Sport): CatalogCourt[] {
  const grupo = cat.sports.find((s) => s.sport === API_SPORT[sport]);
  return (grupo?.courts ?? []).map((c) => ({
    id: c.id,
    n: c.name,
    d: c.detail,
    t: c.isCovered ? 'techada' : 'descubierta',
    durations: c.durations,
  }));
}

function startsOf(day: ApiAvailability['days'][number] | undefined): Set<number> {
  const starts = new Set<number>();
  for (const c of day?.courts ?? []) for (const s of c.slots) starts.add(s.startMinute);
  return starts;
}

// ── Club ─────────────────────────────────────────────────────────────────────

export interface ClubDto {
  nombre: string;
  direccion: string;
  moneda: string;
  /** Porcentaje del total que se cobra online cuando se paga con seña. */
  senaPct: number;
  /** El club tiene un gateway de pago configurado. */
  pagoOnline: boolean;
}

export async function fetchClub(): Promise<ClubDto> {
  const cat = await fetchCatalog();
  return {
    nombre: cat.club.name,
    direccion: cat.club.venue ?? '',
    moneda: cat.club.currency,
    senaPct: cat.club.depositPercent,
    pagoOnline: cat.onlinePayments,
  };
}

export async function fetchCourtCounts(): Promise<Record<Sport, number>> {
  const cat = await fetchCatalog();
  return { padel: courtsOf(cat, 'padel').length, futbol: courtsOf(cat, 'futbol').length };
}

// ── Días ─────────────────────────────────────────────────────────────────────

export interface DayDto {
  i: number;
  date: string;
  top: string;
  num: string;
  mon: string;
  /** "sábado 15 de agosto" */
  long: string;
  /** Horarios de inicio con al menos una cancha libre. */
  free: number;
}

export async function fetchDays(sport: Sport, dias: number): Promise<DayDto[]> {
  const range = await fetchRange(sport);
  // El índice de día se ancla a la primera fecha del payload, no al reloj del navegador.
  return range.days.slice(0, dias).map((day, i) => {
    const d = parseDate(day.date);
    return {
      i,
      date: day.date,
      ...dayChipOf(d, i),
      long: dayLabelOf(d, true),
      free: startsOf(day).size,
    };
  });
}

export async function fetchSportCounts(dateIdx: number): Promise<Record<Sport, number>> {
  const [padel, futbol] = await Promise.all([fetchRange('padel'), fetchRange('futbol')]);
  return {
    padel: startsOf(padel.days[dateIdx]).size,
    futbol: startsOf(futbol.days[dateIdx]).size,
  };
}

// ── Disponibilidad ───────────────────────────────────────────────────────────

export interface HourDto {
  /** Minuto de inicio del turno (p. ej. 510 = 08:30). */
  h: number;
  label: string;
  free: number;
}

export interface CourtDto {
  i: number;
  id: string;
  n: string;
  d: string;
  free: boolean;
  /** Precio del turno según el servidor; null cuando la cancha no está disponible. */
  price: number | null;
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
  /** Fecha ISO del día consultado; null si el índice quedó fuera del rango. */
  date: string | null;
  /** Duraciones ofrecidas por las canchas del deporte, en minutos. */
  durations: Duration[];
  hours: HourDto[];
  anyFree: boolean;
  /** Canchas para el horario elegido; vacío si todavía no se eligió horario. */
  courts: CourtDto[];
  freeCourts: number;
  suggestions: SuggestionDto[];
  reason: 'filtro' | null;
  /** "SÁB 15 AGO" / "sábado 15 de agosto" del día consultado. */
  dayShort: string;
  dayLong: string;
}

export interface AvailabilityQuery {
  sport: Sport;
  dateIdx: number;
  dur: Duration;
  ctype: CourtFilter;
  hour: number | null;
}

export async function fetchAvailability(q: AvailabilityQuery): Promise<AvailabilityDto> {
  const [cat, range] = await Promise.all([fetchCatalog(), fetchRange(q.sport)]);
  const allCourts = courtsOf(cat, q.sport);
  const day = range.days[q.dateIdx];
  const date = day ? parseDate(day.date) : null;

  const durations = [...new Set(allCourts.flatMap((c) => c.durations))].sort((a, b) => a - b);

  const slotsFor = (
    d: ApiAvailability['days'][number] | undefined,
    filter: CourtFilter,
  ): Map<number, { court: CatalogCourt; courtIdx: number; slot: ApiSlot }[]> => {
    const porInicio = new Map<number, { court: CatalogCourt; courtIdx: number; slot: ApiSlot }[]>();
    for (const dc of d?.courts ?? []) {
      const courtIdx = allCourts.findIndex((c) => c.id === dc.courtId);
      if (courtIdx < 0) continue;
      const court = allCourts[courtIdx];
      if (filter !== 'todas' && court.t !== filter) continue;
      for (const slot of dc.slots) {
        if (slot.duration !== q.dur) continue;
        const grupo = porInicio.get(slot.startMinute) ?? [];
        grupo.push({ court, courtIdx, slot });
        porInicio.set(slot.startMinute, grupo);
      }
    }
    return porInicio;
  };

  const porInicio = slotsFor(day, q.ctype);
  const hours: HourDto[] = [...porInicio.keys()]
    .sort((a, b) => a - b)
    .map((h) => ({ h, label: hhmm(h), free: porInicio.get(h)!.length }));
  const anyFree = hours.length > 0;

  const enHora = q.hour == null ? [] : (porInicio.get(q.hour) ?? []);
  const courts: CourtDto[] =
    q.hour == null
      ? []
      : allCourts
          .map((c, i) => ({ c, i }))
          .filter((o) => q.ctype === 'todas' || o.c.t === q.ctype)
          .map((o) => {
            const libre = enHora.find((x) => x.courtIdx === o.i);
            return {
              i: o.i,
              id: o.c.id,
              n: o.c.n,
              d: o.c.d,
              free: libre != null,
              price: libre?.slot.price ?? null,
            };
          });

  // Si el día quedó sin nada, se busca lo más cercano hacia adelante dentro del rango ya cacheado.
  const suggestions: SuggestionDto[] = [];
  if (!anyFree) {
    for (let i = q.dateIdx + 1; i < range.days.length && suggestions.length < 2; i++) {
      const dia = range.days[i];
      const inicios = slotsFor(dia, q.ctype);
      const starts = [...inicios.keys()].sort((a, b) => a - b);
      for (const h of starts.slice(0, 2 - suggestions.length)) {
        const { court, courtIdx, slot } = inicios.get(h)![0];
        suggestions.push({
          dateIdx: i,
          hour: h,
          courtIdx,
          when: `${dayLabelOf(parseDate(dia.date), false)} · ${hhmm(h)} – ${hhmm(h + q.dur)}`,
          court: `${court.n} · ${court.d}`,
          price: slot.price,
        });
      }
    }
  }

  return {
    date: day?.date ?? null,
    durations,
    hours,
    anyFree,
    courts,
    freeCourts: enHora.length,
    suggestions,
    reason: anyFree ? null : 'filtro',
    dayShort: date ? dayLabelOf(date, false) : '',
    dayLong: date ? dayLabelOf(date, true) : '',
  };
}

// ── Reserva ──────────────────────────────────────────────────────────────────

export type ApiPaymentMode = 'club' | 'onlineFull' | 'onlineDeposit';
export type BookingStatus = 'confirmed' | 'cancelled' | 'pendingPayment' | 'expired';

export interface BookingRequest {
  courtId: string;
  date: string;
  startMinute: number;
  durationMinutes: number;
  customerName: string;
  customerPhone: string;
  customerEmail: string | null;
  paymentMode: ApiPaymentMode;
  /** Adónde vuelve el checkout; el servidor le agrega `retorno={id}`. */
  returnUrl: string | null;
}

export interface BookingCreated {
  id: string;
  price: number;
  /** Lo que se cobra online (la seña o el total); igual al precio en modo club. */
  chargeAmount: number;
  status: BookingStatus;
  expiresAt: string | null;
  checkoutUrl: string | null;
}

export async function createBooking(request: BookingRequest): Promise<BookingCreated> {
  const res = await fetch(`${API_URL}/api/portal/${CLUB_SLUG}/bookings`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  if (!res.ok) throw new ApiError(res.status, '/bookings');
  return res.json() as Promise<BookingCreated>;
}

export interface BookingSnapshot {
  id: string;
  courtId: string;
  courtName: string;
  sport: ApiSport;
  date: string;
  startMinute: number;
  durationMinutes: number;
  price: number;
  paidAmount: number;
  status: BookingStatus;
  paymentMode: ApiPaymentMode;
  expiresAt: string | null;
}

export function fetchBooking(id: string): Promise<BookingSnapshot> {
  return getJson<BookingSnapshot>(`/bookings/${id}`);
}

/**
 * La disponibilidad cambió (se reservó o se perdió un turno): se invalida todo,
 * porque días, contadores y grilla derivan del mismo payload cacheado.
 */
export function invalidateAvailability(): Promise<void> {
  return queryClient.invalidateQueries();
}
