import { Navigate, Route, Routes } from 'react-router-dom';
import { CalendarDays, Clock, LandPlot, Users } from 'lucide-react';
import { permisosDe, rutaInicial } from './auth/permisos';
import { useSesion, type Sesion } from './auth/sesion';
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
import { LoginScreen } from './modulos/sesion/LoginScreen';
import { SinAcceso } from './modulos/sesion/SinAcceso';

/** Sin sesión no hay consola: el token es lo que dice quién entró y qué puede ver (ADR-0018). */
export default function App() {
  const sesion = useSesion();
  return sesion ? <Consola sesion={sesion} /> : <LoginScreen />;
}

/**
 * Consola del club. Los módulos son rutas, y el rol decide cuáles se montan: lo que el
 * operador no puede usar no aparece y su URL redirige, en vez de mostrarse apagado. El
 * catálogo de módulos contratados —que es otra cosa, y responde 404— todavía no gatea nada.
 */
function Consola({ sesion }: { sesion: Sesion }) {
  const permisos = permisosDe(sesion.roles);
  const inicio = rutaInicial(permisos);
  const { data: club } = useClub();
  const { deporte, dia } = useParamsAgenda();

  // Los contadores de la barra lateral: los turnos son los del día que se está
  // mirando, el resto es el tamaño de cada catálogo.
  const { data: agenda } = useAgenda(deporte, isoDe(dia), permisos.operarAgenda);
  const { data: canchas } = useCanchas(permisos.configurar);
  const { data: horarios } = useHorarios(permisos.configurar);
  const { data: padron } = usePersonas({ q: '', filtro: 'todas', pagina: 0 }, permisos.verPersonas);

  if (!inicio) return <SinAcceso sesion={sesion} />;

  const operacion: ItemNav[] = [];
  if (permisos.operarAgenda) {
    operacion.push({
      a: '/reservas',
      label: 'Reservas',
      tag: agenda ? String(resumenAgenda(agenda.canchas).turnos) : '',
      icono: CalendarDays,
    });
  }
  if (permisos.configurar) {
    operacion.push(
      { a: '/canchas', label: 'Canchas', tag: canchas ? String(canchas.length) : '', icono: LandPlot },
      { a: '/horarios', label: 'Horarios', tag: horarios ? String(horarios.length) : '', icono: Clock },
    );
  }

  const base: ItemNav[] = [];
  if (permisos.verPersonas) {
    base.push({ a: '/personas', label: 'Personas', tag: padron ? String(padron.padron) : '', icono: Users });
  }

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
      <Navegacion club={club} sesion={sesion} operacion={operacion} base={base} />
      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <Routes>
          {permisos.operarAgenda && <Route path="/reservas" element={<AgendaScreen />} />}
          {permisos.configurar && <Route path="/canchas" element={<CanchasScreen />} />}
          {permisos.configurar && <Route path="/horarios" element={<HorariosScreen />} />}
          {permisos.verPersonas && <Route path="/personas" element={<PersonasScreen />} />}
          <Route path="*" element={<Navigate to={inicio} replace />} />
        </Routes>
      </div>
    </div>
  );
}
