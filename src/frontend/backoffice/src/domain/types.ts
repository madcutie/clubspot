/** Tipos del dominio que ve la consola. Son los DTOs que devolvería la API real. */

export type Deporte = 'padel' | 'futbol';

/** De dónde salió la ficha: la cargó el club o se registró sola en la app. */
export type Origen = 'app' | 'mostrador';

/** Cuánto se cobró del turno. */
export type Pago = 'total' | 'sena' | 'nada';

/** Filtros de la tabla de personas. */
export type FiltroPersonas = 'todas' | 'sinturnos' | 'mostrador' | 'deuda';

export interface Nota {
  txt: string;
  autor: string;
}

export interface Persona {
  id: number;
  nombre: string;
  tel: string;
  email: string;
  origen: Origen;
  deporte: Deporte;
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

/** Día con horario propio que pisa al semanal. Sin tramos = cerrado. */
export interface FechaEspecial {
  fecha: string;
  tramos: Tramo[];
}

export interface Horario {
  id: string;
  nombre: string;
  tz: string;
  /** Tramos por día de la semana, indexados como `Date.getDay()` (0 = domingo). */
  semanal: Record<number, Tramo[]>;
  fechas: FechaEspecial[];
}

export interface Cancha {
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
}

/** Hueco de la grilla: no hay turno vendido. */
export interface SlotLibre {
  libre: true;
  t: number;
  ci: number;
  /** Cantidad de filas de 30 min que ocupa. */
  span: number;
  /** El horario del club no cubre esta franja. */
  cerrado: boolean;
  /** Cae fuera de los arranques permitidos por el incremento de la cancha. */
  offGrid: boolean;
}

/** Turno vendido. */
export interface SlotOcupado {
  libre: false;
  key: string;
  /** Identificador que se le canta al socio por teléfono: TRN-4821. */
  id: string;
  t: number;
  dur: number;
  ci: number;
  persona: string;
  tel: string;
  pago: Pago;
  precio: number;
  saldo: number;
  ausente: boolean;
}

export type Slot = SlotLibre | SlotOcupado;

export interface ColumnaAgenda {
  ci: number;
  nombre: string;
  detalle: string;
  items: Slot[];
}

export interface Agenda {
  columnas: ColumnaAgenda[];
  /** Turnos vendidos ese día. */
  turnos: number;
  /** Porcentaje de ocupación sobre las horas de grilla. */
  ocupacion: number;
  porCobrar: number;
}

export interface Club {
  nombre: string;
  sede: string;
  operador: string;
  operadorIniciales: string;
  rol: string;
}
