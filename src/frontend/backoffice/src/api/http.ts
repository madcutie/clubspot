import { API_URL, DEV_CLUB, DEV_EMAIL, DEV_PASSWORD } from './config';

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

interface SessionResponse {
  accessToken: string;
}

let token: string | null = null;
let sesionEnCurso: Promise<string> | null = null;

async function leerCuerpo(res: Response): Promise<unknown> {
  const texto = await res.text();
  if (!texto) return undefined;
  try {
    return JSON.parse(texto);
  } catch {
    return texto;
  }
}

function iniciarSesion(): Promise<string> {
  if (!sesionEnCurso) {
    sesionEnCurso = fetch(`${API_URL}/api/auth/session`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ club: DEV_CLUB, email: DEV_EMAIL, password: DEV_PASSWORD }),
    })
      .then(async (res) => {
        if (!res.ok) throw new ApiError(res.status, await leerCuerpo(res));
        const data = (await res.json()) as SessionResponse;
        token = data.accessToken;
        return token;
      })
      .finally(() => {
        sesionEnCurso = null;
      });
  }
  return sesionEnCurso;
}

function asegurarToken(): Promise<string> {
  return token ? Promise.resolve(token) : iniciarSesion();
}

function construirHeaders(base: HeadersInit | undefined, bearer: string): Headers {
  const headers = new Headers(base);
  headers.set('Authorization', `Bearer ${bearer}`);
  headers.set('Content-Type', 'application/json');
  return headers;
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const pedir = (bearer: string) =>
    fetch(`${API_URL}${path}`, { ...init, headers: construirHeaders(init.headers, bearer) });

  let res = await pedir(await asegurarToken());

  if (res.status === 401) {
    token = null;
    res = await pedir(await iniciarSesion());
  }

  if (!res.ok) throw new ApiError(res.status, await leerCuerpo(res));
  if (res.status === 204) return undefined as T;
  return (await leerCuerpo(res)) as T;
}
