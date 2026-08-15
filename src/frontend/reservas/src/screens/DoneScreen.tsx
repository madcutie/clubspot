import { CLUB, sportLabel } from '../domain/catalog';
import { dayLabel } from '../domain/dates';
import { fmt } from '../domain/pricing';
import { Body, Footer, Screen } from '../ui/Screen';
import { C, F, card, ctaGhost, ctaOn, rowLabel, rowValue } from '../ui/theme';
import type { BookingApi } from '../state/useBooking';

export function DoneScreen({ api }: { api: BookingApi }) {
  const { st, set, payAmount, saldo } = api;
  const hasSaldo = st.pago === 'sena';

  const mapsUrl = `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(
    `${CLUB.nombre} ${CLUB.direccion}`,
  )}`;
  const waText = encodeURIComponent(
    `Reservé en ${CLUB.nombre}: ${sportLabel(st.sport)}, ${dayLabel(st.dateIdx, true)} ${
      st.sel?.label ?? ''
    }, ${st.sel?.court ?? ''}. Código ${st.code ?? ''}.`,
  );

  return (
    <Screen>
      <Body style={{ padding: 'calc(env(safe-area-inset-top) + 40px) 20px 28px' }}>
        <div
          className="pop"
          style={{
            width: 66, height: 66, borderRadius: '50%', background: C.accent,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}
        >
          <svg width="30" height="30" viewBox="0 0 30 30" fill="none" aria-hidden="true">
            <path
              d="M7 15.5l5.5 5.5L23 10.5"
              stroke="#14161A"
              strokeWidth="3.4"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </div>
        <div style={{ font: `800 30px/1.08 ${F.display}`, letterSpacing: '-.03em', margin: '18px 0 6px' }}>
          Turno confirmado
        </div>
        <div style={{ font: `500 14.5px ${F.body}`, color: C.soft }}>
          Te mandamos el detalle por WhatsApp al {st.tel || 'número que dejaste'}
        </div>

        <div style={{ ...card, marginTop: 20 }}>
          <div
            style={{
              font: `600 11.5px ${F.body}`, color: C.muted, letterSpacing: '.1em',
              textTransform: 'uppercase',
            }}
          >
            Código de reserva
          </div>
          <div style={{ font: `700 30px ${F.display}`, letterSpacing: '.08em', margin: '4px 0 16px' }}>
            {st.code ?? 'FVR-0000'}
          </div>
          <div style={{ height: 1, background: C.line, marginBottom: 14 }} />
          <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
            <Row k="Deporte" v={sportLabel(st.sport)} />
            <Row k="Día" v={dayLabel(st.dateIdx, true)} />
            <Row k="Hora" v={st.sel?.label ?? ''} />
            <Row k="Cancha" v={st.sel?.court ?? ''} />
            <Row k="Pagado" v={fmt(payAmount)} />
          </div>
        </div>

        {hasSaldo && (
          <div
            style={{
              marginTop: 12, borderRadius: 16, border: `1.5px solid ${C.accent}`,
              background: 'rgba(255,201,74,.10)', padding: '16px 18px',
            }}
          >
            <div
              style={{
                font: `700 12px ${F.body}`, color: C.accent, letterSpacing: '.08em',
                textTransform: 'uppercase',
              }}
            >
              Saldo pendiente
            </div>
            <div style={{ font: `700 26px ${F.display}`, letterSpacing: '-.02em', margin: '4px 0 6px' }}>
              {fmt(saldo)}
            </div>
            <div style={{ font: `500 13px/1.5 ${F.body}`, color: '#D8DDE4' }}>
              Se abona en el club antes de entrar a la cancha. Efectivo, débito o transferencia.
            </div>
          </div>
        )}

        <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 22 }}>
          <button type="button" style={ctaGhost} onClick={() => downloadIcs(api)}>
            Agregar al calendario
          </button>
          <a href={`https://wa.me/?text=${waText}`} target="_blank" rel="noreferrer" style={linkBtn}>
            Compartir por WhatsApp
          </a>
          <a href={mapsUrl} target="_blank" rel="noreferrer" style={linkBtn}>
            Cómo llegar
          </a>
        </div>
      </Body>

      <Footer>
        <button type="button" onClick={() => set({ screen: 'mine', tab: 'prox' })} style={ctaOn}>
          Ver mis reservas
        </button>
      </Footer>
    </Screen>
  );
}

const linkBtn = {
  ...ctaGhost,
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  textDecoration: 'none',
  color: C.ink,
} as const;

function Row({ k, v }: { k: string; v: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
      <span style={rowLabel}>{k}</span>
      <span style={rowValue}>{v}</span>
    </div>
  );
}

/** Descarga un .ics con el turno, sin depender de servicios externos. */
function downloadIcs({ st }: BookingApi): void {
  if (!st.sel) return;
  const [from] = st.sel.label.split(' – ');
  const [h, m] = from.split(':').map(Number);
  const start = new Date();
  start.setDate(start.getDate() + st.dateIdx);
  start.setHours(h, m, 0, 0);
  const end = new Date(start.getTime() + st.dur * 60000);
  const stamp = (d: Date) =>
    `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}T${String(
      d.getHours(),
    ).padStart(2, '0')}${String(d.getMinutes()).padStart(2, '0')}00`;

  const ics = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//Chaco Forever Spot//Reservas//ES',
    'BEGIN:VEVENT',
    `UID:${st.code ?? 'FVR-0000'}@foreverspot`,
    `DTSTART:${stamp(start)}`,
    `DTEND:${stamp(end)}`,
    `SUMMARY:${sportLabel(st.sport)} en ${CLUB.nombre}`,
    `LOCATION:${CLUB.direccion}`,
    `DESCRIPTION:Reserva ${st.code ?? ''}`,
    'END:VEVENT',
    'END:VCALENDAR',
  ].join('\r\n');

  const url = URL.createObjectURL(new Blob([ics], { type: 'text/calendar' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = `${st.code ?? 'reserva'}.ics`;
  a.click();
  URL.revokeObjectURL(url);
}
