import { useState } from 'react';
import { useCrearPersona } from '../../api/queries';
import { BotonCerrar, Panel } from '../../ui/Panel';
import { c, campoPanel, mono, primario, sans } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';

/**
 * Alta de mostrador. Pide lo mínimo para poder reservar a su nombre; el resto
 * de los datos se completan después si hacen falta.
 */
export function NuevaPersonaPanel({ onCerrar }: { onCerrar: () => void }) {
  const avisar = useTostada();
  const crear = useCrearPersona();

  const [nombre, setNombre] = useState('');
  const [tel, setTel] = useState('');
  const [email, setEmail] = useState('');

  const listo = nombre.trim().length > 0 && tel.trim().length > 0;

  return (
    <Panel onCerrar={onCerrar}>
      <div
        style={{
          flex: 'none',
          padding: '20px 20px 16px',
          display: 'flex',
          alignItems: 'flex-start',
          justifyContent: 'space-between',
          gap: 12,
          borderBottom: `1px solid ${c.linea}`,
        }}
      >
        <div>
          <div style={{ font: `500 19px ${sans}`, letterSpacing: '-.025em' }}>Nueva persona</div>
          <div style={{ font: `400 12px ${sans}`, color: c.textoGris, marginTop: 5 }}>
            Alta de mostrador. Queda lista para reservar a su nombre.
          </div>
        </div>
        <BotonCerrar onClick={onCerrar} />
      </div>

      <div
        style={{
          flex: 1,
          minHeight: 0,
          overflowY: 'auto',
          padding: '18px 20px',
          display: 'flex',
          flexDirection: 'column',
          gap: 13,
        }}
      >
        <div>
          <Rotulo>NOMBRE Y APELLIDO</Rotulo>
          <input
            type="text"
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
            placeholder="ej. Marcela Ojeda"
            className="f-borde"
            style={campoPanel()}
          />
        </div>
        <div>
          <Rotulo>TELÉFONO / WHATSAPP</Rotulo>
          <input
            type="tel"
            value={tel}
            onChange={(e) => setTel(e.target.value)}
            placeholder="362 4XX-XXXX"
            className="f-borde"
            style={campoPanel()}
          />
          <div style={{ font: `400 11px ${sans}`, color: c.textoTenue, marginTop: 6 }}>
            Es la clave para no duplicar personas.
          </div>
        </div>
        <div>
          <Rotulo>EMAIL (OPCIONAL)</Rotulo>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="nombre@mail.com"
            className="f-borde"
            style={campoPanel()}
          />
        </div>
      </div>

      <div
        style={{
          flex: 'none',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          padding: '13px 20px',
          borderTop: `1px solid ${c.linea}`,
        }}
      >
        <button
          type="button"
          onClick={onCerrar}
          className="h-ghost"
          style={{
            minHeight: 34,
            padding: '0 12px',
            borderRadius: 8,
            border: `1px solid ${c.bordeFirme}`,
            background: 'transparent',
            color: c.textoBoton,
            font: `500 12.5px ${sans}`,
            cursor: 'pointer',
          }}
        >
          Cancelar
        </button>
        <div style={{ flex: 1 }} />
        <button
          type="button"
          className={listo ? 'h-primario' : undefined}
          onClick={() => {
            if (!listo) return;
            crear.mutate(
              { nombre, tel, email },
              {
                onSuccess: () => {
                  avisar('Persona agregada');
                  onCerrar();
                },
              },
            );
          }}
          style={primario(listo)}
        >
          Guardar persona
        </button>
      </div>
    </Panel>
  );
}

function Rotulo({ children }: { children: string }) {
  return (
    <div
      style={{
        font: `400 10.5px ${mono}`,
        color: c.textoTenue,
        letterSpacing: '.08em',
        marginBottom: 7,
      }}
    >
      {children}
    </div>
  );
}
