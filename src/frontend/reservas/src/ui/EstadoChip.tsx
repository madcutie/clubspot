import { CalendarX, Check, Hourglass, Slash } from 'lucide-react';
import { ETIQUETA_ESTADO, type EstadoReserva } from '../domain/bookingStatus';
import { C, F } from './theme';

const ESTILO: Record<EstadoReserva, { fg: string; bg: string; Icono: typeof Check }> = {
  confirmada: { fg: C.accent, bg: 'rgba(205,232,74,.10)', Icono: Check },
  esperando: { fg: C.warn, bg: 'rgba(217,165,20,.12)', Icono: Hourglass },
  vencida: { fg: C.muted, bg: 'rgba(138,145,136,.12)', Icono: CalendarX },
  cancelada: { fg: C.muted, bg: 'rgba(138,145,136,.12)', Icono: Slash },
};

export function EstadoChip({ estado }: { estado: EstadoReserva }) {
  const { fg, bg, Icono } = ESTILO[estado];
  return (
    <span
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 5, flex: 'none',
        padding: '3px 9px 3px 7px', borderRadius: 999, background: bg, color: fg,
        font: `700 11.5px ${F.body}`, letterSpacing: '.01em',
      }}
    >
      <Icono size={12} strokeWidth={2.5} aria-hidden />
      {ETIQUETA_ESTADO[estado]}
    </span>
  );
}
