/**
 * Plata. El prototipo trabaja con pesos redondeados porque así se cobra en el
 * mostrador; en el backend esto es `Money`, que además lleva la moneda.
 */

/** Porcentaje que se pide como seña. */
export const SENA_PCT = 50;

export function pesos(n: number): string {
  return '$' + Math.round(n).toLocaleString('es-AR');
}

/** Seña de un turno, redondeada a la centena como se cobra en caja. */
export function sena(precio: number): number {
  return Math.round((precio * SENA_PCT) / 100 / 100) * 100;
}
