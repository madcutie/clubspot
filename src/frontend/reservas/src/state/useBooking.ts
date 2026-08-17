import { useCallback, useState } from 'react';
import type { ConfirmedBooking, CourtFilter, Duration, PayMode, Screen, Selection, Sport } from '../domain/types';

/**
 * Estado de UI del flujo de reserva. Los datos (catálogo, disponibilidad)
 * viven en React Query; acá solo queda lo que el usuario eligió.
 */
export interface BookingState {
  screen: Screen;
  sport: Sport;
  dateIdx: number;
  dur: Duration;
  ctype: CourtFilter;
  /** Minuto de inicio elegido, o null. */
  hour: number | null;
  courtIdx: number | null;
  /** Turno confirmado al pasar a "Confirmar reserva". */
  sel: Selection | null;
  nombre: string;
  tel: string;
  email: string;
  pago: PayMode;
  /** Reserva recién confirmada por el servidor (pantalla de éxito). */
  done: ConfirmedBooking | null;
  /** Reserva a la que volvió el checkout online (`?retorno={id}`). */
  retornoId: string | null;
}

const INITIAL: BookingState = {
  screen: 'home',
  sport: 'padel',
  dateIdx: 0,
  dur: 60,
  ctype: 'todas',
  hour: null,
  courtIdx: null,
  sel: null,
  nombre: '',
  tel: '',
  email: '',
  pago: 'club',
  done: null,
  retornoId: null,
};

/** La vuelta del checkout entra por `?retorno={id}`; se limpia la URL al leerla. */
function initialState(): BookingState {
  const retorno = new URLSearchParams(window.location.search).get('retorno');
  if (!retorno) return INITIAL;
  window.history.replaceState({}, '', window.location.pathname);
  return { ...INITIAL, screen: 'retorno', retornoId: retorno };
}

export function useBooking() {
  const [st, setSt] = useState<BookingState>(initialState);

  const set = useCallback((patch: Partial<BookingState>) => {
    setSt((prev) => ({ ...prev, ...patch }));
  }, []);

  /** Vuelve al home descartando la selección en curso. */
  const restart = useCallback(() => {
    setSt((prev) => ({
      ...prev, screen: 'home', hour: null, courtIdx: null, sel: null, done: null, retornoId: null,
    }));
  }, []);

  const total = st.sel ? st.sel.price : 0;

  return { st, set, restart, total };
}

export type BookingApi = ReturnType<typeof useBooking>;
