export type Sport = 'padel' | 'futbol';

/** 'todas' es el filtro "sin filtro"; los otros dos son tipos reales de cancha. */
export type CourtFilter = 'todas' | CourtType;
export type CourtType = 'techada' | 'descubierta';

/** Duración del turno en minutos; las opciones reales vienen del catálogo. */
export type Duration = number;

export type Screen = 'home' | 'avail' | 'confirm' | 'done' | 'retorno' | 'mine';

/** Forma de pago elegida: en el club, total online, o seña online. */
export type PayMode = 'club' | 'total' | 'sena';

/** Turno elegido y ya validado contra la disponibilidad. */
export interface Selection {
  key: string;
  courtId: string;
  court: string;
  /** Fecha ISO del día elegido ("2026-08-24"). */
  date: string;
  startMinute: number;
  dur: Duration;
  /** "19:00 – 20:30" */
  label: string;
  /** "sábado 15 de agosto" */
  diaLabel: string;
  price: number;
}

/** Reserva confirmada por el servidor; lo que guarda "Mis reservas" en este dispositivo. */
export interface ConfirmedBooking {
  id: string;
  sport: Sport;
  court: string;
  date: string;
  label: string;
  diaLabel: string;
  price: number;
  nombre: string;
}
