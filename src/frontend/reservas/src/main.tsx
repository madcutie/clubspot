import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import App from './App';
import { queryClient } from './api/queryClient';
import { invalidateAvailability } from './api/portalApi';
import './styles.css';

// Volver con el botón atrás desde el checkout restaura la página congelada (bfcache):
// sin esto, la grilla mostraría la disponibilidad de antes del propio hold.
window.addEventListener('pageshow', (event) => {
  if (event.persisted) void invalidateAvailability();
});

const el = document.getElementById('root');
if (!el) throw new Error('No se encontró #root');

createRoot(el).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
);
