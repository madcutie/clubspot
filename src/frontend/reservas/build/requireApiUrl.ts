const LOOPBACK = new Set(['localhost', '127.0.0.1', '[::1]']);

const HINT =
  'Copy .env.example, or build with scripts/build-frontends.ps1, which sets it for a real deploy.';

/**
 * A production bundle carries the API address inside it, so a missing or local value cannot be
 * caught at runtime — it ships, and shows up as an empty screen instead of an error. This is the
 * frontend's counterpart to the Cors:AllowedOrigins guard the Api runs at startup.
 */
export function requireApiUrl(command: string, mode: string, env: Record<string, string>): void {
  // Sólo al construir. `vite preview` también corre en modo production, pero no compila nada:
  // sirve el dist/ que ya existe, y pedirle la variable ahí impide revisar un bundle terminado.
  if (command !== 'build' || mode !== 'production') return;

  const raw = (env.VITE_API_URL ?? '').trim();
  if (!raw) throw new Error(`VITE_API_URL is required for a production build. ${HINT}`);
  if (raw.endsWith('/')) throw new Error(`VITE_API_URL must not end with a slash, got '${raw}'.`);

  let url: URL;
  try {
    url = new URL(raw);
  } catch {
    throw new Error(`VITE_API_URL must be an absolute url, got '${raw}'. ${HINT}`);
  }

  if (url.protocol !== 'http:' && url.protocol !== 'https:')
    throw new Error(`VITE_API_URL must be http or https, got '${raw}'.`);

  if (LOOPBACK.has(url.hostname) && env.VITE_ALLOW_LOCAL_API !== '1')
    throw new Error(
      `VITE_API_URL points at this machine ('${raw}'), so the bundle would only work here. ` +
        'Set VITE_ALLOW_LOCAL_API=1 when that is on purpose, e.g. to check the build with npm run preview.',
    );
}
