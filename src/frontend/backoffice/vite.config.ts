import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import { requireApiUrl } from './build/requireApiUrl';

export default defineConfig(({ command, mode }) => {
  requireApiUrl(command, mode, loadEnv(mode, process.cwd(), 'VITE_'));

  return {
    plugins: [react()],
    // Puerto propio para poder levantar el backoffice y el portal de reservas a la vez.
    server: { port: 5184, host: true },
    preview: { port: 5184, host: true },
  };
});
