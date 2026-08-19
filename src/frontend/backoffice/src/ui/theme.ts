import type { CSSProperties } from 'react';

/**
 * Paleta y controles de la consola, tomados del diseño "Pizarra azul".
 *
 * Fondo gris azulado de plano técnico, el azul de la cancha de pádel para lo
 * que el club cobra y confirma, ámbar para lo que quedó a medias y naranja
 * para lo que hay que mirar. Los nombres van por rol, no por color, para que
 * cambiar el tono no obligue a renombrar media consola.
 */
export const c = {
  // Superficies
  papel: '#EEF1F5',
  panel: '#FFFFFF',
  blanco: '#FFFFFF',
  hover: '#E4EAF1',
  activo: '#E1E9F8',
  segmento: '#E4EAF1',
  apagado: '#E8EDF4',
  cerrado: '#E4EAF1',
  hueco: '#E4EAF1',
  /** Trama de lo cerrado: el club no abre, no es un hueco que se pueda vender. */
  rayado: 'repeating-linear-gradient(135deg,#DAE0EA 0 4px,#E4EAF1 4px 8px)',

  // Bordes, de más suave a más marcado
  linea: '#DCE2EB',
  borde: '#D8DEE7',
  bordeFirme: '#C2CBD9',
  bordePunteado: '#B6C1D2',
  bordeCasilla: '#A9B5C8',
  bordeFuerte: '#A3AEC0',
  /** Renglón de hora de la grilla, visible en los canales entre canchas. */
  regla: '#C9D3E1',

  // Texto
  titulo: '#14203A',
  tinta: '#14203A',
  texto: '#1E2A44',
  textoSuave: '#33404F',
  textoBoton: '#33404F',
  textoDato: '#4A5772',
  textoTenue: '#54617A',
  textoTenue2: '#54617A',
  textoGris: '#6B7689',
  textoGris2: '#6B7689',
  textoApagado: '#8592A6',
  textoMuyApagado: '#94A0B4',
  puntoApagado: '#B4BFCF',

  // Azul: cobrado, confirmado, seleccionado
  acento: '#2553CC',
  acentoOscuro: '#1B3E9C',
  acentoTexto: '#1B3E9C',
  acentoPunto: '#2553CC',
  acentoFondo: '#E1E9F8',
  acentoFondoSuave: '#EDF2FC',
  acentoBorde: '#B7C9EE',
  acentoBordeSuave: '#2553CC',
  acentoTenue: '#4B62A8',
  /** Sobre el bloque azul pleno de una reserva cobrada. */
  sobreAcento: '#B8CBF5',
  sobreAcentoFuerte: '#CFE0FF',

  // Hueco vendible: lo único de la grilla que se puede clickear
  libreBorde: '#D2DCEC',
  libreIcono: '#8FA6D8',
  holdBorde: '#7EA0EE',

  // Ámbar: seña, fecha propia, a medias
  ambar: '#C08A17',
  ambarTexto: '#7A5B10',
  ambarBorde: '#DEBE62',
  ambarFondo: '#FBF0D2',
  ambarChip: '#96700F',
  ambarFuerte: '#96700F',

  // Naranja: deuda, bloqueo, error
  naranja: '#A24F29',
  naranjaPunto: '#CE6129',
  naranjaBorde: '#E0793F',
  naranjaBordeFirme: '#D89A6E',
  naranjaFondo: '#FDF1EA',
  naranjaTexto: '#8F4A22',
  naranjaFuerte: '#C25A24',

  // Toast
  tostadaFondo: '#14203A',
  tostadaBorde: '#2A3A5C',
  tostadaTexto: '#EEF1F5',
} as const;

export const sans = 'Archivo, sans-serif';
export const mono = '"Spline Sans Mono", monospace';

/** Alto de media hora en la grilla de agenda, en píxeles. */
export const FILA = 32;

/** Aire entre dos celdas de la grilla, para que cada turno se lea como una pieza. */
export const AIRE = 3;

// ── Controles ────────────────────────────────────────────────────────────────

/** Botón principal: la acción que el operador vino a hacer. */
export function primario(habilitado = true): CSSProperties {
  return habilitado
    ? {
        minHeight: 34,
        padding: '0 13px',
        borderRadius: 8,
        border: 'none',
        background: c.acento,
        color: c.blanco,
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
    border: `1px solid ${on ? c.acentoBordeSuave : c.bordeFirme}`,
    background: on ? c.acentoFondo : c.blanco,
    color: on ? c.acentoTexto : deshabilitado ? c.textoApagado : c.textoTenue2,
    font: `${on ? 600 : 500} 12.5px ${sans}`,
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
    border: `1px solid ${on ? c.acentoBordeSuave : c.borde}`,
    background: on ? c.acentoFondo : 'transparent',
    color: on ? c.acentoTexto : deshabilitado ? c.textoApagado : c.textoTenue2,
    font: `${on ? 600 : 500} 12.5px ${sans}`,
    whiteSpace: 'nowrap',
  };
}

/** Chip de posición: qué día se está mirando. Va pleno, no es un filtro más. */
export function chipDia(on: boolean): CSSProperties {
  return {
    minHeight: 34,
    padding: '0 12px',
    borderRadius: 8,
    cursor: 'pointer',
    border: `1px solid ${on ? c.tinta : c.borde}`,
    background: on ? c.tinta : 'transparent',
    color: on ? c.papel : c.textoTenue2,
    font: `${on ? 600 : 500} 12.5px ${sans}`,
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
    border: `1px solid ${on ? c.acento : c.bordeCasilla}`,
    background: on ? c.acento : 'transparent',
    color: c.blanco,
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
    border: `1px solid ${on ? c.acento : c.bordeFirme}`,
    background: on ? c.acento : 'transparent',
    color: on ? c.blanco : deshabilitado ? c.textoApagado : c.textoDato,
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
