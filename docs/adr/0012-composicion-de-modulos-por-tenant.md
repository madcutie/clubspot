# ADR-0012 — Composición de módulos por tenant: qué se vende, qué es de quién

**Fecha:** 16/08/2026 · **Estado:** Aceptada, con un pendiente explícito (ver "Lo que queda
abierto")

## Contexto

El producto se vende por módulos, pero no estaba escrito **cómo se componen** ni **quién es
dueño de qué** cuando dos módulos hablan de la misma persona. Sin esa regla escrita, el modelo
fue derivando hacia decisiones que la contradicen sin que nadie lo notara:

- El catálogo declaraba `members → [core, finance]` y `bookings → [core, finance]`, es decir
  **finanzas obligatorio**. A un cliente que sólo quiere alquilar canchas se le encendía un
  módulo que no pidió, prometiéndole capacidades que quizá ni se le pueden dar.
- La tabla `people` —identidad, del módulo `core`— tiene columnas `debtAmount` y
  `debtCurrency`, que son de finanzas. Antes tuvo `preferredSport`, que era de reservas y se
  eliminó en ADR-0008 por la misma razón, todavía sin la regla que lo explicara.

Las formas reales que el producto tiene que soportar, planteadas por el usuario:

| Cliente | Contrata | Qué significa |
|---|---|---|
| X | club + reservas | Una persona es socia, hace karate y además el sábado alquila una cancha con amigos |
| Y | sólo reservas | Alquiler de fútbol 5. Puede tener cobro o no tenerlo, y no tiene liquidaciones |
| Z | club + reservas + finanzas | Lo anterior más la parte de dinero |

## Decisión

**1. El módulo es la unidad más chica que se vende por separado.** Si un cliente puede pagar
por A sin B, entonces A y B son módulos distintos. La granularidad del catálogo la define
**lo que se puede vender y facturar**, no cómo está organizado el código ni qué tan parecidas
son dos cosas por dentro. El test para saber si el catálogo está bien cortado: *¿existe un
cliente que quiera esto sin aquello?* Si la respuesta es sí, hay que partirlo.

**2. Dependencia dura es sólo "sin el otro, el concepto no existe".** Que un módulo *aproveche*
a otro cuando está presente no lo convierte en dependencia. Confundir las dos cosas es lo que
llevó a declarar finanzas obligatorio.

**3. La persona es una sola y pertenece a `core`.** `core` guarda **quién es**: identidad y
contacto. Ser socio, anotarse en una actividad, alquilar una cancha o deber plata **no son
atributos de la persona**: son **vínculos**, y cada módulo guarda los suyos en sus propias
tablas, apuntando a `personId`.

> **Corolario verificable de un vistazo: ningún módulo agrega columnas a `people`.** Si en
> `people` aparece una columna que sólo entiende otro módulo, la regla se rompió.

**4. Ningún módulo asume el vínculo de otro.** `bookings` tiene que poder venderle un turno a
alguien que **no es socio**; `members` tiene que poder tener un socio que nunca reservó nada.

**5. La integración entre módulos es por contrato y es opcional.** El consumidor declara la
interfaz, el módulo dueño la implementa, DI las une. Si el dueño no está contratado no hay
implementación, y entonces la funcionalidad **no se ofrece** —endpoint 404, campo ausente,
precio sin descuento de socio—; nunca falla ni queda a medias.

## Lo que queda abierto (a definir más adelante, no ahora)

**La granularidad fina de finanzas y el concepto de capacidades.** Cobrar un turno y hacer
liquidaciones son capacidades de tamaño y complejidad distintos: un cliente puede pagar por la
primera sin tener la segunda. Por la regla 1, eso significa que `finance` como bloque único
está mal cortado.

Cómo se parte, y cómo se expresan las **capacidades** que habilitan o no ciertas features
según lo que el cliente tenga contratado, **se define más adelante**. Mientras tanto:

- `finance` sigue declarado como está y las flechas `members → finance` y `bookings → finance`
  quedan **provisionales**: hoy no expresan una dependencia real, sino que la parte financiera
  se está desarrollando junto con reservas.
- No se parte el módulo por anticipado: cortarlo sin saber qué se vende sería adivinar, y un
  corte equivocado es más caro que el bloque único.

## Consecuencias

- `people.debtAmount` / `people.debtCurrency` **violan la regla 3** y son deuda técnica
  reconocida: quedan como stub marcado hasta que se defina la parte financiera, que absorbe el
  tema. Es lo primero que ese trabajo tiene que resolver.
- Toda tabla nueva se pregunta primero de qué módulo es. Si guarda un vínculo entre una persona
  y algo de un módulo, va en las tablas de ese módulo, no en `core`.
- **Actividades** (deportes dictados por profesores, con alumnos) es **parte del módulo de
  club**, no un módulo aparte — confirmado por el usuario el 16/08/2026, corrigiendo la
  suposición contraria. Encaja con la regla 1: se vende junto con el club, entonces no se
  parte. Y confirma la regla 3: **el alumno y el profesor son vínculos sobre la misma
  `Person`**, no entidades nuevas ni columnas de `people`; un alumno puede además pertenecer a
  un grupo familiar.

## Alternativas descartadas

- **Un producto único con features apagables:** es lo que insinuaba el grafo anterior. Vuelve
  obligatorio lo que no lo es y obliga a instalar y prometer lo que el cliente no compró.
- **Partir finanzas ahora en `payments` / `accounts` / `billing`:** el corte parece razonable
  pero es una decisión comercial que todavía no está tomada. Se difiere junto con capacidades.
- **Dejar que cada módulo agregue columnas a `people` "por comodidad":** es exactamente cómo
  llegaron `preferredSport` y `debtAmount`. Barato al escribirlo, imposible de desarmar cuando
  el módulo se apaga.
