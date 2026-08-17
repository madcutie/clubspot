import { QueryClient } from '@tanstack/react-query';

// staleTime corto para que los cambios del backoffice se reflejen sin recargar.
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 15_000, refetchOnWindowFocus: false, retry: false },
  },
});
