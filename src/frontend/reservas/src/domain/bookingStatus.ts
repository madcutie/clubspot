import type { BookingSnapshot } from '../api/portalApi';

/**
 * Cómo se lee una reserva y sus pagos desde afuera. Todo sale de lo que devuelve el servidor:
 * acá no se inventa ni un estado ni un monto.
 */

export type EstadoReserva = 'confirmada' | 'esperando' | 'vencida' | 'cancelada';

export function estadoDe(b: BookingSnapshot, ahora = new Date()): EstadoReserva {
  if (b.status === 'confirmed') return 'confirmada';
  if (b.status === 'cancelled') return 'cancelada';
  if (b.status === 'expired') return 'vencida';
  // El vencimiento del hold es perezoso: la fila sigue en pendingPayment hasta que alguien
  // toca ese horario, así que el TTL hay que mirarlo acá.
  return b.expiresAt != null && new Date(b.expiresAt) < ahora ? 'vencida' : 'esperando';
}

export const ETIQUETA_ESTADO: Record<EstadoReserva, string> = {
  confirmada: 'Confirmada',
  esperando: 'Esperando el pago',
  vencida: 'Vencida',
  cancelada: 'Cancelada',
};

/** Estado de un intento de pago, tal cual lo reportó el proveedor. */
export const ETIQUETA_PAGO: Record<string, string> = {
  approved: 'Acreditado',
  pending: 'Pendiente de acreditación',
  rejected: 'Rechazado',
  // El club recibió la plata pero no coincide con lo acordado; lo está revisando.
  approvedOrphan: 'Recibido · en revisión del club',
};

/** Qué se estaba pagando en ese intento. */
export const ETIQUETA_CONCEPTO: Record<string, string> = {
  full: 'Total del turno',
  deposit: 'Seña',
  balance: 'Saldo',
};

export const ETIQUETA_PROVEEDOR: Record<string, string> = {
  mercadopago: 'Mercado Pago',
  fake: 'Pasarela de prueba',
};

export function proveedorLabel(provider: string): string {
  return ETIQUETA_PROVEEDOR[provider] ?? provider;
}

/** "20 ago 2026, 19:33" — el momento exacto, sin adornos. En 24 h, que es como se lee acá. */
export function momento(iso: string): string {
  return new Date(iso).toLocaleString('es-AR', {
    day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
  });
}

/** "20 ago, 19:33" — para el chip de la lista, donde el año sobra. */
export function momentoCorto(iso: string): string {
  return new Date(iso).toLocaleString('es-AR', {
    day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', hour12: false,
  });
}
