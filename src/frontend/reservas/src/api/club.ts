/**
 * El club sale del primer segmento de la URL: `/chaco-for-ever`. Un solo despliegue del portal
 * sirve a todos los clubes, así que dar de alta uno nuevo no recompila nada.
 *
 * No hay club por defecto a propósito: un default haría que un error de ruteo se viera como el
 * portal andando, mostrando el club equivocado.
 */

// El mismo largo que `clubs.slug` en la base: un segmento más largo no puede ser un club.
const MAX_LENGTH = 60;
const SLUG = /^[a-z0-9]+(-[a-z0-9]+)*$/;

function fromPath(): string | null {
  const first = window.location.pathname.split('/')[1] ?? '';
  return first.length > 0 && first.length <= MAX_LENGTH && SLUG.test(first) ? first : null;
}

/** El slug del club, o `null` si la URL no trae uno con forma de slug. */
export const CLUB_SLUG: string | null = fromPath();

/**
 * El slug para hablar con la API. `App` no monta el flujo de reserva sin club, así que si esto
 * lanza es un error de programa, no una URL mal escrita.
 */
export function requireClubSlug(): string {
  if (CLUB_SLUG === null) throw new Error('El portal se montó sin club en la URL');
  return CLUB_SLUG;
}
