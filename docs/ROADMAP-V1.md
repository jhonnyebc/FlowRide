# FlowRide — Roadmap V1

> **Memoria entre sesiones.** Se actualiza al cerrar cada fase. Antes de empezar una fase, lee los ADR que toca.
> Flujo de trabajo (ADR-0018): una rama `feature/RF-XX-…` por fase → PR hacia `develop`, con todos los gates en verde.
> Al cerrar cada fase hay que detenerse y entregar: los RF funcionales, cómo probarlos (`.http` o pasos), los tests y el commit sugerido.

**Fuera de alcance:** RF-55 (telemetría GPS) y RF-56 (desviación de ETA), es decir, ADR-0005.

Leyenda: ✅ hecho · 🟡 parcial · ⬜ pendiente · ⛔ fuera de alcance

## Estado por fase

| Fase | Nombre | Módulos | ADR | Estado |
|---|---|---|---|---|
| — | Motor de asignación (PoC del ADR-0009) | Asignacion | 0009 | ✅ (commit `d751b09`) |
| — | Revisión de ADR y roadmap | — | todos | ✅ |
| 0 | Base ejecutable | Plataforma | 0010, 0011, 0014, 0017, 0018 | ✅ rama `feature/RF-53-base-ejecutable` (pendiente de PR) |
| 1 | Tenancy y persistencia Silo | Tenancy | 0002, 0004, 0010, 0014 | ⬜ |
| 2 | Identidad, usuarios y auditoría | Identidad, Auditoria | 0003, 0011 | ⬜ |
| 3 | Recursos operativos y parámetros | Operacion | 0006, 0013, 0014, 0016 | ⬜ |
| 4 | Solicitudes e itinerarios | Solicitudes | 0001, 0004, 0007 | ⬜ |
| 5 | Asignación de punta a punta | Asignacion | 0007, 0009, 0016 | ⬜ |
| 6 | Viajes y calificación | Viajes, Calificacion | 0007, 0011 | ⬜ |
| 7 | Canal WhatsApp | Mensajeria | 0003, 0008, 0012 | ⬜ |
| 8 | Notificaciones y alertas | Notificaciones | 0007, 0015, 0016 | ⬜ |
| 9 | Reportes y vista operacional | Reportes | 0006, 0013 | ⬜ |
| 10 | Borde común | Plataforma | 0012, 0014 | ⬜ |
| 11 | Panel Web | Web | 0003, 0015, 0016, 0019 | ⬜ |
| 12 | Operación: despliegue, backups y retención | Plataforma | 0010, 0011, 0013 | ⬜ |

## Detalle por fase

### Fase 0 — Base ejecutable
- [x] `docker-compose.yml`: PostgreSQL 17 (host 5433) con `flowride_catalogo`, `flowride_tenant_a` y `flowride_tenant_b`, cada una con su propio rol; aislamiento verificado con autenticación real.
- [x] Tests separados: `UnitTests` (63), `ArchitectureTests` (9) e `IntegrationTests` (12, Testcontainers).
- [x] OpenAPI, ProblemDetails con `traceId`, `/health/live` y `/health/ready` (comprueba la base del Silo sin exponer la conexión).
- [x] Logging JSON con timestamp UTC, traceId, tenantId, componente y resultado; trazas y métricas con OpenTelemetry (OTLP si se define `OTEL_EXPORTER_OTLP_ENDPOINT`).
- [x] El arranque falla si falta `ConnectionStrings:Silo` o si `FlowRide:TenantId` falta o es inválido (ADR-0014).
- [x] Gates: `dotnet format`, ArchUnitNET (capas + ciclos entre módulos), cobertura (96 % líneas / 85 % ramas), `.gitattributes`, `.editorconfig`, CI en GitHub Actions con gitleaks.
- [ ] **Pendiente:** SonarCloud (requiere crear el proyecto y el secreto `SONAR_TOKEN` en GitHub).
- RF cubiertos parcialmente: RF-50, RF-51, RF-53.

### Fase 1 — Tenancy y persistencia Silo
- [ ] Catálogo de plataforma: operadores y su ruteo, sin datos operacionales.
- [ ] `DbContext` por Silo con EF Core; cadena de conexión resuelta desde un tenant confiable que falla cerrado (ADR-0002).
- [ ] Migraciones por tenant, idempotentes, con *migration bundle*.
- [ ] Pipeline de aprovisionamiento idempotente: BD → migraciones → secretos → realm → ruteo (ADR-0010).
- [ ] El arranque falla si falta un secreto (ADR-0014).
- [ ] Spike 3: 0 lecturas o escrituras cross-tenant.

### Fase 2 — Identidad, usuarios y auditoría
- [ ] Keycloak con un realm por operador y claim `tenant_id`; `IdentityProviderAdapter`; RBAC.
- [ ] Revocación de acceso en 60 s o menos (ADM-SEG-01).
- [ ] `AuditService` persistente por Silo, con cadena de hashes (ADR-0011).

### Fase 3 — Recursos operativos y parámetros
- [ ] Conductores, vehículos y turnos. Cifrado de campo para documento y teléfono (ADR-0014).
- [ ] Documentos del conductor en almacenamiento de objetos (Azurite en local, ADR-0013).
- [ ] `ops.parametros` versionado, con caché de TTL máximo de 60 s (ADR-0006, ADR-0016).

### Fase 4 — Solicitudes e itinerarios
- [ ] `SolicitudViaje` completa con `EstadoSolicitudHistorial`; CQRS ligero para consultas.

### Fase 5 — Asignación de punta a punta
- [ ] Caso de uso `AssignPendingRequestHandler` con transacción, concurrencia optimista y Outbox (ADR-0007).
- [ ] Brechas del motor frente al ADR-0009:
  - el validador debe detectar reglas obligatorias deshabilitadas, precedencias, dependencias y ciclos;
  - al decision log le faltan `Timestamp` (vía `IClock`) y `RuleVersion`;
  - falta el atributo "compatible con servicio crítico".
- [ ] Configuración del motor versionada con rollback (RF-21) y decision log persistente.
- [ ] Spike 2: 0 dobles asignaciones bajo concurrencia.

### Fase 6 — Viajes y calificación
- [ ] `Viaje`, `Parada`, `Incidente` y `Calificacion`; trazabilidad por viaje.

### Fase 7 — Canal WhatsApp
- [ ] `IWhatsAppGateway`, `WhatsAppSandboxAdapter` (número de prueba de Meta) y adapter simulado.
- [ ] Webhook con verificación de firma, idempotencia por `ExternalMessageId` y máquina de estados conversacional.
- [ ] PoC 4: duplicados sin procesamiento duplicado.

### Fase 8 — Notificaciones y alertas
- [ ] `INotificationGateway`: WhatsApp, alerta en el panel por SSE y correo (`IEmailSender`, smtp4dev en local).
- [ ] Worker de la Outbox con `SKIP LOCKED`, deduplicación por `EventId` y recordatorios como eventos futuros.
- [ ] Catálogo de mensajes `es-CO` y registro de plantillas (ADR-0016).

### Fase 9 — Reportes y vista operacional
- [ ] Indicadores, reportes y exportes con URL firmada de 15 min o menos.

### Fase 10 — Borde común
- [ ] YARP: ruteo por subdominio y por `phone_number_id`, límites de tasa, validación `X-Hub-Signature-256` y cabeceras de seguridad.

### Fase 11 — Panel Web
- [ ] SPA en React + TypeScript (`web/`): OIDC + PKCE, cliente generado desde OpenAPI, alertas por SSE, WCAG 2.1 AA.

### Fase 12 — Operación
- [ ] Dockerfile con versión fija, imagen semver+SHA, dos stacks locales (uno por operador).
- [ ] Backups, jobs de retención y archivo con lock consultivo; pruebas de recuperación y de carga.

## Checklist por RF

| RF | Nombre | Fase | Estado |
|---|---|---|---|
| RF-01 | Autenticación basada en token | 2 | ⬜ |
| RF-02 | Autorización basada en roles | 2 | ⬜ |
| RF-03 | Registro de operador como tenant | 1 | ⬜ |
| RF-04 | Configuración inicial del tenant | 3 | ⬜ |
| RF-05 | Creación de usuarios por tenant | 2 | ⬜ |
| RF-06 | Asignación de roles a usuarios | 2 | ⬜ |
| RF-07 | Consulta de disponibilidad de conductores | 3 | ⬜ |
| RF-08 | Registro de itinerarios | 4 | ⬜ |
| RF-09 | Recepción de solicitudes de transporte | 4 | ⬜ |
| RF-10 | Asignación automática de conductores | 5 | 🟡 motor listo, falta el caso de uso |
| RF-11 | Aplicación de reglas de negocio | 5 | 🟡 7 reglas implementadas |
| RF-12 | Reasignación manual | 5 | ⬜ |
| RF-13 | Priorización de solicitudes urgentes | 5 | 🟡 regla implementada |
| RF-14 | Recepción de mensajes de WhatsApp | 7 | ⬜ |
| RF-15 | Procesamiento conversacional de solicitudes | 7 | ⬜ |
| RF-16 | Confirmaciones al pasajero | 8 | ⬜ |
| RF-17 | Notificación al conductor asignado | 8 | ⬜ |
| RF-18 | Actualización de estado del viaje por WhatsApp | 7 | ⬜ |
| RF-19 | Aislamiento físico entre tenants | 1 | ⬜ |
| RF-20 | Configuración separada por tenant | 1 | ⬜ |
| RF-21 | Configuración avanzada de reglas | 5 | ⬜ |
| RF-22 | Cierre de sesión | 2 | ⬜ |
| RF-23 | Recuperación de credenciales | 2 | ⬜ |
| RF-24 | Actualización de información de usuarios | 2 | ⬜ |
| RF-25 | Activación e inactivación de usuarios | 2 | ⬜ |
| RF-26 | Registro de conductores | 3 | ⬜ |
| RF-27 | Actualización de datos de conductores | 3 | ⬜ |
| RF-28 | Activación e inactivación de conductores | 3 | ⬜ |
| RF-29 | Validación de datos mínimos de solicitud | 4 | ⬜ |
| RF-30 | Consulta del estado de solicitudes | 4 | ⬜ |
| RF-31 | Historial de solicitudes y viajes | 4 | ⬜ |
| RF-32 | Modificación o cancelación de solicitudes | 4 | ⬜ |
| RF-33 | Visualización de asignaciones generadas | 5 | ⬜ |
| RF-34 | Actualización del estado de asignación | 5 | ⬜ |
| RF-35 | Recordatorios automáticos | 8 | ⬜ |
| RF-36 | Vista centralizada de solicitudes y viajes | 9 | ⬜ |
| RF-37 | Monitoreo de servicios en curso | 6 | ⬜ |
| RF-38 | Consulta de usuarios, conductores y asignaciones | 3 | ⬜ |
| RF-39 | Intervención manual en la operación | 5 | ⬜ |
| RF-40 | Trazabilidad de cada viaje | 6 | ⬜ |
| RF-41 | Búsqueda y filtrado | 3 | ⬜ |
| RF-42 | Indicadores básicos | 9 | ⬜ |
| RF-43 | Notificación de cambios de estado | 8 | ⬜ |
| RF-44 | Notificación de reasignación o novedades | 8 | ⬜ |
| RF-45 | Alertas operativas | 8 | ⬜ |
| RF-46 | Reporte de viajes realizados | 9 | ⬜ |
| RF-47 | Reporte de solicitudes atendidas y pendientes | 9 | ⬜ |
| RF-48 | Reporte de conductores asignados | 9 | ⬜ |
| RF-49 | Reporte histórico por cliente empresarial | 9 | ⬜ |
| RF-50 | Registro de eventos del sistema | 0 | 🟡 log estructurado por solicitud |
| RF-51 | Registro de errores y excepciones | 0 | 🟡 ProblemDetails + log con traceId |
| RF-52 | Auditoría de acciones administrativas | 2 | ⬜ |
| RF-53 | Monitoreo técnico de disponibilidad | 0 | 🟡 health checks + métricas OTel |
| RF-54 | Calificación del servicio | 6 | ⬜ |
| RF-55 | Ingesta de telemetría GPS | — | ⛔ |
| RF-56 | Detección de desviación de ETA | — | ⛔ |

## Decisiones tomadas fuera de los ADR
| Fecha | Decisión |
|---|---|
| 2026-10-13 | El motor de asignación se mantiene en `FlowRide.Domain/Assignment` (ADR-0009 no exige Application). |
| 2026-10-13 | Se agrega el atributo explícito "compatible con servicio crítico" para `CriticalServicePriorityRule` (Fase 5). |
| 2026-10-13 | La BD de catálogo de plataforma guarda solo operadores y ruteo, sin datos operacionales. |
