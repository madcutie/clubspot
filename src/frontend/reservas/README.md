# Reserva de canchas — Chaco Forever Spot

Mockup navegable para demo con stakeholders. Es la app tal como se shipearía:
pantalla completa, mobile first, sin barras de prototipo. Toda la data sale de
un backend simulado, así que las pantallas se recorren navegando.

## Correr

```bash
npm install
npm run dev
```

- Local: http://localhost:5183
- En el celular: usar la URL `Network:` que imprime Vite (ej. `http://192.168.68.200:5183`),
  con el teléfono en la misma WiFi. Conviene "Agregar a pantalla de inicio" para
  verla sin la barra del navegador.

Para mostrarla sin la PC prendida: `npm run build` y servir `dist/` en cualquier
hosting estático (Netlify, Vercel, GitHub Pages).

## Cómo llegar a cada pantalla desde la app

| Pantalla | Recorrido |
| --- | --- |
| Home | pantalla inicial |
| Horarios / turno elegido | *Ver horarios* → duración → hora → cancha |
| Día sin cupo + sugerencias | en la tira de días, elegir uno marcado **LLENO** (tachado) → *Ver horarios* |
| Sin cupo por filtro | elegir 2 h y filtrar por un tipo de cancha con poca oferta |
| Confirmar reserva | *Continuar* con un turno elegido |
| Seña + saldo pendiente | en Confirmar, elegir *Seña online + resto en el club* |
| Pago rechazado | en Pago, elegir *Tarjeta de crédito o débito* → *Pagar* (el primer intento se rechaza; el segundo aprueba) |
| Turno confirmado | pagar con Mercado Pago o Transferencia |
| Mis reservas | botón *Mis reservas* en Home, o *Ver mis reservas* al confirmar. Tiene datos en **Próximas** y en **Anteriores** |
| Lista vacía | cancelar todas las reservas de *Próximas* |

Las reservas nuevas quedan guardadas en `localStorage`, así la demo sobrevive a
un refresh. Para volver al estado inicial: borrar el sitio de datos del navegador
o llamar a `resetBookings()` desde `src/api/store.ts`.

## Estructura

```
src/
  api/        backend simulado (mockApi.ts) + hooks de React Query (queries.ts)
  domain/     catálogo del club, precios, fechas y reglas de disponibilidad
  screens/    una pantalla por archivo
  state/      estado de UI del flujo (qué eligió el usuario)
  ui/         tokens de diseño y layout compartido
```

Toda la data se pide con **React Query**. `src/api/mockApi.ts` tiene la misma
forma que tendría el API real (funciones async que devuelven DTOs planos, con
latencia): cuando exista el backend, se reemplaza ese archivo por llamadas HTTP
y las pantallas no cambian.

### Reglas del club (editables en `src/domain/catalog.ts`)

- Seña: 50% del total, redondeada a $100.
- Cancelación sin cargo hasta 12 h antes.
- Grilla de 8 a 24 h, 14 días hacia adelante.
- Pádel: 4 canchas. Fútbol 5: 3 canchas. Precio nocturno desde las 19 h.
- La ocupación es determinística (hash): el mismo día muestra siempre lo mismo.
- El día `TORNEO_DIA_IDX` (hoy + 3) tiene torneo interno y queda sin cupo.
