import { CLUB, sportLabel } from '../domain/catalog';
import { dayLabel } from '../domain/dates';
import { fmt } from '../domain/pricing';
import { usePayReservation } from '../api/queries';
import { PaymentRejectedError } from '../api/mockApi';
import { BackTitle, Body, Footer, Header, Screen } from '../ui/Screen';
import { C, F, ctaOn, label, optCard, radio } from '../ui/theme';
import type { BookingApi } from '../state/useBooking';
import type { PayMethod } from '../domain/types';

const METHODS: { k: PayMethod; label: string; sub: string }[] = [
  { k: 'mp', label: 'Mercado Pago', sub: 'Dinero en cuenta o tarjetas guardadas' },
  { k: 'tarjeta', label: 'Tarjeta de crédito o débito', sub: 'Visa, Mastercard, Cabal' },
  { k: 'transfer', label: 'Transferencia', sub: 'CBU del club · acreditación inmediata' },
];

export function PayScreen({ api }: { api: BookingApi }) {
  const { st, set, payAmount } = api;
  const pay = usePayReservation();

  const rejected = pay.error instanceof PaymentRejectedError;

  function onPay() {
    if (!st.sel) return;
    pay.mutate(
      {
        sel: st.sel,
        sport: st.sport,
        dateIdx: st.dateIdx,
        pago: st.pago,
        method: st.method,
        tel: st.tel,
        attempt: st.tries,
      },
      {
        onSuccess: (res) => set({ screen: 'done', code: res.code }),
        onError: () => set({ tries: st.tries + 1 }),
      },
    );
  }

  return (
    <Screen>
      <Header>
        <BackTitle title="Pago" onBack={() => set({ screen: 'confirm' })} />
      </Header>

      <Body>
        <div
          style={{
            borderRadius: 16, background: C.surface, border: '1px solid rgba(255,255,255,.08)',
            padding: '16px 18px',
          }}
        >
          <div
            style={{
              font: `600 11.5px ${F.body}`, color: C.muted, letterSpacing: '.1em',
              textTransform: 'uppercase',
            }}
          >
            {st.pago === 'total' ? 'Pago total' : `Seña ${CLUB.senaPct}%`}
          </div>
          <div style={{ font: `700 34px ${F.display}`, letterSpacing: '-.03em', margin: '6px 0 8px' }}>
            {fmt(payAmount)}
          </div>
          <div style={{ font: `500 13.5px ${F.body}`, color: C.soft }}>
            {sportLabel(st.sport)} · {dayLabel(st.dateIdx, false)} · {st.sel?.label ?? ''} ·{' '}
            {st.sel?.court ?? ''}
          </div>
        </div>

        {rejected && (
          <div
            role="alert"
            style={{
              marginTop: 14, borderRadius: 14, border: '1px solid rgba(255,255,255,.22)',
              background: '#1E1A14', padding: '14px 16px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <div
                style={{
                  width: 22, height: 22, borderRadius: '50%', border: `2px solid ${C.accent}`,
                  color: C.accent, display: 'flex', alignItems: 'center', justifyContent: 'center',
                  font: `700 13px ${F.body}`,
                }}
              >
                !
              </div>
              <div style={{ font: `700 14.5px ${F.body}` }}>Pago rechazado</div>
            </div>
            <div style={{ font: `500 13px/1.5 ${F.body}`, color: C.soft, marginTop: 8 }}>
              La tarjeta fue rechazada por el banco (fondos insuficientes). Tus datos quedaron
              cargados: probá otro medio de pago o reintentá.
            </div>
          </div>
        )}

        <div style={{ ...label, margin: '24px 0 10px' }}>Medio de pago</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {METHODS.map((m) => (
            <button
              key={m.k}
              type="button"
              onClick={() => set({ method: m.k })}
              style={{
                ...optCard(st.method === m.k),
                display: 'flex', alignItems: 'center', gap: 12, minHeight: 66, cursor: 'pointer',
              }}
            >
              <div style={radio(st.method === m.k)} />
              <div style={{ flex: 1, textAlign: 'left' }}>
                <div style={{ font: `700 15.5px ${F.body}` }}>{m.label}</div>
                <div style={{ font: `500 12.5px ${F.body}`, color: C.soft, marginTop: 2 }}>{m.sub}</div>
              </div>
            </button>
          ))}
        </div>

        <div style={{ font: `500 12px/1.5 ${F.body}`, color: C.dim, marginTop: 16 }}>
          Pago protegido. No guardamos los datos de tu tarjeta.
        </div>
      </Body>

      <Footer>
        <button type="button" onClick={onPay} disabled={pay.isPending} style={ctaOn}>
          {rejected ? `Reintentar ${fmt(payAmount)}` : `Pagar ${fmt(payAmount)}`}
        </button>
      </Footer>

      {pay.isPending && (
        <div
          style={{
            position: 'absolute', inset: 0, background: 'rgba(8,9,11,.86)',
            display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
            gap: 16, zIndex: 10,
          }}
        >
          <div
            className="spin"
            style={{
              width: 42, height: 42, borderRadius: '50%',
              border: '3px solid rgba(255,255,255,.14)', borderTopColor: C.accent,
            }}
          />
          <div style={{ font: `600 14.5px ${F.body}`, color: C.ink }}>Procesando el pago…</div>
          <div style={{ font: `500 12.5px ${F.body}`, color: C.muted }}>No cierres esta pantalla</div>
        </div>
      )}
    </Screen>
  );
}
