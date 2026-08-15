import { useState } from 'react';
import { useCrearReserva, usePersonas, usePresupuesto } from '../../api/queries';
import type { Ocupacion } from '../../domain/agenda';
import { libreEn } from '../../domain/agenda';
import { pesos } from '../../domain/dinero';
import { duracionTurno, etiquetaDia, hhmm } from '../../domain/fechas';
import type { ColumnaAgenda, Deporte, Pago } from '../../domain/types';
import { BotonCerrar, Panel } from '../../ui/Panel';
import { c, campoPanel, chipFiltro, mono, primario, sans } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';

/** Slot desde el que se abrió el panel. `t` en `null` = no había lugar libre. */
export interface SlotElegido {
  ci: number;
  t: number | null;
  /** Explicación de por qué se cambió de cancha, si hubo que cambiarla. */
  aviso: string | null;
}

/** Duraciones que ofrece cada deporte en el mostrador. */
const DURACIONES: Record<Deporte, number[]> = {
  padel: [60, 90, 120],
  futbol: [60, 120],
};

/**
 * Venta de un turno desde el mostrador.
 *
 * El orden de los campos es el de la conversación real: cuánto tiempo, en qué
 * cancha, a nombre de quién y cuánto se cobra ahora. Confirmar queda apagado
 * hasta que las cuatro respuestas están.
 */
export function NuevaReservaPanel({
  deporte,
  dia,
  elegido,
  columnas,
  ocupacion,
  onCerrar,
}: {
  deporte: Deporte;
  dia: number;
  elegido: SlotElegido;
  columnas: ColumnaAgenda[];
  ocupacion: Ocupacion;
  onCerrar: () => void;
}) {
  const avisar = useTostada();
  const crear = useCrearReserva();

  const [dur, setDur] = useState(60);
  const [ci, setCi] = useState(elegido.ci);
  const [busqueda, setBusqueda] = useState('');
  const [personaId, setPersonaId] = useState<number | null>(null);
  const [pago, setPago] = useState<Pago>('total');

  const t = elegido.t;
  const { data: presupuesto } = usePresupuesto(deporte, ci, t, dur);
  const precio = presupuesto?.precio ?? 0;
  const anticipo = presupuesto?.sena ?? 0;

  const buscando = busqueda.trim().length > 1 && personaId == null;
  const { data: encontradas } = usePersonas({
    q: buscando ? busqueda.trim() : '',
    filtro: 'todas',
    pagina: 0,
  });
  const coincidencias = buscando ? (encontradas?.items ?? []).slice(0, 4) : [];

  const entraUnaHora = libreEn(ocupacion, ci, t, 60);
  const listo = libreEn(ocupacion, ci, t, dur) && (personaId != null || busqueda.trim().length > 2);

  const nota = !entraUnaHora
    ? 'No hay una hora seguida libre en esta cancha a esa hora. Elegí otra cancha u otro horario.'
    : dur > 60
      ? `Bloque de ${duracionTurno(dur)} seguidas en la misma cancha, un solo cobro.`
      : 'Turno simple de 1 hora.';

  const cobros: { id: Pago; label: string; sub: string; monto: number }[] = [
    { id: 'total', label: 'Pago total ahora', sub: 'Queda cerrado, sin saldo.', monto: precio },
    { id: 'sena', label: 'Seña 50%', sub: 'El resto se cobra en el club.', monto: anticipo },
    { id: 'nada', label: 'Sin cobrar todavía', sub: 'Queda como turno a cobrar.', monto: 0 },
  ];

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
          {elegido.aviso && (
            <div
              style={{
                marginTop: 9,
                padding: '9px 11px',
                borderRadius: 9,
                background: c.ambarFondo,
                border: `1px solid ${c.ambarBorde}`,
                font: `400 11.5px/1.5 ${sans}`,
                color: c.ambarTexto,
              }}
            >
              {elegido.aviso}
            </div>
          )}
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
            {DURACIONES[deporte].map((d) => {
              const posible = libreEn(ocupacion, ci, t, d);
              return (
                <button
                  key={d}
                  type="button"
                  onClick={posible ? () => setDur(d) : undefined}
                  style={chipFiltro(dur === d, !posible)}
                >
                  {duracionTurno(d)}
                </button>
              );
            })}
          </div>
          <div style={{ font: `400 11px ${sans}`, color: c.textoTenue, marginTop: 8 }}>{nota}</div>
        </div>

        <div>
          <Rotulo>CANCHA</Rotulo>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 7 }}>
            {columnas.map((col) => {
              const posible = libreEn(ocupacion, col.ci, t, dur);
              return (
                <button
                  key={col.ci}
                  type="button"
                  onClick={posible ? () => setCi(col.ci) : undefined}
                  style={chipFiltro(ci === col.ci, !posible)}
                >
                  {col.nombre}
                </button>
              );
            })}
          </div>
        </div>

        <div>
          <Rotulo>A NOMBRE DE</Rotulo>
          <input
            type="text"
            value={busqueda}
            onChange={(e) => {
              setBusqueda(e.target.value);
              setPersonaId(null);
            }}
            placeholder="Buscar en la base o escribir un nombre"
            className="f-borde"
            style={campoPanel()}
          />
          {coincidencias.length > 0 && (
            <div
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: 1,
                marginTop: 8,
                border: `1px solid ${c.borde}`,
                borderRadius: 9,
                overflow: 'hidden',
              }}
            >
              {coincidencias.map((p) => (
                <button
                  key={p.id}
                  type="button"
                  onClick={() => {
                    setPersonaId(p.id);
                    setBusqueda(p.nombre);
                  }}
                  className="h-fondo"
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 10,
                    width: '100%',
                    minHeight: 40,
                    padding: '0 12px',
                    border: 'none',
                    borderBottom: `1px solid ${c.linea}`,
                    background: c.panel,
                    cursor: 'pointer',
                    textAlign: 'left',
                  }}
                >
                  <span
                    style={{
                      flex: 1,
                      minWidth: 0,
                      font: `500 12.5px ${sans}`,
                      color: c.tinta,
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                    }}
                  >
                    {p.nombre}
                  </span>
                  <span style={{ flex: 'none', font: `400 11.5px ${mono}`, color: c.textoGris }}>
                    {p.tel}
                  </span>
                </button>
              ))}
            </div>
          )}
          {personaId == null && busqueda.trim().length > 2 && coincidencias.length === 0 && (
            <div style={{ marginTop: 8, font: `400 11.5px ${sans}`, color: c.textoGris }}>
              No está en la base. Se va a crear la ficha con este nombre.
            </div>
          )}
        </div>

        <div>
          <Rotulo>COBRO</Rotulo>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
            {cobros.map((p) => {
              const on = pago === p.id;
              return (
                <button
                  key={p.id}
                  type="button"
                  onClick={() => setPago(p.id)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 11,
                    width: '100%',
                    minHeight: 56,
                    padding: '10px 13px',
                    borderRadius: 10,
                    cursor: 'pointer',
                    border: `1px solid ${on ? c.verdeBordeSuave : c.borde}`,
                    background: on ? c.verdeFondoSuave : c.panel,
                  }}
                >
                  <span
                    style={{
                      width: 16,
                      height: 16,
                      borderRadius: '50%',
                      flex: 'none',
                      border: on ? `5px solid ${c.verde}` : `1.5px solid ${c.bordeCasilla}`,
                      background: 'transparent',
                    }}
                  />
                  <span style={{ flex: 1, textAlign: 'left' }}>
                    <span style={{ display: 'block', font: `500 12.5px ${sans}`, color: c.tinta }}>
                      {p.label}
                    </span>
                    <span
                      style={{
                        display: 'block',
                        font: `400 11px ${sans}`,
                        color: c.textoGris,
                        marginTop: 2,
                      }}
                    >
                      {p.sub}
                    </span>
                  </span>
                  <span style={{ flex: 'none', font: `500 13px ${mono}`, color: c.verde }}>
                    {p.monto === 0 ? '—' : pesos(p.monto)}
                  </span>
                </button>
              );
            })}
          </div>
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
          {t == null ? '' : `${duracionTurno(dur)} · ${pesos(precio)}`}
        </span>
        <div style={{ flex: 1 }} />
        <button
          type="button"
          className={listo ? 'h-primario' : undefined}
          onClick={() => {
            if (!listo || t == null) return;
            crear.mutate(
              {
                deporte,
                dateIdx: dia,
                ci,
                t,
                dur,
                personaId,
                nombre: busqueda,
                pago,
              },
              {
                onSuccess: (r) => {
                  avisar('Turno confirmado · ' + r.persona);
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
