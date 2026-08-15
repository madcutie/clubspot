/**
 * Estado del backend simulado.
 *
 * Vive en memoria y se pierde al recargar, a propósito: es un prototipo para
 * mostrar y discutir, no una base de datos. Cuando exista la API real este
 * archivo desaparece entero.
 */

import type {
  Cancha,
  Club,
  Deporte,
  FechaEspecial,
  Horario,
  Nota,
  Persona,
  Tramo,
  TurnoHistorico,
} from '../domain/types';
import { MESES } from '../domain/fechas';

export const CLUB: Club = {
  nombre: 'Club Chaco For Ever',
  sede: 'sede sarmiento',
  operador: 'Rubén Medina',
  operadorIniciales: 'RM',
  rol: 'encargado',
};

/** Padrón de ejemplo: nombre, teléfono, email, origen, deporte, turnos, última vez, deuda, bloqueada. */
const SEMILLA: [string, string, string, 'app' | 'mostrador', Deporte, number, string | null, number, boolean][] = [
  ['Lucas Benítez', '362 415-8890', 'lucasbenitez@gmail.com', 'app', 'padel', 14, 'hace 3 días', 0, false],
  ['Marcela Ojeda', '362 448-2117', 'marce.ojeda@hotmail.com', 'mostrador', 'futbol', 0, null, 0, false],
  ['Diego Sotelo', '362 430-9042', 'dsotelo@gmail.com', 'app', 'futbol', 26, 'ayer', 21500, false],
  ['Fernanda Rios', '362 461-7734', 'fer.rios88@gmail.com', 'app', 'padel', 0, null, 0, false],
  ['Julián Acosta', '362 415-2290', 'julian.acosta@outlook.com', 'app', 'padel', 9, 'hace 12 días', 0, true],
  ['Verónica Paz', '362 452-6618', 'vpaz@gmail.com', 'mostrador', 'padel', 3, 'hace 1 mes', 7500, false],
  ['Ramiro Chávez', '362 419-3355', 'ramirochavez@gmail.com', 'app', 'futbol', 41, 'hace 5 días', 0, false],
  ['Sofía Miranda', '362 470-1128', 'sofi.miranda@gmail.com', 'app', 'padel', 0, null, 0, false],
  ['Néstor Aguirre', '362 424-8871', '', 'mostrador', 'futbol', 7, 'hace 2 semanas', 0, false],
  ['Camila Duarte', '362 456-3390', 'camiduarte@gmail.com', 'app', 'padel', 18, 'hace 6 días', 0, false],
  ['Hernán Villalba', '362 433-5502', 'hvillalba@gmail.com', 'app', 'futbol', 5, 'hace 3 semanas', 40000, true],
  ['Paula Escobar', '362 447-9963', 'paula.escobar@gmail.com', 'app', 'padel', 0, null, 0, false],
  ['Gonzalo Ferreyra', '362 412-7048', 'gonza.ferreyra@gmail.com', 'app', 'futbol', 33, 'hace 2 días', 0, false],
  ['Silvina Correa', '362 465-2284', '', 'mostrador', 'padel', 1, 'hace 2 meses', 0, false],
  ['Matías Roldán', '362 428-6619', 'matiroldan@outlook.com', 'app', 'padel', 22, 'hace 4 días', 0, false],
  ['Andrea Gómez', '362 459-4471', 'andreagomez@gmail.com', 'app', 'futbol', 0, null, 0, false],
  ['Emiliano Sena', '362 421-3306', 'emisena@gmail.com', 'app', 'padel', 11, 'hace 9 días', 12000, false],
  ['Rocío Maidana', '362 468-8895', 'rocio.maidana@gmail.com', 'mostrador', 'padel', 0, null, 0, false],
  ['Federico Ledesma', '362 414-5527', 'fledesma@gmail.com', 'app', 'futbol', 29, 'hace 1 semana', 0, false],
  ['Nadia Zárate', '362 443-1172', 'nadiazarate@gmail.com', 'app', 'padel', 6, 'hace 3 semanas', 0, true],
  ['Cristian Barrios', '362 437-9910', 'cbarrios@gmail.com', 'app', 'futbol', 15, 'hace 5 días', 0, false],
  ['Lorena Sánchez', '362 472-6633', 'lorena.sanchez@gmail.com', 'mostrador', 'padel', 2, 'hace 1 mes', 0, false],
  ['Ariel Quiroz', '362 426-4408', 'arielquiroz@gmail.com', 'app', 'futbol', 0, null, 0, false],
  ['Belén Cabrera', '362 454-7719', 'belencabrera@gmail.com', 'app', 'padel', 8, 'hace 10 días', 0, false],
];

const NOTAS: Record<number, [string, string][]> = {
  3: [['Pidió factura A a nombre del gremio. Datos en el cuaderno de caja.', 'Rubén M. · hace 2 semanas']],
  5: [['Bloqueado por dos ausencias seguidas sin avisar, en horario pico.', 'Rubén M. · hace 12 días']],
  11: [
    ['Debe el partido del sábado. Dijo que pasa el viernes a pagar.', 'Sandra A. · hace 4 días'],
    ['Organiza el grupo de los martes, 10 jugadores.', 'Rubén M. · hace 2 meses'],
  ],
};

/** Historial de turnos por ficha. Sólo algunas lo tienen cargado. */
export const TURNOS: Record<number, TurnoHistorico[]> = {
  1: [
    { when: 'sáb 9 ago · 21:00 – 22:00', detalle: 'Cancha 1 · Fútbol 5 · 1 h', chip: 'Pagado' },
    { when: 'mar 5 ago · 19:00 – 20:30', detalle: 'Cancha 2 · Pádel · 1 h 30', chip: 'Pagado' },
    { when: 'dom 27 jul · 11:00 – 12:00', detalle: 'Cancha 3 · Pádel · 1 h', chip: 'Ausente' },
  ],
  3: [
    { when: 'mié 13 ago · 20:00 – 22:00', detalle: 'Cancha 1 · Fútbol 5 · 2 h', chip: 'Seña pagada' },
    { when: 'jue 7 ago · 21:00 – 22:00', detalle: 'Cancha 2 · Fútbol 5 · 1 h', chip: 'Pagado' },
  ],
  6: [{ when: 'vie 11 jul · 19:00 – 20:00', detalle: 'Cancha 3 · Pádel · 1 h', chip: 'Seña pagada' }],
  11: [{ when: 'sáb 2 ago · 22:00 – 23:00', detalle: 'Cancha 1 · Fútbol 5 · 1 h', chip: 'Sin pagar' }],
  17: [{ when: 'mar 5 ago · 20:00 – 21:00', detalle: 'Cancha 2 · Pádel · 1 h', chip: 'Seña pagada' }],
};

/** Fecha de alta derivada del orden del padrón, para que la demo sea estable. */
function altaDe(i: number): string {
  const d = new Date(2026, 7, 12 - ((i * 13) % 320));
  return d.getDate() + ' ' + MESES[d.getMonth()] + ' ' + d.getFullYear();
}

function personasIniciales(): Persona[] {
  return SEMILLA.map((p, i) => ({
    id: i + 1,
    nombre: p[0],
    tel: p[1],
    email: p[2],
    origen: p[3],
    deporte: p[4],
    turnos: p[5],
    ultima: p[6],
    deuda: p[7],
    bloqueado: p[8],
    alta: altaDe(i),
    notas: (NOTAS[i + 1] || []).map((n): Nota => ({ txt: n[0], autor: n[1] })),
  }));
}

// ── Canchas ──────────────────────────────────────────────────────────────────

/** Recargo o descuento de la cancha sobre el precio base del deporte. */
interface CanchaBase {
  nombre: string;
  detalle: string;
  techada: boolean;
  extra: number;
}

export const PADEL: CanchaBase[] = [
  { nombre: 'Cancha 1', detalle: 'Blindex techada', techada: true, extra: 0 },
  { nombre: 'Cancha 2', detalle: 'Blindex techada', techada: true, extra: 0 },
  { nombre: 'Cancha 3', detalle: 'Muro descubierta', techada: false, extra: -1000 },
  { nombre: 'Cancha 4', detalle: 'Panorámica techada', techada: true, extra: 2000 },
];

export const FUTBOL: CanchaBase[] = [
  { nombre: 'Cancha 1', detalle: 'Sintético techado', techada: true, extra: 3000 },
  { nombre: 'Cancha 2', detalle: 'Sintético aire libre', techada: false, extra: 0 },
  { nombre: 'Cancha 3', detalle: 'Sintético aire libre', techada: false, extra: 0 },
];

function canchasIniciales(): Cancha[] {
  const armar = (
    deporte: Deporte,
    ci: number,
    base: CanchaBase,
    horarioId: string,
    duraciones: number[],
    dia: number,
    noche: number,
  ): Cancha => ({
    deporte,
    ci,
    nombre: base.nombre,
    detalle: base.detalle,
    techada: base.techada,
    activa: true,
    horarioId,
    duraciones,
    incremento: 60,
    aviso: 0,
    precioDia: dia + base.extra,
    precioNoche: noche + base.extra,
    noche: 19 * 60,
  });

  const out: Cancha[] = [];
  PADEL.forEach((base, i) => {
    const horario = i === 2 ? 's3' : i === 3 ? 's2' : 's1';
    out.push(armar('padel', i, base, horario, [60, 90, 120], 12000, 15000));
  });
  FUTBOL.forEach((base, i) => {
    out.push(armar('futbol', i, base, i === 0 ? 's1' : 's2', [60, 120], 34000, 40000));
  });
  return out;
}

// ── Horarios ─────────────────────────────────────────────────────────────────

function horariosIniciales(): Horario[] {
  /** Mismo juego de tramos de lunes a viernes. */
  const laborables = (tramos: Tramo[]): Record<number, Tramo[]> => ({
    1: tramos.map((t) => [...t] as Tramo),
    2: tramos.map((t) => [...t] as Tramo),
    3: tramos.map((t) => [...t] as Tramo),
    4: tramos.map((t) => [...t] as Tramo),
    5: tramos.map((t) => [...t] as Tramo),
  });

  return [
    {
      id: 's1',
      nombre: 'Temporada alta',
      tz: 'Argentina (GMT−3)',
      semanal: {
        ...laborables([
          [480, 720],
          [780, 1020],
          [1080, 1440],
        ]),
        6: [
          [540, 840],
          [960, 1440],
        ],
        0: [[540, 780]],
      },
      fechas: [
        { fecha: '2026-08-16', tramos: [] },
        { fecha: '2026-08-29', tramos: [[540, 1200]] },
      ] as FechaEspecial[],
    },
    {
      id: 's2',
      nombre: 'Verano · turno tarde',
      tz: 'Argentina (GMT−3)',
      semanal: { ...laborables([[960, 1440]]), 6: [[600, 1440]], 0: [[600, 840]] },
      fechas: [],
    },
    {
      id: 's3',
      nombre: 'Canchas descubiertas',
      tz: 'Argentina (GMT−3)',
      semanal: {
        ...laborables([
          [480, 720],
          [780, 1020],
        ]),
        6: [[540, 840]],
        0: [],
      },
      fechas: [{ fecha: '2026-09-08', tramos: [] }],
    },
  ];
}

// ── Reservas creadas o tocadas durante la sesión ─────────────────────────────

/** Turno vendido desde la consola. Pisa a lo que hubiera generado la grilla. */
export interface ReservaCreada {
  deporte: Deporte;
  dateIdx: number;
  ci: number;
  t: number;
  dur: number;
  persona: string;
  tel: string;
  pago: 'total' | 'sena' | 'nada';
  precio: number;
}

interface Estado {
  personas: Persona[];
  canchas: Cancha[];
  horarios: Horario[];
  creadas: ReservaCreada[];
  /** Claves de turnos cancelados: el hueco vuelve a estar libre. */
  canceladas: string[];
  /** Claves de turnos marcados como ausencia. */
  ausentes: string[];
}

export const estado: Estado = {
  personas: personasIniciales(),
  canchas: canchasIniciales(),
  horarios: horariosIniciales(),
  creadas: [],
  canceladas: [],
  ausentes: [],
};
