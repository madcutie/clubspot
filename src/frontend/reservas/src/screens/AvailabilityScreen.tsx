import { sportLabel } from '../domain/sport';
import { hhmm } from '../domain/dates';
import { durLabel, fmt } from '../domain/pricing';
import { useAvailability } from '../api/queries';
import { Body, Footer, Header, Screen } from '../ui/Screen';
import { C, F, chip, ctaOff, ctaOn, stepNum, stepTitle } from '../ui/theme';
import type { BookingApi } from '../state/useBooking';
import type { CourtFilter } from '../domain/types';

export function AvailabilityScreen({ api }: { api: BookingApi }) {
  const { st, set } = api;
  const isPadel = st.sport === 'padel';

  const q = useAvailability({
    sport: st.sport,
    dateIdx: st.dateIdx,
    dur: st.dur,
    ctype: st.ctype,
    hour: st.hour,
  });

  const durations = q.data?.durations.length ? q.data.durations : [60, 90, 120];
  const hours = q.data?.hours ?? [];
  const anyFree = q.data?.anyFree ?? false;
  const courts = q.data?.courts ?? [];
  const nFree = q.data?.freeCourts ?? 0;
  const suggestions = q.data?.suggestions ?? [];
  const hasHour = st.hour != null && anyFree;

  const picked = courts.find((c) => c.i === st.courtIdx && c.free) ?? null;
  const sel =
    picked && picked.price != null && st.hour != null && q.data?.date
      ? {
          key: `${st.sport}-${st.dateIdx}-${st.hour}-${picked.i}-${st.dur}`,
          courtId: picked.id,
          court: `${picked.n} · ${picked.d}`,
          date: q.data.date,
          startMinute: st.hour,
          dur: st.dur,
          label: `${hhmm(st.hour)} – ${hhmm(st.hour + st.dur)}`,
          diaLabel: q.data?.dayLong ?? '',
          price: picked.price,
        }
      : null;

  const types: { k: CourtFilter; l: string }[] = [
    { k: 'todas', l: 'Todas' },
    { k: 'techada', l: isPadel ? 'Techadas' : 'Techado' },
    { k: 'descubierta', l: isPadel ? 'Descubiertas' : 'Aire libre' },
  ];

  const recap = sel ? `${sel.label} · ${sel.court}` : hasHour ? 'Elegí una cancha' : 'Elegí un horario';

  return (
    <Screen>
      <Header>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <button
            type="button"
            onClick={() => set({ screen: 'home' })}
            aria-label="Volver"
            style={{
              width: 44, height: 44, marginLeft: -10, border: 'none', background: 'transparent',
              color: C.ink, font: `400 22px ${F.body}`, cursor: 'pointer',
            }}
          >
            ←
          </button>
          <div style={{ font: `700 17px ${F.display}`, letterSpacing: '-.01em' }}>Horarios disponibles</div>
        </div>
        <div style={{ display: 'flex', gap: 8, marginTop: 8, flexWrap: 'wrap' }}>
          {[sportLabel(st.sport), q.data?.dayShort ?? '…'].map((l) => (
            <button
              key={l}
              type="button"
              onClick={() => set({ screen: 'home' })}
              style={{
                minHeight: 44, padding: '0 14px', borderRadius: 12,
                border: '1px solid rgba(255,255,255,.14)', background: C.surface, color: C.ink,
                font: `600 13.5px ${F.body}`, cursor: 'pointer',
                display: 'flex', alignItems: 'center', gap: 8,
              }}
            >
              {l}
              <span style={{ color: C.accent, fontSize: 11 }}>CAMBIAR</span>
            </button>
          ))}
        </div>
      </Header>

      <Body style={{ padding: '4px 20px 24px' }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, margin: '18px 0 10px' }}>
          <span style={stepNum}>1</span>
          <span style={stepTitle}>Duración</span>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          {durations.map((d) => (
            <button
              key={d}
              type="button"
              onClick={() => set({ dur: d, hour: null, courtIdx: null })}
              style={chip(st.dur === d)}
            >
              {durLabel(d)}
            </button>
          ))}
        </div>
        {st.dur > 60 && (
          <div
            style={{
              marginTop: 10, padding: '11px 13px', borderRadius: 12,
              background: 'rgba(255,201,74,.09)', border: '1px solid rgba(255,201,74,.26)',
              font: `500 12.5px/1.45 ${F.body}`, color: '#E9D7AE',
            }}
          >
            Bloque de {durLabel(st.dur)} seguidas en la misma cancha. Solo se habilitan las canchas
            libres todo el bloque.
          </div>
        )}

        <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, margin: '24px 0 4px' }}>
          <span style={stepNum}>2</span>
          <span style={stepTitle}>Horario de inicio</span>
        </div>
        <div style={{ font: `500 12px ${F.body}`, color: C.dim, marginBottom: 10 }}>
          {q.isPending
            ? 'Buscando horarios…'
            : anyFree
              ? `Arranques con al menos una cancha libre para ${durLabel(st.dur)}.`
              : 'Sin horarios libres ese día.'}
        </div>
        {anyFree && (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, opacity: q.isFetching ? 0.6 : 1 }}>
            {hours.map((o) => {
              const on = o.free > 0;
              const act = st.hour === o.h;
              // Un bloque largo se come los arranques siguientes: los marcamos
              // para que se vea hasta dónde llega el turno.
              const covered = st.hour != null && o.h > st.hour && o.h < st.hour + st.dur;
              return (
                <button
                  key={o.h}
                  type="button"
                  disabled={!on}
                  onClick={() => set({ hour: o.h, courtIdx: null })}
                  aria-label={covered ? `${o.label}, dentro del turno elegido` : o.label}
                  style={{
                    width: 70, minHeight: 48, borderRadius: 13, flex: 'none',
                    cursor: on ? 'pointer' : 'default',
                    font: `600 15px ${F.display}`,
                    border: act
                      ? `2px solid ${C.accent}`
                      : covered
                        ? '1px solid rgba(255,201,74,.42)'
                        : on
                          ? `1px solid ${C.border}`
                          : '1px dashed rgba(255,255,255,.12)',
                    background: act
                      ? C.accentSoft
                      : covered
                        ? 'rgba(255,201,74,.06)'
                        : on
                          ? C.surface
                          : 'transparent',
                    color: act ? C.accent : covered ? '#E9D7AE' : on ? C.ink : C.faint,
                    textDecoration: on || covered ? 'none' : 'line-through',
                  }}
                >
                  {o.label}
                </button>
              );
            })}
          </div>
        )}

        {hasHour && (
          <>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, margin: '26px 0 4px' }}>
              <span style={stepNum}>3</span>
              <span style={stepTitle}>Cancha</span>
            </div>
            <div style={{ font: `500 12px ${F.body}`, color: C.dim, marginBottom: 10 }}>
              {nFree} {nFree === 1 ? 'cancha libre' : 'canchas libres'} de {hhmm(st.hour!)} a{' '}
              {hhmm(st.hour! + st.dur)}
            </div>
            <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
              {types.map((t) => (
                <button
                  key={t.k}
                  type="button"
                  onClick={() => set({ ctype: t.k, courtIdx: null })}
                  style={chip(st.ctype === t.k)}
                >
                  {t.l}
                </button>
              ))}
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {courts.map((c) => {
                const act = st.courtIdx === c.i && c.free;
                const last = c.free && nFree === 1;
                return (
                  <button
                    key={c.i}
                    type="button"
                    disabled={!c.free}
                    onClick={() => set({ courtIdx: c.i })}
                    style={{
                      display: 'flex', alignItems: 'center', gap: 12, width: '100%', minHeight: 68,
                      padding: act ? '11px 13px' : '12px 14px', borderRadius: 14,
                      border: act
                        ? `2px solid ${C.accent}`
                        : c.free
                          ? '1px solid rgba(255,255,255,.10)'
                          : '1px dashed rgba(255,255,255,.14)',
                      background: act ? 'rgba(255,201,74,.10)' : c.free ? C.surface : 'transparent',
                      color: C.ink, opacity: c.free ? 1 : 0.45,
                      cursor: c.free ? 'pointer' : 'default', textAlign: 'left',
                    }}
                  >
                    <div style={{ flex: 1, minWidth: 0, textAlign: 'left' }}>
                      <div
                        style={{
                          font: `700 17px ${F.display}`, letterSpacing: '-.01em',
                          textDecoration: c.free ? 'none' : 'line-through',
                        }}
                      >
                        {c.n}
                      </div>
                      <div style={{ font: `500 12.5px ${F.body}`, color: C.muted, marginTop: 3 }}>
                        {c.d}
                      </div>
                    </div>
                    <div
                      style={{
                        flex: 'none', display: 'flex', flexDirection: 'column',
                        alignItems: 'flex-end', gap: 5,
                      }}
                    >
                      {c.free && c.price != null && (
                        <div style={{ font: `700 15px ${F.body}`, color: C.ink }}>{fmt(c.price)}</div>
                      )}
                      <div
                        style={{
                          font: `700 10px ${F.body}`, letterSpacing: '.08em', textTransform: 'uppercase',
                          padding: '3px 7px', borderRadius: 6, whiteSpace: 'nowrap',
                          border: `1px solid ${last ? 'rgba(255,201,74,.55)' : 'rgba(255,255,255,.18)'}`,
                          color: c.free ? (last ? C.accent : C.text) : C.muted,
                        }}
                      >
                        {c.free ? (last ? 'Última libre' : 'Libre') : 'No disponible'}
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>
          </>
        )}

        {!q.isPending && !anyFree && (
          <div
            style={{
              marginTop: 26, borderRadius: 18, border: '1px dashed rgba(255,255,255,.16)',
              background: C.surface, padding: '22px 18px',
            }}
          >
            <div
              style={{
                width: 40, height: 40, borderRadius: 12, border: '1px solid rgba(255,255,255,.16)',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                font: `600 18px ${F.display}`, color: C.muted,
              }}
            >
              —
            </div>
            <div style={{ font: `700 18px ${F.display}`, letterSpacing: '-.01em', margin: '14px 0 6px' }}>
              No queda nada libre ese día
            </div>
            <div style={{ font: `500 14px/1.5 ${F.body}`, color: C.soft }}>
              No hay bloques de {durLabel(st.dur)} libres con ese filtro. Probá otra duración o tipo
              de cancha.
            </div>

            {suggestions.length > 0 && (
              <>
                <div
                  style={{
                    font: `700 11px ${F.body}`, color: C.dim, letterSpacing: '.1em',
                    textTransform: 'uppercase', margin: '20px 0 10px',
                  }}
                >
                  Lo más cercano
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {suggestions.map((sg, i) => (
                    <button
                      key={i}
                      type="button"
                      onClick={() =>
                        set({ dateIdx: sg.dateIdx, hour: sg.hour, courtIdx: sg.courtIdx })
                      }
                      style={{
                        display: 'flex', alignItems: 'center', gap: 12, width: '100%', minHeight: 60,
                        padding: '12px 14px', borderRadius: 14,
                        border: '1px solid rgba(255,201,74,.35)', background: 'rgba(255,201,74,.08)',
                        color: C.ink, cursor: 'pointer', textAlign: 'left',
                      }}
                    >
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ font: `700 15px ${F.display}` }}>{sg.when}</div>
                        <div style={{ font: `500 12.5px ${F.body}`, color: C.soft, marginTop: 2 }}>
                          {sg.court}
                        </div>
                      </div>
                      <div style={{ font: `700 14px ${F.body}`, color: C.accent }}>{fmt(sg.price)}</div>
                    </button>
                  ))}
                </div>
              </>
            )}
          </div>
        )}
      </Body>

      <Footer>
        <div style={{ font: `600 12.5px ${F.body}`, color: C.muted, textAlign: 'center' }}>{recap}</div>
        <button
          type="button"
          disabled={!sel}
          onClick={() => sel && set({ screen: 'confirm', sel })}
          style={sel ? ctaOn : ctaOff}
        >
          {sel ? 'Continuar' : hasHour ? 'Elegí una cancha' : 'Elegí un horario'}
        </button>
      </Footer>
    </Screen>
  );
}
