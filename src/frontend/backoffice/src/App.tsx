import { Navigate, Route, Routes } from 'react-router-dom';
import { CalendarDays, Clock, LandPlot, Users } from 'lucide-react';
import { useAgenda, useCanchas, useClub, useHorarios, usePersonas } from './api/queries';
import { resumenAgenda } from './domain/agenda';
import { isoDe } from './domain/fechas';
import { useParamsAgenda } from './rutas';
import { Navegacion, type ItemNav } from './ui/Navegacion';
import { c, sans } from './ui/theme';
import { AgendaScreen } from './modulos/reservas/AgendaScreen';
import { CanchasScreen } from './modulos/canchas/CanchasScreen';
import { HorariosScreen } from './modulos/horarios/HorariosScreen';
import { PersonasScreen } from './modulos/personas/PersonasScreen';

/**
 * Consola del club. Los módulos son rutas; el catálogo de módulos contratados
 * va a decidir cuáles se montan (un módulo apagado responde 404, no 403), pero
 * eso llega con la API: por ahora están todos.
 */
export default function App() {
  const { data: club } = useClub();
  const { deporte, dia } = useParamsAgenda();

  // Los contadores de la barra lateral: los turnos son los del día que se está
  // mirando, el resto es el tamaño de cada catálogo.
  const { data: agenda } = useAgenda(deporte, isoDe(dia));
  const { data: canchas } = useCanchas();
  const { data: horarios } = useHorarios();
  const { data: padron } = usePersonas({ q: '', filtro: 'todas', pagina: 0 });

  const operacion: ItemNav[] = [
    {
      a: '/reservas',
      label: 'Reservas',
      tag: agenda ? String(resumenAgenda(agenda.canchas).turnos) : '',
      icono: CalendarDays,
    },
    { a: '/canchas', label: 'Canchas', tag: canchas ? String(canchas.length) : '', icono: LandPlot },
    { a: '/horarios', label: 'Horarios', tag: horarios ? String(horarios.length) : '', icono: Clock },
  ];
  const base: ItemNav[] = [
    { a: '/personas', label: 'Personas', tag: padron ? String(padron.padron) : '', icono: Users },
  ];

  return (
    <div
      style={{
        display: 'flex',
        height: '100vh',
        minHeight: 640,
        background: c.papel,
        fontFamily: sans,
        color: c.texto,
        overflow: 'hidden',
      }}
    >
      <Navegacion club={club} operacion={operacion} base={base} />
      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <Routes>
          <Route path="/" element={<Navigate to="/reservas" replace />} />
          <Route path="/reservas" element={<AgendaScreen />} />
          <Route path="/canchas" element={<CanchasScreen />} />
          <Route path="/horarios" element={<HorariosScreen />} />
          <Route path="/personas" element={<PersonasScreen />} />
          <Route path="*" element={<Navigate to="/reservas" replace />} />
        </Routes>
      </div>
    </div>
  );
}
