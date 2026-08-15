import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  // host: true expone el server en la red local, para abrirlo desde el celular.
  server: { port: 5183, host: true },
  preview: { port: 5183, host: true },
});
