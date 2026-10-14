# adr-0007-consistencia-concurrencia-outbox.md

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0007 |
| Fecha | 2026-09-09 |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Reemplaza | ADR-06 histórico de Outbox, ampliado para incluir concurrencia optimista. |

## Contexto

Asignar o reasignar un conductor modifica varias piezas del estado operativo: solicitud, conductor, vehículo, asignación, viaje y notificaciones. Bajo concurrencia pueden ocurrir dos intentos sobre el mismo recurso o una caída después de confirmar la transacción pero antes de publicar la notificación.

La arquitectura necesita una única fuente de verdad transaccional sin convertir el sistema en un conjunto de locks globales.

## Decisión

Usar **transacciones PostgreSQL** para cambios de estado del núcleo, **concurrencia optimista** sobre agregados susceptibles a edición concurrente y **Transactional Outbox** para publicar efectos asíncronos derivados de una transacción confirmada.

La fila de asignación/estado incluirá un token de versión administrado por la capa de persistencia. Si dos operaciones compiten, solo una confirma; la otra recibe conflicto y debe reintentar o informar al operador.

Los eventos externos —notificaciones, auditoría derivada, actualizaciones asíncronas— se escriben en la Outbox dentro de la misma transacción y un worker los procesa de forma idempotente.

## Alternativas consideradas

### Alternativa A — Solo transacciones sin Outbox
Descartada para efectos externos: puede existir la ventana 'commit exitoso + publicación perdida'.

### Alternativa B — Locks pesimistas generalizados
Descartada como política por defecto por costo de contención y riesgo de bloqueos.

### Alternativa C — Optimistic locking sin Outbox
Útil para concurrencia, insuficiente para publicación confiable.

### Alternativa D — Optimistic locking + Outbox
Elegida porque separa control de concurrencia de entrega confiable de eventos.

## Consecuencias positivas

- Evita dobles asignaciones confirmadas.
- El estado transaccional permanece como fuente de verdad.
- Reduce pérdida de eventos después de un commit.
- Permite reintentar efectos externos sin repetir la transacción de negocio.

## Consecuencias negativas

- Añade tabla Outbox y un procesador en segundo plano.
- Los consumidores deben ser idempotentes.
- El operador puede ver un conflicto y tener que reintentar una reasignación legítima.

## Tácticas de Bass aplicadas

- **Mantener consistencia de datos** — una transacción protege el agregado.
- **Controlar concurrencia** — versionado optimista detecta escrituras competidoras.
- **Recuperación de fallos** — Outbox reintenta efectos derivados.
- **Encolar** — los eventos pendientes desacoplan el commit del envío externo.

## Escenarios y requisitos relacionados

- `OPLOG-REND-01` — 0 dobles reasignaciones confirmadas.
- `COND-CONF-02` — estado de disponibilidad consistente.
- `PAS-CONF-02` — el pasajero ve el último estado confirmado.
- `RF-09 / RF-10 / RF-12 / RF-34` — solicitud, asignación y cambios de estado.

## ADRs relacionados

- **ADR-0009** — el motor usa esta base de consistencia.
- **ADR-0008** — los mensajes de WhatsApp consumen eventos de forma idempotente.
- **ADR-0004** — PostgreSQL provee la transacción y persistencia de Outbox.

## Notas de implementación

- El worker Outbox debe marcar eventos enviados de forma atómica y soportar reintentos.
- Los handlers de notificación deben deduplicar por `EventId`/`MessageId`.
- Los conflictos optimistas deben mapearse a una respuesta de negocio comprensible.
- Probar concurrencia con escenarios de doble reasignación y cambio simultáneo de disponibilidad.

## Referencias

- `tradeoff_FlowRide_ACTUALIZADO.xlsx` — T-05.
- `RestriccionesTecnicas_FlowRide_ACTUALIZADO.xlsx` — Consistencia.
- Pattern: Transactional Outbox.
