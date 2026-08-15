import type { CSSProperties } from 'react';
import type { Persona, SlotOcupado } from '../domain/types';
import { pesos } from '../domain/dinero';
import { c, mono } from './theme';

/**
 * Cómo se ve cada estado. El color dice de qué se trata antes de leer:
 * verde va, ámbar quedó a medias, naranja hay que mirarlo, gris no pasó nada.
 */
export interface Estado {
  label: string;
  /** Color del texto. */
  fg: string;
  /** Color del punto que lo acompaña. */
  dot: string;
}

/** Estado de una ficha. El orden importa: primero lo que frena la venta. */
export function estadoPersona(p: Persona): Estado {
  if (p.bloqueado) return { label: 'Bloqueada', fg: c.naranja, dot: c.naranjaPunto };
  if (p.deuda > 0) return { label: 'Con deuda', fg: c.ambarChip, dot: c.ambar };
  if (p.turnos === 0) return { label: 'Sin turnos', fg: c.textoGris, dot: c.textoMuyApagado };
  return { label: 'Activa', fg: c.verde, dot: c.verdePunto };
}

export interface EstadoPago extends Estado {
  /** Borde de la tarjeta del turno en la grilla. */
  bd: string;
  /** Fondo de la tarjeta del turno en la grilla. */
  bg: string;
}

/** Estado de cobro de un turno vendido. */
export function estadoPago(t: SlotOcupado): EstadoPago {
  if (t.ausente) {
    return {
      label: 'ausente',
      fg: c.textoGris,
      dot: c.textoMuyApagado,
      bd: c.bordeFirme,
      bg: 'transparent',
    };
  }
  if (t.pago === 'total') {
    return {
      label: 'pagado',
      fg: c.verdeTexto,
      dot: c.verdePunto,
      bd: c.verdeBorde,
      bg: c.verdeFondoSuave,
    };
  }
  if (t.pago === 'sena') {
    return {
      label: 'seña · debe ' + pesos(t.saldo),
      fg: c.ambarTexto,
      dot: c.ambar,
      bd: c.ambarBorde,
      bg: c.ambarFondo,
    };
  }
  return {
    label: 'sin pagar ' + pesos(t.saldo),
    fg: c.naranja,
    dot: c.naranjaPunto,
    bd: c.naranjaBorde,
    bg: c.naranjaFondo,
  };
}

/** Color del chip de un turno del historial de la ficha. */
export function colorTurnoHistorico(chip: string): string {
  if (chip === 'Pagado') return c.verde;
  if (chip === 'Seña pagada') return c.ambarChip;
  return c.naranja;
}

export function puntoStyle(e: Pick<Estado, 'dot'>): CSSProperties {
  return { width: 6, height: 6, borderRadius: '50%', flex: 'none', background: e.dot };
}

export function chipStyle(fg: string): CSSProperties {
  return { font: `400 12px ${mono}`, color: fg, whiteSpace: 'nowrap' };
}
