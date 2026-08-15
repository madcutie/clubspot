export type Sport = 'padel' | 'futbol';

/** 'todas' es el filtro "sin filtro"; los otros dos son tipos reales de cancha. */
export type CourtFilter = 'todas' | CourtType;
export type CourtType = 'techada' | 'descubierta';

export type Duration = 60 | 90 | 120;

export type PayMode = 'total' | 'sena';
export type PayMethod = 'mp' | 'tarjeta' | 'transfer';
export type PayStatus = 'idle' | 'processing' | 'rejected' | 'approved';

export type Screen = 'home' | 'avail' | 'confirm' | 'pay' | 'done' | 'mine';

export interface Court {
  /** Nombre visible, ej. "Cancha 1". */
  n: string;
  /** Descripción corta, ej. "Blindex techada · con luces". */
  d: string;
  t: CourtType;
  /** Diferencia sobre el precio base del deporte, por hora. */
  extra: number;
}

/** Turno elegido y ya validado contra la disponibilidad. */
export interface Selection {
  key: string;
  court: string;
  dur: Duration;
  /** "19:00 – 20:30" */
  label: string;
  price: number;
}

export interface Booking {
  id: string;
  sport: string;
  when: string;
  court: string;
  pay: PayMode;
  saldo: number;
  past: boolean;
}
