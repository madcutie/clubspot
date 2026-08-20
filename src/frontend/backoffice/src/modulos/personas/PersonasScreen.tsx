import { useEffect, useState } from 'react';
import { Check, ChevronLeft, ChevronRight } from 'lucide-react';
import { useBloquearPersonas, usePersonas } from '../../api/queries';
import { pesos } from '../../domain/dinero';
import type { FiltroPersonas } from '../../domain/types';
import { useParamsPersonas } from '../../rutas';
import { Cargando } from '../../ui/Cargando';
import { chipStyle, estadoPersona, puntoStyle } from '../../ui/estados';
import {
  botonPagina,
  c,
  casilla,
  chipFiltro,
  desnudo,
  mono,
  primario,
  sans,
  secundario,
} from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';
import { FichaPanel } from './FichaPanel';
import { ImportarPanel } from './ImportarPanel';
import { NuevaPersonaPanel } from './NuevaPersonaPanel';

/** Columnas de la tabla. Se repiten en el encabezado y en cada fila. */
const COLUMNAS = '38px minmax(180px,1.6fr) 118px minmax(160px,1.4fr) 116px';

const FILTROS: { id: FiltroPersonas; label: string }[] = [
  { id: 'todas', label: 'Todas' },
  { id: 'sinturnos', label: 'Sin turnos' },
  { id: 'mostrador', label: 'Mostrador' },
  { id: 'deuda', label: 'Atención' },
];

/**
 * La base de personas. Es la pantalla más usada del sistema: se entra a buscar
 * a alguien que está parado en el mostrador, así que la búsqueda y el estado
 * de cada ficha van primero.
 */
export function PersonasScreen() {
  const params = useParamsPersonas();
  const avisar = useTostada();
  const { data, isLoading } = usePersonas({
    q: params.q,
    filtro: params.filtro,
    pagina: params.pagina,
  });
  const bloquear = useBloquearPersonas();

  const [marcadas, setMarcadas] = useState<string[]>([]);
  const [panel, setPanel] = useState<'nueva' | 'import' | null>(null);

  // Lo tipeado se escribe en la URL con retardo. Si el input leyera la URL en
  // cada tecla se comería letras: escribir es más rápido que navegar.
  const [texto, setTexto] = useState(params.q);
  const escribirQ = params.setQ;

  useEffect(() => {
    setTexto(params.q);
  }, [params.q]);

  useEffect(() => {
    if (texto === params.q) return;
    const reloj = window.setTimeout(() => escribirQ(texto), 250);
    return () => window.clearTimeout(reloj);
    // `params.q` a propósito fuera: sólo dispara el tipeo.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [texto, escribirQ]);

  // Cambiar de página o de filtro deja fuera de vista lo que estaba marcado:
  // conservar la selección invitaría a operar a ciegas sobre ella.
  useEffect(() => {
    setMarcadas([]);
  }, [params.filtro, params.pagina, params.q]);

  if (!data) return isLoading ? <Cargando que="la base" /> : null;

  const { items, total, pagina, paginas, porPagina } = data;
  const desde = pagina * porPagina + 1;
  const hasta = pagina * porPagina + items.length;
  const todasMarcadas = items.length > 0 && marcadas.length === items.length;

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
            <span
              style={{
                font: `500 40px ${mono}`,
                letterSpacing: '-.04em',
                color: c.titulo,
              }}
            >
              {data.padron}
            </span>
            <div style={{ paddingBottom: 4 }}>
              <div style={{ font: `500 14px ${sans}`, letterSpacing: '-.01em' }}>
                personas en la base
              </div>
              <div style={{ font: `400 12px ${mono}`, color: c.textoGris, marginTop: 3 }}>
                {data.atencion} requieren atención · {pesos(data.deudaTotal)} por cobrar
              </div>
            </div>
          </div>
          <div style={{ display: 'flex', gap: 7 }}>
            <button
              type="button"
              onClick={() => setPanel('import')}
              className="h-ghost"
              style={secundario()}
            >
              Importar
            </button>
            <button
              type="button"
              onClick={() => setPanel('nueva')}
              className="h-primario"
              style={primario()}
            >
              Nueva persona
            </button>
          </div>
        </div>

        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 7,
            marginTop: 20,
            flexWrap: 'wrap',
          }}
        >
          <div
            style={{
              flex: 1,
              minWidth: 220,
              maxWidth: 300,
              position: 'relative',
              display: 'flex',
              alignItems: 'center',
            }}
          >
            <span
              style={{
                position: 'absolute',
                left: 11,
                font: `400 12px ${sans}`,
                color: c.textoTenue,
              }}
            >
              ⌕
            </span>
            <input
              type="text"
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
              placeholder="Buscar nombre, teléfono, email"
              className="f-borde"
              style={{
                width: '100%',
                minHeight: 34,
                padding: '0 40px 0 28px',
                borderRadius: 8,
                border: `1px solid ${c.borde}`,
                background: c.panel,
                fontSize: 12.5,
                color: c.texto,
                outline: 'none',
              }}
            />
            <span
              style={{
                position: 'absolute',
                right: 9,
                font: `400 10px ${mono}`,
                color: c.textoGris2,
                border: `1px solid ${c.bordeFirme}`,
                borderRadius: 4,
                padding: '2px 5px',
              }}
            >
              ⌘K
            </span>
          </div>
          <span style={{ width: 1, height: 20, background: c.borde, margin: '0 3px' }} />
          {FILTROS.map((f) => {
            const on = params.filtro === f.id;
            return (
              <button
                key={f.id}
                type="button"
                onClick={() => params.setFiltro(f.id)}
                style={{
                  ...chipFiltro(on),
                  display: 'flex',
                  alignItems: 'center',
                  gap: 6,
                  padding: '0 11px',
                }}
              >
                {f.label}
                <span
                  style={{
                    font: `400 11px ${mono}`,
                    color: on ? c.acentoTenue : c.textoGris2,
                  }}
                >
                  {data.totales[f.id]}
                </span>
              </button>
            );
          })}
        </div>
      </div>

      {marcadas.length > 0 && (
        <div
          style={{
            flex: 'none',
            margin: '14px 26px 0',
            padding: '8px 10px 8px 14px',
            borderRadius: 9,
            background: c.acentoFondo,
            border: `1px solid ${c.acentoBorde}`,
            display: 'flex',
            alignItems: 'center',
            gap: 9,
          }}
        >
          <span style={{ font: `500 12.5px ${mono}`, color: c.acento }}>
            {marcadas.length} seleccionadas
          </span>
          <div style={{ flex: 1 }} />
          <button
            type="button"
            onClick={() => avisar('Exportando ' + marcadas.length + ' fichas')}
            style={accionMasiva}
          >
            Exportar
          </button>
          <button
            type="button"
            onClick={() => {
              const n = marcadas.length;
              bloquear.mutate(
                { ids: marcadas, bloqueado: true },
                { onSuccess: () => avisar(n === 1 ? 'Ficha bloqueada' : n + ' fichas bloqueadas') },
              );
              setMarcadas([]);
            }}
            style={accionMasiva}
          >
            Bloquear
          </button>
          <button
            type="button"
            onClick={() => setMarcadas([])}
            style={{
              minHeight: 28,
              padding: '0 7px',
              border: 'none',
              background: 'transparent',
              color: c.acentoTenue,
              font: `500 12px ${sans}`,
              cursor: 'pointer',
            }}
          >
            Cancelar
          </button>
        </div>
      )}

      <div style={{ flex: 1, minHeight: 0, overflow: 'auto', padding: '0 26px', marginTop: 16 }}>
        <div
          style={{
            position: 'sticky',
            top: 0,
            zIndex: 2,
            background: c.papel,
            display: 'grid',
            gridTemplateColumns: COLUMNAS,
            alignItems: 'center',
            height: 30,
            minWidth: 612,
            borderBottom: `1px solid ${c.linea}`,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center' }}>
            <button
              type="button"
              aria-label="Seleccionar todas"
              onClick={() => setMarcadas(todasMarcadas ? [] : items.map((p) => p.id))}
              style={casilla(todasMarcadas)}
            >
              {todasMarcadas ? <Check size={11} strokeWidth={3} aria-hidden /> : ''}
            </button>
          </div>
          {['NOMBRE', 'TELÉFONO', 'EMAIL', 'ESTADO'].map((h) => (
            <div key={h} style={{ font: `400 10.5px ${mono}`, color: c.textoTenue, letterSpacing: '.08em' }}>
              {h}
            </div>
          ))}
        </div>

        {items.map((p) => {
          const marcada = marcadas.includes(p.id);
          const e = estadoPersona(p);
          const abierta = params.ficha === p.id;
          return (
            <div
              key={p.id}
              className="h-fondo"
              style={{
                display: 'grid',
                gridTemplateColumns: COLUMNAS,
                alignItems: 'center',
                minHeight: 48,
                minWidth: 612,
                borderBottom: `1px solid ${c.segmento}`,
                background: abierta ? c.apagado : 'transparent',
                boxShadow: abierta ? `inset 2px 0 0 ${c.acento}` : 'none',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center' }}>
                <button
                  type="button"
                  aria-label={`Seleccionar ${p.nombre}`}
                  onClick={() =>
                    setMarcadas(
                      marcada ? marcadas.filter((x) => x !== p.id) : marcadas.concat(p.id),
                    )
                  }
                  style={casilla(marcada)}
                >
                  {marcada ? <Check size={11} strokeWidth={3} aria-hidden /> : ''}
                </button>
              </div>
              <button
                type="button"
                onClick={() => params.abrirFicha(p.id)}
                style={{ ...desnudo, display: 'flex', alignItems: 'center', gap: 9, minWidth: 0 }}
              >
                <span style={{ minWidth: 0 }}>
                  <span
                    style={{
                      display: 'block',
                      font: `500 13px ${sans}`,
                      color: c.tinta,
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                    }}
                  >
                    {p.nombre}
                  </span>
                  <span
                    style={{
                      display: 'block',
                      font: `400 11px ${mono}`,
                      color: c.textoTenue,
                      marginTop: 2,
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                    }}
                  >
                    {p.origen === 'app' ? 'app' : 'mostrador'} ·{' '}
                    {p.turnos === 0 ? 'sin turnos' : p.turnos + ' turnos'}
                    {p.deuda > 0 ? ' · debe ' + pesos(p.deuda) : ''}
                  </span>
                </span>
              </button>
              <div style={{ font: `400 12.5px ${mono}`, color: c.textoDato }}>{p.tel}</div>
              <div
                style={{
                  font: `400 12.5px ${sans}`,
                  color: c.textoDato,
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  paddingRight: 10,
                }}
              >
                {p.email || '—'}
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
                <span style={puntoStyle(e)} />
                <span style={chipStyle(e.fg)}>{e.label}</span>
              </div>
            </div>
          );
        })}

        {items.length === 0 && (
          <div style={{ padding: '80px 20px', textAlign: 'center' }}>
            <div style={{ font: `500 15px ${sans}`, marginBottom: 6 }}>Sin resultados</div>
            <div style={{ font: `400 12.5px ${mono}`, color: c.textoGris }}>
              {params.q
                ? `nada coincide con “${params.q}”`
                : 'ninguna persona cumple con este filtro'}
            </div>
            <button
              type="button"
              onClick={params.limpiar}
              className="h-ghost"
              style={{ ...secundario(), marginTop: 16, minHeight: 32 }}
            >
              Limpiar filtros
            </button>
          </div>
        )}
      </div>

      <div
        style={{
          flex: 'none',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 16,
          height: 46,
          padding: '0 26px',
          borderTop: `1px solid ${c.linea}`,
        }}
      >
        <span style={{ font: `400 11.5px ${mono}`, color: c.textoTenue }}>
          {total === 0 ? 'sin registros' : `${desde}–${hasta} de ${total}`}
        </span>
        <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <button
            type="button"
            aria-label="Página anterior"
            onClick={() => params.setPagina(Math.max(0, pagina - 1))}
            style={botonPagina(false, pagina === 0)}
          >
            <ChevronLeft size={13} strokeWidth={2} aria-hidden />
          </button>
          {Array.from({ length: paginas }, (_, i) => (
            <button
              key={i}
              type="button"
              onClick={() => params.setPagina(i)}
              style={botonPagina(pagina === i, false)}
            >
              {i + 1}
            </button>
          ))}
          <button
            type="button"
            aria-label="Página siguiente"
            onClick={() => params.setPagina(Math.min(paginas - 1, pagina + 1))}
            style={botonPagina(false, pagina === paginas - 1)}
          >
            <ChevronRight size={13} strokeWidth={2} aria-hidden />
          </button>
        </div>
      </div>

      {params.ficha != null && (
        <FichaPanel id={params.ficha} onCerrar={() => params.abrirFicha(null)} />
      )}
      {panel === 'nueva' && <NuevaPersonaPanel onCerrar={() => setPanel(null)} />}
      {panel === 'import' && <ImportarPanel onCerrar={() => setPanel(null)} />}
    </>
  );
}

const accionMasiva = {
  minHeight: 28,
  padding: '0 10px',
  borderRadius: 7,
  border: `1px solid ${c.acentoBordeSuave}`,
  background: 'transparent',
  color: c.acento,
  font: `500 12px ${sans}`,
  cursor: 'pointer',
} as const;
