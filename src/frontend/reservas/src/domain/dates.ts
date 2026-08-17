const D3 = ['DOM', 'LUN', 'MAR', 'MIÉ', 'JUE', 'VIE', 'SÁB'];
const DL = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];
const M3 = ['ENE', 'FEB', 'MAR', 'ABR', 'MAY', 'JUN', 'JUL', 'AGO', 'SEP', 'OCT', 'NOV', 'DIC'];
const ML = [
  'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
  'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre',
];

/** "2026-08-17" → Date local (sin zona). */
export function parseDate(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

/** Date → "2026-08-17". */
export function isoDate(d: Date): string {
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mm}-${dd}`;
}

export function addDays(d: Date, n: number): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate() + n);
}

/** "sábado 15 de agosto" (long) · "SÁB 15 AGO" (short). */
export function dayLabelOf(d: Date, long: boolean): string {
  if (long) return `${DL[d.getDay()]} ${d.getDate()} de ${ML[d.getMonth()]}`;
  return `${D3[d.getDay()]} ${d.getDate()} ${M3[d.getMonth()]}`;
}

/** Encabezado del chip de día: HOY / MAÑ / DOM… (`i` es el índice en la grilla). */
export function dayChipOf(d: Date, i: number): { top: string; num: string; mon: string } {
  return {
    top: i === 0 ? 'HOY' : i === 1 ? 'MAÑ' : D3[d.getDay()],
    num: String(d.getDate()),
    mon: M3[d.getMonth()],
  };
}

/** Minutos desde medianoche → "19:30". */
export function hhmm(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
}
