import { QrCode } from 'lucide-react';
import { useAgenda, useCancelarReserva } from '../../api/queries';
import { pesos } from '../../domain/dinero';
import { duracionTurno, etiquetaDia, hhmm, isoDe } from '../../domain/fechas';
import type { Deporte, ReservaDia } from '../../domain/types';
import { BotonCerrar, FilaDato, Panel } from '../../ui/Panel';
import { c, mono, sans } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';

/**
 * Una reserva confirmada. Cancelar es real; cobrar, marcar ausencia y
 * reprogramar son avisos provisionales hasta que exista la parte financiera
 * y el resto del flujo de agenda.
 */
export function ReservaPanel({
  deporte,
  dia,
  reservaId,
  onCerrar,
  onCobrar,
}: {
  deporte: Deporte;
  dia: number;
  reservaId: string;
  onCerrar: () => void;
  onCobrar: (reserva: ReservaDia, cancha: string) => void;
}) {
  const avisar = useTostada();
  const { data: agenda } = useAgenda(deporte, isoDe(dia));
  const cancelar = useCancelarReserva();

  const hallada = agenda?.canchas
    .flatMap((cancha) => cancha.reservas.map((reserva) => ({ cancha, reserva })))
    .find((x) => x.reserva.id === reservaId);

  if (!hallada) return null;
  const { cancha, reserva } = hallada;
  const saldo = reserva.precio - reserva.pagado;

  return (
    <Panel onCerrar={onCerrar}>
      <div style={{ flex: 'none', padding: '20px 20px 0' }}>
        <div
          style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 12 }}
        >
          <div style={{ minWidth: 0 }}>
            <div style={{ font: `400 10.5px ${mono}`, color: c.textoTenue, letterSpacing: '.08em' }}>
              {reserva.id.slice(0, 8).toUpperCase()}
            </div>
            <div style={{ font: `500 20px ${sans}`, letterSpacing: '-.025em', marginTop: 6 }}>
              {hhmm(reserva.t)} – {hhmm(reserva.t + reserva.dur)}
            </div>
            <div style={{ font: `400 12.5px ${mono}`, color: c.textoGris, marginTop: 6 }}>
              {deporte === 'padel' ? 'Pádel' : 'Fútbol 5'} · {cancha.nombre} · {etiquetaDia(dia)}
            </div>
          </div>
          <BotonCerrar onClick={onCerrar} />
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '20px 20px 22px' }}>
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 11,
            width: '100%',
            padding: '13px 14px',
            borderRadius: 11,
            border: `1px solid ${c.borde}`,
            background: c.panel,
          }}
        >
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ font: `500 14px ${sans}`, color: c.tinta }}>{reserva.persona}</div>
            <div style={{ font: `400 12px ${mono}`, color: c.textoGris, marginTop: 3 }}>
              {reserva.tel ?? '—'}
            </div>
          </div>
        </div>

        <div
          style={{
            border: `1px solid ${c.borde}`,
            borderRadius: 11,
            overflow: 'hidden',
            marginTop: 12,
          }}
        >
          <FilaDato
            k="deporte"
            v={deporte === 'padel' ? 'Pádel' : 'Fútbol 5'}
            estilo={{ font: `400 12.5px ${sans}`, color: c.tinta }}
          />
          <FilaDato
            k="cancha"
            v={`${cancha.nombre} · ${cancha.detalle}`}
            estilo={{ font: `400 12.5px ${sans}`, color: c.tinta }}
          />
          <FilaDato
            k="duración"
            v={duracionTurno(reserva.dur)}
            estilo={{ font: `400 12.5px ${mono}`, color: c.tinta }}
          />
          <FilaDato
            k="precio"
            v={pesos(reserva.precio)}
            estilo={{ font: `400 12.5px ${mono}`, color: c.tinta }}
          />
          <FilaDato
            k={saldo > 0 ? 'debe' : saldo < 0 ? 'cobrado de más' : 'pagado'}
            v={saldo === 0 ? pesos(reserva.pagado) : pesos(Math.abs(saldo))}
            estilo={{
              font: `500 12.5px ${mono}`,
              color: saldo > 0 ? c.ambarTexto : saldo < 0 ? c.naranja : c.acentoTexto,
            }}
          />
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 7, marginTop: 14 }}>
          {saldo > 0 && (
            <button type="button" onClick={() => onCobrar(reserva, cancha.nombre)} style={accion}>
              <QrCode size={14} strokeWidth={2} aria-hidden style={{ marginRight: 8, verticalAlign: -2 }} />
              Cobrar con Mercado Pago
            </button>
          )}
          <button type="button" onClick={() => avisar('Reprogramar turno')} style={accion}>
            Reprogramar turno
          </button>
          <button type="button" onClick={() => avisar('Marcar ausencia')} style={accion}>
            Marcar ausencia
          </button>
          <button type="button" onClick={() => avisar('Abriendo WhatsApp')} style={accion}>
            Avisar por WhatsApp
          </button>
        </div>
      </div>

      <div
        style={{
          flex: 'none',
          display: 'flex',
          alignItems: 'center',
          padding: '13px 20px',
          borderTop: `1px solid ${c.linea}`,
        }}
      >
        <div style={{ flex: 1 }} />
        <button
          type="button"
          disabled={cancelar.isPending}
          onClick={() =>
            cancelar.mutate(reserva.id, {
              onSuccess: () => {
                avisar('Reserva cancelada');
                onCerrar();
              },
            })
          }
          style={{
            minHeight: 34,
            padding: '0 13px',
            borderRadius: 8,
            border: `1px solid ${c.naranjaBorde}`,
            background: 'transparent',
            color: c.naranja,
            font: `500 12.5px ${sans}`,
            cursor: 'pointer',
          }}
        >
          Cancelar reserva
        </button>
      </div>
    </Panel>
  );
}

const accion = {
  width: '100%',
  minHeight: 38,
  borderRadius: 9,
  border: `1px solid ${c.bordeFirme}`,
  background: 'transparent',
  color: c.textoBoton,
  font: `500 12.5px ${sans}`,
  cursor: 'pointer',
  textAlign: 'left',
  padding: '0 13px',
} as const;
