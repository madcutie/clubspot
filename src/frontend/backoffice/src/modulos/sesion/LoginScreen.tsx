import { useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { LoaderCircle, TriangleAlert } from 'lucide-react';
import { abrirSesion } from '../../auth/sesion';
import { ApiError } from '../../api/http';
import { signIn } from '../../api/generated/auth/auth';
import { c, campo, mono, primario, sans } from '../../ui/theme';

/**
 * Entrada a la consola. No pregunta por el club: el usuario ya sabe a cuál pertenece
 * (ADR-0018). El error es siempre el mismo, sin distinguir email inexistente de contraseña
 * equivocada — la API tampoco los distingue.
 */
export function LoginScreen() {
  const queryClient = useQueryClient();
  const [email, setEmail] = useState('');
  const [clave, setClave] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [entrando, setEntrando] = useState(false);

  async function entrar(evento: FormEvent) {
    evento.preventDefault();
    if (entrando) return;
    setEntrando(true);
    setError(null);
    try {
      const { accessToken } = await signIn({ email: email.trim(), password: clave });
      // La consola arranca en blanco: la máquina del mostrador pasa de un turno al otro.
      queryClient.clear();
      if (!abrirSesion(accessToken)) setError('La sesión que devolvió el servidor no es válida.');
    } catch (fallo) {
      setError(
        fallo instanceof ApiError && fallo.status === 401
          ? 'Email o contraseña incorrectos.'
          : 'No se pudo conectar con el servidor.',
      );
    } finally {
      setEntrando(false);
    }
  }

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        height: '100vh',
        background: c.papel,
        fontFamily: sans,
        color: c.texto,
      }}
    >
      <form
        onSubmit={entrar}
        style={{
          width: 332,
          padding: 26,
          borderRadius: 14,
          border: `1px solid ${c.linea}`,
          background: c.panel,
          display: 'flex',
          flexDirection: 'column',
          gap: 14,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <div
            style={{
              width: 22,
              height: 22,
              borderRadius: 7,
              background: c.acento,
              color: c.papel,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              font: `600 11px ${sans}`,
            }}
          >
            C
          </div>
          <div style={{ font: `500 14px ${sans}`, letterSpacing: '-.01em' }}>ClubSpot</div>
        </div>

        <div style={{ font: `400 11.5px ${mono}`, color: c.textoTenue, marginBottom: 2 }}>
          Consola del club
        </div>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <span style={{ font: `500 11.5px ${sans}`, color: c.textoTenue2 }}>Email</span>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="username"
            autoFocus
            required
            className="f-borde"
            style={campo()}
          />
        </label>

        <label style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <span style={{ font: `500 11.5px ${sans}`, color: c.textoTenue2 }}>Contraseña</span>
          <input
            type="password"
            value={clave}
            onChange={(e) => setClave(e.target.value)}
            autoComplete="current-password"
            required
            className="f-borde"
            style={campo()}
          />
        </label>

        {error && (
          <div
            role="alert"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 7,
              padding: '9px 11px',
              borderRadius: 9,
              border: `1px solid ${c.naranjaBordeFirme}`,
              background: c.naranjaFondo,
              color: c.naranjaTexto,
              font: `500 12px ${sans}`,
            }}
          >
            <TriangleAlert size={14} strokeWidth={2} style={{ flex: 'none' }} aria-hidden />
            {error}
          </div>
        )}

        <button
          type="submit"
          disabled={entrando}
          className={entrando ? undefined : 'h-primario'}
          style={{
            ...primario(!entrando),
            marginTop: 4,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 7,
          }}
        >
          {entrando && (
            <LoaderCircle size={14} strokeWidth={2.2} className="girando" aria-hidden />
          )}
          {entrando ? 'Entrando…' : 'Entrar'}
        </button>
      </form>
    </div>
  );
}
