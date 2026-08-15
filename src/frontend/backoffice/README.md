# ClubSpot · Backoffice

Consola de operación del club: agenda de canchas, configuración de canchas y horarios, y la
base de personas.

Es la traducción a React del diseño **"Backoffice Consola"**. **Corre entero contra un mock en
memoria**: no hay una sola llamada HTTP todavía. Sirve para mostrar y discutir el flujo con el
club antes de que exista la API.

```bash
npm install
npm run dev        # http://localhost:5184
npm run typecheck
npm run build
```

El estado del mock vive en memoria: al recargar la página vuelve todo al padrón de ejemplo.

## Rutas

| Ruta | Qué es |
|---|---|
| `/reservas` | Agenda del día, una columna por cancha y media hora por fila |
| `/canchas` | Editor de una cancha: horario, reglas del turno, precios, vista previa |
| `/horarios` | Editor de un horario: horas semanales, fechas propias, calendario |
| `/personas` | Base de personas: búsqueda, filtros, ficha, alta de mostrador |

Lo que se está mirando va en la URL —módulo, deporte, día, filtro, búsqueda, ficha abierta—,
así que cualquier pantalla se puede pasar por link.

## Estructura

```
src/
├─ domain/    tipos y lógica pura: horarios, agenda, fechas, dinero
├─ api/       store.ts (estado del mock) · mockApi.ts (funciones async) · queries.ts (React Query)
├─ ui/        theme.ts (paleta y controles) · Panel · Navegación · Tostadas · estados
├─ modulos/   una carpeta por módulo: reservas, canchas, horarios, personas
└─ rutas.ts   lectura y escritura de los parámetros de la URL
```

**`api/mockApi.ts` es el contrato.** Cuando exista la API real se reemplaza ese archivo por
llamadas HTTP y las pantallas no cambian; `api/store.ts` se borra.

El detalle de decisiones y pendientes está en la sección 10 del `AGENTS.md` de la raíz.
