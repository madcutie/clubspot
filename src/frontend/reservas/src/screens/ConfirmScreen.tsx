import { sportLabel } from '../domain/sport';
import { durLabel, fmt, senaOf } from '../domain/pricing';
import { useClub } from '../api/queries';
import { BackTitle, Body, Footer, Header, Screen } from '../ui/Screen';
import { C, F, card, ctaOff, divider, input, label, optCard, radio, rowLabel, rowValue } from '../ui/theme';
import type { BookingApi } from '../state/useBooking';

// Provisional: política de cancelación pendiente de definición; la reemplaza la regla real.
const CANCEL_HORAS = 12;

export function ConfirmScreen({ api }: { api: BookingApi }) {
  const { st, set, total } = api;

  const club = useClub();
  const senaPct = club.data?.senaPct ?? 0;
  const sena = senaOf(total, senaPct);
  const saldo = total - sena;

  return (
    <Screen>
      <Header>
        <BackTitle title="Confirmar reserva" onBack={() => set({ screen: 'avail' })} />
      </Header>

      <Body>
        <div style={card}>
          <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 12 }}>
            <div style={{ font: `700 20px ${F.display}`, letterSpacing: '-.02em' }}>
              {sportLabel(st.sport)}
            </div>
            <div style={{ font: `600 12px ${F.body}`, color: C.accent, letterSpacing: '.06em' }}>
              {durLabel(st.dur).toUpperCase()}
            </div>
          </div>
          <div style={divider} />
          <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
            <Row k="Día" v={st.sel?.diaLabel ?? ''} />
            <Row k="Hora" v={st.sel?.label ?? ''} />
            <Row k="Cancha" v={st.sel?.court ?? ''} />
          </div>
          <div style={divider} />
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
            <span style={{ font: `700 14px ${F.body}` }}>Total</span>
            <span style={{ font: `700 22px ${F.display}`, letterSpacing: '-.02em' }}>{fmt(total)}</span>
          </div>
        </div>

        <div style={{ ...label, margin: '26px 0 10px' }}>Tus datos</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <input
            type="text"
            value={st.nombre}
            onChange={(e) => set({ nombre: e.target.value })}
            placeholder="Nombre y apellido"
            autoComplete="name"
            style={input}
          />
          <input
            type="tel"
            value={st.tel}
            onChange={(e) => set({ tel: e.target.value })}
            placeholder="Teléfono / WhatsApp"
            autoComplete="tel"
            style={input}
          />
          <input
            type="email"
            value={st.email}
            onChange={(e) => set({ email: e.target.value })}
            placeholder="Email (opcional)"
            autoComplete="email"
            style={input}
          />
        </div>

        <div style={{ ...label, margin: '26px 0 10px' }}>Forma de pago</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <button type="button" onClick={() => set({ pago: 'total' })} style={optCard(st.pago === 'total')}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <div style={radio(st.pago === 'total')} />
              <div style={{ flex: 1, textAlign: 'left' }}>
                <div style={{ font: `700 15.5px ${F.body}` }}>Pago total online</div>
                <div style={{ font: `500 13px/1.45 ${F.body}`, color: C.soft, marginTop: 3 }}>
                  Abonás el 100% y la cancha queda confirmada.
                </div>
              </div>
              <div style={{ font: `700 16px ${F.display}` }}>{fmt(total)}</div>
            </div>
          </button>

          <button type="button" onClick={() => set({ pago: 'sena' })} style={optCard(st.pago === 'sena')}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <div style={radio(st.pago === 'sena')} />
              <div style={{ flex: 1, textAlign: 'left' }}>
                <div style={{ font: `700 15.5px ${F.body}` }}>Seña online + resto en el club</div>
                <div style={{ font: `500 13px/1.45 ${F.body}`, color: C.soft, marginTop: 3 }}>
                  Pagás el {senaPct}% para confirmar el turno.
                </div>
              </div>
              <div style={{ font: `700 16px ${F.display}` }}>{fmt(sena)}</div>
            </div>
            <div
              style={{
                marginTop: 12, padding: '10px 12px', borderRadius: 11,
                background: 'rgba(255,201,74,.10)', border: '1px solid rgba(255,201,74,.28)',
                font: `700 13px ${F.body}`, color: C.accent, textAlign: 'left',
              }}
            >
              Saldo a pagar en el club: {fmt(saldo)}
            </div>
          </button>
        </div>

        <div style={{ font: `500 12px/1.5 ${F.body}`, color: C.dim, marginTop: 16 }}>
          Cancelación sin cargo hasta {CANCEL_HORAS} h antes del turno. Después de ese plazo la
          seña no se devuelve.
        </div>
      </Body>

      <Footer>
        {/* Provisional: la reserva online queda gateada hasta que existan hold+TTL y pagos (F2R decisión 7). */}
        <div
          role="status"
          style={{
            padding: '12px 14px', borderRadius: 12,
            background: 'rgba(255,201,74,.10)', border: '1px solid rgba(255,201,74,.28)',
            font: `500 13px/1.5 ${F.body}`, color: '#E9D7AE', textAlign: 'center',
          }}
        >
          La reserva online todavía no está habilitada. Para asegurar este turno, comunicate con
          el club.
        </div>
        <button type="button" disabled style={ctaOff}>
          Reservar online — próximamente
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
