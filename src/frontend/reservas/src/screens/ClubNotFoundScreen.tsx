import { MapPinOff } from 'lucide-react';
import { Body, Screen } from '../ui/Screen';
import { C, F } from '../ui/theme';

/**
 * La URL no trae club, o trae uno que la API no reconoce. No se redirige a ningún club por
 * defecto: mostrar el club equivocado es peor que decir que no se encontró.
 */
export function ClubNotFoundScreen() {
  return (
    <Screen>
      <Body style={{ display: 'grid', placeItems: 'center', minHeight: '100%', textAlign: 'center' }}>
        <div style={{ maxWidth: 380 }}>
          <MapPinOff size={32} strokeWidth={1.8} color={C.muted} aria-hidden />
          <div style={{ font: `700 20px ${F.display}`, letterSpacing: '-.015em', marginTop: 16 }}>
            No encontramos ese club
          </div>
          <div style={{ font: `500 14px ${F.body}`, color: C.muted, marginTop: 8, lineHeight: 1.5 }}>
            Revisá el link con el que llegaste. La dirección para reservar incluye el nombre del
            club, por ejemplo <span style={{ color: C.ink }}>/mi-club</span>.
          </div>
        </div>
      </Body>
    </Screen>
  );
}
