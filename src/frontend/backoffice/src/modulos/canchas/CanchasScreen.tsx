import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useCanchas, useGuardarCanchas, useHorarios } from '../../api/queries';
import { pesos } from '../../domain/dinero';
import { AHORA, DIA_CORTO, duracionTurno, etiquetaDia, fechaDe, hhmm } from '../../domain/fechas';
import { arranquesFecha, resumenSemanal, turnosPorSemana } from '../../domain/horarios';
import type { Cancha, Deporte, Horario } from '../../domain/types';
import { useParamsSeleccion } from '../../rutas';
import { Cargando } from '../../ui/Cargando';
import { c, campo, chipOpcion, fantasma, mono, sans, selectAncho } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';

const GRUPOS: { deporte: Deporte; label: string }[] = [
  { deporte: 'padel', label: 'PÁDEL' },
  { deporte: 'futbol', label: 'FÚTBOL 5' },
];

const DURACIONES = [60, 90, 120];
const INCREMENTOS = [
  { v: 30, label: 'Cada 30 min' },
  { v: 60, label: 'En punto' },
];
const AVISOS = [
  { v: 0, label: 'Sin mínimo' },
  { v: 120, label: '2 h antes' },
  { v: 720, label: '12 h antes' },
];
/** La tarifa nocturna puede arrancar entre las 16 y las 22. */
const HORAS_NOCHE = Array.from({ length: (22 - 16) * 2 + 1 }, (_, i) => 16 * 60 + i * 30);

/**
 * Configuración de una cancha: qué horario usa, qué turnos ofrece y a qué
 * precio. La vista previa de abajo muestra el resultado —los horarios que va a
 * ver el jugador— para no tener que deducirlo de las reglas.
 */
export function CanchasScreen() {
  const avisar = useTostada();
  const navegar = useNavigate();
  const { data: guardadas, isLoading } = useCanchas();
  const { data: horarios } = useHorarios();
  const guardar = useGuardarCanchas();
  const { sel, setSel } = useParamsSeleccion();

  const [borrador, setBorrador] = useState<Cancha[] | null>(null);
  const [diaPrevia, setDiaPrevia] = useState(1);

  const canchas = borrador ?? guardadas;
  if (!canchas || !horarios) return isLoading ? <Cargando que="las canchas" /> : null;

  const i = Math.min(sel, canchas.length - 1);
  const cancha = canchas[i];
  const horario: Horario = horarios.find((h) => h.id === cancha.horarioId) ?? horarios[0];
  const sucio = borrador != null;

  const parchear = (patch: Partial<Cancha>) =>
    setBorrador(canchas.map((x, k) => (k === i ? { ...x, ...patch } : x)));

  const previa = arranquesFecha(cancha, horario, diaPrevia, AHORA);

  return (
    <div style={{ flex: 1, minHeight: 0, display: 'flex' }}>
      <div
        style={{
          flex: 'none',
          width: 246,
          borderRight: `1px solid ${c.linea}`,
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        <div style={{ flex: 'none', padding: '22px 18px 12px' }}>
          <div style={{ font: `500 18px ${sans}`, letterSpacing: '-.025em' }}>Canchas</div>
          <div style={{ font: `400 11.5px ${mono}`, color: c.textoGris2, marginTop: 4 }}>
            {canchas.length} canchas · pádel y fútbol 5
          </div>
        </div>
        <div style={{ flex: 1, overflowY: 'auto', padding: '0 12px 16px' }}>
          {GRUPOS.map((g) => (
            <div key={g.deporte}>
              <div
                style={{
                  font: `400 10px ${mono}`,
                  color: c.textoGris2,
                  letterSpacing: '.1em',
                  padding: '8px 8px 7px',
                }}
              >
                {g.label}
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 3, marginBottom: 6 }}>
                {canchas
                  .map((x, k) => ({ x, k }))
                  .filter((o) => o.x.deporte === g.deporte)
                  .map((o) => {
                    const on = i === o.k;
                    const suyo = horarios.find((h) => h.id === o.x.horarioId) ?? horarios[0];
                    return (
                      <button
                        key={o.k}
                        type="button"
                        onClick={() => setSel(o.k)}
                        style={{
                          display: 'block',
                          width: '100%',
                          padding: '10px 11px',
                          borderRadius: 10,
                          cursor: 'pointer',
                          border: `1px solid ${on ? c.verdeBordeSuave : 'transparent'}`,
                          background: on ? c.verdeFondo : 'transparent',
                          textAlign: 'left',
                        }}
                      >
                        <span style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
                          <span
                            style={{
                              width: 6,
                              height: 6,
                              borderRadius: '50%',
                              flex: 'none',
                              background: o.x.activa ? c.verdePunto : c.puntoApagado,
                            }}
                          />
                          <span style={{ font: `500 13px ${sans}`, color: c.tinta }}>
                            {o.x.nombre}
                          </span>
                          {!o.x.activa && (
                            <span style={{ font: `400 10px ${mono}`, color: c.textoApagado }}>
                              off
                            </span>
                          )}
                        </span>
                        <span
                          style={{
                            display: 'block',
                            font: `400 10.5px ${mono}`,
                            color: c.textoGris2,
                            marginTop: 5,
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                        >
                          {suyo.nombre}
                        </span>
                      </button>
                    );
                  })}
              </div>
            </div>
          ))}
        </div>
      </div>

      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <div
          style={{
            flex: 'none',
            padding: '22px 26px 16px',
            borderBottom: `1px solid ${c.linea}`,
            display: 'flex',
            alignItems: 'flex-start',
            justifyContent: 'space-between',
            gap: 18,
            flexWrap: 'wrap',
          }}
        >
          <div>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 9 }}>
              <span style={{ font: `500 22px ${sans}`, letterSpacing: '-.03em' }}>
                {cancha.nombre}
              </span>
              <span style={{ font: `400 12px ${mono}`, color: c.textoGris2 }}>
                {cancha.deporte === 'padel' ? 'pádel' : 'fútbol 5'}
              </span>
            </div>
            <div style={{ font: `400 12.5px ${sans}`, color: c.textoTenue2, marginTop: 5 }}>
              {cancha.detalle} · {pesos(cancha.precioDia)} día · {pesos(cancha.precioNoche)} noche
            </div>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
            <div style={{ textAlign: 'right' }}>
              <div
                style={{
                  font: `500 20px ${mono}`,
                  letterSpacing: '-.03em',
                  color: c.tinta,
                }}
              >
                {turnosPorSemana(cancha, horario)}
              </div>
              <div style={{ font: `400 10.5px ${mono}`, color: c.textoGris2, letterSpacing: '.06em' }}>
                TURNOS / SEMANA
              </div>
            </div>
            <button
              type="button"
              onClick={() => parchear({ activa: !cancha.activa })}
              style={{
                minHeight: 34,
                padding: '0 13px',
                borderRadius: 8,
                cursor: 'pointer',
                border: `1px solid ${cancha.activa ? c.verdeBordeSuave : c.bordeFirme}`,
                background: cancha.activa ? c.verdeFondo : c.blanco,
                color: cancha.activa ? c.verde : c.textoTenue2,
                font: `500 12.5px ${sans}`,
              }}
            >
              {cancha.activa ? 'Activa' : 'Desactivada'}
            </button>
          </div>
        </div>

        <div style={{ flex: 1, minHeight: 0, overflow: 'auto', padding: '20px 26px 26px' }}>
          <Titulo>Horario que usa</Titulo>
          <Copia>
            El mismo horario puede estar en varias canchas. Si lo cambiás, cambia en todas.
          </Copia>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            <select
              value={cancha.horarioId}
              onChange={(e) => parchear({ horarioId: e.target.value })}
              aria-label="Horario de la cancha"
              style={selectAncho()}
            >
              {horarios.map((h) => (
                <option key={h.id} value={h.id}>
                  {h.nombre}
                </option>
              ))}
            </select>
            <button
              type="button"
              className="h-ghost"
              onClick={() => {
                const k = horarios.findIndex((h) => h.id === cancha.horarioId);
                navegar(`/horarios?sel=${k < 0 ? 0 : k}&vista=lista`);
              }}
              style={fantasma()}
            >
              Editar horario
            </button>
          </div>
          <div style={{ font: `400 11.5px ${mono}`, color: c.textoGris2, marginTop: 10 }}>
            {resumenSemanal(horario)} · {horario.fechas.length} fechas propias
          </div>

          <Titulo margen>Reglas del turno</Titulo>
          <Copia>
            Se aplican sobre el horario: definen qué puede elegir el jugador y cada cuánto arrancan
            los turnos.
          </Copia>
          <div style={{ display: 'flex', gap: 26, flexWrap: 'wrap' }}>
            <div>
              <Rotulo>DURACIONES</Rotulo>
              <div style={{ display: 'flex', gap: 7 }}>
                {DURACIONES.map((d) => {
                  const on = cancha.duraciones.includes(d);
                  return (
                    <button
                      key={d}
                      type="button"
                      onClick={() => {
                        const siguiente = on
                          ? cancha.duraciones.filter((x) => x !== d)
                          : [...cancha.duraciones, d].sort((a, b) => a - b);
                        if (siguiente.length) parchear({ duraciones: siguiente });
                        else avisar('Tiene que quedar al menos una duración');
                      }}
                      style={chipOpcion(on)}
                    >
                      {duracionTurno(d)}
                    </button>
                  );
                })}
              </div>
            </div>
            <div>
              <Rotulo>INCREMENTO DE INICIO</Rotulo>
              <div style={{ display: 'flex', gap: 7 }}>
                {INCREMENTOS.map((x) => (
                  <button
                    key={x.v}
                    type="button"
                    onClick={() => parchear({ incremento: x.v })}
                    style={chipOpcion(cancha.incremento === x.v)}
                  >
                    {x.label}
                  </button>
                ))}
              </div>
            </div>
          </div>
          <div style={{ display: 'flex', gap: 26, flexWrap: 'wrap', marginTop: 18 }}>
            <div>
              <Rotulo>AVISO MÍNIMO</Rotulo>
              <div style={{ display: 'flex', gap: 7 }}>
                {AVISOS.map((a) => (
                  <button
                    key={a.v}
                    type="button"
                    onClick={() => parchear({ aviso: a.v })}
                    style={chipOpcion(cancha.aviso === a.v)}
                  >
                    {a.label}
                  </button>
                ))}
              </div>
            </div>
          </div>

          <Titulo margen>Precio por hora</Titulo>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginTop: 14 }}>
            <div style={{ flex: 1, minWidth: 140 }}>
              <Rotulo>DÍA</Rotulo>
              <input
                type="text"
                value={String(cancha.precioDia)}
                onChange={(e) =>
                  parchear({ precioDia: parseInt(e.target.value.replace(/[^0-9]/g, ''), 10) || 0 })
                }
                aria-label="Precio de día"
                style={campo()}
              />
            </div>
            <div style={{ flex: 1, minWidth: 140 }}>
              <Rotulo>NOCHE</Rotulo>
              <input
                type="text"
                value={String(cancha.precioNoche)}
                onChange={(e) =>
                  parchear({ precioNoche: parseInt(e.target.value.replace(/[^0-9]/g, ''), 10) || 0 })
                }
                aria-label="Precio de noche"
                style={campo()}
              />
            </div>
            <div style={{ flex: 1, minWidth: 140 }}>
              <Rotulo>LA NOCHE ARRANCA</Rotulo>
              <select
                value={cancha.noche}
                onChange={(e) => parchear({ noche: parseInt(e.target.value, 10) })}
                aria-label="Hora en que arranca la tarifa nocturna"
                style={selectAncho()}
              >
                {HORAS_NOCHE.map((h) => (
                  <option key={h} value={h}>
                    {hhmm(h)}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <Titulo margen>Vista previa</Titulo>
          <Copia>
            Los horarios que va a ofrecer la app para {cancha.nombre}, con turnos de{' '}
            {duracionTurno(Math.min(...cancha.duraciones))} y arranques{' '}
            {cancha.incremento === 60 ? 'en punto' : 'cada 30 min'}.
          </Copia>
          <div style={{ display: 'flex', gap: 7, flexWrap: 'wrap', marginBottom: 12 }}>
            {Array.from({ length: 7 }, (_, k) => (
              <button
                key={k}
                type="button"
                onClick={() => setDiaPrevia(k)}
                style={chipOpcion(diaPrevia === k)}
              >
                {k === 0 ? 'hoy' : `${DIA_CORTO[fechaDe(k).getDay()]} ${fechaDe(k).getDate()}`}
              </button>
            ))}
          </div>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {previa.map((m) => (
              <span
                key={m}
                style={{
                  font: `400 12px ${mono}`,
                  color: c.verde,
                  padding: '5px 9px',
                  borderRadius: 7,
                  background: c.verdeFondo,
                  border: `1px solid ${c.verdeBorde}`,
                }}
              >
                {hhmm(m)}
              </span>
            ))}
            {previa.length === 0 && (
              <span style={{ font: `400 12.5px ${sans}`, color: c.textoApagado }}>
                {!cancha.activa
                  ? 'La cancha está desactivada.'
                  : `Ese día (${etiquetaDia(diaPrevia)}) el horario no tiene horas disponibles.`}
              </span>
            )}
          </div>
        </div>

        <div
          style={{
            flex: 'none',
            display: 'flex',
            alignItems: 'center',
            gap: 12,
            padding: '13px 26px',
            borderTop: `1px solid ${c.linea}`,
            background: c.panel,
          }}
        >
          <span style={{ font: `400 11.5px ${mono}`, color: c.textoGris2 }}>
            {sucio ? 'cambios sin guardar' : 'todo guardado'}
          </span>
          <div style={{ flex: 1 }} />
          <button
            type="button"
            className="h-ghost"
            onClick={() => {
              setBorrador(null);
              avisar('Cambios descartados');
            }}
            style={fantasma()}
          >
            Descartar
          </button>
          <button
            type="button"
            className={sucio ? 'h-primario' : undefined}
            onClick={() => {
              if (!sucio) return;
              guardar.mutate(canchas, {
                onSuccess: () => {
                  setBorrador(null);
                  avisar('Configuración guardada');
                },
              });
            }}
            style={{
              minHeight: 34,
              padding: '0 14px',
              borderRadius: 8,
              border: 'none',
              background: sucio ? c.verde : c.linea,
              color: sucio ? c.blanco : c.textoApagado,
              font: `600 12.5px ${sans}`,
              cursor: sucio ? 'pointer' : 'default',
            }}
          >
            Guardar cambios
          </button>
        </div>
      </div>
    </div>
  );
}

function Titulo({ children, margen }: { children: string; margen?: boolean }) {
  return (
    <div
      style={{
        font: `500 14px ${sans}`,
        letterSpacing: '-.01em',
        margin: margen ? '26px 0 4px' : undefined,
      }}
    >
      {children}
    </div>
  );
}

function Copia({ children }: { children: React.ReactNode }) {
  return (
    <div style={{ font: `400 12px ${sans}`, color: c.textoGris, margin: '4px 0 12px' }}>
      {children}
    </div>
  );
}

function Rotulo({ children }: { children: string }) {
  return (
    <div
      style={{
        font: `400 10.5px ${mono}`,
        color: c.textoGris2,
        letterSpacing: '.08em',
        marginBottom: 8,
      }}
    >
      {children}
    </div>
  );
}
