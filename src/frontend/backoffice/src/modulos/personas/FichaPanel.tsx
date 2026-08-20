import { useState } from 'react';
import { useAgregarNota, useAlternarBloqueo, useFicha, useRegistrarPago } from '../../api/queries';
import { pesos } from '../../domain/dinero';
import { BotonCerrar, FilaDato, Panel } from '../../ui/Panel';
import { chipStyle, colorTurnoHistorico, estadoPersona, puntoStyle } from '../../ui/estados';
import { c, campoPanel, mono, primario, sans } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';

type Pestania = 'resumen' | 'turnos' | 'notas';

const PESTANIAS: { id: Pestania; label: string }[] = [
  { id: 'resumen', label: 'Resumen' },
  { id: 'turnos', label: 'Turnos' },
  { id: 'notas', label: 'Notas' },
];

/**
 * Ficha de una persona. Lo primero que se ve es si debe plata y si está
 * bloqueada, porque de eso depende si se le puede vender un turno.
 */
export function FichaPanel({ id, onCerrar }: { id: string; onCerrar: () => void }) {
  const avisar = useTostada();
  const { data } = useFicha(id);
  const agregarNota = useAgregarNota();
  const registrarPago = useRegistrarPago();
  const alternarBloqueo = useAlternarBloqueo();

  const [pestania, setPestania] = useState<Pestania>('resumen');
  const [nota, setNota] = useState('');

  if (!data) return null;
  const { persona, turnos } = data;
  const estado = estadoPersona(persona);
  const sinTurnos = persona.turnos === 0;
  const copiaSinTurnos =
    persona.origen === 'app'
      ? 'Se registró en la app pero todavía no reservó. Sirve para avisarle cuando quedan horarios libres.'
      : 'La cargó el club en el mostrador. Todavía no tiene turnos asociados.';

  return (
    <Panel onCerrar={onCerrar}>
      <div style={{ flex: 'none', padding: '20px 20px 0' }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 12 }}>
          <div style={{ minWidth: 0 }}>
            <div style={{ font: `400 10.5px ${mono}`, color: c.textoTenue, letterSpacing: '.08em' }}>
              PER-{1000 + persona.id}
            </div>
            <div style={{ font: `500 20px ${sans}`, letterSpacing: '-.025em', marginTop: 6 }}>
              {persona.nombre}
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginTop: 8 }}>
              <span style={puntoStyle(estado)} />
              <span style={chipStyle(estado.fg)}>{estado.label}</span>
              <span style={{ font: `400 11px ${mono}`, color: c.textoTenue }}>
                · {persona.origen === 'app' ? 'app' : 'mostrador'} · alta {persona.alta}
              </span>
            </div>
          </div>
          <BotonCerrar onClick={onCerrar} />
        </div>

        <div style={{ display: 'flex', gap: 7, marginTop: 18 }}>
          <button
            type="button"
            onClick={() => avisar('Abriendo la agenda para reservar a su nombre')}
            className="h-primario"
            style={{ ...primario(), minHeight: 32, padding: '0 12px', font: `600 12px ${sans}` }}
          >
            Reservar turno
          </button>
          <button
            type="button"
            onClick={() => avisar('Abriendo WhatsApp')}
            className="h-ghost"
            style={botonSecundarioPanel}
          >
            WhatsApp
          </button>
          <div style={{ flex: 1 }} />
          <button
            type="button"
            onClick={() => avisar('Edición de datos')}
            className="h-ghost"
            style={{ ...botonSecundarioPanel, padding: '0 10px' }}
          >
            Editar
          </button>
        </div>

        <div style={{ display: 'flex', gap: 16, marginTop: 20, borderBottom: `1px solid ${c.linea}` }}>
          {PESTANIAS.map((t) => {
            const on = pestania === t.id;
            return (
              <button
                key={t.id}
                type="button"
                onClick={() => setPestania(t.id)}
                style={{
                  border: 'none',
                  background: 'transparent',
                  cursor: 'pointer',
                  padding: '0 0 10px',
                  font: `500 12.5px ${sans}`,
                  color: on ? c.titulo : c.textoTenue,
                  borderBottom: `1px solid ${on ? c.acento : 'transparent'}`,
                }}
              >
                {t.label}
              </button>
            );
          })}
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '18px 20px 22px' }}>
        {pestania === 'resumen' && (
          <>
            {persona.deuda > 0 && (
              <div
                style={{
                  padding: '14px 15px',
                  borderRadius: 11,
                  background: c.naranjaFondo,
                  border: `1px solid ${c.naranjaBorde}`,
                  marginBottom: 12,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: 12,
                }}
              >
                <div>
                  <div style={{ font: `400 10.5px ${mono}`, color: c.naranja, letterSpacing: '.08em' }}>
                    SALDO PENDIENTE
                  </div>
                  <div
                    style={{
                      font: `500 22px ${mono}`,
                      color: c.naranja,
                      letterSpacing: '-.03em',
                      marginTop: 4,
                    }}
                  >
                    {pesos(persona.deuda)}
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() =>
                    registrarPago.mutate(persona.id, { onSuccess: () => avisar('Pago registrado') })
                  }
                  style={botonNaranja}
                >
                  Registrar pago
                </button>
              </div>
            )}

            <div
              style={{
                display: 'grid',
                gridTemplateColumns: '1fr 1fr',
                gap: 8,
                marginBottom: 14,
              }}
            >
              {[
                { k: 'TURNOS', v: String(persona.turnos) },
                { k: 'ÚLTIMA VEZ', v: persona.ultima || '—' },
                { k: 'SALDO', v: persona.deuda > 0 ? pesos(persona.deuda) : 'al día' },
              ].map((s) => (
                <div
                  key={s.k}
                  style={{
                    padding: '12px 13px',
                    border: `1px solid ${c.borde}`,
                    borderRadius: 11,
                    background: c.panel,
                  }}
                >
                  <div style={{ font: `400 10.5px ${mono}`, color: c.textoTenue, letterSpacing: '.06em' }}>
                    {s.k}
                  </div>
                  <div
                    style={{
                      font: `500 17px ${mono}`,
                      color: c.tinta,
                      letterSpacing: '-.025em',
                      marginTop: 5,
                    }}
                  >
                    {s.v}
                  </div>
                </div>
              ))}
            </div>

            <div style={{ border: `1px solid ${c.borde}`, borderRadius: 11, overflow: 'hidden' }}>
              <FilaDato k="teléfono" v={persona.tel} estilo={{ font: `400 12.5px ${mono}`, color: c.tinta }} />
              <FilaDato
                k="email"
                v={persona.email || 'sin email'}
                estilo={{ font: `400 12.5px ${sans}`, color: persona.email ? c.tinta : c.textoGris2 }}
              />
              <FilaDato
                k="origen"
                v={persona.origen === 'app' ? 'App de turnos' : 'Mostrador'}
                estilo={{ font: `400 12.5px ${sans}`, color: c.tinta }}
              />
              <FilaDato k="alta" v={persona.alta} estilo={{ font: `400 12.5px ${mono}`, color: c.tinta }} />
            </div>

            {sinTurnos && (
              <div
                style={{
                  marginTop: 12,
                  padding: '12px 14px',
                  borderRadius: 11,
                  background: c.panel,
                  border: `1px solid ${c.borde}`,
                  font: `400 12.5px/1.55 ${sans}`,
                  color: c.textoTenue2,
                }}
              >
                {copiaSinTurnos}
              </div>
            )}
          </>
        )}

        {pestania === 'turnos' &&
          (turnos.length > 0 ? (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
              {turnos.map((t, i) => (
                <div
                  key={i}
                  style={{
                    padding: '13px 14px',
                    border: `1px solid ${c.borde}`,
                    borderRadius: 11,
                    background: c.panel,
                  }}
                >
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: 12,
                    }}
                  >
                    <span style={{ font: `400 12.5px ${mono}`, color: c.tinta }}>{t.when}</span>
                    <span style={chipStyle(colorTurnoHistorico(t.chip))}>{t.chip}</span>
                  </div>
                  <div style={{ font: `400 12px ${sans}`, color: c.textoGris, marginTop: 5 }}>
                    {t.detalle}
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div style={{ padding: '56px 10px', textAlign: 'center' }}>
              <div style={{ font: `500 14.5px ${sans}`, marginBottom: 6 }}>Todavía no jugó</div>
              <div style={{ font: `400 12.5px/1.55 ${sans}`, color: c.textoGris }}>
                {copiaSinTurnos}
              </div>
            </div>
          ))}

        {pestania === 'notas' && (
          <>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 7, marginBottom: 12 }}>
              {persona.notas.map((n, i) => (
                <div
                  key={i}
                  style={{
                    padding: '12px 14px',
                    border: `1px solid ${c.borde}`,
                    borderRadius: 11,
                    background: c.panel,
                  }}
                >
                  <div style={{ font: `400 12.5px/1.55 ${sans}`, color: c.textoSuave }}>{n.txt}</div>
                  <div style={{ font: `400 10.5px ${mono}`, color: c.textoTenue, marginTop: 6 }}>
                    {n.autor}
                  </div>
                </div>
              ))}
              {persona.notas.length === 0 && (
                <div style={{ font: `400 12.5px ${sans}`, color: c.textoGris, padding: '4px 0' }}>
                  Sin notas internas todavía.
                </div>
              )}
            </div>
            <input
              type="text"
              value={nota}
              onChange={(e) => setNota(e.target.value)}
              placeholder="Nota interna, solo la ve el club"
              className="f-borde"
              style={{ ...campoPanel(), fontSize: 12.5 }}
            />
            <button
              type="button"
              className={nota.trim() ? 'h-primario' : undefined}
              onClick={() => {
                if (!nota.trim()) return;
                agregarNota.mutate(
                  { id: persona.id, txt: nota },
                  { onSuccess: () => avisar('Nota agregada') },
                );
                setNota('');
              }}
              style={{ ...primario(nota.trim().length > 0), marginTop: 9, width: '100%' }}
            >
              Agregar nota
            </button>
          </>
        )}
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
            alternarBloqueo.mutate(
              { id: persona.id, bloqueado: !persona.bloqueado },
              {
                onSuccess: (bloqueado) =>
                  avisar(bloqueado ? 'Ficha bloqueada' : 'Ficha desbloqueada'),
              },
            )
          }
          style={{
            minHeight: 34,
            padding: '0 13px',
            borderRadius: 8,
            cursor: 'pointer',
            border: `1px solid ${persona.bloqueado ? c.acentoBordeSuave : c.naranjaBorde}`,
            background: 'transparent',
            color: persona.bloqueado ? c.acento : c.naranja,
            font: `500 12.5px ${sans}`,
          }}
        >
          {persona.bloqueado ? 'Desbloquear persona' : 'Bloquear persona'}
        </button>
      </div>
    </Panel>
  );
}

const botonSecundarioPanel = {
  minHeight: 32,
  padding: '0 12px',
  borderRadius: 8,
  border: `1px solid ${c.bordeFirme}`,
  background: 'transparent',
  color: c.textoBoton,
  font: `500 12px ${sans}`,
  cursor: 'pointer',
} as const;

const botonNaranja = {
  minHeight: 32,
  padding: '0 12px',
  borderRadius: 8,
  border: `1px solid ${c.naranjaBordeFirme}`,
  background: 'transparent',
  color: c.naranja,
  font: `500 12px ${sans}`,
  cursor: 'pointer',
} as const;
