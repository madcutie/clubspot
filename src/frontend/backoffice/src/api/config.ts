// Auto-login de desarrollo: lo reemplaza la autenticación real.
export const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5037';
export const DEV_CLUB = import.meta.env.VITE_DEV_CLUB ?? 'chaco-for-ever';
export const DEV_EMAIL = import.meta.env.VITE_DEV_EMAIL ?? 'admin@chacoforever.test';
export const DEV_PASSWORD = import.meta.env.VITE_DEV_PASSWORD ?? 'clubspot-dev';
