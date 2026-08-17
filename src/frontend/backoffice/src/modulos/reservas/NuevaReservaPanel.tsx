import { useState } from 'react';
import { useCrearReserva } from '../../api/queries';
import { pesos } from '../../domain/dinero';
import { duracionTurno, etiquetaDia, hhmm, isoDe } from '../../domain/fechas';
import type { CanchaAgenda, Deporte } from '../../domain/types';
import { BotonCerrar, Panel } from '../../ui/Panel';
import { c, campoPanel, chipFiltro, mono, primario, sans } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';

/** Slot desde el que se abrió el panel. `t` en `null` = no había lugar libre. */
export interface SlotElegido {
  courtId: string;
  t: number | null;
}

/**
 * Venta de un turno desde el mostrador.
 *
 * Las duraciones y el precio salen de los turnos vendibles que calculó el
 * backend para la celda elegida; acá no se calcula nada.
 */
export function NuevaReservaPanel({
  deporte,
  dia,
  elegido,
  canchas,
  onCerrar,
}: {
  deporte: Deporte;
  dia: number;
  elegido: SlotElegido;
  canchas: CanchaAgenda[];
  onCerrar: () => void;
}) {
  const avisar = useTostada();
  const crear = useCrearReserva();

  const [courtId, setCourtId] = useState(elegido.courtId);
  const [dur, setDur] = useState<number | null>(null);
  const [nombre, setNombre] = useState('');
  const [tel, setTel] = useState('');

  const t = elegido.t;
  const cancha = canchas.find((x) => x.courtId === courtId);
  const opciones = t == null ? [] : (cancha?.turnos ?? []).filter((s) => s.t === t);
  const slot = opciones.find((s) => s.dur === dur) ?? opciones[0] ?? null;

  const disponibles = t == null ? [] : canchas.filter((x) => x.turnos.some((s) => s.t === t));

  const listo = slot != null && nombre.trim().length > 0 && !crear.isPending;

  return (
    <Panel onCerrar={onCerrar}>
      <div
        style={{
          flex: 'none',
          padding: '20px 20px 16px',
          display: 'flex',
          alignItems: 'flex-start',
          justifyContent: 'space-between',
          gap: 12,
          borderBottom: `1px solid ${c.linea}`,
        }}
      >
        <div>
          <div style={{ font: `500 19px ${sans}`, letterSpacing: '-.025em' }}>Nueva reserva</div>
          <div style={{ font: `400 12px ${sans}`, color: c.textoGris, marginTop: 5 }}>
            {t == null
              ? 'No queda lugar libre este día'
              : `${etiquetaDia(dia)} · ${hhmm(t)} · ${deporte === 'padel' ? 'Pádel' : 'Fútbol 5'}`}
          </div>
        </div>
        <BotonCerrar onClick={onCerrar} />
      </div>

      <div
        style={{
          flex: 1,
          minHeight: 0,
          overflowY: 'auto',
          padding: '18px 20px',
          display: 'flex',
          flexDirection: 'column',
          gap: 16,
        }}
      >
        <div>
          <Rotulo>DURACIÓN</Rotulo>
          <div style={{ display: 'flex', gap: 7 }}>
            {opciones.map((s) => (
              <button
                key={s.dur}
                type="button"
                onClick={() => setDur(s.dur)}
                style={chipFiltro(slot?.dur === s.dur)}
              >
                {duracionTurno(s.dur)}
              </button>
            ))}
          </div>
          <div style={{ font: `400 11px ${sans}`, color: c.textoTenue, marginTop: 8 }}>
            {opciones.length === 0
              ? 'No hay turnos vendibles a esa hora en esta cancha.'
              : slot && slot.dur > 60
                ? `Bloque de ${duracionTurno(slot.dur)} seguidas en la misma cancha, un solo cobro.`
                : 'Turno simple de 1 hora.'}
          </div>
        </div>

        <div>
          <Rotulo>CANCHA</Rotulo>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 7 }}>
            {canchas.map((x) => {
              const posible = disponibles.some((d) => d.courtId === x.courtId);
              return (
                <button
                  key={x.courtId}
                  type="button"
                  onClick={posible ? () => setCourtId(x.courtId) : undefined}
                  style={chipFiltro(courtId === x.courtId, !posible)}
                >
                  {x.nombre}
                </button>
              );
            })}
          </div>
        </div>

        <div>
          <Rotulo>A NOMBRE DE</Rotulo>
          <input
            type="text"
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
            placeholder="Nombre y apellido"
            className="f-borde"
            style={campoPanel()}
          />
        </div>

        <div>
          <Rotulo>TELÉFONO (OPCIONAL)</Rotulo>
          <input
            type="tel"
            value={tel}
            onChange={(e) => setTel(e.target.value)}
            placeholder="362 ..."
            className="f-borde"
            style={campoPanel()}
          />
        </div>
      </div>

      <div
        style={{
          flex: 'none',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          padding: '13px 20px',
          borderTop: `1px solid ${c.linea}`,
        }}
      >
        <span style={{ font: `400 11.5px ${mono}`, color: c.textoTenue }}>
          {slot == null ? '' : `${duracionTurno(slot.dur)} · ${pesos(slot.precio)}`}
        </span>
        <div style={{ flex: 1 }} />
        <button
          type="button"
          className={listo ? 'h-primario' : undefined}
          onClick={() => {
            if (!listo || t == null || slot == null) return;
            const persona = nombre.trim();
            crear.mutate(
              {
                courtId,
                fecha: isoDe(dia),
                t,
                dur: slot.dur,
                nombre: persona,
                tel: tel.trim() || null,
              },
              {
                onSuccess: () => {
                  avisar('Turno confirmado · ' + persona);
                  onCerrar();
                },
              },
            );
          }}
          style={{ ...primario(listo), padding: '0 14px' }}
        >
          Confirmar
        </button>
      </div>
    </Panel>
  );
}

function Rotulo({ children }: { children: string }) {
  return (
    <div
      style={{
        font: `400 10.5px ${mono}`,
        color: c.textoTenue,
        letterSpacing: '.08em',
        marginBottom: 8,
      }}
    >
      {children}
    </div>
  );
}
