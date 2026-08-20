/**
 * Adaptador de la base de personas y del contexto del club: traduce el contrato
 * de la API a los tipos del dominio. Las llamadas y las formas del backend vienen
 * del cliente generado (ADR-0016). Las fechas salen de acá ya escritas ("hace 3
 * días", "14 ago 2026"): la pantalla muestra, no calcula.
 */

import { DIA_CORTO, MESES, duracionTurno, fechaLarga, haceCuanto, hhmm } from '../domain/fechas';
import type { Club, FiltroPersonas, Nota, Persona, TurnoHistorico } from '../domain/types';
import { ApiError } from './http';
import { getContext } from './generated/context/context';
import { getPersonBookings } from './generated/bookings/bookings';
import {
  addPersonNote,
  blockPeople,
  createPerson,
  getPerson,
  registerPersonPayment,
  searchPeople,
  setPersonBlock,
} from './generated/people/people';
import type {
  NoteResponse,
  PersonBookingResponse,
  PersonResponse,
} from './generated/clubSpotApiV1.schemas';

const FILTRO_API: Record<FiltroPersonas, string> = {
  todas: 'all',
  sinturnos: 'withoutBookings',
  mostrador: 'counter',
  deuda: 'debt',
};

const ROLES: Record<string, string> = {
  administrator: 'administrador',
  memberDesk: 'socios',
  treasury: 'tesorería',
  courtReception: 'encargado',
  accessControl: 'control de acceso',
  coach: 'profesor',
  member: 'socio',
};

function aPersona(x: PersonResponse, notas: Nota[] = []): Persona {
  return {
    id: x.id,
    nombre: x.name,
    tel: x.phone,
    email: x.email,
    origen: x.origin === 'counter' ? 'mostrador' : 'app',
    turnos: x.bookings,
    ultima: x.lastBookingOn ? haceCuanto(x.lastBookingOn) : null,
    deuda: x.debt,
    bloqueado: x.isBlocked,
    alta: fechaLarga(x.createdAt.slice(0, 10)),
    notas,
  };
}

function aNota(x: NoteResponse): Nota {
  return { txt: x.text, autor: `${x.authorName} · ${haceCuanto(x.createdAt.slice(0, 10))}` };
}

// El historial trae los mismos turnos que cuenta la ficha: lo cancelado y lo vencido no
// cuenta como turno en ningún lado, así que tampoco se lista.
function chipDeTurno(x: PersonBookingResponse): string {
  if (x.paid >= x.price) return 'Pagado';
  return x.paid > 0 ? 'Seña pagada' : 'Sin pagar';
}

function aTurno(x: PersonBookingResponse): TurnoHistorico {
  const p = x.date.split('-').map((n) => parseInt(n, 10));
  const dia = new Date(p[0], p[1] - 1, p[2]);
  const deporte = x.sport === 'football' ? 'Fútbol 5' : 'Pádel';
  return {
    when: `${DIA_CORTO[dia.getDay()]} ${p[2]} ${MESES[p[1] - 1]} · ${hhmm(x.startMinute)} – ${hhmm(x.startMinute + x.durationMinutes)}`,
    detalle: `${x.courtName} · ${deporte} · ${duracionTurno(x.durationMinutes)}`,
    chip: chipDeTurno(x),
  };
}

// ── Club ─────────────────────────────────────────────────────────────────────

export async function fetchClub(): Promise<Club> {
  const ctx = await getContext();
  const partes = ctx.operator.name.trim().split(/\s+/);
  return {
    nombre: ctx.club.name,
    sede: ctx.club.venue ?? '',
    operador: ctx.operator.name,
    operadorIniciales: partes
      .slice(0, 2)
      .map((x) => x[0]?.toUpperCase() ?? '')
      .join(''),
    rol: ROLES[ctx.operator.roles[0]] ?? ctx.operator.roles[0] ?? '',
  };
}

// ── Personas ─────────────────────────────────────────────────────────────────

export interface ConsultaPersonas {
  q: string;
  filtro: FiltroPersonas;
  pagina: number;
}

export interface PaginaPersonas {
  items: Persona[];
  total: number;
  pagina: number;
  paginas: number;
  /** Tamaño de página que decidió el servidor. */
  porPagina: number;
  padron: number;
  atencion: number;
  deudaTotal: number;
  totales: Record<FiltroPersonas, number>;
}

export async function fetchPersonas(q: ConsultaPersonas): Promise<PaginaPersonas> {
  const pagina = await searchPeople({ q: q.q, filter: FILTRO_API[q.filtro], page: q.pagina });
  return {
    items: pagina.items.map((x) => aPersona(x)),
    total: pagina.total,
    pagina: pagina.page,
    paginas: pagina.pages,
    porPagina: pagina.pageSize,
    padron: pagina.census,
    atencion: pagina.needsAttention,
    deudaTotal: pagina.totalDebt,
    totales: {
      todas: pagina.totals.all,
      sinturnos: pagina.totals.withoutBookings,
      mostrador: pagina.totals.counter,
      deuda: pagina.totals.debt,
    },
  };
}

export interface FichaPersona {
  persona: Persona;
  turnos: TurnoHistorico[];
}

export async function fetchFicha(id: string): Promise<FichaPersona | null> {
  const [detalle, turnos] = await Promise.all([
    getPerson(id).catch(sinContenido),
    // El historial es del módulo de reservas: si el club no lo contrató la ruta
    // no existe y la ficha se muestra igual, sin turnos (AGENTS.md §5).
    getPersonBookings(id).catch(sinContenido),
  ]);
  if (!detalle) return null;
  return {
    persona: aPersona(detalle.person, detalle.notes.map(aNota)),
    turnos: (turnos ?? []).map(aTurno),
  };
}

export interface NuevaPersona {
  nombre: string;
  tel: string;
  email: string;
}

export async function crearPersona(input: NuevaPersona): Promise<Persona> {
  const creada = await createPerson({ name: input.nombre, phone: input.tel, email: input.email });
  return aPersona(creada);
}

export async function bloquearPersonas(ids: string[], bloqueado: boolean): Promise<number> {
  const r = await blockPeople({ ids, blocked: bloqueado });
  return r.affected;
}

export async function alternarBloqueo(id: string, bloqueado: boolean): Promise<boolean> {
  const r = await setPersonBlock(id, { blocked: bloqueado });
  return r.blocked;
}

export async function agregarNota(id: string, txt: string): Promise<void> {
  await addPersonNote(id, { text: txt });
}

export async function registrarPago(id: string): Promise<number> {
  const r = await registerPersonPayment(id);
  return r.paid;
}

/** 404 en una lectura no es un error a mostrar: es que no hay nada que mostrar. */
function sinContenido(error: unknown): null {
  if (error instanceof ApiError && error.status === 404) return null;
  throw error;
}
