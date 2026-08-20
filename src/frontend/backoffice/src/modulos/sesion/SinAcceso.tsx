import { useQueryClient } from '@tanstack/react-query';
import { LogOut } from 'lucide-react';
import { cerrarSesion, NOMBRE_ROL, type Sesion } from '../../auth/sesion';
import { c, mono, sans, secundario } from '../../ui/theme';

/**
 * Un usuario válido cuyo rol no opera nada de la consola —un socio, un profesor—. Entró bien,
 * así que no es un error de credenciales: es que acá no tiene nada que hacer.
 */
export function SinAcceso({ sesion }: { sesion: Sesion }) {
  const queryClient = useQueryClient();

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 12,
        height: '100vh',
        background: c.papel,
        fontFamily: sans,
        color: c.texto,
        textAlign: 'center',
      }}
    >
      <div style={{ font: `500 15px ${sans}` }}>Esta consola no es para tu usuario</div>
      <div style={{ font: `400 12.5px ${mono}`, color: c.textoTenue, maxWidth: 380 }}>
        {sesion.nombre} entró como {sesion.roles.map((rol) => NOMBRE_ROL[rol]).join(' · ') || 'sin rol'}, y
        ninguno de esos roles opera el backoffice.
      </div>
      <button
        type="button"
        onClick={() => {
          queryClient.clear();
          cerrarSesion();
        }}
        className="h-ghost"
        style={{ ...secundario(), display: 'flex', alignItems: 'center', gap: 7, marginTop: 4 }}
      >
        <LogOut size={13} strokeWidth={1.8} aria-hidden />
        Salir
      </button>
    </div>
  );
}
