/**
 * Fechas y horas del club.
 *
 * El prototipo trabaja con un "hoy" fijo (14 de agosto de 2026) para que la
 * demo sea siempre la misma. Cuando exista la API, el día lo manda el backend
 * resuelto en la zona del club, nunca `new Date()` del navegador.
 */

export const HOY = { anio: 2026, mes: 7, dia: 14 } as const;

export const MESES = [
  'ene',
  'feb',
  'mar',
  'abr',
  'may',
  'jun',
  'jul',
  'ago',
  'sep',
  'oct',
  'nov',
  'dic',
];

/** Abreviaturas indexadas como `Date.getDay()`. */
export const DIA_CORTO = ['dom', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb'];

/** Días de la semana en el orden en que los lee un operador: lunes primero. */
export const DIAS: { dow: number; label: string }[] = [
  { dow: 1, label: 'Lunes' },
  { dow: 2, label: 'Martes' },
  { dow: 3, label: 'Miércoles' },
  { dow: 4, label: 'Jueves' },
  { dow: 5, label: 'Viernes' },
  { dow: 6, label: 'Sábado' },
  { dow: 0, label: 'Domingo' },
];

/** Hora simulada del reloj del club, en minutos desde medianoche. */
export const AHORA = 14 * 60 + 30;

/** Día `i` contando desde hoy. */
export function fechaDe(i: number): Date {
  return new Date(HOY.anio, HOY.mes, HOY.dia + i);
}

/** "hoy", "mañana" o "sáb 16". */
export function etiquetaDia(i: number): string {
  const d = fechaDe(i);
  if (i === 0) return 'hoy';
  if (i === 1) return 'mañana';
  return DIA_CORTO[d.getDay()] + ' ' + d.getDate();
}

/** Minutos desde medianoche a "20:30". */
export function hhmm(m: number): string {
  return String(Math.floor(m / 60)).padStart(2, '0') + ':' + String(m % 60).padStart(2, '0');
}

/** ISO corto (yyyy-mm-dd) del día `i`, que es como se guardan las fechas propias. */
export function isoDe(i: number): string {
  const d = fechaDe(i);
  return (
    d.getFullYear() +
    '-' +
    String(d.getMonth() + 1).padStart(2, '0') +
    '-' +
    String(d.getDate()).padStart(2, '0')
  );
}

/** "2026-08-29" a "29 ago 2026". */
export function fechaLarga(iso: string): string {
  const p = iso.split('-');
  return parseInt(p[2], 10) + ' ' + MESES[parseInt(p[1], 10) - 1] + ' ' + p[0];
}

/** Duración de un tramo en palabras: "3 h", "2 h 30". */
export function duracionLarga(minutos: number): string {
  if (minutos <= 0) return '';
  return Math.floor(minutos / 60) + ' h' + (minutos % 60 ? ' 30' : '');
}

/** Duración de un turno tal como la nombra el mostrador. */
export function duracionTurno(minutos: number): string {
  if (minutos === 60) return '1 h';
  if (minutos === 90) return '1 h 30';
  return '2 h';
}
