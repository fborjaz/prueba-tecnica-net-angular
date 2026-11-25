/**
 * La API se consume siempre por la ruta relativa /api:
 * - En desarrollo, `ng serve` la redirige a http://localhost:5170 (proxy.conf.json).
 * - En Docker, nginx la redirige al contenedor del backend (nginx.conf).
 */
export const environment = {
  production: true,
  apiUrl: '/api'
};
