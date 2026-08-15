import { BotonCerrar, Panel } from '../../ui/Panel';
import { c, mono, sans, secundario } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';

/**
 * Importación de la planilla que el club ya tiene. Todavía es la pantalla, no
 * el importador: el procesamiento real —idempotente y con informe de rechazos—
 * es trabajo de la fase cero, del lado del backend.
 */
export function ImportarPanel({ onCerrar }: { onCerrar: () => void }) {
  const avisar = useTostada();

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
          <div style={{ font: `500 19px ${sans}`, letterSpacing: '-.025em' }}>Importar planilla</div>
          <div style={{ font: `400 12px ${sans}`, color: c.textoGris, marginTop: 5 }}>
            Para pasar la lista que el club ya tiene en Excel.
          </div>
        </div>
        <BotonCerrar onClick={onCerrar} />
      </div>

      <div style={{ flex: 1, overflowY: 'auto', padding: '18px 20px' }}>
        <div
          style={{
            border: `1px dashed ${c.bordePunteado}`,
            borderRadius: 12,
            padding: '36px 18px',
            textAlign: 'center',
            background: c.panel,
          }}
        >
          <div style={{ font: `500 14px ${sans}` }}>Arrastrá el archivo acá</div>
          <div style={{ font: `400 12px ${mono}`, color: c.textoGris, marginTop: 7 }}>
            .xlsx / .csv
          </div>
          <button
            type="button"
            onClick={() => avisar('Selector de archivos')}
            className="h-ghost"
            style={{ ...secundario(), background: 'transparent', marginTop: 16, minHeight: 32 }}
          >
            Elegir archivo
          </button>
        </div>
        <div style={{ font: `400 12px/1.6 ${sans}`, color: c.textoGris, marginTop: 14 }}>
          La única columna obligatoria es el teléfono. Las repetidas se marcan para revisar antes de
          sumarlas.
        </div>
      </div>
    </Panel>
  );
}
