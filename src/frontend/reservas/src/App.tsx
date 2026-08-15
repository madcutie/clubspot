import { useBooking } from './state/useBooking';
import { HomeScreen } from './screens/HomeScreen';
import { AvailabilityScreen } from './screens/AvailabilityScreen';
import { ConfirmScreen } from './screens/ConfirmScreen';
import { PayScreen } from './screens/PayScreen';
import { DoneScreen } from './screens/DoneScreen';
import { MyBookingsScreen } from './screens/MyBookingsScreen';

export default function App() {
  const api = useBooking();

  return (
    <div className="app-page">
      <div className="app-shell">
        {api.st.screen === 'home' && <HomeScreen api={api} />}
        {api.st.screen === 'avail' && <AvailabilityScreen api={api} />}
        {api.st.screen === 'confirm' && <ConfirmScreen api={api} />}
        {api.st.screen === 'pay' && <PayScreen api={api} />}
        {api.st.screen === 'done' && <DoneScreen api={api} />}
        {api.st.screen === 'mine' && <MyBookingsScreen api={api} />}
      </div>
    </div>
  );
}
