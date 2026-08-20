/**
 * Adaptador de horarios, canchas y agenda: traduce el contrato de la API a los
 * tipos del dominio. Las llamadas y las formas del backend vienen del cliente
 * generado (ADR-0016); acá no se escribe una URL ni una interfaz de respuesta.
 * El PUT del backend es replace-all, así que se reenvía íntegro todo campo que
 * vino en el GET.
 */

import type { AgendaDia, Cancha, Deporte, Horario, Tramo } from '../domain/types';
import { listAvailabilityOverrides, createAvailabilityOverride, deleteAvailabilityOverride } from './generated/availability-overrides/availability-overrides';
import { cancelBooking, createBooking, createBookingCheckout, getAgenda } from './generated/bookings/bookings';
import { getCourts, replaceCourts } from './generated/courts/courts';
import { getSchedules, replaceSchedules } from './generated/schedules/schedules';
import type {
  CourtRequest,
  ScheduleRequest,
  ScheduleResponseWeeklyRanges,
  Sport,
  TimeRange,
} from './generated/clubSpotApiV1.schemas';

/** Claves de día del backend, indexadas como `Date.getDay()` (0 = domingo). */
const DIAS_EN = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

function aSemanal(weeklyRanges: ScheduleResponseWeeklyRanges): Record<number, Tramo[]> {
  const out: Record<number, Tramo[]> = {};
  DIAS_EN.forEach((dia, dow) => {
    const rangos = weeklyRanges[dia];
    if (rangos) out[dow] = rangos.map((r): Tramo => [r.opensAtMinute, r.closesAtMinute]);
  });
  return out;
}

function aWeeklyRanges(semanal: Record<number, Tramo[]>): Record<string, TimeRange[]> {
  const out: Record<string, TimeRange[]> = {};
  DIAS_EN.forEach((dia, dow) => {
    const tramos = semanal[dow];
    if (tramos && tramos.length) {
      out[dia] = tramos.map((t) => ({ opensAtMinute: t[0], closesAtMinute: t[1] }));
    }
  });
  return out;
}

function aDeporte(sport: Sport): Deporte {
  return sport === 'football' ? 'futbol' : 'padel';
}

function aSport(deporte: Deporte): Sport {
  return deporte === 'futbol' ? 'football' : 'padel';
}

export async function fetchHorarios(): Promise<Horario[]> {
  const schedules = await getSchedules();
  return schedules.map((s) => ({
    id: s.id,
    nombre: s.name,
    semanal: aSemanal(s.weeklyRanges),
    version: s.version,
  }));
}

export async function guardarHorarios(horarios: Horario[]): Promise<void> {
  const body: ScheduleRequest[] = horarios.map((h) => ({
    id: h.version === undefined ? null : h.id,
    version: h.version ?? null,
    name: h.nombre,
    weeklyRanges: aWeeklyRanges(h.semanal),
  }));
  await replaceSchedules(body);
}

export async function fetchCanchas(): Promise<Cancha[]> {
  const courts = await getCourts();
  return courts.map((x) => ({
    id: x.id,
    deporte: aDeporte(x.sport),
    ci: x.sortOrder,
    nombre: x.name,
    detalle: x.detail,
    techada: x.isCovered,
    activa: x.isActive,
    horarioId: x.scheduleId,
    duraciones: x.durations,
    incremento: x.startIncrementMinutes,
    aviso: x.minimumNoticeMinutes,
    precioDia: x.dayPrice,
    precioNoche: x.nightPrice,
    noche: x.nightStartsAtMinute,
    version: x.version,
  }));
}

export interface Excepcion {
  id: string;
  /** `null` = todo el club. */
  courtId: string | null;
  fechas: string[];
  /** Vacío = cerrado. */
  tramos: Tramo[];
  motivo: string | null;
  createdAt: string;
}

export interface NuevaExcepcion {
  courtId: string | null;
  fechas: string[];
  tramos: Tramo[];
  motivo: string | null;
}

export async function fetchExcepciones(desde: string, hasta: string): Promise<Excepcion[]> {
  const overrides = await listAvailabilityOverrides({ from: desde, to: hasta });
  return overrides.map((o) => ({
    id: o.id,
    courtId: o.courtId,
    fechas: o.dates,
    tramos: o.windows.map((w): Tramo => [w.opensAtMinute, w.closesAtMinute]),
    motivo: o.reason,
    createdAt: o.createdAt,
  }));
}

export async function crearExcepcion(input: NuevaExcepcion): Promise<void> {
  await createAvailabilityOverride({
    courtId: input.courtId,
    dates: input.fechas,
    windows: input.tramos.map((t) => ({ opensAtMinute: t[0], closesAtMinute: t[1] })),
    reason: input.motivo,
  });
}

export async function borrarExcepcion(id: string): Promise<void> {
  await deleteAvailabilityOverride(id);
}

export async function fetchAgenda(deporte: Deporte, fecha: string): Promise<AgendaDia> {
  const agenda = await getAgenda({ sport: aSport(deporte), date: fecha });
  return {
    moneda: agenda.currency,
    canchas: agenda.courts.map((x) => ({
      courtId: x.courtId,
      nombre: x.name,
      detalle: x.detail,
      techada: x.isCovered,
      ventanas: x.windows.map((w): Tramo => [w.opensAtMinute, w.closesAtMinute]),
      turnos: x.slots.map((s) => ({ t: s.startMinute, dur: s.duration, precio: s.price })),
      // La API sólo manda reservas que bloquean: confirmadas y holds vivos.
      reservas: x.bookings
        .filter((b) => b.status === 'confirmed' || b.status === 'pendingPayment')
        .map((b) => ({
          id: b.id,
          t: b.startMinute,
          dur: b.durationMinutes,
          persona: b.customerName,
          tel: b.customerPhone,
          precio: b.price,
          pagado: b.paidAmount,
          pendientePago: b.status === 'pendingPayment',
        })),
    })),
    // Un hold pendiente que llega acá está vencido: si siguiera vivo, vendría entre las activas.
    inactivas: agenda.inactive.map((b) => ({
      id: b.id,
      cancha: b.courtName,
      t: b.startMinute,
      dur: b.durationMinutes,
      persona: b.customerName,
      tel: b.customerPhone,
      precio: b.price,
      pagado: b.paidAmount,
      estado: b.status === 'cancelled' ? ('cancelada' as const) : ('abandonada' as const),
    })),
  };
}

export interface NuevaReserva {
  courtId: string;
  fecha: string;
  t: number;
  dur: number;
  nombre: string;
  tel: string | null;
}

export async function crearReserva(input: NuevaReserva): Promise<{ id: string; precio: number }> {
  const creada = await createBooking({
    courtId: input.courtId,
    date: input.fecha,
    startMinute: input.t,
    durationMinutes: input.dur,
    customerName: input.nombre,
    customerPhone: input.tel,
  });
  return { id: creada.id, precio: creada.price };
}

export async function cancelarReserva(id: string): Promise<void> {
  await cancelBooking(id);
}

/** Link de pago del saldo, para mostrar como QR o mandar por WhatsApp. Se puede reemitir. */
export interface Cobro {
  url: string;
  monto: number;
  venceEn: string;
}

export async function cobrarReserva(id: string): Promise<Cobro> {
  const cobro = await createBookingCheckout(id);
  return { url: cobro.url, monto: cobro.amount, venceEn: cobro.expiresAt };
}

export async function guardarCanchas(canchas: Cancha[]): Promise<void> {
  const body: CourtRequest[] = canchas.map((x) => ({
    id: x.version === undefined ? null : x.id,
    version: x.version ?? null,
    sport: aSport(x.deporte),
    sortOrder: x.ci,
    name: x.nombre,
    detail: x.detalle,
    isCovered: x.techada,
    isActive: x.activa,
    scheduleId: x.horarioId,
    durations: x.duraciones,
    startIncrementMinutes: x.incremento,
    minimumNoticeMinutes: x.aviso,
    dayPrice: x.precioDia,
    nightPrice: x.precioNoche,
    nightStartsAtMinute: x.noche,
  }));
  await replaceCourts(body);
}
