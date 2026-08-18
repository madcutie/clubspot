import { sportLabel } from '../domain/sport';
import { useClub, useCourtCounts, useDays, useSportCounts } from '../api/queries';
import { Footer, Screen } from '../ui/Screen';
import { C, F, ctaOn, sportCard } from '../ui/theme';
import type { BookingApi } from '../state/useBooking';
import type { Sport } from '../domain/types';

export function HomeScreen({ api }: { api: BookingApi }) {
  const { st, set } = api;
  const isPadel = st.sport === 'padel';

  const club = useClub();
  const days = useDays(st.sport);
  const counts = useSportCounts(st.dateIdx);
  const courtCounts = useCourtCounts();

  const canchas = (sport: Sport) => {
    const n = courtCounts.data?.[sport];
    if (n == null) return '';
    return n === 1 ? '1 cancha' : `${n} canchas`;
  };
  const sportSub = (sport: Sport) => {
    const c = canchas(sport);
    if (counts.data) return c ? `${counts.data[sport]} turnos libres · ${c}` : `${counts.data[sport]} turnos libres`;
    return c;
  };

  const diaLargo = days.data?.find((d) => d.i === st.dateIdx)?.long ?? '';

  return (
    <Screen>
      <div
        style={{
          flex: 'none',
          padding: 'calc(env(safe-area-inset-top) + 20px) 20px 10px',
          display: 'flex',
          alignItems: 'flex-start',
          justifyContent: 'space-between',
          gap: 12,
        }}
      >
        <div style={{ maxWidth: 640 }}>
          <div style={{ font: `700 18px ${F.display}`, letterSpacing: '-.015em' }}>
            {club.data?.nombre ?? ''}
          </div>
          <div style={{ font: `500 13px ${F.body}`, color: C.muted, marginTop: 2 }}>
            {club.data?.direccion ?? ''}
          </div>
        </div>
        <button
          type="button"
          onClick={() => set({ screen: 'mine' })}
          style={{
            flex: 'none',
            minHeight: 44,
            padding: '0 14px',
            borderRadius: 12,
            border: '1px solid #2C312C',
            background: 'transparent',
            color: C.ink,
            font: `600 13px ${F.body}`,
            cursor: 'pointer',
          }}
        >
          Mis reservas
        </button>
      </div>

      <div className="no-scrollbar" style={{ flex: 1, overflowY: 'auto', padding: '6px 0 24px' }}>
        <div style={{ maxWidth: 640, margin: '0 auto', padding: '0 20px' }}>
          <div
            style={{
              font: `800 32px/1.05 ${F.display}`,
              letterSpacing: '-.03em',
              margin: '14px 0 4px',
              textWrap: 'pretty',
            }}
          >
            ¿Cuándo jugás?
          </div>
          <div style={{ font: `500 15px ${F.body}`, color: C.muted, marginBottom: 20 }}>
            Elegí deporte y día para ver los horarios.
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <button
              type="button"
              onClick={() => set({ sport: 'padel', hour: null, courtIdx: null, ctype: 'todas' })}
              style={sportCard(isPadel)}
            >
              <div style={{ font: `700 20px ${F.display}`, letterSpacing: '-.015em' }}>Pádel</div>
              <div style={{ font: `500 12.5px ${F.body}`, color: C.muted, marginTop: 3 }}>
                {sportSub('padel')}
              </div>
            </button>

            <button
              type="button"
              onClick={() => set({ sport: 'futbol', hour: null, courtIdx: null, ctype: 'todas' })}
              style={sportCard(!isPadel)}
            >
              <div style={{ font: `700 20px ${F.display}`, letterSpacing: '-.015em' }}>Fútbol 5</div>
              <div style={{ font: `500 12.5px ${F.body}`, color: C.muted, marginTop: 3 }}>
                {sportSub('futbol')}
              </div>
            </button>
          </div>

          <div
            style={{
              font: `700 11px ${F.body}`,
              color: C.dim,
              letterSpacing: '.1em',
              textTransform: 'uppercase',
              margin: '26px 0 10px',
            }}
          >
            Día
          </div>
        </div>

        <div
          className="no-scrollbar"
          style={{ display: 'flex', gap: 8, overflowX: 'auto', padding: '2px 20px 4px' }}
        >
          {(days.data ?? []).map((d) => {
            const active = st.dateIdx === d.i;
            const lleno = d.free === 0;
            return (
              <button
                key={d.i}
                type="button"
                onClick={() => set({ dateIdx: d.i, hour: null, courtIdx: null })}
                style={{
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  justifyContent: 'center',
                  width: 62,
                  minHeight: 76,
                  borderRadius: 14,
                  flex: 'none',
                  cursor: 'pointer',
                  border: active
                    ? `2px solid ${C.accent}`
                    : lleno
                      ? '1px dashed #2C312C'
                      : '1px solid #2C312C',
                  background: active ? C.accentSoft : lleno ? 'transparent' : C.surface,
                  color: active ? C.accent : lleno ? C.faint : C.text,
                }}
              >
                <span style={{ font: `700 12px ${F.body}`, letterSpacing: '.06em' }}>{d.top}</span>
                <span
                  style={{
                    font: `600 15px ${F.display}`,
                    marginTop: 3,
                    textDecoration: lleno ? 'line-through' : 'none',
                  }}
                >
                  {d.num}
                </span>
                <span style={{ font: `600 10px ${F.body}`, opacity: 0.6, letterSpacing: '.08em' }}>
                  {lleno ? 'LLENO' : d.mon}
                </span>
              </button>
            );
          })}
          {days.isPending &&
            Array.from({ length: 6 }, (_, i) => (
              <div
                key={`sk-${i}`}
                style={{
                  width: 62,
                  minHeight: 76,
                  borderRadius: 14,
                  flex: 'none',
                  background: C.surface,
                  opacity: 0.4,
                }}
              />
            ))}
        </div>

        <div style={{ maxWidth: 640, margin: '0 auto', padding: '20px 20px 0' }}>
          <div
            style={{
              borderRadius: 16,
              border: '1px solid #1D211D',
              background: C.surface,
              padding: '14px 16px',
              display: 'flex',
              gap: 12,
              alignItems: 'center',
            }}
          >
            <div style={{ font: `700 12px ${F.body}`, color: C.accent, letterSpacing: '.06em' }}>PAGO</div>
            <div style={{ font: `500 13px/1.45 ${F.body}`, color: C.soft }}>
              {club.data?.pagoOnline
                ? 'Pagás como prefieras: en el club, online o solo la seña.'
                : 'Reservás online y pagás en el club cuando venís a jugar.'}
            </div>
          </div>
        </div>
      </div>

      <Footer>
        <div style={{ font: `600 12.5px ${F.body}`, color: C.muted, textAlign: 'center' }}>
          {sportLabel(st.sport)}
          {diaLargo ? ` · ${diaLargo}` : ''}
        </div>
        <button type="button" onClick={() => set({ screen: 'avail' })} style={ctaOn}>
          Ver horarios
        </button>
      </Footer>
    </Screen>
  );
}
