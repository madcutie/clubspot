import { useSyncExternalStore } from 'react';

/**
 * La sesión es el token: el nombre del operador, sus roles y el vencimiento vienen adentro
 * (ADR-0018). Se decodifica sin verificar la firma, a propósito — esto dibuja la consola, no
 * autoriza nada; quien autoriza es la API, que valida firma y vencimiento en cada request.
 *
 * Vive en sessionStorage: sobrevive el F5 y la jornada, se pierde al cerrar el navegador. La
 * máquina del mostrador es compartida entre turnos.
 */

export type Rol =
  | 'administrator'
  | 'memberDesk'
  | 'treasury'
  | 'courtReception'
  | 'accessControl'
  | 'coach'
  | 'member';

const ROLES: Rol[] = [
  'administrator',
  'memberDesk',
  'treasury',
  'courtReception',
  'accessControl',
  'coach',
  'member',
];

/** Cómo se llama cada rol en la consola. El código habla inglés, la pantalla español. */
export const NOMBRE_ROL: Record<Rol, string> = {
  administrator: 'administración',
  memberDesk: 'socios',
  treasury: 'tesorería',
  courtReception: 'canchero',
  accessControl: 'control de acceso',
  coach: 'profesor',
  member: 'socio',
};

export interface Sesion {
  token: string;
  nombre: string;
  iniciales: string;
  roles: Rol[];
  /** Epoch en milisegundos. */
  expira: number;
}

const CLAVE = 'clubspot.sesion';

function cuerpoDelToken(token: string): Record<string, unknown> | null {
  const parte = token.split('.')[1];
  if (!parte) return null;
  try {
    const base64 = parte.replace(/-/g, '+').replace(/_/g, '/');
    const binario = atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, '='));
    const bytes = Uint8Array.from(binario, (letra) => letra.charCodeAt(0));
    const cuerpo: unknown = JSON.parse(new TextDecoder().decode(bytes));
    return typeof cuerpo === 'object' && cuerpo !== null ? (cuerpo as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

function iniciales(nombre: string): string {
  return nombre
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((parte) => parte[0]?.toUpperCase() ?? '')
    .join('');
}

function rolesDe(claim: unknown): Rol[] {
  const crudos = Array.isArray(claim) ? claim : claim === undefined ? [] : [claim];
  return crudos.filter((rol): rol is Rol => typeof rol === 'string' && ROLES.includes(rol as Rol));
}

export function leerSesion(token: string): Sesion | null {
  const cuerpo = cuerpoDelToken(token);
  if (!cuerpo) return null;

  const nombre = typeof cuerpo.name === 'string' ? cuerpo.name : '';
  const expira = typeof cuerpo.exp === 'number' ? cuerpo.exp * 1000 : 0;
  if (!nombre || expira <= Date.now()) return null;

  return { token, nombre, iniciales: iniciales(nombre), roles: rolesDe(cuerpo.role), expira };
}

function desdeStorage(): Sesion | null {
  const guardado = sessionStorage.getItem(CLAVE);
  if (!guardado) return null;
  const sesion = leerSesion(guardado);
  if (!sesion) sessionStorage.removeItem(CLAVE);
  return sesion;
}

let actual: Sesion | null = desdeStorage();
const oyentes = new Set<() => void>();

function avisar() {
  for (const oyente of oyentes) oyente();
}

// Puro a propósito: lo lee useSyncExternalStore en cada render y no puede tener efectos.
export function sesionActual(): Sesion | null {
  return actual;
}

export function tokenActual(): string | null {
  // Acá sí se limpia: es el borde por donde sale cada request, fuera de todo render.
  if (actual && actual.expira <= Date.now()) cerrarSesion();
  return actual?.token ?? null;
}

export function abrirSesion(token: string): Sesion | null {
  const sesion = leerSesion(token);
  if (!sesion) return null;
  sessionStorage.setItem(CLAVE, token);
  actual = sesion;
  avisar();
  return sesion;
}

export function cerrarSesion(): void {
  if (!actual) return;
  sessionStorage.removeItem(CLAVE);
  actual = null;
  avisar();
}

function suscribir(oyente: () => void): () => void {
  oyentes.add(oyente);
  return () => oyentes.delete(oyente);
}

export function useSesion(): Sesion | null {
  return useSyncExternalStore(suscribir, sesionActual, () => null);
}
