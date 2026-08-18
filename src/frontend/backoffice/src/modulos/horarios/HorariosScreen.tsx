import { useState } from 'react';
import { Check } from 'lucide-react';
import { useCanchas, useGuardarHorarios, useHorarios } from '../../api/queries';
import { DIAS, MESES, duracionLarga, fechaDe, hhmm } from '../../domain/fechas';
import {
  arranques,
  resumenSemanal,
  tramoMalo,
  tramosSemana,
  turnosPorSemana,
} from '../../domain/horarios';
import type { Cancha, Horario, Tramo } from '../../domain/types';
import { useParamsSeleccion, useParamsVista, type VistaHorario } from '../../rutas';
import { Cargando } from '../../ui/Cargando';
import { c, fantasma, mono, sans } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';
import { ExcepcionesPanel } from './ExcepcionesPanel';
import { SelectHora } from './SelectHora';

/** Id provisional del borrador: el adaptador lo convierte en alta al guardar. */
function idTemporal(horarios: Horario[]): string {
  let n = 1;
  while (horarios.some((h) => h.id === 'nuevo-' + n)) n++;
  return 'nuevo-' + n;
}

/**
 * Horarios de apertura. Un horario es una plantilla que puede estar en varias
 * canchas a la vez: se edita una vez y cambia en todas, que es lo que hace el
 * club cuando cambia la temporada.
 *
 * Los cambios se acumulan en un borrador y recién se persisten al guardar.
 */
export function HorariosScreen() {
  const avisar = useTostada();
  const { data: guardados, isLoading } = useHorarios();
  const { data: canchas } = useCanchas();
  const guardar = useGuardarHorarios();
  const { sel, setSel } = useParamsSeleccion();
  const { vista, setVista } = useParamsVista();

  const [borrador, setBorrador] = useState<Horario[] | null>(null);

  const horarios = borrador ?? guardados;
  if (!horarios || !canchas) return isLoading ? <Cargando que="los horarios" /> : null;

  if (vista === 'excepciones') {
    return (
      <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
        <div
          style={{ flex: 'none', padding: '20px 26px 14px', borderBottom: `1px solid ${c.linea}` }}
        >
          <div style={{ font: `500 21px ${sans}`, letterSpacing: '-.03em' }}>Excepciones</div>
          <div style={{ font: `400 12px ${sans}`, color: c.textoGris, marginTop: 5 }}>
            Fechas concretas que pisan el patrón semanal, para todo el club o para una cancha.
          </div>
          <SegmentoVista vista={vista} setVista={setVista} />
        </div>
        <ExcepcionesPanel />
      </div>
    );
  }

  const horario = horarios.find((h) => h.id === sel) ?? horarios[0];
  const canchasDelHorario = canchas.filter((x) => x.horarioId === horario.id);
  const sucio = borrador != null;

  const escribir = (siguiente: Horario[]) => setBorrador(siguiente);
  const parchear = (patch: Partial<Horario>) =>
    escribir(horarios.map((s) => (s.id === horario.id ? { ...s, ...patch } : s)));
  const setSemanal = (dow: number, tramos: Tramo[]) =>
    parchear({ semanal: { ...horario.semanal, [dow]: tramos } });

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
          <div style={{ font: `500 18px ${sans}`, letterSpacing: '-.025em' }}>Horarios</div>
          <div style={{ font: `400 11.5px ${mono}`, color: c.textoGris2, marginTop: 4 }}>
            {horarios.length} horarios · {canchas.length} canchas
          </div>
        </div>
        <div style={{ flex: 1, overflowY: 'auto', padding: '0 12px' }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
            {horarios.map((s) => {
              const on = s.id === horario.id;
              const n = canchas.filter((x) => x.horarioId === s.id).length;
              return (
                <button
                  key={s.id}
                  type="button"
                  onClick={() => setSel(s.id)}
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
                  <span style={{ display: 'flex', alignItems: 'baseline', gap: 7 }}>
                    <span
                      style={{
                        flex: 1,
                        minWidth: 0,
                        font: `500 13px ${sans}`,
                        color: c.tinta,
                        whiteSpace: 'nowrap',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                      }}
                    >
                      {s.nombre}
                    </span>
                    <span style={{ flex: 'none', font: `400 10.5px ${mono}`, color: c.textoGris2 }}>
                      {n} {n === 1 ? 'cancha' : 'canchas'}
                    </span>
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
                    {resumenSemanal(s)}
                  </span>
                </button>
              );
            })}
          </div>
        </div>
        <div style={{ flex: 'none', padding: 12 }}>
          <button
            type="button"
            onClick={() => {
              const nuevo: Horario = {
                id: idTemporal(horarios),
                nombre: 'Horario nuevo',
                semanal: { 1: [[480, 1200]], 2: [[480, 1200]], 3: [[480, 1200]], 4: [[480, 1200]], 5: [[480, 1200]] },
              };
              escribir([...horarios, nuevo]);
              setSel(nuevo.id);
              avisar('Horario creado');
            }}
            style={{
              width: '100%',
              minHeight: 36,
              borderRadius: 9,
              border: `1px dashed ${c.bordeFirme}`,
              background: 'transparent',
              color: c.textoTenue2,
              font: `500 12.5px ${sans}`,
              cursor: 'pointer',
            }}
          >
            + Crear horario
          </button>
        </div>
      </div>

      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <div style={{ flex: 'none', padding: '20px 26px 14px', borderBottom: `1px solid ${c.linea}` }}>
          <div
            style={{
              display: 'flex',
              alignItems: 'flex-start',
              justifyContent: 'space-between',
              gap: 16,
              flexWrap: 'wrap',
            }}
          >
            <div style={{ flex: 1, minWidth: 220 }}>
              <input
                type="text"
                value={horario.nombre}
                onChange={(e) => parchear({ nombre: e.target.value })}
                aria-label="Nombre del horario"
                style={{
                  width: '100%',
                  maxWidth: 340,
                  minHeight: 34,
                  padding: '0 2px',
                  border: 'none',
                  borderBottom: '1px solid transparent',
                  background: 'transparent',
                  color: c.tinta,
                  font: `500 21px ${sans}`,
                  letterSpacing: '-.03em',
                  outline: 'none',
                }}
              />
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 7,
                  marginTop: 9,
                  flexWrap: 'wrap',
                }}
              >
                <span style={{ font: `400 11.5px ${sans}`, color: c.textoGris }}>Aplicado a</span>
                {canchasDelHorario.map((x) => (
                  <span key={x.id} style={chipCancha}>
                    {x.nombre} · {x.deporte === 'padel' ? 'pádel' : 'fútbol'}
                  </span>
                ))}
                {canchasDelHorario.length === 0 && (
                  <span style={{ font: `400 11.5px ${sans}`, color: c.textoApagado }}>
                    ninguna cancha todavía
                  </span>
                )}
              </div>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
              <button
                type="button"
                className="h-ghost"
                onClick={() => {
                  const copia: Horario = {
                    ...horario,
                    id: idTemporal(horarios),
                    nombre: horario.nombre + ' (copia)',
                    version: undefined,
                  };
                  escribir([...horarios, copia]);
                  setSel(copia.id);
                  avisar('Horario duplicado');
                }}
                style={fantasma()}
              >
                Duplicar
              </button>
              <button
                type="button"
                onClick={() => {
                  if (horarios.length === 1) return avisar('Tiene que quedar al menos un horario');
                  if (canchasDelHorario.length) {
                    return avisar(
                      `Primero pasá sus ${canchasDelHorario.length} canchas a otro horario`,
                    );
                  }
                  const resto = horarios.filter((h) => h.id !== horario.id);
                  escribir(resto);
                  setSel(resto[0].id);
                  avisar('Horario eliminado');
                }}
                style={{
                  ...fantasma(),
                  border: `1px solid ${canchasDelHorario.length ? c.borde : c.naranjaBorde}`,
                  color: canchasDelHorario.length ? c.textoApagado : c.naranja,
                }}
              >
                Eliminar
              </button>
            </div>
          </div>

          <SegmentoVista vista={vista} setVista={setVista} />
        </div>

        <div style={{ flex: 1, minHeight: 0, overflow: 'auto', padding: '20px 26px 26px' }}>
          {vista === 'lista' ? (
            <>
              <div style={{ font: `500 14px ${sans}`, letterSpacing: '-.01em' }}>
                Horas semanales
              </div>
              <div style={{ font: `400 12px ${sans}`, color: c.textoGris, marginTop: 4 }}>
                Se repiten todas las semanas. Cada día puede tener varios bloques, por ejemplo de 8 a
                12 y de 13 a 17.
              </div>

              <div
                style={{
                  marginTop: 14,
                  border: `1px solid ${c.borde}`,
                  borderRadius: 12,
                  overflow: 'hidden',
                }}
              >
                {DIAS.map((d, di) => (
                  <FilaDia
                    key={d.dow}
                    label={d.label}
                    ultima={di === DIAS.length - 1}
                    tramos={horario.semanal[d.dow] || []}
                    referencia={canchasDelHorario[0]}
                    horario={horario}
                    dow={d.dow}
                    onCambiar={(tramos) => setSemanal(d.dow, tramos)}
                    onCopiar={() => {
                      const tramos = horario.semanal[d.dow] || [];
                      if (!tramos.length) return avisar('Ese día no tiene horas para copiar');
                      const semanal: Record<number, Tramo[]> = {};
                      DIAS.forEach((x) => {
                        semanal[x.dow] = tramos.map((t) => [...t] as Tramo);
                      });
                      parchear({ semanal });
                      avisar(`Horas de ${d.label.toLowerCase()} copiadas a los siete días`);
                    }}
                  />
                ))}
              </div>
            </>
          ) : (
            <VistaCalendario horario={horario} />
          )}
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
            {canchasDelHorario.length
              ? `${turnosPorSemana(canchasDelHorario[0], horario)} turnos por semana en cada cancha · ` +
                `${canchasDelHorario.length} ${canchasDelHorario.length === 1 ? 'cancha usa este horario' : 'canchas usan este horario'}`
              : 'todavía no hay canchas con este horario'}
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
              guardar.mutate(horarios, {
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

/** Un día de la semana con sus bloques de apertura. */
function FilaDia({
  label,
  ultima,
  tramos,
  referencia,
  horario,
  dow,
  onCambiar,
  onCopiar,
}: {
  label: string;
  ultima: boolean;
  tramos: Tramo[];
  referencia: Cancha | undefined;
  horario: Horario;
  dow: number;
  onCambiar: (tramos: Tramo[]) => void;
  onCopiar: () => void;
}) {
  const abierto = tramos.length > 0;
  const conteo = abierto
    ? referencia
      ? `${arranques(tramosSemana(horario, dow), referencia).length} turnos`
      : `${tramosSemana(horario, dow).length} bloques`
    : 'no disponible';

  return (
    <div
      style={{
        display: 'flex',
        gap: 12,
        alignItems: 'flex-start',
        padding: '11px 14px',
        minWidth: 408,
        borderBottom: ultima ? 'none' : `1px solid ${c.segmento}`,
        background: abierto ? c.blanco : c.panel,
      }}
    >
      <div style={{ flex: 'none', width: 118, paddingTop: 7 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <button
            type="button"
            aria-label={`Abrir ${label}`}
            onClick={() => onCambiar(abierto ? [] : [[480, 1200]])}
            style={{
              width: 17,
              height: 17,
              borderRadius: 5,
              flex: 'none',
              cursor: 'pointer',
              padding: 0,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              border: `1px solid ${abierto ? c.verde : c.bordeFirme}`,
              background: abierto ? c.verde : c.blanco,
              color: c.blanco,
              font: `600 10px ${sans}`,
              lineHeight: 1,
            }}
          >
            {abierto ? <Check size={11} strokeWidth={3} aria-hidden /> : ''}
          </button>
          <span style={{ font: `500 12.5px ${sans}`, color: abierto ? c.tinta : c.textoApagado }}>
            {label}
          </span>
        </div>
        <div style={{ font: `400 11px ${mono}`, color: c.textoGris2, margin: '5px 0 0 25px' }}>
          {conteo}
        </div>
        <button
          type="button"
          onClick={onCopiar}
          style={{
            display: abierto ? 'block' : 'none',
            margin: '7px 0 0 25px',
            padding: 0,
            border: 'none',
            background: 'transparent',
            color: c.textoTenue2,
            cursor: 'pointer',
            textAlign: 'left',
            font: `400 11px ${sans}`,
            textDecoration: 'underline',
            textDecorationColor: c.bordeFirme,
            textUnderlineOffset: '3px',
          }}
        >
          copiar a los demás
        </button>
      </div>

      <div
        style={{
          flex: 1,
          minWidth: 250,
          display: 'flex',
          flexDirection: 'column',
          gap: 7,
        }}
      >
        {abierto ? (
          <>
            {tramos.map((t, ti) => {
              const error = tramoMalo(tramos, ti);
              return (
                <div key={ti} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <SelectHora
                    valor={t[0]}
                    error={!!error}
                    onCambiar={(v) =>
                      onCambiar(tramos.map((x, k) => (k === ti ? [v, x[1]] : x)) as Tramo[])
                    }
                  />
                  <span style={{ font: `400 12px ${mono}`, color: c.textoGris2 }}>a</span>
                  <SelectHora
                    valor={t[1]}
                    error={!!error}
                    onCambiar={(v) =>
                      onCambiar(tramos.map((x, k) => (k === ti ? [x[0], v] : x)) as Tramo[])
                    }
                  />
                  <span style={{ flex: 'none', font: `400 11.5px ${mono}`, color: c.textoGris2 }}>
                    {duracionLarga(t[1] - t[0])}
                  </span>
                  <button
                    type="button"
                    aria-label="Quitar bloque"
                    className="h-quitar"
                    onClick={() => onCambiar(tramos.filter((_, k) => k !== ti))}
                    style={botonQuitar}
                  >
                    −
                  </button>
                  {error && (
                    <span style={{ font: `400 11.5px ${sans}`, color: c.naranja }}>{error}</span>
                  )}
                </div>
              );
            })}
            <button
              type="button"
              onClick={() => {
                const ultimo = tramos.length ? tramos[tramos.length - 1][1] : 480;
                const desde = Math.min(ultimo + 60, 23 * 60);
                onCambiar([...tramos, [desde, Math.min(desde + 180, 24 * 60)]]);
              }}
              style={{
                alignSelf: 'flex-start',
                minHeight: 30,
                padding: '0 10px',
                borderRadius: 8,
                cursor: 'pointer',
                border: `1px dashed ${c.bordeFirme}`,
                background: 'transparent',
                color: c.textoTenue2,
                font: `500 12px ${sans}`,
              }}
            >
              + Agregar bloque
            </button>
          </>
        ) : (
          <div style={{ font: `400 12.5px ${sans}`, color: c.textoApagado, padding: '7px 0' }}>
            No disponible
          </div>
        )}
      </div>
    </div>
  );
}

/** Las próximas dos semanas con este horario. */
function VistaCalendario({ horario }: { horario: Horario }) {
  return (
    <>
      <div style={{ font: `500 14px ${sans}`, letterSpacing: '-.01em' }}>Próximas dos semanas</div>
      <div style={{ font: `400 12px ${sans}`, color: c.textoGris, margin: '4px 0 14px' }}>
        Lo que va a estar abierto día por día con este horario.
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {Array.from({ length: 14 }, (_, k) => {
          const d = fechaDe(k);
          const tramos = tramosSemana(horario, d.getDay());
          const cerrado = tramos.length === 0;
          return (
            <div
              key={k}
              style={{
                display: 'block',
                padding: '11px 14px',
                borderRadius: 11,
                border: `1px solid ${c.borde}`,
                background: cerrado ? c.panel : c.blanco,
              }}
            >
              <div style={{ display: 'flex', alignItems: 'baseline', gap: 10 }}>
                <span style={{ flex: 'none', font: `400 12.5px ${mono}`, color: c.tinta }}>
                  {d.getDate()} {MESES[d.getMonth()]}
                </span>
                <span
                  style={{ flex: 1, minWidth: 0, font: `400 12px ${sans}`, color: c.textoTenue2 }}
                >
                  {DIAS.find((x) => x.dow === d.getDay())?.label}
                </span>
                <span
                  style={{
                    flex: 'none',
                    font: `400 11px ${mono}`,
                    color: c.textoApagado,
                  }}
                >
                  {cerrado ? '—' : 'semanal'}
                </span>
              </div>
              <div
                style={{
                  font: `400 12.5px/1.5 ${mono}`,
                  color: c.textoTenue2,
                  marginTop: 5,
                  textWrap: 'pretty',
                }}
              >
                {cerrado ? 'cerrado' : tramos.map((t) => `${hhmm(t[0])}–${hhmm(t[1])}`).join(' · ')}
              </div>
            </div>
          );
        })}
      </div>
    </>
  );
}

function SegmentoVista({
  vista,
  setVista,
}: {
  vista: VistaHorario;
  setVista: (v: VistaHorario) => void;
}) {
  return (
    <div
      style={{
        display: 'flex',
        gap: 4,
        marginTop: 16,
        padding: 3,
        borderRadius: 9,
        background: c.segmento,
        width: 'fit-content',
      }}
    >
      {(
        [
          { id: 'lista', label: 'Lista' },
          { id: 'cal', label: 'Calendario' },
          { id: 'excepciones', label: 'Excepciones' },
        ] as const
      ).map((v) => {
        const on = vista === v.id;
        return (
          <button
            key={v.id}
            type="button"
            onClick={() => setVista(v.id)}
            style={{
              minHeight: 30,
              padding: '0 14px',
              borderRadius: 7,
              cursor: 'pointer',
              border: 'none',
              background: on ? c.blanco : 'transparent',
              color: on ? c.tinta : c.textoTenue2,
              font: `500 12.5px ${sans}`,
              boxShadow: on ? '0 1px 2px rgba(20,20,18,.10)' : 'none',
            }}
          >
            {v.label}
          </button>
        );
      })}
    </div>
  );
}

const botonQuitar = {
  flex: 'none',
  width: 28,
  height: 28,
  borderRadius: 7,
  border: `1px solid ${c.borde}`,
  background: c.blanco,
  color: c.textoGris,
  font: `400 13px ${sans}`,
  cursor: 'pointer',
} as const;

const chipCancha = {
  font: `500 11.5px ${sans}`,
  padding: '3px 9px',
  borderRadius: 999,
  background: c.apagado,
  border: `1px solid ${c.borde}`,
  color: c.textoDato,
  whiteSpace: 'nowrap',
} as const;
