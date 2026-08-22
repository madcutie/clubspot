import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import { requireApiUrl } from './build/requireApiUrl';

export default defineConfig(({ command, mode }) => {
  requireApiUrl(command, mode, loadEnv(mode, process.cwd(), 'VITE_'));

  return {
    plugins: [react()],
    // host: true expone el server en la red local, para abrirlo desde el celular.
    server: { port: 5183, host: true },
    preview: { port: 5183, host: true },
  };
});
