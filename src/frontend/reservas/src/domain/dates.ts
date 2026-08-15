const D3 = ['DOM', 'LUN', 'MAR', 'MIÉ', 'JUE', 'VIE', 'SÁB'];
const DL = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];
const M3 = ['ENE', 'FEB', 'MAR', 'ABR', 'MAY', 'JUN', 'JUL', 'AGO', 'SEP', 'OCT', 'NOV', 'DIC'];
const ML = [
  'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
  'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre',
];

/** Fecha base de la grilla: hoy a las 00:00. Se calcula una vez por sesión. */
const HOY = (() => {
  const n = new Date();
  return new Date(n.getFullYear(), n.getMonth(), n.getDate());
})();

/** Fecha del día `i` de la grilla (0 = hoy). */
export function dateOf(i: number): Date {
  return new Date(HOY.getFullYear(), HOY.getMonth(), HOY.getDate() + i);
}

/** "sábado 15 de agosto" (long) · "SÁB 15 AGO" (short). */
export function dayLabel(i: number, long: boolean): string {
  const d = dateOf(i);
  if (long) return `${DL[d.getDay()]} ${d.getDate()} de ${ML[d.getMonth()]}`;
  return `${D3[d.getDay()]} ${d.getDate()} ${M3[d.getMonth()]}`;
}

/** Encabezado del chip de día: HOY / MAÑ / DOM… */
export function dayChip(i: number): { top: string; num: string; mon: string } {
  const d = dateOf(i);
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
