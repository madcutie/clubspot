# Datos de prueba para consultas (solo lectura)

> ⚠️ **Contiene datos personales reales del padrón de Chaco For Ever.**
> Uso interno para reproducir consultas durante la investigación. No publicar, no versionar
> en un repo público, no cargar en ningún servicio externo.

## Entorno

| Dato | Valor |
|---|---|
| URL backoffice | `https://gestion.ourclub.io/chacoforever/#/` |
| API base | `https://gestion.ourclub.io/chacoforever/api/` |
| Usuario de la sesión relevada | ALDANA SOFÍA (perfil administrativo) |
| Versión | Sistema de Gestión de Socios **V1.44** |

## Apellido de búsqueda acordado

**`lopez`** — usar siempre este apellido para pruebas de búsqueda de personas.

## Personas / Socios encontrados con apellido LOPEZ

Resultado de *Caja → Consulta Deuda Total* con Apellido = `lopez`:

| Nro Socio | Documento | Apellido y Nombre | Notas |
|---|---|---|---|
| 00006628 | 42705634 | EDGAR R. ALTAMIRANDA LOPEZ | Persona **ACTIVO** / Socio **BAJA**. Sin deuda. |
| 00010012 | 95454562 | ALEJANDRA AMBITO LOPEZ | Doc de 8 dígitos con formato atípico (95…) |
| 00016894 | 41428935 | IGNACIO JESUS BARCO LOPEZ | |
| 00008892 | 49104621 | TIAGO NICOLAS CUELLAR LOPEZ | |
| 00007077 | 53789254 | CIELO EVANGELINA ESCALANTE LOPEZ | |
| 00009436 | 559822311 | MAIA SOLEDAD GAUNA LOPEZ | Documento de **9 dígitos** — el campo no valida largo de DNI |
| 00002914 | 47672991 | LAUTARO NICOLAS GONZALEZ LOPEZ | |
| ~00012055 | (parcial) | SAMUEL GABRIEL JARA LOPEZ | fila cortada en el viewport |

### Observaciones de modelado que salen de estos datos

- **Nro. Socio** es un entero con padding a 8 dígitos (`00006628`). No es el DNI.
- El **documento no está normalizado**: convive con 8 y 9 dígitos. Nuestro modelo debería
  guardar `tipo_documento` + `numero_documento` y validar por tipo, no asumir DNI argentino.
- **Persona y Socio tienen estados independientes.** Una persona `ACTIVO` puede tener
  su condición de socio en `BAJA`. Son dos agregados distintos, no un flag.

## Trámites de ejemplo (Inscripción Web)

| Trámite | Documento | Estado | Fecha |
|---|---|---|---|
| 00000344 | 13719650 | ANULADO | 21/07/2022 |
| 00000345 | 13719651 | ANULADO | 21/07/2022 |

- Nro. de trámite también con padding a 8 dígitos.
- Total de trámites de inscripción web en el sistema: **2.131**.
