# ADR-0011: adoptar OpenTelemetry y auditoría persistente por tenant

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0011 |
| Fecha | 2026-09-09 |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Reemplaza | — |

## Contexto

FlowRide debe reconstruir el flujo de una solicitud desde su recepción hasta asignación, notificación y viaje, y además conservar evidencia de acciones administrativas sensibles. La observabilidad técnica y la auditoría legal persiguen objetivos distintos: logs/traces ayudan a diagnosticar el sistema; auditoría permite demostrar quién hizo qué.

La auditoría legal se conserva 5 años. La telemetría GPS solo 6 meses. Mezclar ambas retenciones o enviar datos personales completos a logs técnicos aumentaría costo y riesgo.

## Decisión

Instrumentar el backend con **OpenTelemetry** para trazas, métricas y logs correlacionados mediante `traceId`, `tenantId`, componente y resultado, evitando registrar secretos o datos personales innecesarios.

Implementar un `AuditService` separado conceptualmente de los logs técnicos. Cada Silo conserva sus eventos de auditoría en estructuras persistentes propias, con retención de 5 años y campos mínimos: actor, acción, recurso, timestamp, resultado, motivo cuando aplique y valores relevantes antes/después cuando la trazabilidad lo requiera.

La tecnología de backend de observabilidad puede cambiar sin afectar la instrumentación OTLP/OpenTelemetry.

## Alternativas consideradas

### Alternativa A — Logs de texto sin estructura
Descartada: dificulta correlación y métricas.

### Alternativa B — Usar logs técnicos como auditoría
Descartada: retenciones, integridad y propósitos son distintos.

### Alternativa C — Proveedor propietario acoplado desde código
Descartada como contrato; puede usarse como backend mediante exporter.

### Alternativa D — OpenTelemetry + AuditService persistente
Elegida por separación de responsabilidades y portabilidad.

## Consecuencias positivas

- Correlación uniforme entre API, motor, WhatsApp y telemetría.
- Backend de observabilidad sustituible mediante exporters.
- Auditoría mantiene retención e integridad independientes de los logs.
- Facilita medir SLA y tiempos P95/P99.

## Consecuencias negativas

- Aumenta volumen de datos y requiere políticas de muestreo/retención.
- Instrumentación mal diseñada puede exponer información personal.
- Los eventos de auditoría añaden escritura y almacenamiento de largo plazo.

## Tácticas de Bass aplicadas

- **Detectar fallos** — métricas y health checks.
- **Registrar eventos** — logs/traces estructurados.
- **Auditar** — registro persistente e inmutable de acciones sensibles.
- **Limitar exposición** — redacción/minimización de datos sensibles.

## Escenarios y requisitos relacionados

- `OPLOG-AUD-01` — auditoría de servicios.
- `PAS-AUD-02` — auditoría de cancelaciones.
- `OPLOG-AUD-02` — auditoría de intervención manual.
- `PAS-AUD-03` — trazabilidad de calificación.
- `RF-50 / RF-51 / RF-52 / RF-53` — eventos, errores, auditoría y disponibilidad.

## ADRs relacionados

- **ADR-0008** — instrumenta webhook y entrega de mensajes.
- **ADR-0007** — Outbox genera trazabilidad de eventos confiables.
- **ADR-0005** — instrumenta throughput/errores de telemetría.
- **ADR-0010** — health checks y SLA de despliegue.

## Notas de implementación

- No registrar tokens, contraseñas, coordenadas completas en logs generales ni payloads completos de WhatsApp.
- Propagar `traceId` a workers y eventos Outbox.
- Separar retención: logs técnicos según necesidad operacional; auditoría 5 años; GPS 6 meses.
- Emitir métricas para P95/P99, backlog, fallos de webhook, eventos GPS/min y conflictos de concurrencia.

## Referencias

- OpenTelemetry .NET — traces, metrics and logs.
- `Drivers_Arquitectonicos_FlowRide_FINAL.docx`.
- `RestriccionesNegocio_FlowRide_ACTUALIZADO.xlsx`.
- `tradeoff_FlowRide_ACTUALIZADO.xlsx`.
