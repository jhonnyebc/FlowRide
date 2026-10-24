# ADR-0020: Guardar los instantes pasados en UTC y los eventos futuros como hora local con su zona horaria

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0020 |
| Fecha | 2026-10-20 |
| Estado | **Accepted** |
| Participantes | Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Origen | Requisito del curso sobre el manejo de fechas (video del profesor, 2026-10-20). Complementa ADR-0004 en la forma de guardar las fechas. |

## Contexto

FlowRide maneja dos clases de fechas con necesidades distintas:

- **Hechos que ya ocurrieron:** cuándo se registró una solicitud, cuándo se aceptó una asignación, cuándo inició un viaje o cuándo se auditó una acción. Su significado es un instante absoluto en la línea de tiempo.
- **Eventos futuros que ocurren en un lugar:** la hora de un servicio, el inicio y el fin de un turno, la hora estimada de una parada o el horario de un itinerario recurrente. Lo que el operador, el pasajero y el conductor acordaron es una **hora de reloj en un lugar** («mañana a las 6:00 en Bogotá»), no un instante calculado de antemano.

Hasta la versión anterior todo se guardaba como instante UTC. Eso funciona para el pasado, pero para el futuro tiene un defecto: si las reglas de la zona horaria cambian entre el día en que se registra el servicio y el día en que ocurre (un cambio de horario de verano, una decisión de gobierno o una actualización de la base de zonas horarias), el instante guardado ya no corresponde a la hora acordada. Además, convertir a UTC al guardar pierde la información de en qué zona se acordó. El requisito del curso pide distinguir ambos casos de forma explícita.

## Decisión

1. **Instantes pasados en UTC.** Todo hecho ocurrido se guarda como instante UTC (`timestamptz` en PostgreSQL, ADR-0004) y se muestra en la zona de quien lo mira.
2. **Eventos futuros como hora local más el nombre IANA de su zona.** Se guarda la hora de reloj sin desfase (por ejemplo, `2026-10-22T06:00`) junto con la zona (por ejemplo, `America/Bogota`), en un value object de dominio, `FechaHoraLocal`. Se aplica a la hora del servicio de una solicitud, al inicio y fin de un turno, a la hora estimada de una parada y al horario de un itinerario.
3. **Instante UTC derivado.** Cada evento futuro conserva además su instante UTC calculado a partir de la hora local y la zona, para ordenar, filtrar y detectar traslapes en la base. Ese instante es derivado: la fuente de verdad es la hora local con su zona.
4. **Recálculo al iniciar.** Al arrancar, cada stack recalcula el instante derivado de los eventos futuros, de modo que una actualización de las reglas de zona horaria del sistema se refleje sin migraciones manuales.
5. **Contrato de la API.** Para los eventos futuros la API recibe la fecha y hora local **sin** desfase y, opcionalmente, la zona (si falta, se usa la zona del operador). Si la fecha trae desfase o `Z`, la API la rechaza con 400, porque sería ambiguo si es una hora local o un instante. Las respuestas devuelven la hora local, la zona y el instante derivado.
6. **Presentación.** El Panel Web muestra un evento futuro en su hora local con el nombre de su zona («22 oct 2026, 6:00 a. m. (America/Bogota)») y un instante pasado en la zona del navegador.

## Alternativas consideradas

### Alternativa A — Todo en UTC (situación anterior)

Simple y uniforme, pero un evento futuro deja de corresponder a la hora acordada si cambian las reglas de su zona, y se pierde en qué zona se acordó. **Descartada** para eventos futuros; se conserva para los instantes pasados.

### Alternativa B — `DateTimeOffset` con el desfase del momento del registro

Conserva el desfase, pero un desfase no es una zona: `-05:00` no dice si el lugar tendrá otro desfase en la fecha del evento. Tiene el mismo problema que la alternativa A ante cambios de reglas. **Descartada.**

### Alternativa C — Solo la hora local, sin zona

Conserva la hora acordada, pero no permite calcular el instante ni comparar eventos de lugares distintos, y depende de suponer la zona del servidor. **Descartada.**

### Alternativa D — Hora local + zona IANA + instante derivado (elegida)

Conserva lo acordado, permite ordenar y validar traslapes con el instante derivado y tolera cambios de reglas recalculando ese instante.

| Criterio | D (elegida) | A. Todo UTC | B. Desfase fijo | C. Solo hora local |
|---|---|---|---|---|
| Conserva la hora acordada | Sí | No ante cambios de reglas | No ante cambios de reglas | Sí |
| Sabe en qué zona se acordó | Sí | No | Parcial (solo el desfase) | No |
| Permite ordenar y detectar traslapes en la base | Sí (instante derivado) | Sí | Sí | No |
| Complejidad | Media | Baja | Baja | Baja |

## Consecuencias positivas

- Un servicio, un turno o un itinerario conserva la hora que se acordó aunque cambien las reglas de la zona.
- Queda explícito en qué zona ocurre cada evento futuro; los mensajes de WhatsApp y el panel lo muestran sin conversiones sorpresivas.
- El contrato de la API evita la ambigüedad entre hora local e instante.

## Consecuencias negativas

- Cada evento futuro guarda tres datos (hora local, zona e instante derivado) en lugar de uno.
- Hay que recalcular el instante derivado cuando cambian las reglas de zona; se resuelve al iniciar cada stack.
- Los clientes de la API deben enviar la hora local sin desfase; un cliente que envíe un instante recibe un error explícito.

## Tácticas de Bass aplicadas

- **Mantener la integridad semántica de los datos** (confiabilidad): la fuente de verdad de un evento futuro es lo acordado, no un cálculo.
- **Validar la entrada** (seguridad y confiabilidad): la API rechaza fechas ambiguas.
- **Recuperación automática / reconciliación** (confiabilidad): el recálculo al iniciar corrige los instantes derivados.

## Escenarios y requisitos relacionados

- RF-08 (itinerarios), RF-09 (solicitudes), RF-07 y RF-12 (turnos y disponibilidad), RF-35 (recordatorios), RF-36 (vista del día).
- OPLOG-CAP-01 (tres turnos simultáneos) y COND-CONF-02 (cambio de disponibilidad durante una asignación), que dependen de comparar franjas horarias correctamente.

## ADRs relacionados

- **ADR-0004** — persistencia en PostgreSQL: los instantes se guardan como `timestamptz` UTC y los traslapes se protegen con restricciones sobre el instante derivado.
- **ADR-0009** — el motor de asignación evalúa franjas y turnos con los instantes derivados.
- **ADR-0015** y **ADR-0016** — los mensajes al pasajero y al conductor muestran la hora local del servicio.
- **ADR-0019** — el Panel Web aplica la regla de presentación.

## Notas de implementación

### Implementación en FlowRide (estado al 22-10-2026)

- Value object `FechaHoraLocal` en el dominio; `SolicitudViaje` guarda `HoraLocalServicio` y `ZonaHoraria` y deriva `FechaHoraServicio`; `Turno` guarda `InicioLocal`, `FinLocal` y `ZonaHoraria`; `Parada` guarda `HoraEstimadaLocal`; `Itinerario` guarda su `ZonaHoraria`.
- La zona por defecto es la del operador (`ZonaHorariaOperador`); los reportes CSV y los mensajes del bot usan esa zona para mostrar instantes.
- El módulo Horarios recalcula los instantes derivados de los eventos futuros al iniciar el stack (`RecalculoHorariosAlIniciar`).
- La migración `FechasConZona` agregó las columnas y completó las existentes a partir de los instantes ya guardados.
- Las pruebas usan horas y zonas explícitas (sin depender de la zona de la máquina) y el tiempo entra siempre como parámetro.

## Referencias

- IANA. *Time Zone Database* (tz database). iana.org/time-zones.
- Skeet, J. *Storing UTC is not a silver bullet*. codeblog.jonskeet.uk, 2019.
- Microsoft. *Choosing between DateTime, DateOnly, DateTimeOffset, TimeSpan, TimeOnly, and TimeZoneInfo in .NET*. Microsoft Learn.
- PostgreSQL. *Date/Time Types* (`timestamp with time zone`). postgresql.org/docs.
