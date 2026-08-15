import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';
import App from './App';
import { ProveedorTostadas } from './ui/Tostadas';
import './styles.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // El mock no cambia solo: evitamos refetch de fondo durante una demo.
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: false,
    },
    mutations: { retry: false },
  },
});

const el = document.getElementById('root');
if (!el) throw new Error('No se encontró #root');

createRoot(el).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <ProveedorTostadas>
          <App />
        </ProveedorTostadas>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
