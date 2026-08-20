import { useQuery } from '@tanstack/react-query';
import { CreditCard, Receipt } from 'lucide-react';
import { fetchBooking } from '../api/portalApi';
import { loadBookingToken } from '../state/bookingTokens';
import { dayLabelOf, hhmm, parseDate } from '../domain/dates';
import { sportLabel } from '../domain/sport';
import { fmt } from '../domain/pricing';
import {
  ETIQUETA_CONCEPTO, ETIQUETA_PAGO, estadoDe, momento, proveedorLabel,
} from '../domain/bookingStatus';
import { BackTitle, Body, Footer, Screen } from '../ui/Screen';
import { C, F, card, ctaOn, divider, rowLabel, rowValue } from '../ui/theme';
import { EstadoChip } from '../ui/EstadoChip';
import type { BookingApi } from '../state/useBooking';

/** Detalle de una reserva: qué se reservó, qué se pagó y qué informó el proveedor de cada intento. */
export function BookingDetailScreen({ api }: { api: BookingApi }) {
  const { st, set } = api;
  const id = st.detalleId;
  const token = id != null ? loadBookingToken(id) : null;

  const q = useQuery({
    queryKey: ['booking', id],
    queryFn: () => fetchBooking(id!, token),
    enabled: id != null,
  });

  const volver = () => set({ screen: 'mine', detalleId: null });
  const b = q.data;

  return (
    <Screen>
      <BackTitle title="Detalle de la reserva" onBack={volver} />
      <Body>
        {q.isPending && <Estado texto="Buscando la reserva…" />}
        {q.isError && (
          <Estado texto="No pudimos traer esta reserva. Puede que el link ya no sea válido desde este dispositivo." />
        )}

        {b && (
          <>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 14 }}>
              <EstadoChip estado={estadoDe(b)} />
              <div style={{ font: `500 12.5px ${F.body}`, color: C.dim }}>
                Reservada el {momento(b.createdAt)}
              </div>
            </div>

            <div style={card}>
              <Row k="Deporte" v={sportLabel(b.sport === 'padel' ? 'padel' : 'futbol')} />
              <Row k="Día" v={dayLabelOf(parseDate(b.date), true)} />
              <Row k="Hora" v={`${hhmm(b.startMinute)} – ${hhmm(b.startMinute + b.durationMinutes)}`} />
              <Row k="Cancha" v={b.courtName} />
              <div style={divider} />
              <Row k="Total del turno" v={fmt(b.price)} />
              <Row k="Pagado" v={fmt(b.paidAmount)} />
              <Row k="Saldo" v={fmt(b.price - b.paidAmount)} destacado />
            </div>

            <div style={{ font: `700 14px ${F.display}`, margin: '20px 0 10px', display: 'flex', alignItems: 'center', gap: 7 }}>
              <Receipt size={15} strokeWidth={2} aria-hidden />
              Movimientos
            </div>

            {b.payments.length === 0 ? (
              <div style={{ ...card, font: `500 13.5px ${F.body}`, color: C.muted }}>
                Todavía no hay ningún pago registrado para esta reserva.
                {estadoDe(b) === 'esperando' && ' Si ya pagaste, puede tardar unos minutos en acreditarse.'}
              </div>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {b.payments.map((p) => (
                  <div key={`${p.provider}-${p.externalId}`} style={card}>
                    <div style={{ display: 'flex', alignItems: 'baseline', gap: 10 }}>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ font: `700 14.5px ${F.display}` }}>
                          {ETIQUETA_CONCEPTO[p.kind] ?? p.kind}
                        </div>
                        <div style={{ font: `500 12.5px ${F.body}`, color: C.muted, marginTop: 3 }}>
                          {ETIQUETA_PAGO[p.status] ?? p.status}
                        </div>
                      </div>
                      <div style={{ font: `700 15px ${F.body}`, flex: 'none' }}>{fmt(p.amount)}</div>
                    </div>
                    <div style={divider} />
                    <Row k="Fecha" v={momento(p.at)} />
                    <Row k="Medio" v={proveedorLabel(p.provider)} />
                    <Row k="Nº de operación" v={p.externalId} mono />
                    <Row k="Moneda" v={p.currency} />
                  </div>
                ))}
              </div>
            )}

            <div style={{ font: `500 12px ${F.body}`, color: C.dim, marginTop: 14, display: 'flex', gap: 7 }}>
              <CreditCard size={14} strokeWidth={2} aria-hidden style={{ flex: 'none', marginTop: 1 }} />
              <span>
                El número de operación es el que te da {proveedorLabel(b.payments[0]?.provider ?? 'mercadopago')}.
                Tenelo a mano si necesitás hablar con el club.
              </span>
            </div>
          </>
        )}
      </Body>

      <Footer>
        <button type="button" onClick={volver} style={ctaOn}>Volver a mis reservas</button>
      </Footer>
    </Screen>
  );
}

function Estado({ texto }: { texto: string }) {
  return (
    <div style={{ padding: '40px 8px', textAlign: 'center', font: `500 14px ${F.body}`, color: C.muted }}>
      {texto}
    </div>
  );
}

function Row({ k, v, destacado, mono }: { k: string; v: string; destacado?: boolean; mono?: boolean }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 16, padding: '4px 0' }}>
      <div style={rowLabel}>{k}</div>
      <div style={{ ...rowValue, ...(destacado ? { color: C.accent } : {}), ...(mono ? { font: '600 13px ui-monospace, SFMono-Regular, Menlo, monospace' } : {}) }}>
        {v}
      </div>
    </div>
  );
}
