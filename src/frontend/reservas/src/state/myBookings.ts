import { requireClubSlug } from '../api/club';
import type { ConfirmedBooking } from '../domain/types';

/**
 * Reservas hechas desde este dispositivo. Sin login no hay identidad, así que
 * la lista vive en localStorage; la versión server-side llega con el login.
 */

// La clave lleva el club adentro: dos clubes en el mismo dominio comparten origen, y sin esto
// las reservas de uno aparecerían en el otro.
const key = () => `clubspot.${requireClubSlug()}.misReservas`;

export function loadMyBookings(): ConfirmedBooking[] {
  try {
    const raw = localStorage.getItem(key());
    return raw ? (JSON.parse(raw) as ConfirmedBooking[]) : [];
  } catch {
    return [];
  }
}

export function saveMyBooking(booking: ConfirmedBooking): void {
  const list = [booking, ...loadMyBookings()].slice(0, 50);
  localStorage.setItem(key(), JSON.stringify(list));
}
