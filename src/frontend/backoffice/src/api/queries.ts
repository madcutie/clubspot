import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { Cancha, Deporte, Horario } from '../domain/types';
import { useTostada } from '../ui/Tostadas';
import {
  borrarExcepcion,
  cancelarReserva,
  cobrarReserva,
  crearExcepcion,
  crearReserva,
  fetchAgenda,
  fetchCanchas,
  fetchExcepciones,
  fetchHorarios,
  guardarCanchas,
  guardarHorarios,
  type NuevaExcepcion,
  type NuevaReserva,
} from './apiHttp';
import { ApiError } from './http';
import {
  agregarNota,
  alternarBloqueo,
  bloquearPersonas,
  crearPersona,
  fetchClub,
  fetchFicha,
  fetchPersonas,
  registrarPago,
  type ConsultaPersonas,
  type NuevaPersona,
} from './personasHttp';

export const qk = {
  club: () => ['club'] as const,
  personas: () => ['personas'] as const,
  personasPagina: (q: ConsultaPersonas) => ['personas', q.filtro, q.q, q.pagina] as const,
  ficha: (id: string | null) => ['ficha', id] as const,
  agenda: () => ['agenda'] as const,
  agendaDia: (deporte: Deporte, fecha: string) => ['agenda', deporte, fecha] as const,
  cobro: (reservaId: string) => ['cobro', reservaId] as const,
  canchas: () => ['canchas'] as const,
  horarios: () => ['horarios'] as const,
  excepciones: (desde: string, hasta: string) => ['excepciones', desde, hasta] as const,
};

/**
 * Cada vez que cambia algo se invalida todo lo que lo mira, en vez de parchear
 * cachés a mano.
 */
export function useInvalidar() {
  const qc = useQueryClient();
  return {
    personas: () => qc.invalidateQueries({ queryKey: qk.personas() }),
    fichas: () => qc.invalidateQueries({ queryKey: ['ficha'] }),
    agenda: () => qc.invalidateQueries({ queryKey: qk.agenda() }),
    canchas: () => qc.invalidateQueries({ queryKey: qk.canchas() }),
    horarios: () => qc.invalidateQueries({ queryKey: qk.horarios() }),
    excepciones: () => qc.invalidateQueries({ queryKey: ['excepciones'] }),
  };
}

// ── Club ─────────────────────────────────────────────────────────────────────

export function useClub() {
  return useQuery({ queryKey: qk.club(), queryFn: fetchClub, staleTime: Infinity });
}

// ── Personas ─────────────────────────────────────────────────────────────────

export function usePersonas(q: ConsultaPersonas, habilitado = true) {
  return useQuery({
    queryKey: qk.personasPagina(q),
    queryFn: () => fetchPersonas(q),
    enabled: habilitado,
    // Al tipear o cambiar de página se mantiene la tabla anterior en pantalla
    // en vez de vaciarla mientras llega la nueva.
    placeholderData: (prev) => prev,
  });
}

export function useFicha(id: string | null) {
  return useQuery({
    queryKey: qk.ficha(id),
    queryFn: () => fetchFicha(id as string),
    enabled: id != null,
  });
}

export function useCrearPersona() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (input: NuevaPersona) => crearPersona(input),
    onSuccess: () => inv.personas(),
  });
}

export function useBloquearPersonas() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (v: { ids: string[]; bloqueado: boolean }) => bloquearPersonas(v.ids, v.bloqueado),
    onSuccess: () => {
      inv.personas();
      inv.fichas();
    },
  });
}

export function useAlternarBloqueo() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (v: { id: string; bloqueado: boolean }) => alternarBloqueo(v.id, v.bloqueado),
    onSuccess: () => {
      inv.personas();
      inv.fichas();
    },
  });
}

export function useAgregarNota() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (v: { id: string; txt: string }) => agregarNota(v.id, v.txt),
    onSuccess: () => inv.fichas(),
  });
}

export function useRegistrarPago() {
  const inv = useInvalidar();
  return useMutation({
    mutationFn: (id: string) => registrarPago(id),
    onSuccess: () => {
      inv.personas();
      inv.fichas();
    },
  });
}

// ── Agenda ───────────────────────────────────────────────────────────────────

export function useAgenda(deporte: Deporte, fecha: string, habilitado = true) {
  return useQuery({
    queryKey: qk.agendaDia(deporte, fecha),
    queryFn: () => fetchAgenda(deporte, fecha),
    enabled: habilitado,
    placeholderData: (prev) => prev,
  });
}

export function useCrearReserva() {
  const inv = useInvalidar();
  const avisar = useTostada();
  return useMutation({
    mutationFn: (input: NuevaReserva) => crearReserva(input),
    onSuccess: () => inv.agenda(),
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        avisar('Ese turno acaba de ocuparse');
        inv.agenda();
      } else {
        avisar('No se pudo crear la reserva. Probá de nuevo.');
      }
    },
  });
}

/**
 * Link de cobro de un turno. Es una query y no una mutación a propósito: el cache de
 * React Query es global, así que el link sobrevive a que el panel se desmonte —una
 * mutación pierde su resultado ahí—. Reemitir es `refetch`: la cancha ya está
 * reservada, no hay hold que cuidar.
 */
export function useCobro(reservaId: string) {
  return useQuery({
    queryKey: qk.cobro(reservaId),
    queryFn: () => cobrarReserva(reservaId),
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    retry: false,
  });
}

export function useCancelarReserva() {
  const inv = useInvalidar();
  const avisar = useTostada();
  return useMutation({
    mutationFn: (v: { id: string; motivo: string }) => cancelarReserva(v.id, v.motivo),
    onSuccess: () => inv.agenda(),
    onError: () => avisar('No se pudo cancelar la reserva. Probá de nuevo.'),
  });
}

// ── Canchas y horarios ───────────────────────────────────────────────────────

// `habilitado` lo decide el rol: sin permiso no se pide, así la consola no dispara requests
// que la API va a rechazar con 403 (ADR-0018). Vale también para agenda y personas.
export function useCanchas(habilitado = true) {
  return useQuery({ queryKey: qk.canchas(), queryFn: fetchCanchas, enabled: habilitado });
}

export function useHorarios(habilitado = true) {
  return useQuery({ queryKey: qk.horarios(), queryFn: fetchHorarios, enabled: habilitado });
}

function mensajeDeGuardado(error: unknown): string {
  return error instanceof ApiError && error.status === 409
    ? 'No se pudo guardar: la configuración cambió en el servidor. Recargá para ver lo último'
    : 'No se pudo guardar. Probá de nuevo.';
}

export function useGuardarCanchas() {
  const inv = useInvalidar();
  const avisar = useTostada();
  return useMutation({
    mutationFn: (canchas: Cancha[]) => guardarCanchas(canchas),
    onSuccess: () => {
      inv.canchas();
      inv.agenda();
    },
    onError: (error) => avisar(mensajeDeGuardado(error)),
  });
}

export function useGuardarHorarios() {
  const inv = useInvalidar();
  const avisar = useTostada();
  return useMutation({
    mutationFn: (horarios: Horario[]) => guardarHorarios(horarios),
    onSuccess: () => {
      inv.horarios();
      inv.agenda();
    },
    onError: (error) => avisar(mensajeDeGuardado(error)),
  });
}

// ── Excepciones ──────────────────────────────────────────────────────────────

export function useExcepciones(desde: string, hasta: string) {
  return useQuery({
    queryKey: qk.excepciones(desde, hasta),
    queryFn: () => fetchExcepciones(desde, hasta),
  });
}

export function useCrearExcepcion() {
  const inv = useInvalidar();
  const avisar = useTostada();
  return useMutation({
    mutationFn: (input: NuevaExcepcion) => crearExcepcion(input),
    onSuccess: () => {
      inv.excepciones();
      inv.agenda();
    },
    onError: (error) => avisar(mensajeDeGuardado(error)),
  });
}

export function useBorrarExcepcion() {
  const inv = useInvalidar();
  const avisar = useTostada();
  return useMutation({
    mutationFn: (id: string) => borrarExcepcion(id),
    onSuccess: () => {
      inv.excepciones();
      inv.agenda();
    },
    onError: () => avisar('No se pudo borrar la excepción. Probá de nuevo.'),
  });
}
