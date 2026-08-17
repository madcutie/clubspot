import { hhmm } from '../../domain/fechas';
import { c, mono } from '../../ui/theme';

/** Opciones de hora de los desplegables: de 06:00 a 24:00, cada media hora. */
export const HORAS = Array.from({ length: (24 - 6) * 2 + 1 }, (_, i) => 6 * 60 + i * 30);

export function SelectHora({
  valor,
  onCambiar,
  error,
  label,
}: {
  valor: number;
  onCambiar: (v: number) => void;
  error?: boolean;
  label?: string;
}) {
  return (
    <select
      value={valor}
      onChange={(e) => onCambiar(parseInt(e.target.value, 10))}
      aria-label={label}
      style={{
        minHeight: 32,
        padding: '0 8px',
        borderRadius: 8,
        border: `1px solid ${error ? c.naranjaBorde : c.bordeFirme}`,
        background: c.blanco,
        color: c.tinta,
        font: `400 12.5px ${mono}`,
        cursor: 'pointer',
      }}
    >
      {HORAS.map((h) => (
        <option key={h} value={h}>
          {hhmm(h)}
        </option>
      ))}
    </select>
  );
}
