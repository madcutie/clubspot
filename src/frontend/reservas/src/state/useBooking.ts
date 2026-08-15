import { useCallback, useState } from 'react';
import { senaOf } from '../domain/pricing';
import type {
  CourtFilter,
  Duration,
  PayMethod,
  PayMode,
  Screen,
  Selection,
  Sport,
} from '../domain/types';

/**
 * Estado de UI del flujo de reserva. Los datos (disponibilidad, reservas) viven
 * en React Query; acá solo queda lo que el usuario eligió.
 */
export interface BookingState {
  screen: Screen;
  sport: Sport;
  dateIdx: number;
  dur: Duration;
  ctype: CourtFilter;
  hour: number | null;
  courtIdx: number | null;
  /** Turno confirmado al pasar a "Confirmar reserva". */
  sel: Selection | null;
  nombre: string;
  tel: string;
  email: string;
  pago: PayMode;
  method: PayMethod;
  /** Intentos de pago ya hechos para este turno. */
  tries: number;
  tab: 'prox' | 'ant';
  code: string | null;
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
  pago: 'total',
  method: 'mp',
  tries: 0,
  tab: 'prox',
  code: null,
};

export function useBooking() {
  const [st, setSt] = useState<BookingState>(INITIAL);

  const set = useCallback((patch: Partial<BookingState>) => {
    setSt((prev) => ({ ...prev, ...patch }));
  }, []);

  /** Vuelve al home descartando la selección y el intento de pago en curso. */
  const restart = useCallback(() => {
    setSt((prev) => ({
      ...prev,
      screen: 'home',
      hour: null,
      courtIdx: null,
      sel: null,
      tries: 0,
      code: null,
    }));
  }, []);

  const total = st.sel ? st.sel.price : 0;
  const sena = senaOf(total);
  const saldo = total - sena;
  const payAmount = st.pago === 'total' ? total : sena;

  return { st, set, restart, total, sena, saldo, payAmount };
}

export type BookingApi = ReturnType<typeof useBooking>;
