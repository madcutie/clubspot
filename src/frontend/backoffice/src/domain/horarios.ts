/**
 * Reglas del horario: qué está abierto y, sobre eso, en qué minutos puede
 * arrancar un turno. Las usan los editores de Canchas y Horarios para la
 * vista previa de un borrador que todavía no se guardó.
 */

import type { Cancha, Horario, Tramo } from './types';
import { DIAS, fechaDe, hhmm } from './fechas';

/** Tramos de un día de la semana, descartando los que quedaron mal cargados. */
export function tramosSemana(h: Horario, dow: number): Tramo[] {
  return (h.semanal[dow] || []).filter((t) => t[1] > t[0]);
}

/** Tramos de un día concreto, según el patrón semanal. */
export function tramosDelDia(h: Horario, dateIdx: number): Tramo[] {
  return tramosSemana(h, fechaDe(dateIdx).getDay());
}

/** Motivo por el que un tramo no sirve, o `null` si está bien. */
export function tramoMalo(tramos: Tramo[], i: number): string | null {
  const t = tramos[i];
  if (t[1] <= t[0]) return 'el cierre tiene que ser posterior';
  for (let k = 0; k < tramos.length; k++) {
    if (k !== i && t[0] < tramos[k][1] && tramos[k][0] < t[1]) return 'se superpone con otro tramo';
  }
  return null;
}

/**
 * Minutos en que puede arrancar un turno dentro de esos tramos: se respeta el
 * incremento de la cancha y el turno más corto tiene que entrar completo.
 */
export function arranques(tramos: Tramo[], cancha: Cancha): number[] {
  const min = Math.min(...cancha.duraciones);
  const paso = cancha.incremento;
  const out: number[] = [];
  [...tramos]
    .sort((a, b) => a[0] - b[0])
    .forEach((t) => {
      const primero = Math.ceil(t[0] / paso) * paso;
      for (let m = primero; m + min <= t[1]; m += paso) out.push(m);
    });
  return out;
}

/** Arranques de un día de la semana cualquiera. */
export function arranquesDow(cancha: Cancha, horario: Horario, dow: number): number[] {
  if (!cancha.activa) return [];
  return arranques(tramosSemana(horario, dow), cancha);
}

/** Arranques de una fecha concreta. Hoy se descarta lo que ya pasó o no llega al aviso mínimo. */
export function arranquesFecha(
  cancha: Cancha,
  horario: Horario,
  dateIdx: number,
  ahora: number,
): number[] {
  if (!cancha.activa) return [];
  const out = arranques(tramosDelDia(horario, dateIdx), cancha);
  if (dateIdx === 0) return out.filter((m) => m >= ahora + cancha.aviso);
  return out;
}

/** Cuántos turnos ofrece la cancha en una semana tipo. */
export function turnosPorSemana(cancha: Cancha, horario: Horario): number {
  return DIAS.reduce((a, d) => a + arranquesDow(cancha, horario, d.dow).length, 0);
}

/** Resumen de una línea: "lun 08–12, 13–17 · mar 08–12 · +3 días". */
export function resumenSemanal(h: Horario): string {
  const partes: string[] = [];
  DIAS.forEach((d) => {
    const tr = tramosSemana(h, d.dow);
    if (!tr.length) return;
    partes.push(
      d.label.slice(0, 3).toLowerCase() +
        ' ' +
        tr.map((t) => hhmm(t[0]).slice(0, 2) + '–' + hhmm(t[1]).slice(0, 2)).join(', '),
    );
  });
  if (!partes.length) return 'sin horas cargadas';
  return (
    partes.slice(0, 2).join(' · ') + (partes.length > 2 ? ' · +' + (partes.length - 2) + ' días' : '')
  );
}
