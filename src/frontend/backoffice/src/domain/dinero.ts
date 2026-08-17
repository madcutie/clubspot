/**
 * Plata. La consola muestra pesos redondeados porque así se cobra en el
 * mostrador; en el backend esto es `Money`, que además lleva la moneda.
 */

export function pesos(n: number): string {
  return '$' + Math.round(n).toLocaleString('es-AR');
}
