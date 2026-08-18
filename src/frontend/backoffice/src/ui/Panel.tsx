import type { ReactNode } from 'react';
import { X } from 'lucide-react';
import { c } from './theme';

/**
 * Panel lateral. Todo lo que se abre desde una fila —la ficha, el turno, un
 * alta— entra por acá: la pantalla de atrás no se pierde, que es lo que el
 * mostrador necesita cuando tiene a alguien enfrente.
 */
export function Panel({ onCerrar, children }: { onCerrar: () => void; children: ReactNode }) {
  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(26,26,24,.24)',
        display: 'flex',
        justifyContent: 'flex-end',
        animation: 'fadein .13s ease-out',
        zIndex: 40,
      }}
    >
      {/* Clic afuera para cerrar. */}
      <div onClick={onCerrar} style={{ flex: 1 }} />
      <div
        style={{
          flex: 'none',
          width: 428,
          maxWidth: '94vw',
          height: '100%',
          background: c.blanco,
          borderLeft: `1px solid ${c.borde}`,
          display: 'flex',
          flexDirection: 'column',
          animation: 'slidein .16s ease-out',
        }}
      >
        {children}
      </div>
    </div>
  );
}

/** Botón de cerrar del encabezado del panel. */
export function BotonCerrar({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label="Cerrar"
      className="h-fondo"
      style={{
        flex: 'none',
        width: 28,
        height: 28,
        borderRadius: 8,
        border: `1px solid ${c.bordeFirme}`,
        background: 'transparent',
        color: c.textoGris,
        cursor: 'pointer',
        display: 'grid',
        placeItems: 'center',
      }}
    >
      <X size={14} strokeWidth={2} aria-hidden />
    </button>
  );
}

/** Fila de dato del panel: rótulo a la izquierda, valor a la derecha. */
export function FilaDato({
  k,
  v,
  estilo,
}: {
  k: string;
  v: string;
  estilo?: React.CSSProperties;
}) {
  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: 14,
        padding: '11px 13px',
        borderBottom: `1px solid ${c.linea}`,
      }}
    >
      <span style={{ font: `400 11.5px "Geist Mono", monospace`, color: c.textoTenue }}>{k}</span>
      <span style={estilo}>{v}</span>
    </div>
  );
}
