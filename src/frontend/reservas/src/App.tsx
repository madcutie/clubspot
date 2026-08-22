import { useEffect } from 'react';
import { CLUB_SLUG } from './api/club';
import { ApiError } from './api/portalApi';
import { useClub } from './api/queries';
import { useBooking } from './state/useBooking';
import { ClubNotFoundScreen } from './screens/ClubNotFoundScreen';
import { HomeScreen } from './screens/HomeScreen';
import { AvailabilityScreen } from './screens/AvailabilityScreen';
import { ConfirmScreen } from './screens/ConfirmScreen';
import { SuccessScreen } from './screens/SuccessScreen';
import { ReturnScreen } from './screens/ReturnScreen';
import { BookingDetailScreen } from './screens/BookingDetailScreen';
import { MyBookingsScreen } from './screens/MyBookingsScreen';

export default function App() {
  // Sin club en la URL no se monta nada del flujo: todo lo de adentro habla con la API por slug.
  return CLUB_SLUG === null ? <ClubNotFoundScreen /> : <Portal />;
}

function Portal() {
  const api = useBooking();
  const club = useClub();

  // El nombre del club sale del catálogo, no del `index.html`: el portal es uno solo.
  useEffect(() => {
    if (club.data?.nombre) document.title = `${club.data.nombre} · Reserva de canchas`;
  }, [club.data?.nombre]);

  if (club.error instanceof ApiError && club.error.status === 404) return <ClubNotFoundScreen />;

  return (
    <div className="app-page">
      <div className="app-shell">
        {api.st.screen === 'home' && <HomeScreen api={api} />}
        {api.st.screen === 'avail' && <AvailabilityScreen api={api} />}
        {api.st.screen === 'confirm' && <ConfirmScreen api={api} />}
        {api.st.screen === 'done' && <SuccessScreen api={api} />}
        {api.st.screen === 'retorno' && <ReturnScreen api={api} />}
        {api.st.screen === 'mine' && <MyBookingsScreen api={api} />}
        {api.st.screen === 'detalle' && <BookingDetailScreen api={api} />}
      </div>
    </div>
  );
}
