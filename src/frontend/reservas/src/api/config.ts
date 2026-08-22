// El fallback existe sólo en desarrollo: `import.meta.env.DEV` es `false` literal en el bundle de
// producción, así que el `localhost` se va del archivo en vez de quedar como código muerto. Ahí la
// variable es obligatoria y la impone `build/requireApiUrl.ts` al compilar.
// El `?? ''` no alcanza por sí solo: una variable declarada y vacía llega como cadena vacía.
const raw = (import.meta.env.VITE_API_URL ?? '').trim();

export const API_URL = (import.meta.env.DEV ? raw || 'http://localhost:5037' : raw).replace(
  /\/+$/,
  '',
);
