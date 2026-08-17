import { sportLabel } from '../domain/sport';
import { fmt } from '../domain/pricing';
import { loadMyBookings } from '../state/myBookings';
import { Footer, Header, Screen } from '../ui/Screen';
import { C, F, ctaOn } from '../ui/theme';
import type { BookingApi } from '../state/useBooking';

export function MyBookingsScreen({ api }: { api: BookingApi }) {
  const { restart } = api;
  const bookings = loadMyBookings();

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
        <div style={{ font: `500 12px ${F.body}`, color: C.dim, marginTop: 6 }}>
          Reservas hechas desde este dispositivo.
        </div>
      </Header>

      <div className="no-scrollbar" style={{ flex: 1, overflowY: 'auto' }}>
        <div style={{ maxWidth: 640, margin: '0 auto', padding: '16px 20px 28px' }}>
          {bookings.length === 0 ? (
            <div style={{ padding: '44px 8px', textAlign: 'center' }}>
              <div style={{ font: `700 17px ${F.display}`, marginBottom: 6 }}>
                Todavía no tenés reservas
              </div>
              <div style={{ font: `500 14px ${F.body}`, color: C.muted }}>
                Cuando reserves un turno lo vas a ver en esta lista.
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {bookings.map((b) => (
                <div
                  key={b.id}
                  style={{
                    borderRadius: 14, border: '1px solid rgba(255,255,255,.10)',
                    background: C.surface, padding: '13px 15px',
                    display: 'flex', alignItems: 'center', gap: 12,
                  }}
                >
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ font: `700 15px ${F.display}` }}>
                      {b.diaLabel} · {b.label}
                    </div>
                    <div style={{ font: `500 12.5px ${F.body}`, color: C.muted, marginTop: 3 }}>
                      {sportLabel(b.sport)} · {b.court}
                    </div>
                  </div>
                  <div style={{ font: `700 14px ${F.body}`, color: C.ink, flex: 'none' }}>
                    {fmt(b.price)}
                  </div>
                </div>
              ))}
            </div>
          )}
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
