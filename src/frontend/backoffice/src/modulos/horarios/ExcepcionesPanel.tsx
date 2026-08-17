import { useState } from 'react';
import {
  useBorrarExcepcion,
  useCanchas,
  useCrearExcepcion,
  useExcepciones,
} from '../../api/queries';
import { fechaLarga, hhmm, isoDe } from '../../domain/fechas';
import { tramoMalo } from '../../domain/horarios';
import type { Tramo } from '../../domain/types';
import { Cargando } from '../../ui/Cargando';
import { c, campo, chipOpcion, fantasma, mono, primario, rotulo, sans, selectAncho } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';
import { SelectHora } from './SelectHora';

type TipoExcepcion = 'cerrado' | 'horario';

const bloquesIniciales = (): Tramo[] => [[480, 1200]];

/**
 * Excepciones de disponibilidad: fechas concretas que pisan el patrón semanal,
 * para todo el club o para una cancha. Cerrado o con un horario propio.
 */
export function ExcepcionesPanel() {
  const avisar = useTostada();
  const desde = isoDe(0);
  const hasta = isoDe(90);
  const { data: excepciones, isLoading } = useExcepciones(desde, hasta);
  const { data: canchas } = useCanchas();
  const crear = useCrearExcepcion();
  const borrar = useBorrarExcepcion();

  const [alcance, setAlcance] = useState('club');
  const [fecha, setFecha] = useState('');
  const [fechas, setFechas] = useState<string[]>([]);
  const [tipo, setTipo] = useState<TipoExcepcion>('cerrado');
  const [tramos, setTramos] = useState<Tramo[]>(bloquesIniciales);
  const [motivo, setMotivo] = useState('');

  if (!excepciones || !canchas) return isLoading ? <Cargando que="las excepciones" /> : null;

  const nombreAlcance = (courtId: string | null) =>
    courtId == null ? 'Todo el club' : (canchas.find((x) => x.id === courtId)?.nombre ?? 'Cancha');

  const bloquesInvalidos =
    tipo === 'horario' &&
    (tramos.length === 0 || tramos.some((_, i) => tramoMalo(tramos, i) != null));
  const puedeCrear = fechas.length > 0 && !bloquesInvalidos && !crear.isPending;

  const agregarFecha = () => {
    if (!fecha) return avisar('Elegí una fecha primero');
    if (fechas.includes(fecha)) return avisar('Esa fecha ya está en la lista');
    setFechas([...fechas, fecha].sort());
  };

  const agregarBloque = () => {
    const ultimo = tramos.length ? tramos[tramos.length - 1][1] : 480;
    const inicio = Math.min(ultimo + 60, 23 * 60);
    setTramos([...tramos, [inicio, Math.min(inicio + 180, 24 * 60)]]);
  };

  const limpiar = () => {
    setAlcance('club');
    setFecha('');
    setFechas([]);
    setTipo('cerrado');
    setTramos(bloquesIniciales());
    setMotivo('');
  };

  const crearExcepcion = () => {
    if (!puedeCrear) return;
    crear.mutate(
      {
        courtId: alcance === 'club' ? null : alcance,
        fechas,
        tramos: tipo === 'cerrado' ? [] : tramos,
        motivo: motivo.trim() || null,
      },
      {
        onSuccess: () => {
          limpiar();
          avisar('Excepción creada');
        },
      },
    );
  };

  const ordenadas = [...excepciones].sort((a, b) =>
    ([...a.fechas].sort()[0] ?? '').localeCompare([...b.fechas].sort()[0] ?? ''),
  );

  return (
    <div style={{ flex: 1, minHeight: 0, overflow: 'auto', padding: '20px 26px 26px' }}>
      <div style={{ maxWidth: 720 }}>
        <Titulo>Próximas excepciones</Titulo>
        <Copia>Hasta 90 días hacia adelante. Cada una pisa el patrón semanal en sus fechas.</Copia>
        {ordenadas.length === 0 ? (
          <div style={{ font: `400 12.5px ${sans}`, color: c.textoApagado, padding: '10px 0' }}>
            No hay excepciones cargadas.
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {ordenadas.map((x) => {
              const cerrada = x.tramos.length === 0;
              return (
                <div
                  key={x.id}
                  style={{
                    display: 'flex',
                    gap: 12,
                    alignItems: 'flex-start',
                    padding: '12px 14px',
                    borderRadius: 11,
                    border: `1px solid ${c.borde}`,
                    background: c.blanco,
                  }}
                >
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ display: 'flex', alignItems: 'baseline', gap: 9, flexWrap: 'wrap' }}>
                      <span style={{ font: `500 13px ${sans}`, color: c.tinta }}>
                        {nombreAlcance(x.courtId)}
                      </span>
                      <span
                        style={{
                          font: `400 11.5px ${mono}`,
                          color: cerrada ? c.naranja : c.textoTenue2,
                        }}
                      >
                        {cerrada
                          ? 'Cerrado'
                          : x.tramos.map((t) => `${hhmm(t[0])}–${hhmm(t[1])}`).join(' · ')}
                      </span>
                    </div>
                    <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 8 }}>
                      {[...x.fechas].sort().map((f) => (
                        <span key={f} style={chipFecha}>
                          {fechaLarga(f)}
                        </span>
                      ))}
                    </div>
                    {x.motivo && (
                      <div style={{ font: `400 12px ${sans}`, color: c.textoGris, marginTop: 7 }}>
                        {x.motivo}
                      </div>
                    )}
                  </div>
                  <button
                    type="button"
                    aria-label="Borrar excepción"
                    className="h-quitar"
                    onClick={() => borrar.mutate(x.id)}
                    style={botonQuitar}
                  >
                    −
                  </button>
                </div>
              );
            })}
          </div>
        )}

        <Titulo margen>Nueva excepción</Titulo>
        <Copia>
          Elegí el alcance, sumá una o varias fechas y decidí si esos días se cierra o se abre con
          un horario propio.
        </Copia>
        <div
          style={{
            border: `1px solid ${c.borde}`,
            borderRadius: 12,
            padding: '16px 16px 18px',
            background: c.blanco,
          }}
        >
          <Rotulo>ALCANCE</Rotulo>
          <select
            value={alcance}
            onChange={(e) => setAlcance(e.target.value)}
            aria-label="Alcance de la excepción"
            style={selectAncho()}
          >
            <option value="club">Todo el club</option>
            {canchas.map((x) => (
              <option key={x.id} value={x.id}>
                {x.nombre} · {x.deporte === 'padel' ? 'pádel' : 'fútbol'}
              </option>
            ))}
          </select>

          <div style={{ marginTop: 18 }}>
            <Rotulo>FECHAS</Rotulo>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
              <input
                type="date"
                value={fecha}
                onChange={(e) => setFecha(e.target.value)}
                aria-label="Fecha a agregar"
                style={{ ...campo(), width: 170 }}
              />
              <button type="button" className="h-ghost" onClick={agregarFecha} style={fantasma()}>
                Agregar fecha
              </button>
            </div>
            {fechas.length > 0 && (
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 10 }}>
                {fechas.map((f) => (
                  <span
                    key={f}
                    style={{ ...chipFecha, display: 'inline-flex', alignItems: 'center', gap: 6 }}
                  >
                    {fechaLarga(f)}
                    <button
                      type="button"
                      aria-label={`Quitar ${fechaLarga(f)}`}
                      onClick={() => setFechas(fechas.filter((x) => x !== f))}
                      style={quitarChip}
                    >
                      ×
                    </button>
                  </span>
                ))}
              </div>
            )}
          </div>

          <div style={{ marginTop: 18 }}>
            <Rotulo>ESOS DÍAS</Rotulo>
            <div style={{ display: 'flex', gap: 7 }}>
              <button type="button" onClick={() => setTipo('cerrado')} style={chipOpcion(tipo === 'cerrado')}>
                Cerrado
              </button>
              <button type="button" onClick={() => setTipo('horario')} style={chipOpcion(tipo === 'horario')}>
                Horario propio
              </button>
            </div>
            {tipo === 'horario' && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 7, marginTop: 12 }}>
                {tramos.map((t, ti) => {
                  const error = tramoMalo(tramos, ti);
                  return (
                    <div key={ti} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <SelectHora
                        valor={t[0]}
                        error={!!error}
                        label="Desde"
                        onCambiar={(v) =>
                          setTramos(tramos.map((x, k) => (k === ti ? [v, x[1]] : x)) as Tramo[])
                        }
                      />
                      <span style={{ font: `400 12px ${mono}`, color: c.textoGris2 }}>a</span>
                      <SelectHora
                        valor={t[1]}
                        error={!!error}
                        label="Hasta"
                        onCambiar={(v) =>
                          setTramos(tramos.map((x, k) => (k === ti ? [x[0], v] : x)) as Tramo[])
                        }
                      />
                      <button
                        type="button"
                        aria-label="Quitar bloque"
                        className="h-quitar"
                        onClick={() => setTramos(tramos.filter((_, k) => k !== ti))}
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
                  onClick={agregarBloque}
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
              </div>
            )}
          </div>

          <div style={{ marginTop: 18 }}>
            <Rotulo>MOTIVO (OPCIONAL)</Rotulo>
            <input
              type="text"
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              placeholder="feriado, mantenimiento…"
              aria-label="Motivo de la excepción"
              style={{ ...campo(), maxWidth: 340 }}
            />
          </div>

          <div style={{ marginTop: 20 }}>
            <button
              type="button"
              className={puedeCrear ? 'h-primario' : undefined}
              onClick={crearExcepcion}
              style={primario(puedeCrear)}
            >
              Crear excepción
            </button>
          </div>
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
  return <div style={{ ...rotulo(), marginBottom: 8 }}>{children}</div>;
}

const chipFecha = {
  font: `400 11.5px ${mono}`,
  padding: '3px 9px',
  borderRadius: 7,
  background: c.ambarFondo,
  border: `1px solid ${c.ambarBorde}`,
  color: c.ambarTexto,
  whiteSpace: 'nowrap',
} as const;

const quitarChip = {
  border: 'none',
  background: 'transparent',
  color: c.ambarTexto,
  cursor: 'pointer',
  padding: 0,
  font: `500 12px ${sans}`,
  lineHeight: 1,
} as const;

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
