/**
 * Backend simulado de la consola.
 *
 * Todas las funciones son `async` y devuelven DTOs planos, con la misma forma
 * que tendría la API real: cuando exista, se reemplaza este archivo por
 * llamadas HTTP y las pantallas no cambian.
 */

import type {
  Agenda,
  Cancha,
  Club,
  ColumnaAgenda,
  Deporte,
  FiltroPersonas,
  Horario,
  Pago,
  Persona,
  Slot,
  SlotOcupado,
  TurnoHistorico,
} from '../domain/types';
import { sena } from '../domain/dinero';
import { abierto, abiertoRango } from '../domain/horarios';
import { CLUB, TURNOS, estado, type ReservaCreada } from './store';

const LATENCIA = 180;

function demora<T>(valor: T, ms = LATENCIA): Promise<T> {
  return new Promise((resolve) => setTimeout(() => resolve(valor), ms));
}

/** Copia defensiva: el mock no debe entregar referencias a su propio estado. */
function clonar<T>(v: T): T {
  return JSON.parse(JSON.stringify(v)) as T;
}

/** Hash estable (FNV-1a) para que la agenda de ejemplo sea siempre la misma. */
function hash(s: string): number {
  let h = 2166136261;
  for (let i = 0; i < s.length; i++) {
    h ^= s.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return h >>> 0;
}

// ── Club ─────────────────────────────────────────────────────────────────────

export async function fetchClub(): Promise<Club> {
  return demora(CLUB, 60);
}

// ── Personas ─────────────────────────────────────────────────────────────────

export const POR_PAGINA = 14;

export interface ConsultaPersonas {
  q: string;
  filtro: FiltroPersonas;
  pagina: number;
}

export interface PaginaPersonas {
  items: Persona[];
  /** Personas que pasan el filtro. */
  total: number;
  pagina: number;
  paginas: number;
  /** Cuántas hay en la base, sin filtrar. */
  padron: number;
  /** Con deuda o bloqueadas. */
  atencion: number;
  deudaTotal: number;
  totales: Record<FiltroPersonas, number>;
}

function pasaFiltro(p: Persona, filtro: FiltroPersonas): boolean {
  if (filtro === 'sinturnos') return p.turnos === 0;
  if (filtro === 'mostrador') return p.origen === 'mostrador';
  if (filtro === 'deuda') return p.deuda > 0 || p.bloqueado;
  return true;
}

/**
 * Si lo tipeado tiene dígitos se busca por teléfono y sólo por teléfono: en el
 * mostrador el teléfono es el identificador, y mezclarlo con el nombre devuelve
 * ruido justo cuando hay alguien esperando.
 */
function pasaBusqueda(p: Persona, q: string): boolean {
  const crudo = q.trim().toLowerCase();
  if (!crudo) return true;
  const digitos = crudo.replace(/[^0-9]/g, '');
  if (digitos) return p.tel.replace(/[^0-9]/g, '').includes(digitos);
  return p.nombre.toLowerCase().includes(crudo) || p.email.toLowerCase().includes(crudo);
}

export async function fetchPersonas(q: ConsultaPersonas): Promise<PaginaPersonas> {
  const todas = estado.personas;
  const lista = todas.filter((p) => pasaFiltro(p, q.filtro)).filter((p) => pasaBusqueda(p, q.q));
  const paginas = Math.max(1, Math.ceil(lista.length / POR_PAGINA));
  const pagina = Math.min(Math.max(0, q.pagina), paginas - 1);
  const atencion = todas.filter((p) => p.deuda > 0 || p.bloqueado).length;

  return demora({
    items: clonar(lista.slice(pagina * POR_PAGINA, pagina * POR_PAGINA + POR_PAGINA)),
    total: lista.length,
    pagina,
    paginas,
    padron: todas.length,
    atencion,
    deudaTotal: todas.reduce((a, p) => a + p.deuda, 0),
    totales: {
      todas: todas.length,
      sinturnos: todas.filter((p) => p.turnos === 0).length,
      mostrador: todas.filter((p) => p.origen === 'mostrador').length,
      deuda: atencion,
    },
  });
}

export interface FichaPersona {
  persona: Persona;
  turnos: TurnoHistorico[];
}

export async function fetchFicha(id: number): Promise<FichaPersona | null> {
  const persona = estado.personas.find((p) => p.id === id);
  if (!persona) return demora(null, 60);
  return demora({ persona: clonar(persona), turnos: clonar(TURNOS[id] || []) }, 60);
}

export interface NuevaPersona {
  nombre: string;
  tel: string;
  email: string;
  deporte: Deporte;
}

export async function crearPersona(input: NuevaPersona): Promise<Persona> {
  const nueva: Persona = {
    id: 1000 + estado.personas.length,
    nombre: input.nombre.trim(),
    tel: input.tel.trim(),
    email: input.email.trim(),
    origen: 'mostrador',
    deporte: input.deporte,
    turnos: 0,
    ultima: null,
    deuda: 0,
    bloqueado: false,
    alta: '14 ago 2026',
    notas: [],
  };
  estado.personas = [nueva, ...estado.personas];
  return demora(clonar(nueva));
}

/** Bloquea o desbloquea varias fichas de una. */
export async function bloquearPersonas(ids: number[], bloqueado: boolean): Promise<number> {
  estado.personas = estado.personas.map((p) =>
    ids.includes(p.id) ? { ...p, bloqueado } : p,
  );
  return demora(ids.length);
}

export async function alternarBloqueo(id: number): Promise<boolean> {
  const p = estado.personas.find((x) => x.id === id);
  if (!p) return demora(false);
  const bloqueado = !p.bloqueado;
  estado.personas = estado.personas.map((x) => (x.id === id ? { ...x, bloqueado } : x));
  return demora(bloqueado);
}

export async function agregarNota(id: number, txt: string): Promise<void> {
  const nota = { txt: txt.trim(), autor: CLUB.operador.split(' ')[0] + ' ' + CLUB.operador.split(' ')[1][0] + '. · ahora' };
  estado.personas = estado.personas.map((p) =>
    p.id === id ? { ...p, notas: [nota, ...p.notas] } : p,
  );
  return demora(undefined);
}

/** Cancela el saldo de la ficha. En el sistema real esto es un movimiento de caja. */
export async function registrarPago(id: number): Promise<number> {
  const p = estado.personas.find((x) => x.id === id);
  const deuda = p ? p.deuda : 0;
  estado.personas = estado.personas.map((x) => (x.id === id ? { ...x, deuda: 0 } : x));
  return demora(deuda);
}

// ── Agenda ───────────────────────────────────────────────────────────────────

/** Primera y última hora que muestra la grilla. */
export const GRILLA_DESDE = 8 * 60;
export const GRILLA_HASTA = 24 * 60;

function canchasDe(deporte: Deporte): Cancha[] {
  return estado.canchas.filter((c) => c.deporte === deporte).sort((a, b) => a.ci - b.ci);
}

function horarioDe(cancha: Cancha): Horario {
  return estado.horarios.find((h) => h.id === cancha.horarioId) || estado.horarios[0];
}

/** Precio de un turno según la tarifa cargada en la cancha, redondeado a la centena. */
export function precioTurno(cancha: Cancha, t: number, dur: number): number {
  const base = t >= cancha.noche ? cancha.precioNoche : cancha.precioDia;
  return Math.round(((base * dur) / 60 / 100)) * 100;
}

export function claveTurno(deporte: Deporte, dateIdx: number, ci: number, t: number): string {
  return deporte + '|' + dateIdx + '|' + ci + '|' + t;
}

function ocupadoDe(
  deporte: Deporte,
  dateIdx: number,
  cancha: Cancha,
  t: number,
  dur: number,
  creada: ReservaCreada | null,
): SlotOcupado {
  const key = claveTurno(deporte, dateIdx, cancha.ci, t);
  let persona: string;
  let tel: string;
  let pago: Pago;

  if (creada) {
    persona = creada.persona;
    tel = creada.tel;
    pago = creada.pago;
  } else {
    // Turno de ejemplo: la persona y el estado de cobro salen del hash de la
    // celda, así la demo muestra siempre la misma agenda.
    const p = estado.personas[hash(key + 'p') % estado.personas.length];
    persona = p.nombre;
    tel = p.tel;
    const r = hash(key + 'g') % 100;
    pago = r < 52 ? 'total' : r < 88 ? 'sena' : 'nada';
  }

  const precio = creada && creada.precio ? creada.precio : precioTurno(cancha, t, dur);
  const anticipo = sena(precio);
  return {
    libre: false,
    key,
    id: 'TRN-' + ((hash(key) % 9000) + 1000),
    t,
    dur,
    ci: cancha.ci,
    persona,
    tel,
    pago,
    precio,
    saldo: pago === 'total' ? 0 : pago === 'sena' ? precio - anticipo : precio,
    ausente: estado.ausentes.includes(key),
  };
}

/**
 * Arma la columna de una cancha recorriendo el día de media hora en media hora.
 * Cada paso decide una de cuatro cosas: turno que vendió la consola, franja
 * cerrada, arranque que el incremento no permite, o hueco vendible.
 */
function columnaDe(deporte: Deporte, dateIdx: number, cancha: Cancha): Slot[] {
  const horario = horarioDe(cancha);
  const propias = estado.creadas.filter(
    (a) => a.deporte === deporte && a.dateIdx === dateIdx && a.ci === cancha.ci,
  );
  const filaLibre = (m: number) =>
    m < GRILLA_HASTA && abierto(cancha, horario, dateIdx, m) && !propias.some((x) => x.t === m);

  const out: Slot[] = [];
  let t = GRILLA_DESDE;

  while (t < GRILLA_HASTA) {
    const creada = propias.find((x) => x.t === t);
    if (creada) {
      out.push(ocupadoDe(deporte, dateIdx, cancha, t, creada.dur, creada));
      t += creada.dur;
      continue;
    }

    const key = claveTurno(deporte, dateIdx, cancha.ci, t);
    if (estado.canceladas.includes(key)) {
      out.push({ libre: true, t, ci: cancha.ci, span: 1, cerrado: false, offGrid: false });
      t += 30;
      continue;
    }

    if (!abierto(cancha, horario, dateIdx, t)) {
      let n = 0;
      while (
        t + n * 30 < GRILLA_HASTA &&
        !abierto(cancha, horario, dateIdx, t + n * 30) &&
        !propias.some((x) => x.t === t + n * 30)
      ) {
        n++;
      }
      out.push({ libre: true, t, ci: cancha.ci, span: n, cerrado: true, offGrid: false });
      t += n * 30;
      continue;
    }

    const inc = cancha.incremento;
    if (t % inc !== 0) {
      const hastaArranque = Math.max(1, (Math.ceil(t / inc) * inc - t) / 30);
      let k = 1;
      while (k < hastaArranque && filaLibre(t + k * 30)) k++;
      out.push({ libre: true, t, ci: cancha.ci, span: k, cerrado: false, offGrid: true });
      t += k * 30;
      continue;
    }

    const r = hash(key);
    const nocturno = t >= cancha.noche;
    if (r % 100 < (nocturno ? 70 : 38)) {
      const duraciones = [...cancha.duraciones].sort((a, b) => a - b);
      const minima = duraciones[0];
      let dur = duraciones[r % duraciones.length];
      const entra = (d: number) =>
        t + d <= GRILLA_HASTA &&
        abiertoRango(cancha, horario, dateIdx, t, d) &&
        !propias.some((x) => x.t > t && x.t < t + d);
      while (dur > minima && !entra(dur)) dur -= 30;
      if (entra(dur)) {
        out.push(ocupadoDe(deporte, dateIdx, cancha, t, dur, null));
        t += dur;
        continue;
      }
    }

    // Hueco vendible: se agrupan las medias horas hasta el próximo arranque.
    let n = 1;
    const filas = Math.max(1, inc / 30);
    while (n < filas && filaLibre(t + n * 30)) n++;
    out.push({ libre: true, t, ci: cancha.ci, span: n, cerrado: false, offGrid: false });
    t += n * 30;
  }

  return out;
}

export async function fetchAgenda(deporte: Deporte, dateIdx: number): Promise<Agenda> {
  const canchas = canchasDe(deporte);
  const columnas: ColumnaAgenda[] = canchas.map((cancha) => ({
    ci: cancha.ci,
    nombre: cancha.nombre,
    detalle: cancha.detalle,
    items: columnaDe(deporte, dateIdx, cancha),
  }));

  const vendidos = columnas.flatMap((col) => col.items.filter((i): i is SlotOcupado => !i.libre));
  const minutos = vendidos.reduce((a, b) => a + b.dur, 0);
  const capacidad = Math.max(1, canchas.length) * (GRILLA_HASTA - GRILLA_DESDE);

  return demora({
    columnas,
    turnos: vendidos.length,
    ocupacion: Math.round((minutos / capacidad) * 100),
    porCobrar: vendidos.reduce((a, b) => a + b.saldo, 0),
  });
}

/** Precio y seña de un turno que todavía no se vendió. */
export async function fetchPresupuesto(
  deporte: Deporte,
  ci: number,
  t: number,
  dur: number,
): Promise<{ precio: number; sena: number }> {
  const cancha = canchasDe(deporte)[ci];
  const precio = cancha ? precioTurno(cancha, t, dur) : 0;
  return demora({ precio, sena: sena(precio) }, 40);
}

export interface NuevaReserva {
  deporte: Deporte;
  dateIdx: number;
  ci: number;
  t: number;
  dur: number;
  /** Ficha existente, o `null` si hay que crearla con `nombre`. */
  personaId: number | null;
  nombre: string;
  pago: Pago;
}

export async function crearReserva(input: NuevaReserva): Promise<{ persona: string }> {
  const cancha = canchasDe(input.deporte)[input.ci];
  const precio = cancha ? precioTurno(cancha, input.t, input.dur) : 0;
  const existente = input.personaId != null
    ? estado.personas.find((p) => p.id === input.personaId)
    : undefined;

  const persona = existente ? existente.nombre : input.nombre.trim();
  const tel = existente ? existente.tel : '—';

  if (!existente) {
    // Turno a nombre de alguien que no está en la base: se le abre la ficha
    // en el mismo movimiento, para que el turno nunca quede huérfano.
    estado.personas = [
      {
        id: 2000 + estado.personas.length,
        nombre: persona,
        tel: '—',
        email: '',
        origen: 'mostrador',
        deporte: input.deporte,
        turnos: 1,
        ultima: 'hoy',
        deuda: 0,
        bloqueado: false,
        alta: '14 ago 2026',
        notas: [],
      },
      ...estado.personas,
    ];
  }

  estado.creadas = [
    ...estado.creadas,
    {
      deporte: input.deporte,
      dateIdx: input.dateIdx,
      ci: input.ci,
      t: input.t,
      dur: input.dur,
      persona,
      tel,
      pago: input.pago,
      precio,
    },
  ];

  return demora({ persona });
}

export interface RefTurno {
  deporte: Deporte;
  dateIdx: number;
  ci: number;
  t: number;
}

/** Cobra el saldo pendiente: el turno queda pagado y sin saldo. */
export async function cobrarTurno(
  ref: RefTurno,
  datos: { dur: number; persona: string; tel: string; precio: number },
): Promise<number> {
  const previo = estado.creadas.find(
    (a) => a.deporte === ref.deporte && a.dateIdx === ref.dateIdx && a.ci === ref.ci && a.t === ref.t,
  );
  estado.creadas = estado.creadas
    .filter((a) => a !== previo)
    .concat([{ ...ref, ...datos, pago: 'total' }]);
  return demora(0);
}

export async function cancelarTurno(ref: RefTurno): Promise<void> {
  const key = claveTurno(ref.deporte, ref.dateIdx, ref.ci, ref.t);
  estado.canceladas = [...estado.canceladas, key];
  estado.creadas = estado.creadas.filter(
    (a) => !(a.deporte === ref.deporte && a.dateIdx === ref.dateIdx && a.ci === ref.ci && a.t === ref.t),
  );
  return demora(undefined);
}

/** Marca o desmarca la ausencia. Devuelve el estado en que quedó. */
export async function alternarAusencia(key: string): Promise<boolean> {
  const estabaMarcada = estado.ausentes.includes(key);
  estado.ausentes = estabaMarcada
    ? estado.ausentes.filter((k) => k !== key)
    : [...estado.ausentes, key];
  return demora(!estabaMarcada);
}

// ── Canchas y horarios ───────────────────────────────────────────────────────

export async function fetchCanchas(): Promise<Cancha[]> {
  return demora(clonar(estado.canchas), 80);
}

export async function guardarCanchas(canchas: Cancha[]): Promise<void> {
  estado.canchas = clonar(canchas);
  return demora(undefined);
}

export async function fetchHorarios(): Promise<Horario[]> {
  return demora(clonar(estado.horarios), 80);
}

export async function guardarHorarios(horarios: Horario[]): Promise<void> {
  estado.horarios = clonar(horarios);
  return demora(undefined);
}
