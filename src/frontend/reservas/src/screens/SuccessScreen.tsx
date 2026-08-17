import { sportLabel } from '../domain/sport';
import { fmt } from '../domain/pricing';
import { Body, Footer, Header, Screen } from '../ui/Screen';
import { C, F, card, ctaOn, divider, rowLabel, rowValue } from '../ui/theme';
import type { BookingApi } from '../state/useBooking';

export function SuccessScreen({ api }: { api: BookingApi }) {
  const { st, set, restart } = api;
  const done = st.done;
  if (!done) return null;

  return (
    <Screen>
      <Header>
        <div style={{ font: `700 17px ${F.display}`, letterSpacing: '-.01em' }}>Reserva confirmada</div>
      </Header>

      <Body>
        <div style={{ textAlign: 'center', padding: '26px 0 18px' }}>
          <div
            aria-hidden
            style={{
              width: 56, height: 56, margin: '0 auto 14px', borderRadius: 18,
              border: `2px solid ${C.accent}`, color: C.accent,
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              font: `700 26px ${F.display}`,
            }}
          >
            ✓
          </div>
          <div style={{ font: `800 24px ${F.display}`, letterSpacing: '-.02em' }}>
            ¡Listo, {done.nombre.split(' ')[0]}!
          </div>
          <div style={{ font: `500 14px/1.5 ${F.body}`, color: C.soft, marginTop: 6 }}>
            Tu cancha queda reservada a tu nombre.
          </div>
        </div>

        <div style={card}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
            <Row k="Deporte" v={sportLabel(done.sport)} />
            <Row k="Día" v={done.diaLabel} />
            <Row k="Hora" v={done.label} />
            <Row k="Cancha" v={done.court} />
          </div>
          <div style={divider} />
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
            <span style={{ font: `700 14px ${F.body}` }}>Pagás en el club</span>
            <span style={{ font: `700 22px ${F.display}`, letterSpacing: '-.02em' }}>{fmt(done.price)}</span>
          </div>
        </div>

        <div style={{ font: `500 12.5px/1.5 ${F.body}`, color: C.dim, marginTop: 16, textAlign: 'center' }}>
          Si no podés venir, avisale al club para liberar el turno.
        </div>
      </Body>

      <Footer>
        <button
          type="button"
          onClick={() => set({ screen: 'mine' })}
          style={{
            minHeight: 48, borderRadius: 14, border: '1px solid rgba(255,255,255,.16)',
            background: 'transparent', color: C.ink, font: `600 15px ${F.body}`, cursor: 'pointer',
          }}
        >
          Ver mis reservas
        </button>
        <button type="button" onClick={restart} style={ctaOn}>
          Volver al inicio
        </button>
      </Footer>
    </Screen>
  );
}

function Row({ k, v }: { k: string; v: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
      <span style={rowLabel}>{k}</span>
      <span style={rowValue}>{v}</span>
    </div>
  );
}
