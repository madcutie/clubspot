/** Fechas y horas del club. */

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

/** Minutos desde medianoche del reloj real, para la línea de la hora en la agenda. */
export function minutosDeAhora(): number {
  const d = new Date();
  return d.getHours() * 60 + d.getMinutes();
}

/** Día `i` contando desde hoy, en la fecha local del navegador. */
export function fechaDe(i: number): Date {
  const hoy = new Date();
  return new Date(hoy.getFullYear(), hoy.getMonth(), hoy.getDate() + i);
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

/** Duración de un turno tal como la nombra el mostrador: "1 h", "1 h 30". */
export function duracionTurno(minutos: number): string {
  const horas = Math.floor(minutos / 60);
  const resto = minutos % 60;
  return resto ? `${horas} h ${resto}` : `${horas} h`;
}

/** "2026-08-16" a "hace 3 días", como lo diría el mostrador. */
export function haceCuanto(iso: string): string {
  const p = iso.split('-').map((x) => parseInt(x, 10));
  const dia = new Date(p[0], p[1] - 1, p[2]);
  const hoy = new Date();
  const dias = Math.round(
    (new Date(hoy.getFullYear(), hoy.getMonth(), hoy.getDate()).getTime() - dia.getTime()) / 86400000,
  );
  if (dias <= 0) return 'hoy';
  if (dias === 1) return 'ayer';
  if (dias < 7) return `hace ${dias} días`;
  if (dias < 14) return 'hace 1 semana';
  if (dias < 31) return `hace ${Math.floor(dias / 7)} semanas`;
  if (dias < 62) return 'hace 1 mes';
  if (dias < 365) return `hace ${Math.floor(dias / 30)} meses`;
  return fechaLarga(iso);
}
