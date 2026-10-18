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
| — | Modelo de dominio completo (diagrama de clases, sin telemetría) | Todos | — | ✅ PR #3 fusionado |
| 0 | Base ejecutable | Plataforma | 0010, 0011, 0014, 0017, 0018 | ✅ PR #2 fusionado |
| 1 | Tenancy y persistencia Silo | Tenancy, Persistencia | 0002, 0004, 0010, 0014 | ✅ PR #4 fusionada |
| 2 | Identidad, usuarios y auditoría | Identidad, Auditoria | 0003, 0011 | ✅ PR #5 fusionada |
| 3 | Recursos operativos y parámetros | Operacion, Parametros | 0006, 0013, 0014, 0016 | ✅ PR #6 fusionada |
| 4 | Solicitudes e itinerarios | Solicitudes | 0001, 0004, 0007 | ✅ rama `feature/RF-09-solicitudes-itinerarios` (pendiente de PR) |
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
- [x] Catálogo de plataforma (`flowride_catalogo`, esquema `plataforma`): entidad `Operador` (Id = tenant, razón social, NIT, correo, estado), sin datos operacionales. El ruteo (subdominio, `phone_number_id`) se agrega en la Fase 10.
- [x] `FlowRideDbContext` por Silo con EF Core: las 17 entidades en el esquema `ops` (snake_case, como el modelo ER), herencia TPT, `xmin` como token de versión, únicos, checks y `EXCLUDE` (turnos y asignaciones sin solaparse, `btree_gist`). El stack resuelve su Silo desde su configuración de despliegue y falla cerrado si no tiene cadena de conexión (ADR-0002); el claim `tenant_id` del JWT se valida contra ella en la Fase 2.
- [x] Migraciones por tenant, idempotentes: cada Silo registra su versión en `ops.__ef_migraciones`; `migrar` y `migrar-todos` (en orden por Id; un Silo con error no detiene a los demás).
- [ ] *Migration bundle* (`dotnet ef migrations bundle`): se genera y versiona con la imagen en la Fase 12 (pipeline de despliegue). `dotnet-ef` ya está como herramienta local (`dotnet-tools.json`).
- [x] Pipeline de aprovisionamiento idempotente (RF-03): catálogo → secreto → BD y rol → migraciones → operador activo, con la CLI `src/FlowRide.Aprovisionamiento`. El realm de Keycloak se agrega en la Fase 2 y la ruta del borde en la Fase 10.
- [x] Secretos por operador: en local, un almacén user-secrets por operador (`flowride-<tenant>`); la API lo lee en Development. El adaptador de Azure Key Vault queda para la Fase 12.
- [x] El arranque falla si falta un secreto (API: `ConnectionStrings:Silo`; CLI: `Catalogo` y `AdministracionServidor`).
- [x] Spike 3: las credenciales de un operador no abren la base de otro (`42501`) y cada contexto solo ve sus datos.
- [x] Estilo de puertos y adaptadores del profesor aplicado al módulo Tenancy (plantilla para las demás fases): casos de uso con interfaz genérica, puertos y adaptadores de entrada con DTO, servicio de dominio `OperadorDominio` (NIT único, operador suspendido), repositorios como puertos del dominio y `FlowRideExcepcion` → 400 en la API.
- [x] `Viaje.EtaProgramada` eliminada (fuera de alcance: ADR-0005) con la migración `QuitarEtaProgramada`.
- Pruebas: 173 unitarias (94,5 % líneas / 92,1 % ramas), 15 de arquitectura (la API no usa el catálogo; estilo hexagonal), 47 de integración.
- RF cubiertos: RF-03, RF-19; RF-20 parcial (persistencia y secretos separados; reglas y parámetros en las Fases 3 y 5).

### Fase 2 — Identidad, usuarios y auditoría
- [x] Keycloak (26.3, docker-compose) con un realm `flowride-<tenant>` por operador, creado por la CLI desde la plantilla versionada `plantilla-realm.json` (sin secretos): cliente público `flowride-panel` con PKCE, claims `tenant_id` y audiencia `flowride-api`, tokens de 5 min; cliente de servicio `flowride-api` (solo gestiona cuentas de su realm, secreto en el almacén del operador). Paso "realm" agregado al pipeline de aprovisionamiento.
- [x] `IdentityProviderAdapter` (ADR-0003): la API valida el token, exige que `tenant_id` sea el del stack y resuelve el usuario interno por `sub` en cada petición; el rol sale de FlowRide (`RolesSistema`, sembrados por migración), no del token.
- [x] RBAC: políticas `Administrador` y `PanelWeb`; endpoints `/api/usuarios` (crear, listar, activar, desactivar, cambiar rol, `yo`) bajo `/api`, que exige token. Reglas: solo roles del panel desde el panel, correo y WhatsApp únicos, nadie se desactiva ni se cambia el rol a sí mismo.
- [x] Alta de usuarios: FlowRide crea la cuenta en el realm y Keycloak envía el correo para definir la contraseña (Mailpit en local). El primer Administrador lo crea la CLI (`crear-operador --admin-*`) con la composición del stack del operador.
- [x] Revocación en 60 s o menos (ADM-SEG-01): inmediata, porque el estado interno se consulta en cada petición sin caché; además se deshabilita la cuenta y se cierran sus sesiones en Keycloak.
- [x] Auditoría por Silo (ADR-0011): `audit.evento_auditoria` solo INSERT (trigger), cadena SHA-256 sellada en la misma transacción que el cambio (`IUnidadTrabajo` + candado consultivo), sin datos personales; `GET /api/auditoria/verificacion` detecta eventos alterados, borrados o insertados.
- Pruebas: 235 unitarias (95,4 % líneas / 92,7 % ramas), 16 de arquitectura (la API solo conoce puertos de entrada y no usa la cuenta de administración de Keycloak), 78 de integración (Keycloak y Mailpit reales con Testcontainers: token de un realm rechazado por el stack de otro operador, revocación y correo de contraseña).
- RF cubiertos: RF-01, RF-02; RF-23 parcial (el realm permite recuperar la contraseña y envía correos; el flujo se prueba con la pantalla de inicio de sesión del Panel Web, Fase 11).

### Fase 3 — Recursos operativos y parámetros
- [x] Empresas cliente, pasajeros, conductores (estado operativo Activo/Suspendido/Inactivo), turnos sin solaparse y vehículos (con conductor habitual), con sus endpoints bajo `/api` y auditoría de cada alta o cambio. Consultan Administrador y Operador logístico; administra el Administrador (los turnos, también el Operador logístico).
- [x] RF-07: conductores disponibles en una franja según lo programado (activo, licencia vigente, turno que la cubre y sin asignación vigente solapada; sin GPS).
- [x] RF-38/RF-41: listados con búsqueda (nombre, o valor exacto de teléfono, licencia, NIT o código) y filtros (estado, activo, empresa), paginados (máx. 200).
- [x] Cifrado de campo (ADR-0014) del teléfono de todos los usuarios y de la licencia del conductor (en Colombia, la cédula): AES-256-GCM determinista con la clave propia del operador, versionada; la CLI la crea al aprovisionar y cifra los datos anteriores (`RecifradorCamposSilo`).
- [x] Documentos del conductor (ADR-0013) en el bucket privado del operador (Azurite en docker-compose), PDF/PNG/JPG ≤ 5 MB con verificación de la firma del archivo, descarga por URL firmada de 15 min y borrado al eliminarlos. La CLI crea el bucket al aprovisionar.
- [x] Parámetros (ADR-0016) en `ops.parametro`: catálogo en código con tipo, rango y valor por defecto; cada cambio sube la versión (también token de concurrencia) y se audita; caché local con TTL ≤ 60 s invalidada tras confirmar (ADR-0006).
- Pruebas: 293 unitarias (95,3 % líneas / 87,7 % ramas), 16 de arquitectura, 92 de integración (Azurite real: subida, URL firmada y borrado; cifrado verificado en la base; otra réplica ve el cambio de parámetro al vencer su caché).
- RF cubiertos: RF-04, RF-05 (pasajeros y conductores), RF-07, RF-26, RF-27, RF-28, RF-41; RF-38 parcial (las asignaciones se consultan desde la Fase 5).

### Fase 4 — Solicitudes e itinerarios
- [x] `SolicitudViaje` completa con `EstadoSolicitudHistorial`: se registra desde el Panel Web por el pasajero (canal PanelWeb; el bot de WhatsApp reutilizará el caso de uso en la Fase 7), se modifica sin asignar y se cancela con motivo; cada cambio queda en el historial y en la auditoría (sin direcciones).
- [x] RF-29: datos mínimos, servicio futuro, pasajero y empresa activos, y sin solicitudes activas solapadas del mismo pasajero.
- [x] RF-32: ventana del parámetro `solicitud.ventana_cancelacion_minutos` para modificar o cancelar; fuera de ella solo cancela el Operador logístico, con motivo; cancelar una asignada cancela su asignación vigente.
- [x] CQRS ligero: lectura por `IConsultasSolicitudes` con proyecciones (cola de asignación con urgentes primero, historial por pasajero, empresa, estado y fechas, detalle con historial); escritura por el agregado.
- [x] PAS-SEG-01: las solicitudes personales llegan sin direcciones a quien no puede verlas según `solicitud.visibilidad_personales` (el Administrador nunca).
- [x] RF-08: itinerarios por rango de fechas y días de la semana (uno por fecha, sin repetir código y fecha); una solicitud toma origen, destino y horario del itinerario, en la hora local del operador.
- [x] Conflictos de concurrencia optimista devueltos como 409.
- Pruebas: 329 unitarias (95,6 % líneas / 87,5 % ramas), 16 de arquitectura, 99 de integración.
- RF cubiertos: RF-08, RF-09, RF-29, RF-30, RF-32; RF-31 parcial (los viajes se agregan en la Fase 6).

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
| RF-01 | Autenticación basada en token | 2 | ✅ |
| RF-02 | Autorización basada en roles | 2 | ✅ |
| RF-03 | Registro de operador como tenant | 1 | ✅ CLI `crear-operador` |
| RF-04 | Configuración inicial del tenant | 3 | ✅ |
| RF-05 | Creación de usuarios por tenant | 2-3 | ✅ panel (Fase 2), pasajeros y conductores (Fase 3) |
| RF-06 | Asignación de roles a usuarios | 2 | ✅ roles del panel; pasajero y conductor reciben su rol al registrarse |
| RF-07 | Consulta de disponibilidad de conductores | 3 | ✅ |
| RF-08 | Registro de itinerarios | 4 | ✅ |
| RF-09 | Recepción de solicitudes de transporte | 4 | ✅ |
| RF-10 | Asignación automática de conductores | 5 | 🟡 motor listo, falta el caso de uso |
| RF-11 | Aplicación de reglas de negocio | 5 | 🟡 7 reglas implementadas |
| RF-12 | Reasignación manual | 5 | ⬜ |
| RF-13 | Priorización de solicitudes urgentes | 5 | 🟡 regla implementada |
| RF-14 | Recepción de mensajes de WhatsApp | 7 | ⬜ |
| RF-15 | Procesamiento conversacional de solicitudes | 7 | ⬜ |
| RF-16 | Confirmaciones al pasajero | 8 | ⬜ |
| RF-17 | Notificación al conductor asignado | 8 | ⬜ |
| RF-18 | Actualización de estado del viaje por WhatsApp | 7 | ⬜ |
| RF-19 | Aislamiento físico entre tenants | 1 | ✅ Spike 3 |
| RF-20 | Configuración separada por tenant | 1 | 🟡 BD y secretos por operador |
| RF-21 | Configuración avanzada de reglas | 5 | ⬜ |
| RF-22 | Cierre de sesión | 2 | ⬜ |
| RF-23 | Recuperación de credenciales | 2 | 🟡 |
| RF-24 | Actualización de información de usuarios | 2 | ⬜ |
| RF-25 | Activación e inactivación de usuarios | 2 | ⬜ |
| RF-26 | Registro de conductores | 3 | ✅ |
| RF-27 | Actualización de datos de conductores | 3 | ✅ |
| RF-28 | Activación e inactivación de conductores | 3 | ✅ |
| RF-29 | Validación de datos mínimos de solicitud | 4 | ✅ |
| RF-30 | Consulta del estado de solicitudes | 4 | ✅ |
| RF-31 | Historial de solicitudes y viajes | 4 | 🟡 solicitudes por pasajero, empresa, estado y fechas; viajes en la Fase 6 |
| RF-32 | Modificación o cancelación de solicitudes | 4 | ✅ |
| RF-33 | Visualización de asignaciones generadas | 5 | ⬜ |
| RF-34 | Actualización del estado de asignación | 5 | ⬜ |
| RF-35 | Recordatorios automáticos | 8 | ⬜ |
| RF-36 | Vista centralizada de solicitudes y viajes | 9 | ⬜ |
| RF-37 | Monitoreo de servicios en curso | 6 | ⬜ |
| RF-38 | Consulta de usuarios, conductores y asignaciones | 3 | 🟡 usuarios, conductores, pasajeros y vehículos; asignaciones en la Fase 5 |
| RF-39 | Intervención manual en la operación | 5 | ⬜ |
| RF-40 | Trazabilidad de cada viaje | 6 | ⬜ |
| RF-41 | Búsqueda y filtrado | 3 | ✅ |
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
| 2026-10-16 | El aprovisionamiento de operadores (RF-03) se dispara con una herramienta de línea de comandos (`src/FlowRide.Aprovisionamiento`), no con un endpoint: no se expone una API con permisos para crear bases y lo opera un administrador técnico. |
| 2026-10-16 | En local, el equivalente del Key Vault por operador (ADR-0014) es un almacén user-secrets por operador (`flowride-<tenant>`). El adaptador de Azure Key Vault llega con el despliegue (Fase 12). |
| 2026-10-16 | Los Silos ya no se crean en `docker-compose`: el contenedor crea solo el catálogo y la herramienta aprovisiona cada operador. Si un rol ya existía, el secreto manda y el rol toma su contraseña. |
| 2026-10-16 | Se adopta el estilo de puertos y adaptadores del proyecto de referencia del profesor (`uco-test-api`): casos de uso con interfaz genérica, puertos/adaptadores de entrada con DTO, servicios de dominio `XxxDominio` con los repositorios como puertos del dominio y excepción de negocio → 400. Se mantiene el mapeo fluido de EF Core (sin modelo de persistencia aparte ni mappers) y la organización por módulo primero (ADR-0001). |
| 2026-10-16 | `Viaje.EtaProgramada` se elimina: es un dato de ETA y ADR-0005 (RF-55/RF-56) está fuera de alcance. |
| 2026-10-17 | Fase 2 (decisiones del usuario): FlowRide crea las cuentas del Panel Web en Keycloak (Admin API); la CLI crea el realm y el primer Administrador al aprovisionar; la contraseña la define el usuario con el correo de Keycloak (Mailpit en local); la revocación se cumple consultando el estado interno en cada petición, sin caché. |
| 2026-10-17 | La API gestiona las cuentas con un cliente de servicio por realm (`flowride-api`, secreto en el almacén del operador), nunca con el administrador de Keycloak de la plataforma, que solo usa la CLI. En local, Keycloak corre en modo desarrollo con su almacenamiento en un volumen; su base propia llega con el despliegue (Fase 12). |
| 2026-10-17 | Los cuatro roles son fijos (`RolesSistema`, Id sembrados por migración). La auditoría guarda solo rol y estado como valores antes/después (sin datos personales, ADR-0013). |
| 2026-10-17 | Fase 3 (decisiones del usuario): cifrado determinista del teléfono y la licencia (permite índice único y búsqueda exacta); empresas cliente y pasajeros entran en la Fase 3; parámetros por defecto: recordatorio 60 min, ventana de cancelación 30 min, franjas críticas vacías, solicitudes personales visibles para el pasajero y el Operador logístico; documentos subidos por la API (PDF/PNG/JPG ≤ 5 MB). |
| 2026-10-17 | En Operación, un puerto de entrada por recurso (con varias operaciones) en lugar de uno por acción, para no multiplicar archivos; se mantiene la regla `XAdaptadorEntrada` → `IXPuertoEntrada`. |
| 2026-10-17 | En local, todos los buckets comparten la cuenta de desarrollo de Azurite; en Azure la credencial de cada operador debe limitarse a su contenedor (Fase 12). |
| 2026-10-17 | Fase 4 (decisiones del usuario): la ventana de cancelación aplica al pasajero y a quien registra por él, y el Operador logístico puede cancelar fuera de ella con motivo; solo se modifican solicitudes sin asignar; no se permiten solicitudes activas solapadas del mismo pasajero; los itinerarios recurrentes se registran por rango de fechas y días (uno por fecha). |
| 2026-10-17 | La hora de los itinerarios es local del operador: configuración `FlowRide:ZonaHoraria` del stack, por defecto `America/Bogota` (V1 opera en Colombia, es-CO). |
