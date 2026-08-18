import { useEffect, useRef } from 'react';
import { CalendarX, Check, Hourglass } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { fetchBooking, invalidateAvailability, type BookingSnapshot } from '../api/portalApi';
import { dayLabelOf, hhmm, parseDate } from '../domain/dates';
import { sportLabel } from '../domain/sport';
import { fmt } from '../domain/pricing';
import { loadMyBookings, saveMyBooking } from '../state/myBookings';
import { Body, Footer, Header, Screen } from '../ui/Screen';
import { C, F, card, ctaOn, divider, rowLabel, rowValue } from '../ui/theme';
import type { BookingApi } from '../state/useBooking';
import type { Sport } from '../domain/types';

const SPORT_OF: Record<BookingSnapshot['sport'], Sport> = { padel: 'padel', football: 'futbol' };

/** Vuelta del checkout online: espera el webhook y muestra cómo terminó. */
export function ReturnScreen({ api }: { api: BookingApi }) {
  const { st, restart } = api;
  const id = st.retornoId;

  const q = useQuery({
    queryKey: ['booking', id],
    queryFn: () => fetchBooking(id!),
    enabled: id != null,
    refetchInterval: (query) =>
      query.state.data?.status === 'pendingPayment' ? 2500 : false,
  });

  const b = q.data;
  const holdVencido =
    b?.status === 'expired' ||
    (b?.status === 'pendingPayment' && b.expiresAt != null && new Date(b.expiresAt) < new Date());
  const confirmada = b?.status === 'confirmed';
  const saldo = b ? b.price - b.paidAmount : 0;

  const saved = useRef(false);
  useEffect(() => {
    if (!confirmada || !b || saved.current) return;
    saved.current = true;
    if (!loadMyBookings().some((x) => x.id === b.id)) {
      saveMyBooking({
        id: b.id,
        sport: SPORT_OF[b.sport],
        court: b.courtName,
        date: b.date,
        label: `${hhmm(b.startMinute)} – ${hhmm(b.startMinute + b.durationMinutes)}`,
        diaLabel: dayLabelOf(parseDate(b.date), true),
        price: b.price,
        nombre: '',
      });
    }
    void invalidateAvailability();
  }, [confirmada, b]);

  return (
    <Screen>
      <Header>
        <div style={{ font: `700 17px ${F.display}`, letterSpacing: '-.01em' }}>
          {confirmada ? 'Reserva confirmada' : holdVencido ? 'Turno liberado' : 'Procesando el pago'}
        </div>
      </Header>

      <Body>
        {b == null ? (
          <div style={{ padding: '44px 8px', textAlign: 'center', font: `500 14px ${F.body}`, color: C.muted }}>
            {q.isError ? 'No encontramos esa reserva.' : 'Buscando tu reserva…'}
          </div>
        ) : (
          <>
            <div style={{ textAlign: 'center', padding: '26px 0 18px' }}>
              <div
                aria-hidden
                style={{
                  width: 56, height: 56, margin: '0 auto 14px', borderRadius: 18,
                  border: `2px solid ${confirmada ? C.accent : '#3A403A'}`,
                  color: confirmada ? C.accent : C.muted,
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                }}
              >
                {confirmada ? (
                  <Check size={28} strokeWidth={2.5} />
                ) : holdVencido ? (
                  <CalendarX size={26} strokeWidth={2} />
                ) : (
                  <Hourglass size={26} strokeWidth={2} />
                )}
              </div>
              <div style={{ font: `800 24px ${F.display}`, letterSpacing: '-.02em' }}>
                {confirmada
                  ? '¡Pago acreditado!'
                  : holdVencido
                    ? 'El pago no se acreditó'
                    : 'Esperando la confirmación'}
              </div>
              <div style={{ font: `500 14px/1.5 ${F.body}`, color: C.soft, marginTop: 6 }}>
                {confirmada
                  ? 'Tu cancha queda reservada a tu nombre.'
                  : holdVencido
                    ? 'El turno se liberó y volvió a estar disponible. Si te cobraron, comunicate con el club.'
                    : 'En cuanto el pago se acredite, la reserva queda confirmada sola.'}
              </div>
            </div>

            <div style={card}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
                <Row k="Deporte" v={sportLabel(SPORT_OF[b.sport])} />
                <Row k="Día" v={dayLabelOf(parseDate(b.date), true)} />
                <Row k="Hora" v={`${hhmm(b.startMinute)} – ${hhmm(b.startMinute + b.durationMinutes)}`} />
                <Row k="Cancha" v={b.courtName} />
              </div>
              <div style={divider} />
              <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
                <Row k="Total" v={fmt(b.price)} />
                <Row k="Pagado online" v={fmt(b.paidAmount)} />
                {confirmada && saldo > 0 && <Row k="Saldo en el club" v={fmt(saldo)} />}
              </div>
            </div>
          </>
        )}
      </Body>

      <Footer>
        <button type="button" onClick={restart} style={ctaOn}>
          {holdVencido ? 'Buscar otro horario' : 'Volver al inicio'}
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
