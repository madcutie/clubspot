import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { c, sans } from './theme';

/**
 * Avisos breves de la consola: confirman lo que acaba de pasar y se van solos.
 * No sirven para errores que requieren decidir algo — eso va en la pantalla.
 */

const DURACION = 2400;

const Contexto = createContext<(mensaje: string) => void>(() => {});

/** Muestra un aviso al pie. */
export function useTostada() {
  return useContext(Contexto);
}

export function ProveedorTostadas({ children }: { children: ReactNode }) {
  const [mensaje, setMensaje] = useState<string | null>(null);
  const reloj = useRef<number | null>(null);

  const avisar = useCallback((m: string) => {
    setMensaje(m);
    if (reloj.current) window.clearTimeout(reloj.current);
    reloj.current = window.setTimeout(() => setMensaje(null), DURACION);
  }, []);

  useEffect(() => () => {
    if (reloj.current) window.clearTimeout(reloj.current);
  }, []);

  return (
    <Contexto.Provider value={avisar}>
      {children}
      {mensaje != null && (
        <div
          role="status"
          style={{
            position: 'fixed',
            left: '50%',
            bottom: 22,
            transform: 'translateX(-50%)',
            zIndex: 60,
            padding: '10px 16px',
            borderRadius: 9,
            background: c.tostadaFondo,
            border: `1px solid ${c.tostadaBorde}`,
            color: c.tostadaTexto,
            font: `500 12.5px ${sans}`,
            animation: 'fadein .13s ease-out',
          }}
        >
          {mensaje}
        </div>
      )}
    </Contexto.Provider>
  );
}
