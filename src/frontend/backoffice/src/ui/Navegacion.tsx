import { NavLink } from 'react-router-dom';
import { ArrowUpDown, type LucideIcon } from 'lucide-react';
import type { Club } from '../domain/types';
import { c, mono, sans } from './theme';
import { useTostada } from './Tostadas';

/**
 * Barra lateral. Separa lo que se usa todos los días (Operación) de lo que se
 * configura una vez (Base), y muestra al lado de cada módulo el número que
 * importa: turnos del día, canchas, horarios, personas.
 */

export interface ItemNav {
  a: string;
  label: string;
  tag: string;
  icono: LucideIcon;
}

function Item({ item }: { item: ItemNav }) {
  return (
    <NavLink to={item.a} style={{ textDecoration: 'none' }}>
      {({ isActive }) => (
        <span
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 9,
            width: '100%',
            minHeight: 32,
            padding: '0 9px',
            borderRadius: 8,
            cursor: 'pointer',
            background: isActive ? c.activo : 'transparent',
            color: isActive ? c.tinta : c.textoGris2,
            font: `500 12.5px ${sans}`,
            textAlign: 'left',
          }}
        >
          <span
            style={{
              width: 2,
              height: 12,
              borderRadius: 2,
              flex: 'none',
              background: isActive ? c.acento : 'transparent',
            }}
          />
          <item.icono size={14} strokeWidth={1.8} style={{ flex: 'none' }} aria-hidden />
          {item.label}
          <span
            style={{
              marginLeft: 'auto',
              font: `400 11px ${mono}`,
              color: isActive ? c.textoTenue : c.textoMuyApagado,
            }}
          >
            {item.tag}
          </span>
        </span>
      )}
    </NavLink>
  );
}

function Rotulo({ children, primero }: { children: string; primero?: boolean }) {
  return (
    <div
      style={{
        font: `400 10px ${mono}`,
        color: c.textoGris2,
        letterSpacing: '.1em',
        padding: primero ? '0 9px 7px' : '20px 9px 7px',
      }}
    >
      {children}
    </div>
  );
}

export function Navegacion({
  club,
  operacion,
  base,
}: {
  club: Club | undefined;
  operacion: ItemNav[];
  base: ItemNav[];
}) {
  const avisar = useTostada();

  return (
    <nav
      style={{
        flex: 'none',
        width: 196,
        display: 'flex',
        flexDirection: 'column',
        padding: '14px 10px',
        borderRight: `1px solid ${c.linea}`,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '4px 8px 16px' }}>
        <div
          style={{
            width: 20,
            height: 20,
            borderRadius: 6,
            background: c.acento,
            color: c.papel,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            font: `600 10px ${sans}`,
          }}
        >
          C
        </div>
        <div style={{ font: `500 13.5px ${sans}`, letterSpacing: '-.01em' }}>ClubSpot</div>
      </div>

      <button
        type="button"
        onClick={() => avisar('Cambiar de club o sede')}
        className="h-borde-suave"
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          width: '100%',
          padding: '8px 9px',
          borderRadius: 9,
          border: `1px solid ${c.borde}`,
          background: c.panel,
          cursor: 'pointer',
          textAlign: 'left',
          marginBottom: 16,
        }}
      >
        <div style={{ flex: 1, minWidth: 0 }}>
          <div
            style={{
              font: `500 12px ${sans}`,
              color: c.texto,
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
            }}
          >
            {club?.nombre ?? '—'}
          </div>
          <div style={{ font: `400 10.5px ${mono}`, color: c.textoTenue, marginTop: 2 }}>
            {club?.sede ?? ''}
          </div>
        </div>
        <ArrowUpDown size={12} strokeWidth={1.8} style={{ flex: 'none', color: c.textoGris2 }} aria-hidden />
      </button>

      <Rotulo primero>OPERACIÓN</Rotulo>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
        {operacion.map((n) => (
          <Item key={n.a} item={n} />
        ))}
      </div>

      <Rotulo>BASE</Rotulo>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
        {base.map((n) => (
          <Item key={n.a} item={n} />
        ))}
      </div>

      <div style={{ flex: 1 }} />

      <button
        type="button"
        onClick={() => avisar('Perfil y permisos')}
        className="h-fondo"
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 9,
          width: '100%',
          padding: 8,
          borderRadius: 9,
          border: '1px solid transparent',
          background: 'transparent',
          cursor: 'pointer',
          textAlign: 'left',
        }}
      >
        <div
          style={{
            width: 24,
            height: 24,
            borderRadius: 7,
            background: c.borde,
            color: c.textoTenue2,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            font: `500 10.5px ${sans}`,
          }}
        >
          {club?.operadorIniciales ?? '··'}
        </div>
        <div style={{ minWidth: 0 }}>
          <div style={{ font: `500 11.5px ${sans}`, color: c.textoSuave }}>
            {club?.operador ?? ''}
          </div>
          <div style={{ font: `400 10px ${mono}`, color: c.textoTenue }}>{club?.rol ?? ''}</div>
        </div>
      </button>
    </nav>
  );
}
