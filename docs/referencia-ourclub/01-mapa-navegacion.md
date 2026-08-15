# Mapa de navegación — OurClub / Chaco For Ever

- **URL base:** `https://gestion.ourclub.io/chacoforever/#/`
- **Producto:** OurClub ("Powered by ourclub") — SaaS multi-tenant de gestión de clubes. El tenant va en el path (`/chacoforever/`).
- **Tipo de app:** SPA con routing por hash (`#/ruta`), sidebar con menú acordeón (metisMenu / patrón AdminLTE-Inspinia).
- **Sesión relevada:** usuario `ALDANA SOFÍA` (perfil administrativo del club).

> Nota: el sidebar renderiza sólo los módulos habilitados para el tenant + permisos del usuario.
> Este árbol es lo visible con la sesión actual, no necesariamente el catálogo completo del producto.

## Árbol completo del menú lateral

| # | Módulo | Ítem | Ruta |
|---|--------|------|------|
| 1 | **Inicio** | — | `#/inicio` |
| 2 | **Trámites** | Inscripcion Web | `/InscripcionWebBuscador/` |
| | | Tablero Inscripcion Web | `/InscripcionWebTablero` |
| | | Registro Socios | `/EmpadronamientoBuscador/` |
| | | Tablero Registro Socios | `/ReempadronamientoTablero` |
| | | Suscripciones | `/SuscripcionWebBuscador/` |
| | | Renaper | `/renaper_info` |
| 3 | **Caja** | Gestion de Caja | `/caja/gestion/` |
| | | Consulta Deuda Total | `/caja/consulta_total_ctacte` |
| | | Cierre | `/caja/cierre_caja` |
| | | Consulta Caja | `/caja/consulta_caja` |
| | | Recibos Anulados | `/caja/consulta_anulados` |
| | | Consulta Conceptos | `/caja/consulta_conceptos` |
| 4 | **Recibos** | Consulta | `/caja/consultarecibos` |
| | | Envios Masivos | `/recibos/envioMasivo` |
| | | Administrar | `/caja/gestionrecibos` |
| 5 | **Reportes** | General | `/consulta_general` |
| | | Cuenta Corriente | `/consulta_ctacte` |
| | | Recibos | `/consulta_recibos` |
| | | Cuenta Corriente Anulados | `/ConsultaCuentaCorrienteAnulados` |
| | | Socios Altas-Bajas | `/consulta_socios_altas_bajas` |
| 6 | **Dashboard** | — | `/dashboard_gral` |
| 7 | **Personas** | Gestión de Personas | `/PersonasBuscador` |
| | | Consulta Migracion | `/persona/migracion_consulta` |
| 8 | **Socios** | Gestión de Socios | `/socios` |
| | | Consulta | `/consulta_socios` |
| | | Grupos Familiares | `/grupos_familiares` |
| | | Categorias Vigentes | `/socios/CategoriasVigentes` |
| | | Conceptos Historicos | `/CategoriaSocioConcpetosHistorico` |
| 9 | **Cobradores** | Gestión de Cobradores | `/CobradoresBuscador` |
| | | Rendir | `/CobradoresRendiciones/-1` |
| | | Rendiciones | `/CobradoresRendiciones/consulta_rendiciones` |
| | | Cierre | `/CobradoresRendicionesCierre` |
| | | Reasignaciones | `/CobradoresRendicionesCambioAsignacionRecibo` |
| | | Control | `/CobradoresControl` |
| | | Consulta Recibos | `/CobradoresConsultaRecibo` |
| | | Arqueos | `/CobradoresRendiciones/consulta_arqueos` |
| 10 | **Deportes** | Actividades Deportivas | `/ActividadesDeportivas/false` |
| | | Deportistas | `/deportistas` |
| | | Profesores | `/profesores` |
| | | Aranceles | `/deportes/consulta` |
| | | Gestion Aranceles | `/ActividadesDeportivasArancelMasivo` |
| | | Becas | `/ActividadesBecas` |
| | | Liquidación Deportistas | `/liquidacion_deportes` |
| 11 | **Colegio** | Alumnos | `/alumnos` |
| | | Divisiones | `/escuela/ColegioDivisionesCons` |
| | | Liquidación Manual | `/ColegioArancelMasivo` |
| 12 | **Ecommerce** | Vender | `/vender_ecommerce` |
| | | Ventas | `/ventas_ecommerce` |
| | | Productos | `/productos_ecommerce` |
| | | Rentabilidad | `/reportes_ecommerce` |
| | | Configuracion | `/catalogo_ecommerce` |
| 13 | **Reservas Club+** | Gestor de Espacios | `/actividades` |
| | | Reservar | `/reservar_espacio` |
| | | Consulta | `/reservas_consultas` |
| 14 | **Empresas** | Gestion | `/EmpresasGestion` |
| | | Consulta | `/EmpresasConsulta` |
| 15 | **Eventos** ⭐ | Boleteria | `/BoleteriaV2` |
| | | Gestion Eventos | `/EventosV2` |
| | | Derecho Admision | `/DerechoAdmision` |
| | | Torneos | `/GestionTorneos/false` |
| 16 | **Control Rapido** | (sin submenú visible) | — |
| 17 | **Control Acceso** | (sin submenú visible) | — |
| 18 | **Filiales** | Miembros | `/Filiales/Miembros` |
| | | Gestion | `/Filiales/Gestion` |
| | | Consulta | `/Filiales/Consulta` |
| 19 | **APPs Noticias** | — | `/AppFanNovedades` |
| 20 | **Liquidación** | Liquidación Mensual | `/liquidacion/proceso/index` |
| | | Liquidacion Anual | `/liquidacion/proceso/anual/index` |
| | | Busqueda / Gestion | `/liquidacion/gestion` |
| | | Actividades Deportivas | `/ActividadesCategoriasLiquidacion` |
| 21 | **Presentaciones** | Gestión | `/presentacion/gestion` |
| | | Consulta | `/presentacion/pendientes_proceso` |
| | | Pendientes Debito Automatico | `/presentacion/debito_automatico` |
| 22 | **Ranking** | Calculo Scoring | `/scoring/calculo` |
| 23 | **Mi Perfil** | — | `/perfil_user/miPerfil` |
| 24 | **Notas** | — | `/notas` |
| 25 | **Configuraciones** | — | `/configuracion_general` |
| 26 | **Administracion** | Sistema | (sin href directo) |
| | | Migracion | (sin href directo) |

⭐ = módulo directamente relacionado con los dos pains a reconstruir.

## Lectura rápida de la arquitectura funcional

Los 26 módulos se agrupan en 5 grandes dominios:

1. **Padrón de personas y socios** — Personas → Socios → Grupos Familiares → Categorías.
   Es el núcleo: una *Persona* (identidad, DNI, Renaper) puede tener uno o varios roles
   (*Socio*, *Deportista*, *Alumno*, *Profesor*, *Miembro de filial*).
2. **Facturación y cobranzas** — Liquidación (genera la deuda periódica) → Cuenta Corriente →
   Caja / Cobradores / Presentaciones (débito automático) → Recibos.
3. **Actividades** — Deportes, Colegio, Reservas Club+, Torneos.
4. **Venta y accesos** — Eventos/Boletería, Control Acceso, Control Rápido, Ecommerce, Derecho de Admisión.
5. **Soporte** — Reportes, Dashboard, Configuración, Administración, Notas, Ranking/Scoring.
