import { useMemo } from 'react';
import { useSesion, type Rol } from './sesion';

/**
 * Qué dibuja la consola para cada rol. Es de presentación: apaga botones y esconde módulos,
 * **no autoriza nada** — quien autoriza es la API (ADR-0018). Espejo de las políticas de
 * `AuthorizationPolicies.cs`; si allá cambia el reparto, acá también.
 */

export interface Permisos {
  /** `agenda.operate` */
  operarAgenda: boolean;
  /** `people.view` */
  verPersonas: boolean;
  /** `people.manage` */
  gestionarPersonas: boolean;
  /** `configuration.edit` */
  configurar: boolean;
}

export function permisosDe(roles: Rol[]): Permisos {
  const alguno = (...admitidos: Rol[]) => roles.some((rol) => admitidos.includes(rol));
  return {
    operarAgenda: alguno('administrator', 'courtReception'),
    verPersonas: alguno('administrator', 'memberDesk', 'courtReception'),
    gestionarPersonas: alguno('administrator', 'memberDesk'),
    configurar: alguno('administrator'),
  };
}

export function usePermisos(): Permisos {
  const sesion = useSesion();
  const roles = sesion?.roles;
  return useMemo(() => permisosDe(roles ?? []), [roles]);
}

/** Dónde cae quien entra, y a dónde vuelve quien escribe a mano una ruta que no le toca. */
export function rutaInicial(permisos: Permisos): string | null {
  if (permisos.operarAgenda) return '/reservas';
  if (permisos.verPersonas) return '/personas';
  return null;
}
