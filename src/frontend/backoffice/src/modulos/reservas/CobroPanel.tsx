import { useEffect, useRef, useState } from 'react';
import { Check, Copy, MessageCircle, RefreshCw } from 'lucide-react';
import { QRCodeSVG } from 'qrcode.react';
import { useAgenda, useCobro, useInvalidar } from '../../api/queries';
import { ApiError } from '../../api/http';
import { pesos } from '../../domain/dinero';
import { hhmm, isoDe } from '../../domain/fechas';
import type { Deporte, ReservaDia } from '../../domain/types';
import { BotonCerrar, Panel } from '../../ui/Panel';
import { c, mono, sans } from '../../ui/theme';
import { useTostada } from '../../ui/Tostadas';

/**
 * Cobro de un turno con Mercado Pago: el cliente escanea el QR con su celular.
 *
 * La cancha ya está reservada, así que el link no bloquea nada y reemitirlo es
 * gratis (decisión del usuario, 19/08/2026). Mientras el panel está abierto se
 * consulta la agenda: cuando el pago se acredita, avisa y cierra solo.
 */
export function CobroPanel({
  reserva,
  cancha,
  deporte,
  dia,
  onCerrar,
}: {
  reserva: ReservaDia;
  cancha: string;
  deporte: Deporte;
  dia: number;
  onCerrar: () => void;
}) {
  const avisar = useTostada();
  const inv = useInvalidar();
  const cobrar = useCobro(reserva.id);
  const { data: agenda } = useAgenda(deporte, isoDe(dia));
  const [copiado, setCopiado] = useState(false);
  // La reserva que llegó por props es la foto del momento en que se abrió el panel;
  // lo cobrado se relee de la agenda, que es la que se entera del webhook.
  const actual =
    agenda?.canchas.flatMap((x) => x.reservas).find((x) => x.id === reserva.id) ?? reserva;
  const saldo = actual.precio - actual.pagado;
  const cobro = cobrar.data ?? null;

  // El webhook confirma en segundos; la agenda es la que se entera.
  useEffect(() => {
    const timer = setInterval(() => void inv.agenda(), 3000);
    return () => clearInterval(timer);
  }, []);

  const acreditado = useRef(false);
  useEffect(() => {
    if (saldo > 0 || acreditado.current) return;
    acreditado.current = true;
    avisar(`Cobrado ${pesos(actual.pagado)}`);
    onCerrar();
  }, [saldo]);

  const copiar = async () => {
    if (!cobro) return;
    await navigator.clipboard.writeText(cobro.url);
    setCopiado(true);
    setTimeout(() => setCopiado(false), 1800);
  };

  const whatsapp = () => {
    if (!cobro) return;
    const numero = telefonoInternacional(reserva.tel);
    const texto = encodeURIComponent(
      `Hola ${reserva.persona}, para confirmar tu turno de ${hhmm(reserva.t)} en ${cancha} ` +
        `podés pagar ${pesos(cobro.monto)} acá: ${cobro.url}`,
    );
    window.open(numero ? `https://wa.me/${numero}?text=${texto}` : `https://wa.me/?text=${texto}`, '_blank');
  };

  return (
    <Panel onCerrar={onCerrar}>
      <div style={{ flex: 'none', padding: '20px 20px 0' }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 12 }}>
          <div style={{ minWidth: 0 }}>
            <div style={{ font: `500 20px ${sans}`, letterSpacing: '-.025em' }}>Cobrar con Mercado Pago</div>
            <div style={{ font: `400 12.5px ${mono}`, color: c.textoGris, marginTop: 6 }}>
              {reserva.persona} · {cancha} · {hhmm(reserva.t)}
            </div>
          </div>
          <BotonCerrar onClick={onCerrar} />
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '18px 20px 22px', textAlign: 'center' }}>
        <div style={{ font: `500 34px ${mono}`, letterSpacing: '-.03em', color: c.titulo }}>
          {pesos(cobro?.monto ?? saldo)}
        </div>
        {actual.pagado > 0 && (
          <div style={{ font: `400 11.5px ${mono}`, color: c.textoGris, marginTop: 4 }}>
            saldo · ya pagó {pesos(actual.pagado)} de {pesos(actual.precio)}
          </div>
        )}

        {cobro ? (
          <>
            <div
              style={{
                display: 'inline-block',
                padding: 14,
                marginTop: 18,
                borderRadius: 14,
                border: `1px solid ${c.borde}`,
                background: c.blanco,
              }}
            >
              <QRCodeSVG value={cobro.url} size={208} level="M" />
            </div>
            <div style={{ font: `400 12.5px/1.5 ${sans}`, color: c.textoGris, marginTop: 12 }}>
              Que lo escanee con la cámara o con la app de Mercado Pago.
              <br />
              Vence a las {new Date(cobro.venceEn).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit' })}.
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 7, marginTop: 18 }}>
              <button type="button" onClick={() => void copiar()} style={accion}>
                {copiado ? <Check size={14} strokeWidth={2.5} aria-hidden /> : <Copy size={14} strokeWidth={2} aria-hidden />}
                {copiado ? 'Link copiado' : 'Copiar link de pago'}
              </button>
              <button type="button" onClick={whatsapp} style={accion}>
                <MessageCircle size={14} strokeWidth={2} aria-hidden />
                Mandar por WhatsApp
              </button>
              <button
                type="button"
                disabled={cobrar.isFetching}
                onClick={() => void cobrar.refetch()}
                style={accion}
              >
                <RefreshCw size={14} strokeWidth={2} aria-hidden />
                {cobrar.isFetching ? 'Generando…' : 'Generar otro'}
              </button>
            </div>
          </>
        ) : (
          <div style={{ font: `400 13px ${sans}`, color: c.textoGris, padding: '60px 0' }}>
            {cobrar.isError ? mensajeDeError(cobrar.error) : 'Generando el código…'}
          </div>
        )}
      </div>

      <div
        style={{
          flex: 'none',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          padding: '13px 20px',
          borderTop: `1px solid ${c.linea}`,
          font: `400 12px ${mono}`,
          color: c.textoGris,
        }}
      >
        <span className="spin" style={{ width: 11, height: 11, borderRadius: '50%', border: `2px solid ${c.bordeFirme}`, borderTopColor: c.acento, flex: 'none' }} />
        Esperando el pago…
      </div>
    </Panel>
  );
}

function mensajeDeError(error: unknown): string {
  if (error instanceof ApiError && error.status === 409) return 'Esta reserva no tiene saldo por cobrar.';
  if (error instanceof ApiError && error.status === 422) return 'El club todavía no tiene configurado el cobro online.';
  return 'No se pudo generar el cobro. Probá de nuevo.';
}

/** WhatsApp quiere el número con código de país y sin separadores. */
function telefonoInternacional(tel: string | null): string | null {
  if (!tel) return null;
  const digitos = tel.replace(/\D/g, '');
  if (digitos.length < 8) return null;
  return digitos.startsWith('54') ? digitos : `54${digitos}`;
}

const accion = {
  width: '100%',
  minHeight: 38,
  borderRadius: 9,
  border: `1px solid ${c.bordeFirme}`,
  background: 'transparent',
  color: c.textoBoton,
  font: `500 12.5px ${sans}`,
  cursor: 'pointer',
  display: 'flex',
  alignItems: 'center',
  gap: 8,
  padding: '0 13px',
} as const;
