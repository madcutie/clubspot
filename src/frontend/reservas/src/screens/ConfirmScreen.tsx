import { useState } from 'react';
import { sportLabel } from '../domain/sport';
import { durLabel, fmt } from '../domain/pricing';
import { ApiError, createBooking, invalidateAvailability, type ApiPaymentMode } from '../api/portalApi';
import { useClub } from '../api/queries';
import { saveMyBooking } from '../state/myBookings';
import { saveBookingToken } from '../state/bookingTokens';
import { BackTitle, Body, Footer, Header, Screen } from '../ui/Screen';
import { C, F, card, ctaOff, ctaOn, divider, input, label, optCard, radio, rowLabel, rowValue } from '../ui/theme';
import type { BookingApi } from '../state/useBooking';
import type { PayMode } from '../domain/types';

const API_MODE: Record<PayMode, ApiPaymentMode> = {
  club: 'club',
  total: 'onlineFull',
  sena: 'onlineDeposit',
};

export function ConfirmScreen({ api }: { api: BookingApi }) {
  const { st, set, total } = api;

  const club = useClub();
  const pagoOnline = club.data?.pagoOnline ?? false;
  const senaPct = club.data?.senaPct ?? 0;
  const sena = Math.round((total * senaPct) / 100);
  const saldoSena = total - sena;

  const [sending, setSending] = useState(false);
  const [slotTaken, setSlotTaken] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const ready = st.nombre.trim().length > 0 && st.tel.trim().length > 0;
  // Con gateway configurado el turno sólo se toma contra un pago online (seña o total): el servidor
  // rechaza el modo "club" desde el portal, así que la UI tampoco lo ofrece.
  const pago: PayMode = pagoOnline ? (st.pago === 'club' ? 'total' : st.pago) : 'club';

  const confirm = async () => {
    if (!st.sel || sending) return;
    setSending(true);
    setError(null);
    try {
      const created = await createBooking({
        courtId: st.sel.courtId,
        date: st.sel.date,
        startMinute: st.sel.startMinute,
        durationMinutes: st.sel.dur,
        customerName: st.nombre.trim(),
        customerPhone: st.tel.trim(),
        customerEmail: st.email.trim() || null,
        paymentMode: API_MODE[pago],
        returnUrl: pago === 'club' ? null : window.location.origin + window.location.pathname,
      });
      // Antes de cualquier redirección: a la vuelta del checkout sólo tenemos el id de la URL,
      // y sin el token el servidor no nos deja ni mirar la reserva.
      saveBookingToken(created.id, created.token);
      if (created.checkoutUrl) {
        // El hold quedó tomado; el resto pasa en el checkout y en la pantalla de retorno.
        window.location.href = created.checkoutUrl;
        return;
      }
      const done = {
        id: created.id,
        sport: st.sport,
        court: st.sel.court,
        date: st.sel.date,
        label: st.sel.label,
        diaLabel: st.sel.diaLabel,
        price: created.price,
        nombre: st.nombre.trim(),
      };
      saveMyBooking(done);
      void invalidateAvailability();
      set({ screen: 'done', done });
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) {
        setSlotTaken(true);
        void invalidateAvailability();
      } else {
        setError('No se pudo confirmar la reserva. Probá de nuevo en un momento.');
      }
    } finally {
      setSending(false);
    }
  };

  const backToAvailability = () => {
    set({ screen: 'avail', hour: null, courtIdx: null, sel: null });
  };

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

        {pagoOnline ? (
          <>
            <div style={{ ...label, margin: '26px 0 10px' }}>Forma de pago</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <PayOption
                active={pago === 'total'}
                onClick={() => set({ pago: 'total' })}
                title="Pago total online"
                detail="Abonás el 100% y la cancha queda confirmada."
                amount={fmt(total)}
              />
              <PayOption
                active={pago === 'sena'}
                onClick={() => set({ pago: 'sena' })}
                title="Seña online + resto en el club"
                detail={`Pagás el ${senaPct}% para confirmar el turno.`}
                amount={fmt(sena)}
                note={`Saldo a pagar en el club: ${fmt(saldoSena)}`}
              />
            </div>
          </>
        ) : (
          <div
            style={{
              marginTop: 18, padding: '12px 14px', borderRadius: 12,
              background: C.surface, border: '1px solid #2C312C',
              font: `500 13px/1.5 ${F.body}`, color: C.soft,
            }}
          >
            <span style={{ font: `700 13px ${F.body}`, color: C.ink }}>Pagás en el club.</span>{' '}
            El turno queda confirmado a tu nombre y abonás {fmt(total)} cuando venís a jugar.
          </div>
        )}

        {/* Regla del club (17/08/2026): sin cargo hasta 24 h antes; después se cobra el 50%. */}
        <div style={{ font: `500 12px/1.5 ${F.body}`, color: C.dim, marginTop: 16 }}>
          Cancelación sin cargo hasta 24 h antes del turno. Con menos de 24 h se cobra el 50%
          del valor{senaPct === 50 ? ' (la seña no se devuelve)' : ''}.
        </div>
      </Body>

      <Footer>
        {slotTaken && (
          <div
            role="alert"
            style={{
              padding: '12px 14px', borderRadius: 12,
              background: 'rgba(205,232,74,.10)', border: '1px solid rgba(205,232,74,.28)',
              font: `500 13px/1.5 ${F.body}`, color: '#DCE6B4', textAlign: 'center',
            }}
          >
            Ese turno se acaba de ocupar. Elegí otro horario.
          </div>
        )}
        {error && !slotTaken && (
          <div
            role="alert"
            style={{
              padding: '12px 14px', borderRadius: 12,
              background: 'rgba(205,232,74,.10)', border: '1px solid rgba(205,232,74,.28)',
              font: `500 13px/1.5 ${F.body}`, color: '#DCE6B4', textAlign: 'center',
            }}
          >
            {error}
          </div>
        )}
        {slotTaken ? (
          <button type="button" onClick={backToAvailability} style={ctaOn}>
            Ver horarios disponibles
          </button>
        ) : (
          <button
            type="button"
            disabled={!ready || sending}
            onClick={confirm}
            style={ready && !sending ? ctaOn : ctaOff}
          >
            {sending
              ? 'Confirmando…'
              : !ready
                ? 'Completá tus datos'
                : pago === 'club'
                  ? 'Confirmar reserva'
                  : `Continuar al pago — ${fmt(pago === 'sena' ? sena : total)}`}
          </button>
        )}
      </Footer>
    </Screen>
  );
}

function PayOption({ active, onClick, title, detail, amount, note }: {
  active: boolean;
  onClick: () => void;
  title: string;
  detail: string;
  amount: string;
  note?: string;
}) {
  return (
    <button type="button" onClick={onClick} style={optCard(active)}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={radio(active)} />
        <div style={{ flex: 1, textAlign: 'left' }}>
          <div style={{ font: `700 15.5px ${F.body}` }}>{title}</div>
          <div style={{ font: `500 13px/1.45 ${F.body}`, color: C.soft, marginTop: 3 }}>{detail}</div>
        </div>
        <div style={{ font: `700 16px ${F.display}` }}>{amount}</div>
      </div>
      {note && (
        <div
          style={{
            marginTop: 12, padding: '10px 12px', borderRadius: 11,
            background: 'rgba(205,232,74,.10)', border: '1px solid rgba(205,232,74,.28)',
            font: `700 13px ${F.body}`, color: C.accent, textAlign: 'left',
          }}
        >
          {note}
        </div>
      )}
    </button>
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
