import { fmt } from '../domain/pricing';
import { useBookings, useCancelBooking } from '../api/queries';
import { Footer, Header, Screen } from '../ui/Screen';
import { C, F, ctaOn } from '../ui/theme';
import type { BookingApi } from '../state/useBooking';

export function MyBookingsScreen({ api }: { api: BookingApi }) {
  const { st, set, restart } = api;
  const q = useBookings();
  const cancel = useCancelBooking();

  const reservas = (q.data ?? []).filter((r) => (st.tab === 'prox' ? !r.past : r.past));

  const tabStyle = (on: boolean) => ({
    border: 'none',
    background: 'transparent',
    cursor: 'pointer',
    padding: '0 0 10px',
    font: `700 14.5px ${F.body}`,
    color: on ? C.accent : C.muted,
    borderBottom: on ? `2px solid ${C.accent}` : '2px solid transparent',
  });

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
        <div style={{ display: 'flex', gap: 22, marginTop: 10 }}>
          <button type="button" onClick={() => set({ tab: 'prox' })} style={tabStyle(st.tab === 'prox')}>
            Próximas
          </button>
          <button type="button" onClick={() => set({ tab: 'ant' })} style={tabStyle(st.tab === 'ant')}>
            Anteriores
          </button>
        </div>
      </Header>

      <div className="no-scrollbar" style={{ flex: 1, overflowY: 'auto' }}>
        <div
          style={{
            maxWidth: 640, margin: '0 auto', padding: '16px 20px 28px',
            display: 'flex', flexDirection: 'column', gap: 10,
          }}
        >
          {reservas.map((r) => (
            <div
              key={r.id}
              style={{
                borderRadius: 16, border: '1px solid rgba(255,255,255,.09)',
                background: C.surface, padding: '16px 18px',
                opacity: cancel.isPending && cancel.variables === r.id ? 0.5 : 1,
              }}
            >
              <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 12 }}>
                <div style={{ font: `700 16.5px ${F.display}`, letterSpacing: '-.01em' }}>{r.sport}</div>
                <div style={{ font: `600 12px ${F.body}`, color: C.muted }}>{r.id}</div>
              </div>
              <div style={{ font: `600 14.5px ${F.body}`, marginTop: 6 }}>{r.when}</div>
              <div style={{ font: `500 13px ${F.body}`, color: C.muted, marginTop: 2 }}>{r.court}</div>
              <div style={{ height: 1, background: C.line, margin: '12px 0' }} />
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
                <div
                  style={{
                    font: `700 12.5px ${F.body}`, padding: '5px 9px', borderRadius: 8,
                    border: `1px solid ${r.pay === 'total' ? 'rgba(255,255,255,.18)' : 'rgba(255,201,74,.5)'}`,
                    color: r.pay === 'total' ? C.text : C.accent,
                  }}
                >
                  {r.pay === 'total' ? 'Pagado' : `Seña pagada — saldo ${fmt(r.saldo)}`}
                </div>
                {st.tab === 'prox' && (
                  <button
                    type="button"
                    onClick={() => cancel.mutate(r.id)}
                    disabled={cancel.isPending}
                    style={{
                      minHeight: 44, padding: '0 14px', borderRadius: 11,
                      border: '1px solid rgba(255,255,255,.16)', background: 'transparent',
                      color: C.ink, font: `600 13px ${F.body}`, cursor: 'pointer',
                    }}
                  >
                    Cancelar
                  </button>
                )}
              </div>
            </div>
          ))}

          {!q.isPending && reservas.length === 0 && (
            <div style={{ padding: '44px 8px', textAlign: 'center' }}>
              <div style={{ font: `700 17px ${F.display}`, marginBottom: 6 }}>Nada por acá</div>
              <div style={{ font: `500 14px ${F.body}`, color: C.muted }}>
                Cuando reserves un turno lo vas a ver en esta lista.
              </div>
            </div>
          )}

          {q.isPending &&
            Array.from({ length: 2 }, (_, i) => (
              <div
                key={`sk-${i}`}
                style={{ height: 138, borderRadius: 16, background: C.surface, opacity: 0.4 }}
              />
            ))}
        </div>
      </div>

      <Footer>
        <button type="button" onClick={restart} style={ctaOn}>
          Reservar otro turno
        </button>
      </Footer>
    </Screen>
  );
}
