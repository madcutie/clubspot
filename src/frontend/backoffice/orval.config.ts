import { defineConfig } from 'orval';

// El documento lo reescribe el build de la Api; acá sólo se lee. Nada de lo que sale de
// `src/api/generated` se edita a mano (ADR-0016).
export default defineConfig({
  clubspot: {
    input: '../../../docs/api/clubspot.openapi.json',
    output: {
      target: 'src/api/generated',
      mode: 'tags-split',
      client: 'fetch',
      clean: true,
      prettier: false,
      override: {
        // Único lugar donde vive `fetch`: el mutator ya resuelve sesión, reintento en 401 y ApiError.
        mutator: { path: './src/api/http.ts', name: 'api' },
        fetch: { includeHttpResponseReturnType: false },
      },
    },
  },
});
