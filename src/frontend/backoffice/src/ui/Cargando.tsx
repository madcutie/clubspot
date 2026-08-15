import { c, mono } from './theme';

/** Espera de la primera carga. Con datos previos en pantalla no se muestra. */
export function Cargando({ que }: { que: string }) {
  return (
    <div
      style={{
        flex: 1,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        font: `400 12.5px ${mono}`,
        color: c.textoGris,
      }}
    >
      cargando {que}…
    </div>
  );
}
