import { Footer, Header, Screen } from '../ui/Screen';
import { C, F, ctaOn } from '../ui/theme';
import type { BookingApi } from '../state/useBooking';

export function MyBookingsScreen({ api }: { api: BookingApi }) {
  const { restart } = api;

  return (
    <Screen>
      <Header>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <button
            type="button"
            onClick={restart}
            aria-label="Volver"
            style={{
              width: 44, height: 44, marginLeft: -10, border: 'none', background: 'transparent',
              color: C.ink, font: `400 22px ${F.body}`, cursor: 'pointer',
            }}
          >
            ←
          </button>
          <div style={{ font: `700 17px ${F.display}`, letterSpacing: '-.01em' }}>Mis reservas</div>
        </div>
      </Header>

      <div className="no-scrollbar" style={{ flex: 1, overflowY: 'auto' }}>
        <div style={{ maxWidth: 640, margin: '0 auto', padding: '16px 20px 28px' }}>
          <div style={{ padding: '44px 8px', textAlign: 'center' }}>
            <div style={{ font: `700 17px ${F.display}`, marginBottom: 6 }}>
              Todavía no tenés reservas
            </div>
            <div style={{ font: `500 14px ${F.body}`, color: C.muted }}>
              Cuando reserves un turno lo vas a ver en esta lista.
            </div>
          </div>
        </div>
      </div>

      <Footer>
        <button type="button" onClick={restart} style={ctaOn}>
          Reservar un turno
        </button>
      </Footer>
    </Screen>
  );
}
