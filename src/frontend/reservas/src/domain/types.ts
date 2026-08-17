export type Sport = 'padel' | 'futbol';

/** 'todas' es el filtro "sin filtro"; los otros dos son tipos reales de cancha. */
export type CourtFilter = 'todas' | CourtType;
export type CourtType = 'techada' | 'descubierta';

/** Duración del turno en minutos; las opciones reales vienen del catálogo. */
export type Duration = number;

export type PayMode = 'total' | 'sena';

export type Screen = 'home' | 'avail' | 'confirm' | 'mine';

/** Turno elegido y ya validado contra la disponibilidad. */
export interface Selection {
  key: string;
  court: string;
  dur: Duration;
  /** "19:00 – 20:30" */
  label: string;
  /** "sábado 15 de agosto" */
  diaLabel: string;
  price: number;
}
