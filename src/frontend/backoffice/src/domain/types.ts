/** Tipos del dominio que ve la consola. Son los DTOs que devolvería la API real. */

export type Deporte = 'padel' | 'futbol';

/** De dónde salió la ficha: la cargó el club o se registró sola en la app. */
export type Origen = 'app' | 'mostrador';

/** Filtros de la tabla de personas. */
export type FiltroPersonas = 'todas' | 'sinturnos' | 'mostrador' | 'deuda';

export interface Nota {
  txt: string;
  autor: string;
}

export interface Persona {
  id: string;
  nombre: string;
  tel: string;
  email: string;
  origen: Origen;
  turnos: number;
  /** Texto relativo listo para mostrar: "hace 3 días". `null` si nunca jugó. */
  ultima: string | null;
  deuda: number;
  bloqueado: boolean;
  alta: string;
  notas: Nota[];
}

/** Turno ya jugado que aparece en la ficha. */
export interface TurnoHistorico {
  when: string;
  detalle: string;
  chip: string;
}

/** Tramo horario en minutos desde la medianoche: [apertura, cierre]. */
export type Tramo = [number, number];

export interface Horario {
  id: string;
  nombre: string;
  /** Tramos por día de la semana, indexados como `Date.getDay()` (0 = domingo). */
  semanal: Record<number, Tramo[]>;
  version?: number;
}

export interface Cancha {
  id: string;
  deporte: Deporte;
  /** Índice de la cancha dentro de su deporte. */
  ci: number;
  nombre: string;
  detalle: string;
  techada: boolean;
  activa: boolean;
  /** Id del horario que usa. */
  horarioId: string;
  /** Duraciones de turno habilitadas, en minutos. */
  duraciones: number[];
  /** Cada cuántos minutos puede arrancar un turno. */
  incremento: number;
  /** Aviso mínimo antes del turno, en minutos. */
  aviso: number;
  precioDia: number;
  precioNoche: number;
  /** Minuto en que empieza la tarifa nocturna. */
  noche: number;
  version?: number;
}

/** Arranque que se puede vender, con la duración y el precio que fijó el servidor. */
export interface TurnoDisponible {
  t: number;
  dur: number;
  precio: number;
}

/** Reserva del día, con el snapshot de precio del servidor. */
export interface ReservaDia {
  id: string;
  t: number;
  dur: number;
  persona: string;
  tel: string | null;
  precio: number;
  /** Acreditado hasta ahora. Mayor al precio ⇒ se cobró de más y hay que devolver. */
  pagado: number;
  /** Hold de pago online todavía sin acreditar; bloquea el turno hasta vencer. */
  pendientePago: boolean;
}

/** Una cancha en la agenda del día, tal como la calcula el backend. */
export interface CanchaAgenda {
  courtId: string;
  nombre: string;
  detalle: string;
  techada: boolean;
  /** Franjas abiertas del día según horario y excepciones. */
  ventanas: Tramo[];
  turnos: TurnoDisponible[];
  reservas: ReservaDia[];
}

/**
 * Reserva que ya no bloquea el turno, con lo que tenía pagado. `abandonada` es un
 * intento de compra online que nunca se pagó: el hold venció, nadie la canceló.
 */
export interface ReservaInactiva {
  id: string;
  cancha: string;
  t: number;
  dur: number;
  persona: string;
  tel: string | null;
  precio: number;
  pagado: number;
  estado: 'cancelada' | 'abandonada';
  /** Por qué la canceló el club; null en una abandonada o en un hold vencido. */
  motivo: string | null;
}

export interface AgendaDia {
  moneda: string;
  canchas: CanchaAgenda[];
  inactivas: ReservaInactiva[];
}

export interface Club {
  nombre: string;
  sede: string;
  operador: string;
  operadorIniciales: string;
  rol: string;
}
