import type { ConfirmedBooking } from '../domain/types';

/**
 * Reservas hechas desde este dispositivo. Sin login no hay identidad, así que
 * la lista vive en localStorage; la versión server-side llega con el login.
 */

const KEY = 'clubspot.misReservas';

export function loadMyBookings(): ConfirmedBooking[] {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? (JSON.parse(raw) as ConfirmedBooking[]) : [];
  } catch {
    return [];
  }
}

export function saveMyBooking(booking: ConfirmedBooking): void {
  const list = [booking, ...loadMyBookings()].slice(0, 50);
  localStorage.setItem(KEY, JSON.stringify(list));
}
