import { useState } from 'react';
import { CalendarX, ChevronLeft, ChevronRight } from 'lucide-react';
import { useAgenda } from '../../api/queries';
import {
  GRILLA_DESDE,
  GRILLA_HASTA,
  celdasDe,
  resumenAgenda,
  type CeldaLibre,
} from '../../domain/agenda';
import { pesos } from '../../domain/dinero';
import { etiquetaDia, hhmm, isoDe } from '../../domain/fechas';
import type { Deporte, ReservaDia, ReservaInactiva } from '../../domain/types';
import { useParamsAgenda } from '../../rutas';
import { Cargando } from '../../ui/Cargando';
import { FILA, c, chipFiltro, mono, primario, sans, secundario } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';
import { CobroPanel } from './CobroPanel';
import { NuevaReservaPanel, type SlotElegido } from './NuevaReservaPanel';
import { ReservaPanel } from './ReservaPanel';

const DEPORTES: { id: Deporte; label: string }[] = [
  { id: 'padel', label: 'Pádel' },
  { id: 'futbol', label: 'Fútbol 5' },
];

/**
 * Agenda del día: una columna por cancha, media hora por fila.
 *
 * La grilla no es sólo una vista: es la superficie de venta. Se dibuja de lo
 * que calculó el backend: franjas cerradas desde las ventanas efectivas,
 * tarjetas desde las reservas confirmadas y celdas clickeables desde los
 * arranques vendibles con su precio.
 */
export function AgendaScreen() {
  const avisar = useTostada();
  const { deporte, dia, setDeporte, setDia } = useParamsAgenda();
  const fecha = isoDe(dia);
  const { data: agenda, isLoading } = useAgenda(deporte, fecha);

  const [verReserva, setVerReserva] = useState<string | null>(null);
  const [nueva, setNueva] = useState<SlotElegido | null>(null);
  // El cobro se monta acá, no dentro del panel de la reserva: ese se desmonta cada vez
  // que la agenda se refresca, y con él se perdía el código recién emitido.
  const [cobro, setCobro] = useState<{ reserva: ReservaDia; cancha: string } | null>(null);

  if (!agenda) return isLoading ? <Cargando que="la agenda" /> : null;

  const canchas = agenda.canchas;
  const resumen = resumenAgenda(canchas);

  const horas = Array.from(
    { length: (GRILLA_HASTA - GRILLA_DESDE) / 60 },
    (_, i) => GRILLA_DESDE + i * 60,
  );

  /** Primer arranque vendible de la tarde-noche, o el primero que haya. */
  const abrirNuevaLibre = () => {
    const vendibles = canchas
      .flatMap((cancha) => cancha.turnos.map((s) => ({ courtId: cancha.courtId, t: s.t })))
      .sort((a, b) => a.t - b.t);
    const elegido = vendibles.find((x) => x.t >= 19 * 60) ?? vendibles[0];
    setNueva(elegido ?? { courtId: canchas[0]?.courtId ?? '', t: null });
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
              {resumen.turnos}
            </span>
            <div style={{ paddingBottom: 4 }}>
              <div style={{ font: `500 14px ${sans}`, letterSpacing: '-.01em' }}>
                turnos {etiquetaDia(dia)}
              </div>
              <div style={{ font: `400 12px ${mono}`, color: c.textoGris, marginTop: 3 }}>
                {resumen.ocupacion}% de ocupación
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
                setVerReserva(null);
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
              setVerReserva(null);
            }}
            style={flecha}
          >
            <ChevronLeft size={15} strokeWidth={2} aria-hidden />
          </button>
          {Array.from({ length: 7 }, (_, i) => (
            <button
              key={i}
              type="button"
              onClick={() => {
                setDia(i);
                setVerReserva(null);
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
              setVerReserva(null);
            }}
            style={flecha}
          >
            <ChevronRight size={15} strokeWidth={2} aria-hidden />
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
            {canchas.map((cancha) => (
              <div key={cancha.courtId} style={{ flex: 1, minWidth: 118 }}>
                <div style={{ font: `500 12.5px ${sans}`, color: c.tinta }}>{cancha.nombre}</div>
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
                  {cancha.detalle}
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

            {canchas.map((cancha) => (
              <div
                key={cancha.courtId}
                style={{ flex: 1, minWidth: 118, display: 'flex', flexDirection: 'column' }}
              >
                {celdasDe(cancha).map((it) =>
                  it.libre ? (
                    <Hueco
                      key={`l${it.t}`}
                      celda={it}
                      onVender={() => setNueva({ courtId: cancha.courtId, t: it.t })}
                    />
                  ) : (
                    <Turno
                      key={it.reserva.id}
                      reserva={it.reserva}
                      activo={verReserva === it.reserva.id}
                      onAbrir={() => setVerReserva(it.reserva.id)}
                    />
                  ),
                )}
              </div>
            ))}
          </div>

          {agenda.inactivas.length > 0 && <Inactivas lista={agenda.inactivas} />}
        </div>
      </div>

      {verReserva && (
        <ReservaPanel
          deporte={deporte}
          dia={dia}
          reservaId={verReserva}
          onCerrar={() => setVerReserva(null)}
          onCobrar={(reserva, cancha) => {
            setVerReserva(null);
            setCobro({ reserva, cancha });
          }}
        />
      )}
      {cobro && (
        <CobroPanel
          reserva={cobro.reserva}
          cancha={cobro.cancha}
          deporte={deporte}
          dia={dia}
          onCerrar={() => setCobro(null)}
        />
      )}
      {nueva && (
        <NuevaReservaPanel
          deporte={deporte}
          dia={dia}
          elegido={nueva}
          canchas={canchas}
          onCerrar={() => setNueva(null)}
        />
      )}
    </>
  );
}

/**
 * Registro del día: reservas que ya no bloquean el turno pero existieron.
 * Sobre todo importa la plata: una cancelada con pago es un pendiente del operador.
 */
function Inactivas({ lista }: { lista: ReservaInactiva[] }) {
  return (
    <div style={{ marginTop: 22, borderTop: `1px solid ${c.linea}`, paddingTop: 14 }}>
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 7,
          font: `500 12.5px ${sans}`,
          color: c.textoGris,
          marginBottom: 8,
        }}
      >
        <CalendarX size={13} strokeWidth={2} aria-hidden />
        Canceladas y abandonadas del día
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
        {lista.map((r) => (
          <div
            key={r.id}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 12,
              padding: '7px 10px',
              borderRadius: 8,
              border: `1px dashed ${c.bordeFirme}`,
              background: c.hueco,
            }}
          >
            <span style={{ font: `400 11px ${mono}`, color: c.textoDato, width: 84, flex: 'none' }}>
              {hhmm(r.t)}–{hhmm(r.t + r.dur)}
            </span>
            <span
              style={{
                font: `400 11px ${mono}`,
                color: c.textoTenue,
                width: 92,
                flex: 'none',
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}
            >
              {r.cancha}
            </span>
            <span
              style={{
                font: `500 12.5px ${sans}`,
                color: c.tinta,
                flex: 1,
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}
            >
              {r.persona}
            </span>
            <span
              title={
                r.estado === 'abandonada'
                  ? 'Empezó a reservar con pago online y no completó el pago'
                  : 'La canceló el club'
              }
              style={{ font: `400 10.5px ${mono}`, color: c.textoGris, flex: 'none' }}
            >
              {r.estado}
            </span>
            <span
              style={{
                font: `500 11px ${mono}`,
                color: r.pagado > 0 ? '#c9971f' : c.textoTenue,
                flex: 'none',
                width: 120,
                textAlign: 'right',
              }}
            >
              {r.pagado > 0 ? `pagó ${pesos(r.pagado)}` : pesos(r.precio)}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

/** Hueco de la grilla. Sólo un arranque vendible se puede clickear. */
function Hueco({ celda, onVender }: { celda: CeldaLibre; onVender: () => void }) {
  return (
    <button
      type="button"
      onClick={celda.vendible ? onVender : undefined}
      className={celda.vendible ? 'h-borde' : undefined}
      style={{
        height: FILA * (celda.span || 1),
        flex: 'none',
        borderRadius: 6,
        cursor: celda.vendible ? 'pointer' : 'default',
        display: 'flex',
        alignItems: 'center',
        padding: celda.vendible ? 0 : '0 9px',
        border:
          celda.vendible || celda.cerrado ? '1px solid transparent' : `1px dashed ${c.bordeFirme}`,
        background: celda.vendible
          ? `repeating-linear-gradient(135deg,#E3E7E0 0 5px,#F1F3EE 5px 10px)`
          : celda.cerrado
            ? c.cerrado
            : c.hueco,
      }}
    >
      {celda.cerrado && (
        <span style={{ font: `400 9.5px ${mono}`, color: '#66665E', letterSpacing: '.04em' }}>
          cerrado
        </span>
      )}
    </button>
  );
}

/** Reserva confirmada, con su estado de cobro. */
function Turno({
  reserva,
  activo,
  onAbrir,
}: {
  reserva: ReservaDia;
  activo: boolean;
  onAbrir: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onAbrir}
      style={{
        height: (reserva.dur / 30) * FILA,
        flex: 'none',
        cursor: 'pointer',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'stretch',
        padding: '5px 9px',
        borderRadius: 8,
        textAlign: 'left',
        overflow: 'hidden',
        border: reserva.pendientePago
          ? '1px dashed #c9971f'
          : `1px solid ${activo ? c.verde : c.verdeBorde}`,
        background: reserva.pendientePago ? 'rgba(201,151,31,.08)' : c.verdeFondoSuave,
      }}
    >
      <span style={{ flex: 'none', display: 'flex', alignItems: 'center', gap: 6, height: 14 }}>
        {/* Relleno = cobrado. En una grilla llena el color se lee antes que el texto. */}
        <span
          style={{
            width: 6, height: 6, borderRadius: '50%', flex: 'none',
            border: `1.5px solid ${reserva.pendientePago ? '#c9971f' : c.verdePunto}`,
            background: reserva.pendientePago
              ? '#c9971f'
              : reserva.pagado >= reserva.precio
                ? c.verdePunto
                : 'transparent',
          }}
        />
        <span style={{ font: `400 10.5px ${mono}`, color: c.textoDato, lineHeight: '14px' }}>
          {hhmm(reserva.t)}–{hhmm(reserva.t + reserva.dur)}
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
        {reserva.persona}
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
        {estadoDeCobro(reserva)}
      </span>
    </button>
  );
}

/** Lo que el mostrador necesita de un vistazo: si ese turno está cobrado o no. */
function estadoDeCobro(reserva: ReservaDia): string {
  if (reserva.pendientePago) return `${pesos(reserva.precio)} · pago pendiente`;
  const saldo = reserva.precio - reserva.pagado;
  if (saldo < 0) return `${pesos(reserva.precio)} · cobrado de más`;
  if (saldo === 0) return `${pesos(reserva.precio)} · pagado`;
  if (reserva.pagado > 0) return `debe ${pesos(saldo)}`;
  return pesos(reserva.precio);
}

const flecha = {
  width: 30,
  minHeight: 34,
  borderRadius: 8,
  cursor: 'pointer',
  border: `1px solid ${c.borde}`,
  background: 'transparent',
  color: c.textoTenue2,
  display: 'grid',
  placeItems: 'center',
} as const;
