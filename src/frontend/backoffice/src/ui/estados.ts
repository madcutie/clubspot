import type { CSSProperties } from 'react';
import type { Persona } from '../domain/types';
import { c, mono } from './theme';

/**
 * Cómo se ve cada estado. El color dice de qué se trata antes de leer:
 * azul va, ámbar quedó a medias, naranja hay que mirarlo, gris no pasó nada.
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
  return { label: 'Activa', fg: c.acento, dot: c.acentoPunto };
}

/** Color del chip de un turno del historial de la ficha. */
export function colorTurnoHistorico(chip: string): string {
  if (chip === 'Pagado') return c.acento;
  if (chip === 'Seña pagada') return c.ambarChip;
  return c.naranja;
}

export function puntoStyle(e: Pick<Estado, 'dot'>): CSSProperties {
  return { width: 6, height: 6, borderRadius: '50%', flex: 'none', background: e.dot };
}

export function chipStyle(fg: string): CSSProperties {
  return { font: `400 12px ${mono}`, color: fg, whiteSpace: 'nowrap' };
}
