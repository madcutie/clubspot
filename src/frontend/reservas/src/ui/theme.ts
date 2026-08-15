import type { CSSProperties } from 'react';

/**
 * Blanco y negro del club, con un solo amarillo para la acción principal y los
 * estados de disponibilidad. Todo estado se distingue además por texto y borde,
 * no solo por color.
 */
export const C = {
  page: '#08090B',
  screen: '#0D0E11',
  surface: '#14161A',
  accent: '#FFC94A',
  accentSoft: 'rgba(255,201,74,.12)',
  ink: '#F5F7FA',
  text: '#C7CDD4',
  soft: '#9BA1AC',
  muted: '#8A9199',
  dim: '#6C737F',
  faint: '#5F6772',
  line: 'rgba(255,255,255,.08)',
  border: 'rgba(255,255,255,.13)',
} as const;

export const F = {
  display: "'Space Grotesk', sans-serif",
  body: 'Manrope, system-ui, sans-serif',
} as const;

export const label: CSSProperties = {
  font: `700 11px ${F.body}`,
  color: C.dim,
  letterSpacing: '.1em',
  textTransform: 'uppercase',
};

export const stepNum: CSSProperties = { font: `700 12px ${F.body}`, color: C.accent };

export const stepTitle: CSSProperties = {
  font: `700 12px ${F.body}`,
  letterSpacing: '.1em',
  textTransform: 'uppercase',
};

export const card: CSSProperties = {
  borderRadius: 18,
  background: C.surface,
  border: '1px solid rgba(255,255,255,.08)',
  padding: 18,
};

export const divider: CSSProperties = { height: 1, background: C.line, margin: '14px 0' };

export const rowLabel: CSSProperties = { font: `500 14px ${F.body}`, color: C.muted };
export const rowValue: CSSProperties = { font: `600 14px ${F.body}`, textAlign: 'right' };

export const input: CSSProperties = {
  width: '100%',
  minHeight: 52,
  padding: '0 15px',
  borderRadius: 14,
  border: `1px solid ${C.border}`,
  background: C.surface,
  color: C.ink,
  fontSize: 16,
  outline: 'none',
};

export function chip(active: boolean): CSSProperties {
  return {
    minHeight: 40,
    padding: '0 12px',
    borderRadius: 11,
    cursor: 'pointer',
    border: active ? `1px solid ${C.accent}` : `1px solid ${C.border}`,
    background: active ? C.accentSoft : C.surface,
    color: active ? C.accent : C.text,
    font: `600 13.5px ${F.body}`,
    whiteSpace: 'nowrap',
    flex: 'none',
  };
}

export function radio(active: boolean): CSSProperties {
  return {
    width: 22,
    height: 22,
    borderRadius: '50%',
    flex: 'none',
    border: active ? `7px solid ${C.accent}` : '2px solid rgba(255,255,255,.28)',
    background: 'transparent',
  };
}

export function sportCard(active: boolean): CSSProperties {
  return {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'flex-start',
    justifyContent: 'flex-end',
    minHeight: 96,
    padding: '18px 16px 20px',
    borderRadius: 18,
    cursor: 'pointer',
    textAlign: 'left',
    border: active ? `2px solid ${C.accent}` : '1px solid rgba(255,255,255,.11)',
    background: active ? 'rgba(255,201,74,.10)' : C.surface,
    color: active ? C.accent : C.ink,
  };
}

export function optCard(active: boolean): CSSProperties {
  return {
    display: 'block',
    width: '100%',
    padding: '15px 16px',
    borderRadius: 16,
    cursor: 'pointer',
    border: active ? `2px solid ${C.accent}` : '1px solid rgba(255,255,255,.11)',
    background: active ? 'rgba(255,201,74,.08)' : C.surface,
    color: C.ink,
  };
}

export const ctaOn: CSSProperties = {
  width: '100%',
  minHeight: 52,
  border: 'none',
  borderRadius: 14,
  background: C.accent,
  color: C.surface,
  font: `700 16.5px ${F.body}`,
  cursor: 'pointer',
};

export const ctaOff: CSSProperties = {
  width: '100%',
  minHeight: 52,
  border: '1px solid rgba(255,255,255,.12)',
  borderRadius: 14,
  background: 'transparent',
  color: C.faint,
  font: `700 16.5px ${F.body}`,
  cursor: 'default',
};

export const ctaGhost: CSSProperties = {
  width: '100%',
  minHeight: 50,
  borderRadius: 14,
  border: '1px solid rgba(255,255,255,.16)',
  background: 'transparent',
  color: C.ink,
  font: `600 15px ${F.body}`,
  cursor: 'pointer',
};
