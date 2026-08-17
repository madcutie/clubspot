/**
 * Backend simulado de la base de personas. Provisional: la pantalla de
 * Personas se conecta a la API real en un paso aparte (el backend de People ya
 * existe); cuando eso pase, este archivo y `store.ts` desaparecen.
 */

import type { Club, Deporte, FiltroPersonas, Persona, TurnoHistorico } from '../domain/types';
import { CLUB, TURNOS, estado } from './store';

const LATENCIA = 180;

function demora<T>(valor: T, ms = LATENCIA): Promise<T> {
  return new Promise((resolve) => setTimeout(() => resolve(valor), ms));
}

/** Copia defensiva: el mock no debe entregar referencias a su propio estado. */
function clonar<T>(v: T): T {
  return JSON.parse(JSON.stringify(v)) as T;
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
