import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { Deporte, FiltroPersonas } from './domain/types';

/**
 * Lo que el operador está mirando vive en la URL, no en un estado global: así
 * el botón de atrás funciona, un turno se puede pasar por link y volver de un
 * módulo a otro no pierde el contexto.
 *
 * Lo que es puramente transitorio —qué panel está abierto, un borrador sin
 * guardar— se queda en el componente.
 */

const FILTROS: FiltroPersonas[] = ['todas', 'sinturnos', 'mostrador', 'deuda'];

function entero(v: string | null, min: number, max: number, porDefecto: number): number {
  const n = parseInt(v ?? '', 10);
  if (Number.isNaN(n)) return porDefecto;
  return Math.min(max, Math.max(min, n));
}

export interface ParamsAgenda {
  deporte: Deporte;
  /** Día contando desde hoy, 0 a 6. */
  dia: number;
  setDeporte: (d: Deporte) => void;
  setDia: (i: number) => void;
}

export function useParamsAgenda(): ParamsAgenda {
  const [sp, setSp] = useSearchParams();
  const deporte: Deporte = sp.get('deporte') === 'futbol' ? 'futbol' : 'padel';
  const dia = entero(sp.get('dia'), 0, 6, 0);

  const escribir = useCallback(
    (patch: Record<string, string>) => {
      setSp(
        (prev) => {
          const next = new URLSearchParams(prev);
          Object.entries(patch).forEach(([k, v]) => next.set(k, v));
          return next;
        },
        { replace: true },
      );
    },
    [setSp],
  );

  return {
    deporte,
    dia,
    setDeporte: (d) => escribir({ deporte: d }),
    setDia: (i) => escribir({ dia: String(i) }),
  };
}

export interface ParamsPersonas {
  q: string;
  filtro: FiltroPersonas;
  pagina: number;
  /** Ficha abierta en el panel lateral. */
  ficha: string | null;
  setQ: (v: string) => void;
  setFiltro: (v: FiltroPersonas) => void;
  setPagina: (v: number) => void;
  abrirFicha: (id: string | null) => void;
  limpiar: () => void;
}

export function useParamsPersonas(): ParamsPersonas {
  const [sp, setSp] = useSearchParams();
  const crudo = sp.get('filtro');
  const filtro = (FILTROS.find((f) => f === crudo) ?? 'todas') as FiltroPersonas;
  const ficha = sp.get('ficha');

  const escribir = useCallback(
    (patch: Record<string, string | null>) => {
      setSp(
        (prev) => {
          const next = new URLSearchParams(prev);
          Object.entries(patch).forEach(([k, v]) => {
            if (v == null || v === '') next.delete(k);
            else next.set(k, v);
          });
          return next;
        },
        { replace: true },
      );
    },
    [setSp],
  );

  // Los setters se memorizan porque hay efectos que dependen de su identidad.
  const acciones = useMemo(
    () => ({
      // Cambiar la búsqueda o el filtro vuelve a la primera página: si no, se
      // queda mirando una página que ya no existe.
      setQ: (v: string) => escribir({ q: v || null, pagina: null }),
      setFiltro: (v: FiltroPersonas) =>
        escribir({ filtro: v === 'todas' ? null : v, pagina: null }),
      setPagina: (v: number) => escribir({ pagina: v === 0 ? null : String(v) }),
      abrirFicha: (id: string | null) => escribir({ ficha: id }),
      limpiar: () => escribir({ q: null, filtro: null, pagina: null }),
    }),
    [escribir],
  );

  return {
    q: sp.get('q') ?? '',
    filtro,
    pagina: entero(sp.get('pagina'), 0, 9999, 0),
    ficha: ficha || null,
    ...acciones,
  };
}

export interface ParamsSeleccion {
  sel: string | null;
  setSel: (id: string) => void;
}

/** Elemento elegido en la lista izquierda de Canchas u Horarios, por id. */
export function useParamsSeleccion(): ParamsSeleccion {
  const [sp, setSp] = useSearchParams();
  return {
    sel: sp.get('sel'),
    setSel: (id) =>
      setSp(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.set('sel', id);
          return next;
        },
        { replace: true },
      ),
  };
}

export type VistaHorario = 'lista' | 'cal' | 'excepciones';

export function useParamsVista(): { vista: VistaHorario; setVista: (v: VistaHorario) => void } {
  const [sp, setSp] = useSearchParams();
  const crudo = sp.get('vista');
  return {
    vista: crudo === 'cal' || crudo === 'excepciones' ? crudo : 'lista',
    setVista: (v) =>
      setSp(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.set('vista', v);
          return next;
        },
        { replace: true },
      ),
  };
}
