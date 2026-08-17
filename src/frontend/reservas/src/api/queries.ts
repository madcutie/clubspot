import { useQuery } from '@tanstack/react-query';
import type { Sport } from '../domain/types';
import {
  DIAS_VISIBLES,
  fetchAvailability,
  fetchClub,
  fetchCourtCounts,
  fetchDays,
  fetchSportCounts,
  type AvailabilityQuery,
} from './portalApi';

export const qk = {
  club: () => ['club'] as const,
  courtCounts: () => ['courtCounts'] as const,
  days: (sport: Sport) => ['days', sport] as const,
  sportCounts: (dateIdx: number) => ['sportCounts', dateIdx] as const,
  availability: (q: AvailabilityQuery) =>
    ['availability', q.sport, q.dateIdx, q.dur, q.ctype, q.hour] as const,
};

export function useClub() {
  return useQuery({ queryKey: qk.club(), queryFn: fetchClub });
}

export function useCourtCounts() {
  return useQuery({ queryKey: qk.courtCounts(), queryFn: fetchCourtCounts });
}

export function useDays(sport: Sport) {
  return useQuery({
    queryKey: qk.days(sport),
    queryFn: () => fetchDays(sport, DIAS_VISIBLES),
  });
}

export function useSportCounts(dateIdx: number) {
  return useQuery({
    queryKey: qk.sportCounts(dateIdx),
    queryFn: () => fetchSportCounts(dateIdx),
  });
}

export function useAvailability(q: AvailabilityQuery) {
  return useQuery({
    queryKey: qk.availability(q),
    queryFn: () => fetchAvailability(q),
    // Al cambiar duración/filtro mantenemos la grilla anterior visible en vez
    // de vaciar la pantalla mientras llega la nueva.
    placeholderData: (prev) => prev,
  });
}
