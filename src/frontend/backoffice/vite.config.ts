import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  // Puerto propio para poder levantar el backoffice y el portal de reservas a la vez.
  server: { port: 5184, host: true },
  preview: { port: 5184, host: true },
});
