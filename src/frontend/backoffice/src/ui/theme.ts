import type { CSSProperties } from 'react';

/**
 * Paleta y controles de la consola, tomados del diseño.
 *
 * El diseño es intencionalmente sobrio: papel claro, un verde para lo que el
 * club cobra y confirma, ámbar para lo que quedó a medias y naranja para lo que
 * hay que mirar. Nada más. Los nombres van por rol, no por color, para que
 * cambiar el tono no obligue a renombrar media consola.
 */
export const c = {
  // Superficies
  papel: '#F7F7F5',
  panel: '#FCFCFA',
  blanco: '#FFFFFF',
  hover: '#F0F0EC',
  activo: '#EDEDE8',
  segmento: '#EDEDE9',
  apagado: '#F1F3EF',
  cerrado: '#F2F2EE',
  hueco: '#EFEFEA',

  // Bordes, de más suave a más marcado
  linea: '#E7E7E2',
  borde: '#E2E2DD',
  bordeFirme: '#D6D6D1',
  bordePunteado: '#CBCBC5',
  bordeCasilla: '#BEBEB7',
  bordeFuerte: '#B2B2AB',

  // Texto
  titulo: '#0F0F0D',
  tinta: '#121210',
  texto: '#1A1A18',
  textoSuave: '#2A2A27',
  textoBoton: '#333330',
  textoDato: '#4B4B46',
  textoTenue: '#5C5C55',
  textoTenue2: '#5C5C56',
  textoGris: '#6A6A63',
  textoGris2: '#6E6E66',
  textoApagado: '#94948C',
  textoMuyApagado: '#A4A49D',
  puntoApagado: '#C0C0B8',

  // Verde: cobrado, confirmado, seleccionado
  verde: '#1F7A4D',
  verdeOscuro: '#17603C',
  verdeTexto: '#2C6B45',
  verdePunto: '#2F9E5F',
  verdeFondo: '#EAF3EC',
  verdeFondoSuave: '#F0F7F2',
  verdeBorde: '#BEDCC8',
  verdeBordeSuave: '#BCD9C6',
  verdeTenue: '#5C7F69',

  // Ámbar: seña, fecha propia, a medias
  ambar: '#C29A26',
  ambarTexto: '#87640F',
  ambarBorde: '#E9D8AA',
  ambarFondo: '#FDF7E9',
  ambarChip: '#E8D07A',

  // Naranja: deuda, bloqueo, error
  naranja: '#A24F29',
  naranjaPunto: '#CE6129',
  naranjaBorde: '#EFC9B2',
  naranjaBordeFirme: '#DBAA8D',
  naranjaFondo: '#FDF1EA',

  // Toast
  tostadaFondo: '#1A1A18',
  tostadaBorde: '#2E2E2A',
  tostadaTexto: '#F7F7F5',
} as const;

export const sans = 'Geist, sans-serif';
export const mono = '"Geist Mono", monospace';

/** Alto de media hora en la grilla de agenda, en píxeles. */
export const FILA = 32;

// ── Controles ────────────────────────────────────────────────────────────────

/** Botón principal: la acción que el operador vino a hacer. */
export function primario(habilitado = true): CSSProperties {
  return habilitado
    ? {
        minHeight: 34,
        padding: '0 13px',
        borderRadius: 8,
        border: 'none',
        background: c.verde,
        color: c.papel,
        font: `600 12.5px ${sans}`,
        cursor: 'pointer',
      }
    : {
        minHeight: 34,
        padding: '0 13px',
        borderRadius: 8,
        border: 'none',
        background: c.borde,
        color: c.textoGris2,
        font: `600 12.5px ${sans}`,
        cursor: 'default',
      };
}

/** Botón secundario de una barra de acciones. */
export function secundario(): CSSProperties {
  return {
    minHeight: 34,
    padding: '0 12px',
    borderRadius: 8,
    border: `1px solid ${c.bordeFirme}`,
    background: c.panel,
    color: c.textoBoton,
    font: `500 12.5px ${sans}`,
    cursor: 'pointer',
  };
}

/** Botón secundario chico, el de los encabezados de editor. */
export function fantasma(): CSSProperties {
  return {
    minHeight: 32,
    padding: '0 11px',
    borderRadius: 8,
    border: `1px solid ${c.bordeFirme}`,
    background: c.blanco,
    color: c.textoBoton,
    font: `500 12px ${sans}`,
    cursor: 'pointer',
  };
}

/** Chip de opción con fondo blanco: elige una configuración. */
export function chipOpcion(on: boolean, deshabilitado = false): CSSProperties {
  return {
    minHeight: 34,
    padding: '0 12px',
    borderRadius: 8,
    cursor: deshabilitado ? 'default' : 'pointer',
    border: `1px solid ${on ? c.verdeBordeSuave : c.bordeFirme}`,
    background: on ? c.verdeFondo : c.blanco,
    color: on ? c.verde : deshabilitado ? c.textoApagado : c.textoTenue2,
    font: `500 12.5px ${sans}`,
    whiteSpace: 'nowrap',
  };
}

/** Chip de filtro transparente: acota lo que se está mirando. */
export function chipFiltro(on: boolean, deshabilitado = false): CSSProperties {
  return {
    minHeight: 34,
    padding: '0 12px',
    borderRadius: 8,
    cursor: deshabilitado ? 'default' : 'pointer',
    border: `1px solid ${on ? c.verdeBordeSuave : c.borde}`,
    background: on ? c.verdeFondo : 'transparent',
    color: on ? c.verde : deshabilitado ? c.textoApagado : c.textoTenue2,
    font: `500 12.5px ${sans}`,
    whiteSpace: 'nowrap',
  };
}

/** Casilla de selección de la tabla. */
export function casilla(on: boolean): CSSProperties {
  return {
    width: 15,
    height: 15,
    borderRadius: 4,
    cursor: 'pointer',
    padding: 0,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    border: `1px solid ${on ? c.verde : c.bordeCasilla}`,
    background: on ? c.verde : 'transparent',
    color: c.papel,
    font: `600 9px ${sans}`,
    lineHeight: 1,
  };
}

/** Botón de paginación. */
export function botonPagina(on: boolean, deshabilitado: boolean): CSSProperties {
  return {
    minWidth: 28,
    minHeight: 28,
    padding: '0 8px',
    borderRadius: 7,
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    cursor: deshabilitado ? 'default' : 'pointer',
    border: `1px solid ${on ? c.verde : c.bordeFirme}`,
    background: on ? c.verde : 'transparent',
    color: on ? c.papel : deshabilitado ? c.textoApagado : c.textoDato,
    font: `500 11.5px ${mono}`,
  };
}

/** Campo de texto de los editores. */
export function campo(): CSSProperties {
  return {
    minHeight: 36,
    padding: '0 11px',
    borderRadius: 9,
    width: '100%',
    border: `1px solid ${c.bordeFirme}`,
    background: c.blanco,
    color: c.tinta,
    font: `400 13px ${mono}`,
    outline: 'none',
  };
}

/** Campo de texto de los paneles laterales. */
export function campoPanel(): CSSProperties {
  return {
    width: '100%',
    minHeight: 38,
    padding: '0 12px',
    borderRadius: 9,
    border: `1px solid ${c.borde}`,
    background: c.panel,
    fontSize: 13,
    color: c.texto,
    outline: 'none',
  };
}

/** Desplegable ancho: elegir horario, hora de la noche. */
export function selectAncho(): CSSProperties {
  return {
    minWidth: 200,
    minHeight: 36,
    padding: '0 9px',
    borderRadius: 9,
    border: `1px solid ${c.bordeFirme}`,
    background: c.blanco,
    color: c.tinta,
    font: `400 13px ${sans}`,
    cursor: 'pointer',
  };
}

/** Etiqueta de sección en versalitas monoespaciadas. */
export function rotulo(): CSSProperties {
  return {
    font: `400 10.5px ${mono}`,
    color: c.textoGris2,
    letterSpacing: '.08em',
  };
}

/** Botón sin adornos: el contenedor le pone el estilo. */
export const desnudo: CSSProperties = {
  border: 'none',
  background: 'transparent',
  padding: 0,
  cursor: 'pointer',
  textAlign: 'left',
};
