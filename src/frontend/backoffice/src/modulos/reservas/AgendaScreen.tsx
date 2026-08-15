import { useMemo, useState } from 'react';
import { useAgenda } from '../../api/queries';
import { GRILLA_DESDE, GRILLA_HASTA } from '../../api/mockApi';
import { libreEn, ocupacion, primeraLibre } from '../../domain/agenda';
import { pesos } from '../../domain/dinero';
import { etiquetaDia, hhmm } from '../../domain/fechas';
import type { Deporte, SlotLibre, SlotOcupado } from '../../domain/types';
import { useParamsAgenda } from '../../rutas';
import { Cargando } from '../../ui/Cargando';
import { estadoPago } from '../../ui/estados';
import { FILA, c, chipFiltro, mono, primario, sans, secundario } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';
import { NuevaReservaPanel, type SlotElegido } from './NuevaReservaPanel';
import { ReservaPanel } from './ReservaPanel';

const DEPORTES: { id: Deporte; label: string }[] = [
  { id: 'padel', label: 'Pádel' },
  { id: 'futbol', label: 'Fútbol 5' },
];

/** Turno más corto que se puede vender: por debajo de esto un hueco no sirve. */
const TURNO_MINIMO = 60;

/**
 * Agenda del día: una columna por cancha, media hora por fila.
 *
 * La grilla no es sólo una vista: es la superficie de venta. Un hueco que no
 * alcanza para un turno se muestra apagado y no se puede clickear, y si la
 * cancha elegida no da, el sistema ofrece la que sí antes de abrir el panel.
 */
export function AgendaScreen() {
  const avisar = useTostada();
  const { deporte, dia, setDeporte, setDia } = useParamsAgenda();
  const { data: agenda, isLoading } = useAgenda(deporte, dia);

  const [verTurno, setVerTurno] = useState<{ ci: number; t: number } | null>(null);
  const [nueva, setNueva] = useState<SlotElegido | null>(null);

  const columnas = agenda?.columnas ?? [];
  const ocup = useMemo(() => ocupacion(columnas), [columnas]);

  if (!agenda) return isLoading ? <Cargando que="la agenda" /> : null;

  const horas = Array.from(
    { length: (GRILLA_HASTA - GRILLA_DESDE) / 60 },
    (_, i) => GRILLA_DESDE + i * 60,
  );

  /** Abre el panel de venta, cambiando de cancha si en la elegida no entra. */
  const vender = (ci: number, t: number) => {
    const entra = libreEn(ocup, ci, t, TURNO_MINIMO);
    const alternativa = entra ? ci : primeraLibre(ocup, t, TURNO_MINIMO);
    if (alternativa < 0) return;
    setNueva({
      ci: alternativa,
      t,
      aviso: entra
        ? null
        : `En ${columnas[ci].nombre} solo quedan 30 min a esa hora y el turno mínimo es 1 h. ` +
          `Te pasamos a ${columnas[alternativa].nombre}, que está libre.`,
    });
  };

  /**
   * Primer hueco vendible de la tarde-noche, o el primero que haya. Tiene que
   * ser vendible de verdad: un arranque válido con una hora libre por delante,
   * o el panel se abre en un callejón sin salida.
   */
  const abrirNuevaLibre = () => {
    const vendibles = columnas.flatMap((col) =>
      col.items
        .filter((i): i is SlotLibre => i.libre)
        .filter((i) => !i.cerrado && !i.offGrid && libreEn(ocup, col.ci, i.t, TURNO_MINIMO))
        .map((i) => ({ ci: col.ci, t: i.t })),
    );
    const elegido = vendibles.find((x) => x.t >= 19 * 60) ?? vendibles[0];
    setNueva(elegido ? { ...elegido, aviso: null } : { ci: 0, t: null, aviso: null });
  };

  return (
    <>
      <div style={{ flex: 'none', padding: '22px 26px 0' }}>
        <div
          style={{
            display: 'flex',
            alignItems: 'flex-end',
            justifyContent: 'space-between',
            gap: 20,
            flexWrap: 'wrap',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 14 }}>
            <span style={{ font: `500 40px ${mono}`, letterSpacing: '-.04em', color: c.titulo }}>
              {agenda.turnos}
            </span>
            <div style={{ paddingBottom: 4 }}>
              <div style={{ font: `500 14px ${sans}`, letterSpacing: '-.01em' }}>
                turnos {etiquetaDia(dia)}
              </div>
              <div style={{ font: `400 12px ${mono}`, color: c.textoGris, marginTop: 3 }}>
                {agenda.ocupacion}% de ocupación · {pesos(agenda.porCobrar)} por cobrar
              </div>
            </div>
          </div>
          <div style={{ display: 'flex', gap: 7 }}>
            <button
              type="button"
              onClick={() => avisar('Bloquear un horario del club')}
              className="h-ghost"
              style={secundario()}
            >
              Bloquear horario
            </button>
            <button
              type="button"
              onClick={abrirNuevaLibre}
              className="h-primario"
              style={primario()}
            >
              Nueva reserva
            </button>
          </div>
        </div>

        <div
          style={{ display: 'flex', alignItems: 'center', gap: 7, marginTop: 20, flexWrap: 'wrap' }}
        >
          {DEPORTES.map((d) => (
            <button
              key={d.id}
              type="button"
              onClick={() => {
                setDeporte(d.id);
                setVerTurno(null);
              }}
              style={chipFiltro(deporte === d.id)}
            >
              {d.label}
            </button>
          ))}
          <span style={{ width: 1, height: 20, background: c.borde, margin: '0 5px' }} />
          <button
            type="button"
            aria-label="Día anterior"
            onClick={() => {
              setDia(Math.max(0, dia - 1));
              setVerTurno(null);
            }}
            style={flecha}
          >
            ←
          </button>
          {Array.from({ length: 7 }, (_, i) => (
            <button
              key={i}
              type="button"
              onClick={() => {
                setDia(i);
                setVerTurno(null);
              }}
              style={chipFiltro(dia === i)}
            >
              {etiquetaDia(i)}
            </button>
          ))}
          <button
            type="button"
            aria-label="Día siguiente"
            onClick={() => {
              setDia(Math.min(6, dia + 1));
              setVerTurno(null);
            }}
            style={flecha}
          >
            →
          </button>
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, overflow: 'auto', marginTop: 18, padding: '0 26px 22px' }}>
        <div style={{ minWidth: 600 }}>
          <div
            style={{
              position: 'sticky',
              top: 0,
              zIndex: 3,
              background: c.papel,
              display: 'flex',
              gap: 6,
              paddingBottom: 8,
              borderBottom: `1px solid ${c.linea}`,
            }}
          >
            <div style={{ flex: 'none', width: 52 }} />
            {columnas.map((col) => (
              <div key={col.ci} style={{ flex: 1, minWidth: 118 }}>
                <div style={{ font: `500 12.5px ${sans}`, color: c.tinta }}>{col.nombre}</div>
                <div
                  style={{
                    font: `400 10.5px ${mono}`,
                    color: c.textoTenue,
                    marginTop: 3,
                    whiteSpace: 'nowrap',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                  }}
                >
                  {col.detalle}
                </div>
              </div>
            ))}
          </div>

          <div style={{ display: 'flex', gap: 6, paddingTop: 6 }}>
            <div style={{ flex: 'none', width: 52, display: 'flex', flexDirection: 'column' }}>
              {horas.map((h) => (
                <div
                  key={h}
                  style={{
                    height: FILA * 2,
                    flex: 'none',
                    font: `400 11px ${mono}`,
                    color: c.textoGris2,
                    paddingTop: 2,
                  }}
                >
                  {hhmm(h)}
                </div>
              ))}
            </div>

            {columnas.map((col) => (
              <div
                key={col.ci}
                style={{ flex: 1, minWidth: 118, display: 'flex', flexDirection: 'column' }}
              >
                {col.items.map((it) =>
                  it.libre ? (
                    <Hueco
                      key={`l${it.t}`}
                      slot={it}
                      vendible={
                        !it.cerrado &&
                        !it.offGrid &&
                        (libreEn(ocup, col.ci, it.t, TURNO_MINIMO) ||
                          primeraLibre(ocup, it.t, TURNO_MINIMO) >= 0)
                      }
                      onVender={() => vender(col.ci, it.t)}
                    />
                  ) : (
                    <Turno
                      key={`o${it.t}`}
                      slot={it}
                      activo={verTurno?.ci === col.ci && verTurno?.t === it.t}
                      onAbrir={() => setVerTurno({ ci: col.ci, t: it.t })}
                    />
                  ),
                )}
              </div>
            ))}
          </div>
        </div>
      </div>

      {verTurno && (
        <ReservaPanel
          deporte={deporte}
          dia={dia}
          turno={verTurno}
          onCerrar={() => setVerTurno(null)}
        />
      )}
      {nueva && (
        <NuevaReservaPanel
          deporte={deporte}
          dia={dia}
          elegido={nueva}
          columnas={columnas}
          ocupacion={ocup}
          onCerrar={() => setNueva(null)}
        />
      )}
    </>
  );
}

/** Hueco de la grilla. Si no alcanza para un turno se muestra apagado. */
function Hueco({
  slot,
  vendible,
  onVender,
}: {
  slot: SlotLibre;
  vendible: boolean;
  onVender: () => void;
}) {
  return (
    <button
      type="button"
      onClick={vendible ? onVender : undefined}
      className={vendible ? 'h-borde' : undefined}
      style={{
        height: FILA * (slot.span || 1),
        flex: 'none',
        borderRadius: 6,
        cursor: vendible ? 'pointer' : 'default',
        display: 'flex',
        alignItems: 'center',
        padding: vendible ? 0 : '0 9px',
        border: vendible || slot.cerrado ? '1px solid transparent' : `1px dashed ${c.bordeFirme}`,
        background: vendible
          ? `repeating-linear-gradient(135deg,#E3E7E0 0 5px,#F1F3EE 5px 10px)`
          : slot.cerrado
            ? c.cerrado
            : c.hueco,
      }}
    >
      {!vendible && (
        <span style={{ font: `400 9.5px ${mono}`, color: '#66665E', letterSpacing: '.04em' }}>
          {slot.cerrado ? 'cerrado' : slot.offGrid ? '' : 'hueco 30 min'}
        </span>
      )}
    </button>
  );
}

/** Turno vendido. El color dice si está cobrado antes de leer el texto. */
function Turno({
  slot,
  activo,
  onAbrir,
}: {
  slot: SlotOcupado;
  activo: boolean;
  onAbrir: () => void;
}) {
  const v = estadoPago(slot);
  return (
    <button
      type="button"
      onClick={onAbrir}
      style={{
        height: (slot.dur / 30) * FILA,
        flex: 'none',
        cursor: 'pointer',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'stretch',
        padding: '5px 9px',
        borderRadius: 8,
        textAlign: 'left',
        overflow: 'hidden',
        border: `1px solid ${activo ? c.verde : v.bd}`,
        background: v.bg === 'transparent' ? c.blanco : v.bg,
        opacity: slot.ausente ? 0.55 : 1,
      }}
    >
      <span style={{ flex: 'none', display: 'flex', alignItems: 'center', gap: 6, height: 14 }}>
        <span
          style={{ width: 6, height: 6, borderRadius: '50%', flex: 'none', background: v.dot }}
        />
        <span style={{ font: `400 10.5px ${mono}`, color: c.textoDato, lineHeight: '14px' }}>
          {hhmm(slot.t)}–{hhmm(slot.t + slot.dur)}
        </span>
      </span>
      <span
        style={{
          flex: 'none',
          display: 'block',
          font: `500 12.5px ${sans}`,
          lineHeight: '17px',
          color: c.tinta,
          marginTop: 2,
          whiteSpace: 'nowrap',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
        }}
      >
        {slot.persona}
      </span>
      <span
        style={{
          flex: 'none',
          display: 'block',
          font: `400 10px ${mono}`,
          lineHeight: '13px',
          color: c.textoTenue,
          marginTop: 1,
          whiteSpace: 'nowrap',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
        }}
      >
        {v.label}
      </span>
    </button>
  );
}

const flecha = {
  width: 30,
  minHeight: 34,
  borderRadius: 8,
  cursor: 'pointer',
  border: `1px solid ${c.borde}`,
  background: 'transparent',
  color: c.textoTenue2,
  font: `400 13px ${sans}`,
} as const;
