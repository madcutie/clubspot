/**
 * Prueba de que esta reserva es nuestra. El id no alcanza: viaja a Mercado Pago y queda en la
 * barra de direcciones, así que el servidor pide además el token que emitió al crearla.
 *
 * Se guarda antes de irse al checkout, porque a la vuelta sólo tenemos el id de la URL.
 */

import { requireClubSlug } from '../api/club';

// La clave lleva el club adentro, por el mismo motivo que la de las reservas.
const key = () => `clubspot.${requireClubSlug()}.tokensReserva`;

type Tokens = Record<string, string>;

function load(): Tokens {
  try {
    const raw = localStorage.getItem(key());
    return raw ? (JSON.parse(raw) as Tokens) : {};
  } catch {
    return {};
  }
}

export function saveBookingToken(id: string, token: string): void {
  const all = load();
  all[id] = token;
  // Acotado: son capacidades de reservas puntuales, no un historial.
  const entries = Object.entries(all).slice(-50);
  localStorage.setItem(key(), JSON.stringify(Object.fromEntries(entries)));
}

export function loadBookingToken(id: string): string | null {
  return load()[id] ?? null;
}
