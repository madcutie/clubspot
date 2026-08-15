import { useNavigate } from 'react-router-dom';
import {
  useAgenda,
  useAlternarAusencia,
  useCancelarTurno,
  useCobrarTurno,
  usePersonas,
} from '../../api/queries';
import { pesos } from '../../domain/dinero';
import { duracionTurno, etiquetaDia, hhmm } from '../../domain/fechas';
import type { Deporte, SlotOcupado } from '../../domain/types';
import { BotonCerrar, FilaDato, Panel } from '../../ui/Panel';
import { estadoPago } from '../../ui/estados';
import { c, mono, sans } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';

/**
 * Un turno vendido. Todo lo que el mostrador hace con un turno pasa por acá:
 * cobrarlo, marcar que no vino, avisarle, reprogramarlo o cancelarlo.
 */
export function ReservaPanel({
  deporte,
  dia,
  turno,
  onCerrar,
}: {
  deporte: Deporte;
  dia: number;
  turno: { ci: number; t: number };
  onCerrar: () => void;
}) {
  const avisar = useTostada();
  const navegar = useNavigate();
  const { data: agenda } = useAgenda(deporte, dia);
  const cobrar = useCobrarTurno();
  const cancelar = useCancelarTurno();
  const ausencia = useAlternarAusencia();

  const columna = agenda?.columnas.find((col) => col.ci === turno.ci);
  const slot = columna?.items.find((i): i is SlotOcupado => !i.libre && i.t === turno.t);

  // La ficha se busca por nombre: en la agenda de ejemplo el turno guarda a
  // quién se le vendió, no el id.
  const { data: coincidencias } = usePersonas({
    q: slot?.persona ?? '',
    filtro: 'todas',
    pagina: 0,
  });

  if (!agenda || !columna || !slot) return null;
  const v = estadoPago(slot);
  const ref = { deporte, dateIdx: dia, ci: turno.ci, t: turno.t };

  const verFicha = () => {
    const p = coincidencias?.items.find((x) => x.nombre === slot.persona);
    if (p) navegar(`/personas?ficha=${p.id}`);
    else avisar('Esta persona no tiene ficha todavía');
  };

  return (
    <Panel onCerrar={onCerrar}>
      <div style={{ flex: 'none', padding: '20px 20px 0' }}>
        <div
          style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 12 }}
        >
          <div style={{ minWidth: 0 }}>
            <div style={{ font: `400 10.5px ${mono}`, color: c.textoTenue, letterSpacing: '.08em' }}>
              {slot.id}
            </div>
            <div style={{ font: `500 20px ${sans}`, letterSpacing: '-.025em', marginTop: 6 }}>
              {hhmm(slot.t)} – {hhmm(slot.t + slot.dur)}
            </div>
            <div style={{ font: `400 12.5px ${mono}`, color: c.textoGris, marginTop: 6 }}>
              {deporte === 'padel' ? 'Pádel' : 'Fútbol 5'} · {columna.nombre} · {etiquetaDia(dia)}
            </div>
          </div>
          <BotonCerrar onClick={onCerrar} />
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '20px 20px 22px' }}>
        <button
          type="button"
          onClick={verFicha}
          className="h-borde"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 11,
            width: '100%',
            padding: '13px 14px',
            borderRadius: 11,
            border: `1px solid ${c.borde}`,
            background: c.panel,
            cursor: 'pointer',
            textAlign: 'left',
          }}
        >
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ font: `500 14px ${sans}`, color: c.tinta }}>{slot.persona}</div>
            <div style={{ font: `400 12px ${mono}`, color: c.textoGris, marginTop: 3 }}>
              {slot.tel}
            </div>
          </div>
          <span style={{ flex: 'none', font: `400 11px ${mono}`, color: c.textoTenue }}>
            ver ficha →
          </span>
        </button>

        {slot.saldo > 0 && (
          <div
            style={{
              marginTop: 12,
              padding: '14px 15px',
              borderRadius: 11,
              background: c.naranjaFondo,
              border: `1px solid ${c.naranjaBorde}`,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: 12,
            }}
          >
            <div>
              <div style={{ font: `400 10.5px ${mono}`, color: c.naranja, letterSpacing: '.08em' }}>
                {slot.pago === 'sena' ? 'SALDO A COBRAR EN EL CLUB' : 'TURNO SIN PAGAR'}
              </div>
              <div
                style={{
                  font: `500 22px ${mono}`,
                  color: c.naranja,
                  letterSpacing: '-.03em',
                  marginTop: 4,
                }}
              >
                {pesos(slot.saldo)}
              </div>
            </div>
            <button
              type="button"
              onClick={() => {
                const monto = slot.saldo;
                cobrar.mutate(
                  {
                    ref,
                    datos: {
                      dur: slot.dur,
                      persona: slot.persona,
                      tel: slot.tel,
                      precio: slot.precio,
                    },
                  },
                  { onSuccess: () => avisar('Cobrado ' + pesos(monto)) },
                );
              }}
              style={{
                minHeight: 32,
                padding: '0 12px',
                borderRadius: 8,
                border: `1px solid ${c.naranjaBordeFirme}`,
                background: 'transparent',
                color: c.naranja,
                font: `500 12px ${sans}`,
                cursor: 'pointer',
              }}
            >
              Cobrar
            </button>
          </div>
        )}

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
            v={`${columna.nombre} · ${columna.detalle}`}
            estilo={{ font: `400 12.5px ${sans}`, color: c.tinta }}
          />
          <FilaDato
            k="duración"
            v={duracionTurno(slot.dur)}
            estilo={{ font: `400 12.5px ${mono}`, color: c.tinta }}
          />
          <FilaDato
            k="precio"
            v={pesos(slot.precio)}
            estilo={{ font: `400 12.5px ${mono}`, color: c.tinta }}
          />
          <FilaDato
            k="cobrado"
            v={pesos(slot.precio - slot.saldo)}
            estilo={{ font: `400 12.5px ${mono}`, color: c.tinta }}
          />
          <FilaDato k="estado" v={v.label} estilo={{ font: `400 12.5px ${mono}`, color: v.fg }} />
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 7, marginTop: 14 }}>
          <button type="button" onClick={() => avisar('Reprogramar turno')} style={accion}>
            Reprogramar turno
          </button>
          <button
            type="button"
            onClick={() =>
              ausencia.mutate(slot.key, {
                onSuccess: (marcada) =>
                  avisar(marcada ? 'Ausencia registrada' : 'Ausencia quitada'),
              })
            }
            style={accion}
          >
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
          onClick={() =>
            cancelar.mutate(ref, {
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
