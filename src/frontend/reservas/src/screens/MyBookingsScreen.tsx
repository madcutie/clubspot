import { useQueries } from '@tanstack/react-query';
import { ChevronRight } from 'lucide-react';
import { fetchBooking, type BookingSnapshot } from '../api/portalApi';
import { dayLabelOf, hhmm, parseDate } from '../domain/dates';
import { sportLabel } from '../domain/sport';
import { fmt } from '../domain/pricing';
import { estadoDe, momentoCorto } from '../domain/bookingStatus';
import { loadMyBookings } from '../state/myBookings';
import { loadBookingToken } from '../state/bookingTokens';
import { BackTitle, Footer, Header, Screen } from '../ui/Screen';
import { C, F, ctaOn } from '../ui/theme';
import { EstadoChip } from '../ui/EstadoChip';
import type { BookingApi } from '../state/useBooking';

/**
 * La lista de este dispositivo, pero con el estado que dice el servidor: sin login no hay
 * identidad, así que los ids salen de localStorage y cada uno se consulta con su token.
 */
export function MyBookingsScreen({ api }: { api: BookingApi }) {
  const { restart, set } = api;
  const guardadas = loadMyBookings();

  const consultas = useQueries({
    queries: guardadas.map((b) => ({
      queryKey: ['booking', b.id],
      queryFn: () => fetchBooking(b.id, loadBookingToken(b.id)),
      // Una reserva vieja del mismo dispositivo no cambia sola: no hace falta reconsultarla al volver.
      staleTime: 30_000,
      retry: false,
    })),
  });

  const abrir = (id: string) => set({ screen: 'detalle', detalleId: id });

  return (
    <Screen>
      <Header>
        <BackTitle title="Mis reservas" onBack={restart} />
        <div style={{ font: `500 12px ${F.body}`, color: C.dim, marginTop: 6 }}>
          Reservas hechas desde este dispositivo.
        </div>
      </Header>

      <div className="no-scrollbar" style={{ flex: 1, overflowY: 'auto' }}>
        <div style={{ maxWidth: 640, margin: '0 auto', padding: '16px 20px 28px' }}>
          {guardadas.length === 0 ? (
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
              {guardadas.map((guardada, i) => (
                <Fila
                  key={guardada.id}
                  id={guardada.id}
                  respaldo={{ titulo: `${guardada.diaLabel} · ${guardada.label}`, detalle: `${sportLabel(guardada.sport)} · ${guardada.court}`, precio: guardada.price }}
                  datos={consultas[i]?.data}
                  cargando={consultas[i]?.isPending ?? false}
                  onOpen={() => abrir(guardada.id)}
                />
              ))}
            </div>
          )}
        </div>
      </div>

      <Footer>
        <button type="button" onClick={restart} style={ctaOn}>Reservar un turno</button>
      </Footer>
    </Screen>
  );
}

function Fila({
  respaldo, datos, cargando, onOpen,
}: {
  id: string;
  respaldo: { titulo: string; detalle: string; precio: number };
  datos: BookingSnapshot | undefined;
  cargando: boolean;
  onOpen: () => void;
}) {
  // Mientras la consulta viaja —o si falló— se muestra lo guardado al reservar, que no miente
  // sobre el turno; lo único que falta es el estado, y ese no se inventa.
  const titulo = datos
    ? `${dayLabelOf(parseDate(datos.date), true)} · ${hhmm(datos.startMinute)} – ${hhmm(datos.startMinute + datos.durationMinutes)}`
    : respaldo.titulo;
  const detalle = datos
    ? `${sportLabel(datos.sport === 'padel' ? 'padel' : 'futbol')} · ${datos.courtName}`
    : respaldo.detalle;

  return (
    <button
      type="button"
      onClick={onOpen}
      style={{
        width: '100%', textAlign: 'left', cursor: 'pointer',
        borderRadius: 14, border: '1px solid #2C312C', background: C.surface,
        padding: '13px 13px 13px 15px', display: 'flex', alignItems: 'center', gap: 10,
        color: C.ink, font: 'inherit',
      }}
    >
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ font: `700 15px ${F.display}` }}>{titulo}</div>
        <div style={{ font: `500 12.5px ${F.body}`, color: C.muted, marginTop: 3 }}>{detalle}</div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 8, flexWrap: 'wrap' }}>
          {datos && <EstadoChip estado={estadoDe(datos)} />}
          {datos && (
            <span style={{ font: `500 11.5px ${F.body}`, color: C.dim }}>
              Reservada el {momentoCorto(datos.createdAt)}
            </span>
          )}
          {!datos && cargando && (
            <span style={{ font: `500 11.5px ${F.body}`, color: C.dim }}>Actualizando…</span>
          )}
        </div>
      </div>
      <div style={{ textAlign: 'right', flex: 'none' }}>
        <div style={{ font: `700 14px ${F.body}` }}>{fmt(datos ? datos.price : respaldo.precio)}</div>
        {datos && datos.paidAmount > 0 && (
          <div style={{ font: `500 11.5px ${F.body}`, color: C.accent, marginTop: 3 }}>
            {fmt(datos.paidAmount)} pagado
          </div>
        )}
      </div>
      <ChevronRight size={18} strokeWidth={2} aria-hidden style={{ color: C.dim, flex: 'none' }} />
    </button>
  );
}
