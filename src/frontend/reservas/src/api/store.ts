import type { Booking } from '../domain/types';
import { SEED } from '../state/seed';

const KEY = 'forever-spot/reservas/v1';

/**
 * "Base de datos" del mock. Persiste en localStorage para que una demo en el
 * celular sobreviva a un refresh y las reservas nuevas sigan estando.
 */
function load(): Booking[] {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return SEED;
    const parsed = JSON.parse(raw) as Booking[];
    return Array.isArray(parsed) && parsed.length > 0 ? parsed : SEED;
  } catch {
    return SEED;
  }
}

let bookings: Booking[] = load();

function persist(): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(bookings));
  } catch {
    // Modo privado / storage lleno: la demo sigue funcionando en memoria.
  }
}

export function readBookings(): Booking[] {
  return bookings;
}

export function addBooking(b: Booking): void {
  bookings = [b, ...bookings];
  persist();
}

export function removeBooking(id: string): void {
  bookings = bookings.filter((b) => b.id !== id);
  persist();
}

/** Vuelve a dejar la demo como recién instalada. */
export function resetBookings(): void {
  bookings = SEED;
  persist();
}
