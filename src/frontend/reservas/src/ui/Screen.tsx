import type { CSSProperties, ReactNode } from 'react';
import { ArrowLeft } from 'lucide-react';
import { C, F } from './theme';

/** Layout común: header fijo, cuerpo scrolleable, footer con la acción principal. */
export function Screen({ children }: { children: ReactNode }) {
  return (
    <div
      style={{
        position: 'absolute',
        inset: 0,
        display: 'flex',
        flexDirection: 'column',
        background: C.screen,
      }}
    >
      {children}
    </div>
  );
}

export function Header({ children, bordered = true }: { children: ReactNode; bordered?: boolean }) {
  return (
    <div
      style={{
        flex: 'none',
        padding: 'calc(env(safe-area-inset-top) + 18px) 20px 12px',
        borderBottom: bordered ? '1px solid #1D211D' : undefined,
      }}
    >
      <div style={{ maxWidth: 640, margin: '0 auto' }}>{children}</div>
    </div>
  );
}

export function Body({ children, style }: { children: ReactNode; style?: CSSProperties }) {
  return (
    <div className="no-scrollbar" style={{ flex: 1, overflowY: 'auto' }}>
      <div style={{ maxWidth: 640, margin: '0 auto', padding: '16px 20px 28px', ...style }}>
        {children}
      </div>
    </div>
  );
}

export function Footer({ children }: { children: ReactNode }) {
  return (
    <div
      style={{
        flex: 'none',
        padding: '12px 20px calc(env(safe-area-inset-bottom) + 18px)',
        borderTop: '1px solid #1D211D',
        background: C.screen,
      }}
    >
      <div style={{ maxWidth: 640, margin: '0 auto', display: 'flex', flexDirection: 'column', gap: 8 }}>
        {children}
      </div>
    </div>
  );
}

export function BackTitle({ title, onBack }: { title: string; onBack: () => void }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
      <button
        type="button"
        onClick={onBack}
        aria-label="Volver"
        style={{
          width: 44,
          height: 44,
          marginLeft: -10,
          border: 'none',
          background: 'transparent',
          color: C.ink,
          cursor: 'pointer',
          display: 'grid',
          placeItems: 'center',
        }}
      >
        <ArrowLeft size={22} strokeWidth={2} />
      </button>
      <div style={{ font: `700 17px ${F.display}`, letterSpacing: '-.01em' }}>{title}</div>
    </div>
  );
}
