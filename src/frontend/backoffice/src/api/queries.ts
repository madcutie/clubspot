import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { Cancha, Deporte, Horario } from '../domain/types';
import {
  agregarNota,
  alternarAusencia,
  alternarBloqueo,
  bloquearPersonas,
  cancelarTurno,
  cobrarTurno,
  crearPersona,
  crearReserva,
  fetchAgenda,
  fetchCanchas,
  fetchClub,
  fetchFicha,
  fetchHorarios,
  fetchPersonas,
  fetchPresupuesto,
  guardarCanchas,
  guardarHorarios,
  registrarPago,
  type ConsultaPersonas,
  type NuevaPersona,
  type NuevaReserva,
  type RefTurno,
} from './mockApi';

export const qk = {
  club: () => ['club'] as const,
  personas: () => ['personas'] as const,
  personasPagina: (q: ConsultaPersonas) => ['personas', q.filtro, q.q, q.pagina] as const,
  ficha: (id: number | null) => ['ficha', id] as const,
  agenda: () => ['agenda'] as const,
  agendaDia: (deporte: Deporte, dateIdx: number) => ['agenda', deporte, dateIdx] as const,
  canchas: () => ['canchas'] as const,
  horarios: () => ['horarios'] as const,
  presupuesto: (deporte: Deporte, ci: number, t: number, dur: number) =>
    ['presupuesto', deporte, ci, t, dur] as const,
};

/**
 * Un turno vendido lo pinta la agenda, pero también mueve el contador de la
 * ficha y el del padrón. Cada vez que cambia algo se invalida todo lo que lo
 * mira, en vez de parchear cachés a mano.
 */
function useInvalidar() {
  const qc = useQueryClient();
  return {
    personas: () => qc.invalidateQueries({ queryKey: qk.personas() }),
    fichas: () => qc.invalidateQueries({ queryKey: ['ficha'] }),
    agenda: () => {
      qc.invalidateQueries({ queryKey: qk.agenda() });
      qc.invalidateQueries({ queryKey: ['presupuesto'] });
    },
    canchas: () => qc.invalidateQueries({ queryKey: qk.canchas() }),
    horarios: () => qc.invalidateQueries({ queryKey: qk.horarios() }),
  };
}

// ── Club ─────────────────────────────────────────────────────────────────────

export function useClub() {
  return useQuery({ queryKey: qk.club(), queryFn: fetchClub, staleTime: Infinity });
}

// ── Personas ─────────────────────────────────────────────────────────────────

export function usePersonas(q: ConsultaPersonas) {
  return useQuery({
    queryKey: qk.personasPagina(q),
    queryFn: () => fetchPersonas(q),
    // Al tipear o cambiar de página se mantiene la tabla anterior en pantalla
    // en vez de vaciarla mientras llega la nueva.
    placeholderData: (prev) => prev,
  });
}

export function useFicha(id: number | null) {
  return useQuery({
    queryKey: qk.ficha(id),
    queryFn: () => fetchFicha(id as number),
    enabled: id != null,
  });
}

export function useCrearPersona() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (input: NuevaPersona) => crearPersona(input),
    onSuccess: () => {
      inv.personas();
      inv.agenda();
    },
  });
}

export function useBloquearPersonas() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (v: { ids: number[]; bloqueado: boolean }) => bloquearPersonas(v.ids, v.bloqueado),
    onSuccess: () => {
      inv.personas();
      inv.fichas();
    },
  });
}

export function useAlternarBloqueo() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (id: number) => alternarBloqueo(id),
    onSuccess: () => {
      inv.personas();
      inv.fichas();
    },
  });
}

export function useAgregarNota() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (v: { id: number; txt: string }) => agregarNota(v.id, v.txt),
    onSuccess: () => inv.fichas(),
  });
}

export function useRegistrarPago() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (id: number) => registrarPago(id),
    onSuccess: () => {
      inv.personas();
      inv.fichas();
    },
  });
}

// ── Agenda ───────────────────────────────────────────────────────────────────

export function useAgenda(deporte: Deporte, dateIdx: number) {
  return useQuery({
    queryKey: qk.agendaDia(deporte, dateIdx),
    queryFn: () => fetchAgenda(deporte, dateIdx),
    placeholderData: (prev) => prev,
  });
}

/** Precio y seña de un turno que se está por vender. */
export function usePresupuesto(deporte: Deporte, ci: number, t: number | null, dur: number) {
  return useQuery({
    queryKey: qk.presupuesto(deporte, ci, t ?? -1, dur),
    queryFn: () => fetchPresupuesto(deporte, ci, t as number, dur),
    enabled: t != null,
    placeholderData: (prev) => prev,
  });
}

export function useCrearReserva() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (input: NuevaReserva) => crearReserva(input),
    onSuccess: () => {
      inv.agenda();
      inv.personas();
    },
  });
}

export function useCobrarTurno() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (v: {
      ref: RefTurno;
      datos: { dur: number; persona: string; tel: string; precio: number };
    }) => cobrarTurno(v.ref, v.datos),
    onSuccess: () => inv.agenda(),
  });
}

export function useCancelarTurno() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (ref: RefTurno) => cancelarTurno(ref),
    onSuccess: () => inv.agenda(),
  });
}

export function useAlternarAusencia() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (key: string) => alternarAusencia(key),
    onSuccess: () => inv.agenda(),
  });
}

// ── Canchas y horarios ───────────────────────────────────────────────────────

export function useCanchas() {
  return useQuery({ queryKey: qk.canchas(), queryFn: fetchCanchas });
}

export function useHorarios() {
  return useQuery({ queryKey: qk.horarios(), queryFn: fetchHorarios });
}

export function useGuardarCanchas() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (canchas: Cancha[]) => guardarCanchas(canchas),
    onSuccess: () => {
      inv.canchas();
      inv.agenda();
    },
  });
}

export function useGuardarHorarios() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (horarios: Horario[]) => guardarHorarios(horarios),
    onSuccess: () => {
      inv.horarios();
      inv.agenda();
    },
  });
}

