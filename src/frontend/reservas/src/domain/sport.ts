import type { Sport } from './types';

export function sportLabel(sport: Sport): string {
  return sport === 'padel' ? 'Pádel' : 'Fútbol 5';
}
