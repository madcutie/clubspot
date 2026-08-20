import { cerrarSesion, tokenActual } from '../auth/sesion';
import { API_URL } from './config';

/**
 * El único lugar donde vive `fetch` (ADR-0016): es el mutator del cliente generado.
 * No inicia sesión ni sabe de credenciales — sólo adjunta el token que haya y, si la API
 * contesta 401 con una sesión abierta, la cierra: la app vuelve al login sola.
 */

export class ApiError extends Error {
  status: number;
  body?: unknown;

  constructor(status: number, body?: unknown) {
    super(`API respondió ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }
}

async function leerCuerpo(res: Response): Promise<unknown> {
  const texto = await res.text();
  if (!texto) return undefined;
  try {
    return JSON.parse(texto);
  } catch {
    return texto;
  }
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('Content-Type', 'application/json');
  const token = tokenActual();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const res = await fetch(`${API_URL}${path}`, { ...init, headers });

  // Con token, un 401 es sesión vencida o revocada. Sin token es el login que falló, y de eso
  // se ocupa la pantalla.
  if (res.status === 401 && token) cerrarSesion();

  if (!res.ok) throw new ApiError(res.status, await leerCuerpo(res));
  if (res.status === 204) return undefined as T;
  return (await leerCuerpo(res)) as T;
}
