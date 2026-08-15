import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CLUB } from '../domain/catalog';
import type { Sport } from '../domain/types';
import {
  cancelBooking,
  fetchAvailability,
  fetchBookings,
  fetchDays,
  fetchSportCounts,
  payReservation,
  type AvailabilityQuery,
  type PayInput,
} from './mockApi';

export const qk = {
  days: (sport: Sport) => ['days', sport] as const,
  sportCounts: (dateIdx: number) => ['sportCounts', dateIdx] as const,
  availability: (q: AvailabilityQuery) =>
    ['availability', q.sport, q.dateIdx, q.dur, q.ctype, q.hour] as const,
  bookings: () => ['bookings'] as const,
};

export function useDays(sport: Sport) {
  return useQuery({
    queryKey: qk.days(sport),
    queryFn: () => fetchDays(sport, CLUB.diasVisibles),
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

export function useBookings() {
  return useQuery({ queryKey: qk.bookings(), queryFn: fetchBookings });
}

export function usePayReservation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: PayInput) => payReservation(input),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.bookings() });
    },
  });
}

export function useCancelBooking() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => cancelBooking(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.bookings() });
    },
  });
}
