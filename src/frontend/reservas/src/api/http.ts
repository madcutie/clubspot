import { API_URL } from './config';

/**
 * Mutator del cliente generado: el único lugar del portal donde vive `fetch`
 * (ADR-0016). El portal es anónimo, así que acá no hay sesión ni token de
 * usuario; la prueba de propiedad de una reserva viaja por header y la pone
 * quien llama.
 */

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    path: string,
  ) {
    super(`La API respondió ${status} en ${path}`);
    this.name = 'ApiError';
  }
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, init);
  if (!res.ok) throw new ApiError(res.status, path);
  if (res.status === 204) return undefined as T;
  const texto = await res.text();
  return (texto ? JSON.parse(texto) : undefined) as T;
}
