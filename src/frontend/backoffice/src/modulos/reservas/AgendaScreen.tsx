import { useEffect, useState } from 'react';
import { CalendarX, ChevronLeft, ChevronRight, Clock, Plus } from 'lucide-react';
import { useAgenda, useCanchas } from '../../api/queries';
import {
  GRILLA_DESDE,
  GRILLA_HASTA,
  celdasDe,
  resumenAgenda,
  type Celda,
  type CeldaLibre,
} from '../../domain/agenda';
import { pesos } from '../../domain/dinero';
import { etiquetaDia, hhmm, isoDe, minutosDeAhora } from '../../domain/fechas';
import type { Cancha, CanchaAgenda, Deporte, ReservaDia, ReservaInactiva } from '../../domain/types';
import { useParamsAgenda } from '../../rutas';
import { Cargando } from '../../ui/Cargando';
import { AIRE, FILA, c, chipDia, chipFiltro, mono, primario, sans, secundario } from '../../ui/theme';
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
  // El catálogo ya está en caché por la barra lateral: sale la tarifa sin pedir nada nuevo.
  const { data: catalogo } = useCanchas();
  const ahora = useAhora();

  const [verReserva, setVerReserva] = useState<string | null>(null);
  const [nueva, setNueva] = useState<SlotElegido | null>(null);
  // El cobro se monta acá, no dentro del panel de la reserva: ese se desmonta cada vez
  // que la agenda se refresca, y con él se perdía el código recién emitido.
  const [cobro, setCobro] = useState<{ reserva: ReservaDia; cancha: string } | null>(null);

  if (!agenda) return isLoading ? <Cargando que="la agenda" /> : null;

  const canchas = agenda.canchas;
  const resumen = resumenAgenda(canchas);
  const tarifas = new Map((catalogo ?? []).map((x) => [x.id, x] as const));

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
              style={chipDia(dia === i)}
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
              zIndex: 6,
              background: c.papel,
              display: 'flex',
              gap: 6,
              paddingBottom: 8,
              borderBottom: `1px solid ${c.linea}`,
            }}
          >
            <div style={{ flex: 'none', width: 52 }} />
            {canchas.map((cancha) => (
              <Encabezado key={cancha.courtId} cancha={cancha} tarifa={tarifas.get(cancha.courtId)} />
            ))}
          </div>

          <div style={{ display: 'flex', gap: 6, paddingTop: 6, position: 'relative' }}>
            <div
              aria-hidden
              style={{ position: 'absolute', inset: '6px 0 0', zIndex: 1, pointerEvents: 'none' }}
            >
              {horas.slice(1).map((h) => (
                <div
                  key={h}
                  style={{
                    position: 'absolute',
                    left: 0,
                    right: 0,
                    top: ((h - GRILLA_DESDE) / 30) * FILA,
                    height: 1,
                    background: c.regla,
                  }}
                />
              ))}
            </div>

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
                style={{
                  flex: 1,
                  minWidth: 118,
                  display: 'flex',
                  flexDirection: 'column',
                  position: 'relative',
                }}
              >
                {celdasDe(cancha).map((it, i, todas) =>
                  it.libre ? (
                    <Hueco
                      key={`l${it.t}`}
                      celda={it}
                      abreBloque={!vieneDeUnHuecoVendible(todas[i - 1])}
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

            {dia === 0 && ahora > GRILLA_DESDE && ahora < GRILLA_HASTA && (
              <div
                aria-hidden
                style={{
                  position: 'absolute',
                  left: 0,
                  right: 0,
                  top: 6 + ((ahora - GRILLA_DESDE) / 30) * FILA - 7,
                  zIndex: 3,
                  display: 'flex',
                  alignItems: 'center',
                  pointerEvents: 'none',
                }}
              >
                <span style={{ flex: 'none', width: 52 }}>
                  <span
                    style={{
                      font: `500 9.5px ${mono}`,
                      color: c.papel,
                      background: c.tinta,
                      borderRadius: 4,
                      padding: '1px 5px',
                    }}
                  >
                    {hhmm(ahora)}
                  </span>
                </span>
                <span style={{ flex: 1, height: 1.5, background: c.tinta, marginLeft: 6 }} />
              </div>
            )}
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
                  : (r.motivo ?? 'La canceló el club')
              }
              style={{ font: `400 10.5px ${mono}`, color: c.textoGris, flex: 'none' }}
            >
              {r.estado}
            </span>
            <span
              title={r.motivo ?? undefined}
              style={{
                font: `400 11.5px ${sans}`,
                color: c.textoTenue,
                flex: 2,
                minWidth: 0,
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}
            >
              {r.motivo ?? ''}
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

/**
 * Hueco de la grilla. Sólo un arranque vendible se puede clickear, y por eso es
 * lo único que se dibuja en blanco y con precio: en una agenda lo primero que
 * hay que ver es qué se puede vender. El rayado es lo cerrado, no lo libre.
 */
function Hueco({
  celda,
  abreBloque,
  onVender,
}: {
  celda: CeldaLibre;
  abreBloque: boolean;
  onVender: () => void;
}) {
  return (
    <button
      type="button"
      onClick={celda.vendible ? onVender : undefined}
      className={celda.vendible ? 'h-vendible' : undefined}
      style={{
        height: FILA * (celda.span || 1),
        flex: 'none',
        border: 'none',
        cursor: celda.vendible ? 'pointer' : 'default',
        display: 'flex',
        // Arriba de todo: centrado, un renglón de hora le pasaría por encima al texto.
        alignItems: 'flex-start',
        gap: 5,
        padding: '7px 9px 0',
        overflow: 'hidden',
        background: celda.cerrado ? c.rayado : celda.vendible ? c.hueco : c.papel,
      }}
    >
      {celda.cerrado && (
        <span style={{ font: `400 9.5px ${mono}`, color: c.textoGris, letterSpacing: '.04em' }}>
          cerrado
        </span>
      )}
      {celda.vendible && abreBloque && (
        <>
          <Plus size={11} strokeWidth={2.2} color={c.libreIcono} style={{ flex: 'none' }} aria-hidden />
          {celda.precio !== null && (
            <span
              style={{
                font: `400 10px ${mono}`,
                color: c.textoGris,
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}
            >
              {celda.desde ? `desde ${pesos(celda.precio)}` : pesos(celda.precio)}
            </span>
          )}
        </>
      )}
    </button>
  );
}

/** Reserva confirmada. El color de la tarjeta dice cómo está la plata. */
function Turno({
  reserva,
  activo,
  onAbrir,
}: {
  reserva: ReservaDia;
  activo: boolean;
  onAbrir: () => void;
}) {
  const pinta = pintaDe(reserva, activo);
  // Media hora no da para tres renglones: nombre y plata en una sola línea.
  const compacto = reserva.dur <= 30;
  return (
    <button
      type="button"
      onClick={onAbrir}
      style={{
        height: (reserva.dur / 30) * FILA - AIRE,
        margin: `${AIRE / 2}px 0`,
        flex: 'none',
        position: 'relative',
        zIndex: 2,
        cursor: 'pointer',
        display: 'flex',
        flexDirection: compacto ? 'row' : 'column',
        alignItems: compacto ? 'center' : 'stretch',
        gap: compacto ? 8 : 0,
        padding: compacto ? '0 9px' : '6px 10px',
        borderRadius: 8,
        textAlign: 'left',
        overflow: 'hidden',
        background: pinta.fondo,
        border: pinta.borde,
      }}
    >
      {!compacto && (
        <span style={{ font: `400 10.5px ${mono}`, color: pinta.hora, lineHeight: '14px' }}>
          {hhmm(reserva.t)}–{hhmm(reserva.t + reserva.dur)}
        </span>
      )}
      <span
        style={{
          flex: compacto ? 1 : 'none',
          minWidth: 0,
          display: 'block',
          font: `600 12.5px ${sans}`,
          lineHeight: '17px',
          color: pinta.nombre,
          marginTop: compacto ? 0 : 1,
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
          display: 'flex',
          alignItems: 'center',
          gap: 4,
          marginTop: compacto ? 0 : 2,
          minWidth: 0,
        }}
      >
        <span
          style={{
            font: `500 10px ${mono}`,
            lineHeight: '13px',
            color: pinta.plata,
            whiteSpace: 'nowrap',
            overflow: 'hidden',
            textOverflow: 'ellipsis',
          }}
        >
          {pinta.texto}
        </span>
        {pinta.reloj && (
          <Clock size={10} strokeWidth={2.2} color={pinta.plata} style={{ flex: 'none' }} aria-hidden />
        )}
      </span>
    </button>
  );
}

/**
 * Los cuatro estados de plata de un turno, que es lo que el mostrador necesita
 * de un vistazo: cobrada, señada, sin pagar y hold de pago online sin acreditar.
 */
function pintaDe(reserva: ReservaDia, activo: boolean) {
  if (reserva.pendientePago) {
    return {
      fondo: c.blanco,
      borde: `1.5px dashed ${activo ? c.acento : c.holdBorde}`,
      hora: c.acentoTenue,
      nombre: c.tinta,
      plata: c.acento,
      texto: `${pesos(reserva.precio)} · pago online pendiente`,
      reloj: true,
    };
  }
  const saldo = reserva.precio - reserva.pagado;
  if (saldo <= 0) {
    return {
      fondo: c.acento,
      borde: `1.5px solid ${activo ? c.tinta : c.acento}`,
      hora: c.sobreAcento,
      nombre: c.blanco,
      plata: c.sobreAcentoFuerte,
      texto:
        saldo < 0
          ? `${pesos(reserva.precio)} · cobrado de más`
          : `${pesos(reserva.precio)} · cobrada`,
      reloj: false,
    };
  }
  if (reserva.pagado > 0) {
    return {
      fondo: c.ambarFondo,
      borde: `1.5px solid ${activo ? c.ambarFuerte : c.ambarBorde}`,
      hora: c.ambarTexto,
      nombre: c.tinta,
      plata: c.ambarFuerte,
      texto: `seña ${pesos(reserva.pagado)} · resta ${pesos(saldo)}`,
      reloj: false,
    };
  }
  return {
    fondo: c.blanco,
    borde: `1.5px dashed ${activo ? c.naranjaFuerte : c.naranjaBorde}`,
    hora: c.naranjaTexto,
    nombre: c.tinta,
    plata: c.naranjaFuerte,
    texto: `${pesos(reserva.precio)} · sin pagar`,
    reloj: false,
  };
}

/** Un hueco vendible sigue al anterior: entre los dos no se corta el bloque. */
function vieneDeUnHuecoVendible(previa: Celda | undefined): boolean {
  return previa !== undefined && previa.libre && previa.vendible;
}

/** Encabezado de columna: qué cancha es, cómo es y cuánto sale. */
function Encabezado({ cancha, tarifa }: { cancha: CanchaAgenda; tarifa: Cancha | undefined }) {
  const atributos = cancha.detalle
    .split('·')
    .map((x) => x.trim())
    .filter(Boolean);
  return (
    <div style={{ flex: 1, minWidth: 118 }}>
      <div
        style={{
          font: `600 13px ${sans}`,
          color: c.tinta,
          letterSpacing: '-.01em',
          whiteSpace: 'nowrap',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
        }}
      >
        {cancha.nombre}
      </div>
      <div style={{ display: 'flex', gap: 4, marginTop: 5, overflow: 'hidden' }}>
        {atributos.map((a) => (
          <span
            key={a}
            style={{
              font: `400 10px ${mono}`,
              color: c.textoTenue,
              background: c.hueco,
              borderRadius: 4,
              padding: '1px 6px',
              whiteSpace: 'nowrap',
            }}
          >
            {a}
          </span>
        ))}
      </div>
      <div
        style={{
          font: `400 10px ${mono}`,
          color: c.textoApagado,
          marginTop: 6,
          whiteSpace: 'nowrap',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
        }}
      >
        {tarifa ? `día ${pesos(tarifa.precioDia)} · noche ${pesos(tarifa.precioNoche)}` : ''}
      </div>
    </div>
  );
}

/** Minuto del reloj, refrescado solo: la línea de la hora se mueve sin recargar. */
function useAhora(): number {
  const [ahora, setAhora] = useState(minutosDeAhora);
  useEffect(() => {
    const id = setInterval(() => setAhora(minutosDeAhora()), 30_000);
    return () => clearInterval(id);
  }, []);
  return ahora;
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
