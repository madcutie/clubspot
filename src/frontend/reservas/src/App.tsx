import { useBooking } from './state/useBooking';
import { HomeScreen } from './screens/HomeScreen';
import { AvailabilityScreen } from './screens/AvailabilityScreen';
import { ConfirmScreen } from './screens/ConfirmScreen';
import { SuccessScreen } from './screens/SuccessScreen';
import { ReturnScreen } from './screens/ReturnScreen';
import { MyBookingsScreen } from './screens/MyBookingsScreen';

export default function App() {
  const api = useBooking();

  return (
    <div className="app-page">
      <div className="app-shell">
        {api.st.screen === 'home' && <HomeScreen api={api} />}
        {api.st.screen === 'avail' && <AvailabilityScreen api={api} />}
        {api.st.screen === 'confirm' && <ConfirmScreen api={api} />}
        {api.st.screen === 'done' && <SuccessScreen api={api} />}
        {api.st.screen === 'retorno' && <ReturnScreen api={api} />}
        {api.st.screen === 'mine' && <MyBookingsScreen api={api} />}
      </div>
    </div>
  );
}
