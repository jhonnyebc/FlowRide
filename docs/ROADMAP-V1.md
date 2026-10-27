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
| 4 | Solicitudes e itinerarios | Solicitudes | 0001, 0004, 0007 | ✅ PR #7 fusionada |
| 5 | Asignación de punta a punta | Asignacion | 0007, 0009, 0016 | ✅ PR #8 fusionada |
| 6 | Viajes y calificación | Viajes, Calificacion | 0007, 0011 | ✅ PR #9 fusionada |
| 7 | Canal WhatsApp | Mensajeria | 0003, 0008, 0012 | ✅ PR #10 fusionada |
| 8 | Notificaciones y alertas | Notificaciones | 0007, 0015, 0016 | ✅ PR #11 fusionada |
| 9 | Reportes y vista operacional | Reportes | 0006, 0013 | ✅ PR #12 fusionada |
| 10 | Borde común | Plataforma | 0012, 0014 | ✅ PR #13 fusionada |
| 11 | Panel Web | Web | 0003, 0015, 0016, 0019 | 🟡 11a (PR #16), 11b-1 (PR #17) y 11b-2 cerradas; 11c pendiente |
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
- [x] Caso de uso `AsignarSolicitudCasoUso` (el `AssignPendingRequestHandler` del plan): una transacción con decision log, asignación (o sin candidatos) y evento en la Outbox `notif.outbox_mensaje` (ADR-0007); concurrencia optimista y exclusiones de franjas diferibles; los conflictos salen como `ConflictoConcurrenciaExcepcion` (409 en la API).
- [x] Disparo: worker de la cola cada 15 s (urgentes primero, RF-13) y `POST /api/solicitudes/{id}/asignar`. Candidatos: vehículo habitual del conductor o, si no tiene, cada vehículo activo libre; la ocupación la descarta la regla de disponibilidad con su motivo.
- [x] Brechas del motor frente al ADR-0009:
  - el validador detecta reglas obligatorias deshabilitadas, precedencias (orden > 0 y único por tipo), dependencias deshabilitadas y ciclos;
  - el decision log tiene `EvaluatedAt` (del reloj del caso de uso) y la versión de cada regla (`RuleVersion`);
  - "compatible con servicio crítico" es una marca del conductor (`AptoServicioCritico`) que usa `CriticalServicePriorityRule` v2.
- [x] Configuración del motor versionada (RF-21): borrador → activa → archivada, validada antes de crearla y de activarla; rollback = activar una versión anterior; v1 sembrada por migración. Decision log persistente (`motor.decision_asignacion`, `evaluacion_candidato`, `resultado_regla`, solo INSERT) y consultable.
- [x] RF-12 (Operador logístico): reasignar, asignar a mano una sin candidatos o devolverla a la cola, con motivo y auditoría; el candidato manual pasa por el motor. Abandonar un turno cancela sus asignaciones no iniciadas y devuelve las solicitudes a la cola.
- [x] Spike 2: dos réplicas del worker compitiendo por 12 solicitudes y 3 conductores en la misma franja → 0 asignaciones solapadas (5 corridas seguidas).
- Pruebas: 361 unitarias (94,4 % líneas / 87,5 % ramas), 16 de arquitectura, 107 de integración.
- RF cubiertos: RF-10, RF-11, RF-12, RF-13, RF-21, RF-38 (asignaciones).

### Fase 6 — Viajes y calificación
- [x] Aceptar la asignación crea el viaje Programado; iniciar y finalizar mueven viaje, asignación y solicitud (EnCurso → Completada). Paradas (con llegada) e incidentes en el agregado `Viaje`; los incidentes salen a la Outbox para las alertas de la Fase 8.
- [x] Rechazo: la solicitud vuelve a la cola y el motor no se la propone otra vez a ese conductor (`AvailabilityRule` v3). Sin respuesta dentro de `asignacion.plazo_respuesta_minutos` (15) = rechazo del sistema, revisado por el worker de la cola.
- [x] Cancelar la solicitud, reasignar o abandonar el turno cancela también el viaje programado.
- [x] Calificación (RF-54): una por viaje y pasajero, de 1 a 5, dentro de `calificacion.plazo_dias` (7).
- [x] Consultas: historial de viajes (RF-31), monitoreo de viajes en curso y próximos con retrasados marcados (RF-37) y trazabilidad del servicio de punta a punta (RF-40).
- [x] Hasta la Fase 7, las acciones del conductor y del pasajero las ejecuta el Operador logístico desde la API del panel, con auditoría.
- Pruebas: 400 unitarias (94,7 % líneas / 86,8 % ramas), 16 de arquitectura, 111 de integración; Spike 2 en verde 5 veces seguidas.
- RF cubiertos: RF-31 (completo), RF-33, RF-34, RF-37, RF-39, RF-40, RF-54. Sin migración: las tablas `viaje`, `parada`, `incidente` y `calificacion` ya existían.

### Fase 7 — Canal WhatsApp
- [x] `IWhatsAppGateway` con `WhatsAppSandboxAdapter` (número de prueba de Meta Cloud API, Graph API) y `WhatsAppSimuladoAdapter` (por defecto); ambos leen el webhook con el formato y la firma de Meta. Guía para probar con el celular: `docs/GUIA-WHATSAPP-META.md` (Meta + ngrok, gratis).
- [x] Webhook con verificación de suscripción y de firma `X-Hub-Signature-256`; guarda el mensaje (bandeja de entrada, único por id externo) y responde de inmediato; ignora los eventos de otro número.
- [x] Worker del canal (cada 2 s): procesa en orden de llegada y envía la bandeja de salida con reintentos y espera creciente; errores transitorios vs. permanentes; correlación del id externo y del estado de entrega (sent, delivered, read, failed).
- [x] Máquina de estados conversacional (menú numerado y pasos guiados): el pasajero pide transporte, consulta, cancela y califica; el conductor acepta o rechaza, inicia y finaliza viajes y reporta novedades. Identidad por el teléfono registrado (solo pasajeros y conductores activos).
- [x] PoC 4: un mensaje repetido no se procesa dos veces (prueba de integración con el webhook firmado).
- Pruebas: 456 unitarias (91,9 % líneas / 82,6 % ramas), 16 de arquitectura, 126 de integración. Migración `CanalWhatsApp` (esquema `canal`). Probado con WhatsApp real (número de prueba de Meta + ngrok): el bot respondió desde el celular.
- RF cubiertos: RF-14, RF-15, RF-18; RF-16 parcial (el bot confirma al instante; las notificaciones por cambios llegan en la Fase 8).

### Fase 8 — Notificaciones y alertas
- [x] `GatewayNotificaciones` (el `INotificationGateway` del ADR-0015): pasajero y conductor por WhatsApp (texto en la ventana de 24 h; fuera de ella, la plantilla aprobada o alerta), alertas del panel guardadas y en vivo por SSE, y correo a los administradores para las críticas (`IEnviadorCorreo` por SMTP; Mailpit en local, puerto 1025).
- [x] `WorkerNotificaciones`: Outbox con `FOR UPDATE SKIP LOCKED` y marcado en la misma transacción que las notificaciones (deduplicación por evento; dos réplicas no duplican); reintentos con espera creciente. Recordatorios como eventos futuros (`recordatorio.servicio`, N min antes según `recordatorio.anticipacion_minutos`).
- [x] Matriz de avisos (decisión del 2026-10-18): conductor (asignación nueva, retirada, cancelada); pasajero (conductor asignado, cambio de conductor, inicio, fin, retraso, cancelación por el operador); panel (sin candidatos, urgente sin candidatos, incidente, turno abandonado, asignación vencida, WhatsApp fallido).
- [x] Catálogo `es-CO` (`mensajes.es-CO.json`, recurso embebido) y registro de plantillas por operador (`notif.plantilla_whatsapp`, sembrado Pendiente; el Administrador registra la aprobada).
- Pruebas: 480 unitarias (91,9 % líneas / 81,8 % ramas), 16 de arquitectura, 130 de integración. Migración `Notificaciones`.
- RF cubiertos: RF-16, RF-17, RF-35, RF-43, RF-44, RF-45.

### Fase 9 — Reportes y vista operacional
- [x] Vista centralizada del día (RF-36): solicitudes con su asignación y viaje, semaforizadas (crítico: sin candidatos, retrasado o sin conductor confirmado a 30 min; atención: espera asignación o respuesta, urgente o con incidentes).
- [x] Indicadores básicos (RF-42): volumen y estados, cumplimiento (puntualidad con tolerancia configurable, retraso promedio, incidentes), conductores (aceptación, rechazo, vencidas, calificación, los 5 con más viajes) y por cliente empresarial. Cache-aside (ADR-0006) solo para periodos cerrados.
- [x] Reportes RF-46 (viajes realizados), RF-47 (atendidas, pendientes y canceladas), RF-48 (conductores asignados) y RF-49 (histórico por cliente empresarial), con resumen y filas paginadas; periodo de hasta 93 días.
- [x] Exporte CSV síncrono al bucket del operador (`exports/`, ADR-0013) con enlace firmado de 15 min y auditoría. El borrado a los 7 días es la política de ciclo de vida del bucket (Fase 12, en Azure).
- Pruebas: 516 unitarias (93,9 % líneas / 83,0 % ramas), 16 de arquitectura, 133 de integración. Sin migración (solo un parámetro nuevo con valor por defecto).
- RF cubiertos: RF-36, RF-42, RF-46, RF-47, RF-48, RF-49.

### Fase 10 — Borde común
- [x] Proyecto `FlowRide.Borde` con YARP: una ruta por operador activo, `{subdominio}.{dominio}` → su stack, recargada del catálogo cada 30 s sin reiniciar. Tabla `plataforma.ruta_operador` (migración `RutasBorde` del catálogo) y comandos `registrar-ruta` y `listar-rutas` de la CLI.
- [x] Router de webhooks: handshake, validación `X-Hub-Signature-256` con la app de Meta de la plataforma, destino por `phone_number_id` y reenvío del cuerpo intacto (el stack vuelve a validar). Stack caído → 502 para que Meta reintente; métricas de rechazos por firma y de números sin operador.
- [x] Límites de tasa por IP y por ruta, tamaño máximo de petición, métodos permitidos y cabeceras de seguridad (sin `Server`). HTTPS local en `https://localhost:8443` y `https://<operador>.dev.localhost:8443`.
- [ ] WAF con OWASP CRS: gestionado de Azure (Front Door o Application Gateway), en la Fase 12 (decisión del 2026-10-19).
- Pruebas: 532 unitarias (94,1 % líneas / 83,2 % ramas), 17 de arquitectura, 139 de integración (el borde reenvía a la API real del stack).
- RF relacionados: RF-14, RF-19 (escenarios OPLOG-SEG-02, ADM-SEG-02, COND-DISP-01).

### Requisitos del profesor (videos del 2026-10-20), antes de la Fase 11
Fechas y horas (PR #14 fusionada):
- [x] Corregido: el motor y la disponibilidad sacaban el día del servicio de la hora UTC (un servicio a las 8 p. m. en Bogotá contaba como del día siguiente y descartaba licencias o SOAT que vencían ese día).
- [x] Eventos futuros en hora local + nombre IANA de la zona (`FechaHoraLocal`): servicio de la solicitud, turnos, hora estimada de paradas e itinerarios. Instante UTC derivado y recalculado al arrancar (`RecalculoHorariosAlIniciar`). Migración `FechasConZona` con relleno desde los instantes existentes (America/Bogota).
- [x] API: hora local sin desfase + `zonaHoraria` opcional (por defecto la del operador); desfase o `Z` → 400. Las respuestas devuelven el instante y la hora local con su zona.
- Pruebas: 547 unitarias (94,1 % / 83,2 %), 17 de arquitectura, 140 de integración.

Seguridad (OWASP: secretos, CSRF, autenticación rota; reverse proxy vs API gateway), PR #15 fusionada:
- [x] Escaneo de secretos con gitleaks antes de cada push (hook versionado `.githooks/pre-push`, gitleaks local o Docker), además del CI. Probado: bloquea un token falso.
- [x] Keycloak: contraseña de 12+ (mayúscula, minúscula, número, historial de 5, argon2), bloqueo tras 5 intentos fallidos y TOTP obligatorio para el Administrador (al crearse o al pasar a ese rol).
- [x] Reautenticación reciente (≤ 5 min, claim `auth_time`, RFC 9470) en crear, activar, desactivar y cambiar rol de usuarios, parámetros, plantillas y configuración del motor: 401 `insufficient_user_authentication` con `max_age`.
- [x] El borde valida el token del realm del operador de ese subdominio antes de reenviar a `/api/` (parte AUTH del API gateway); el stack vuelve a validar.
- Pruebas: 548 unitarias (94,1 % / 83,2 %), 17 de arquitectura, 142 de integración (Keycloak real con inicio de sesión tipo navegador para `auth_time`).
- Notas: la API usa solo `Authorization: Bearer` (sin cookies), así que CSRF no aplica a la API; el Panel Web (Fase 11) no guardará tokens en cookies ni secretos en variables `VITE_*`.

### Fase 11 — Panel Web
Decisiones del usuario (2026-10-20): Mantine, tres entregas, pruebas unitarias con axe, tema claro y oscuro.

11a — base + Operación (PR #16, cerrada el 2026-10-21):
- [x] Contrato OpenAPI tipado: `TypedResults`, enums como texto, números estrictos y `ContratoOpenApiTests` (el contrato del frontend no se desincroniza).
- [x] SPA en `web/` (Vite + React + TypeScript + Mantine): OIDC código + PKCE con tokens en memoria, reautenticación con `max_age=0` ante el desafío de RFC 9470, cliente generado con `openapi-fetch`, alertas en vivo por SSE con `fetch`.
- [x] Pantallas: vista del día con semáforo (RF-36), solicitudes (lista, nueva, detalle con trazabilidad, asignar ya, cancelar, devolver a la cola), viajes en curso y su detalle (iniciar, finalizar, incidentes) y alertas (RF-45).
- [x] Accesibilidad WCAG 2.1 AA verificada con axe en cada pantalla; job "Frontend" en el CI.
- [x] Rediseño tras la prueba local (2026-10-21): tablero con indicadores y gráficas (`@mantine/charts`), marco con contadores, detalles completos (conductor asignado, decisiones del motor, etapas, puntualidad, calificación) y "aceptar o rechazar en nombre del conductor" para el Operador logístico.
- Pruebas: 41 del frontend; backend 548 unitarias, 17 de arquitectura, 143 de integración.

11b — administración (decisiones del usuario, 2026-10-21: lista con panel lateral para crear o editar; el Operador logístico
consulta y programa turnos, Usuarios es solo del Administrador; dos entregas):
- [x] 11b-1 (PR #17, cerrada el 2026-10-21): RF-24 en el backend (`PUT /api/usuarios/{id}`: nombre, correo y
  WhatsApp; el correo nuevo se sincroniza con Keycloak y queda pendiente de confirmar) y pantallas de usuarios, empresas
  cliente y pasajeros. Pruebas: 553 unitarias, 17 de arquitectura, 50 del frontend.
- [x] 11b-2 (2026-10-24, solo panel; el backend ya existía): conductores con detalle de turnos (programar en hora local;
  el abandono lo registra solo el Operador logístico, como exige la API) y documentos (cargar, descargar con enlace
  temporal y eliminar; solo el Administrador carga y elimina), vehículos (vencimientos de RTM y SOAT, activar y
  desactivar, conductor habitual) e itinerarios (registro por rango de fechas y días, y consulta; sin editar ni
  eliminar, decisión del usuario). Las tres pantallas van en Administración. Pruebas: 59 del frontend.

11c — configuración y reportes:
- [ ] Parámetros, plantillas de WhatsApp, configuración del motor, indicadores y reportes con exporte (RF-42, RF-46 a RF-49).

### Entrega de documentación (2026-10-28)
- [x] Documentación del proyecto en Markdown en `docs/` (índice en `docs/README.md`): DAS v3.0 con diagramas en Mermaid, drivers, restricciones, funcionalidades significativas, trade-off y los 20 ADR con sus notas de implementación (ADR-0020 nuevo: fechas con zona horaria).

### Fase 12 — Operación
- [ ] Dockerfile con versión fija, imagen semver+SHA, dos stacks locales (uno por operador).
- [ ] Demostración con HTTPS local (decisión del usuario, 2026-10-22: basta con local, sin publicar en internet): el borde sirve el Panel Web compilado en `https://tenant-a.localhost:8443`; Keycloak con HTTPS (certificado de desarrollo o detrás del borde); direcciones de regreso del realm y CSP del borde ajustadas a esos orígenes; certificado de desarrollo confiado (`dotnet dev-certs https --trust`). Para WhatsApp sigue haciendo falta un túnel (ngrok) en una red que no lo bloquee.
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
| RF-10 | Asignación automática de conductores | 5 | ✅ |
| RF-11 | Aplicación de reglas de negocio | 5 | ✅ |
| RF-12 | Reasignación manual | 5 | ✅ |
| RF-13 | Priorización de solicitudes urgentes | 5 | ✅ |
| RF-14 | Recepción de mensajes de WhatsApp | 7 | ✅ |
| RF-15 | Procesamiento conversacional de solicitudes | 7 | ✅ |
| RF-16 | Confirmaciones al pasajero | 7-8 | ✅ confirmación del bot y avisos de conductor asignado, inicio y fin |
| RF-17 | Notificación al conductor asignado | 8 | ✅ |
| RF-18 | Actualización de estado del viaje por WhatsApp | 7 | ✅ |
| RF-19 | Aislamiento físico entre tenants | 1 | ✅ Spike 3 |
| RF-20 | Configuración separada por tenant | 1, 3, 5 | ✅ base, secretos, realm, objetos, parámetros y configuración del motor por operador |
| RF-21 | Configuración avanzada de reglas | 5 | ✅ |
| RF-22 | Cierre de sesión | 11a | ✅ «Cerrar sesión» del Panel Web (cierra la sesión en Keycloak) |
| RF-23 | Recuperación de credenciales | 2, 11a | ✅ «¿Olvidaste tu contraseña?» de Keycloak con correo (probado en local con Mailpit, 2026-10-21) |
| RF-24 | Actualización de información de usuarios | 2, 11b | ✅ API y panel (11b-1) |
| RF-25 | Activación e inactivación de usuarios | 2, 11b | ✅ API con revocación en Keycloak (Fase 2) y panel (11b-1) |
| RF-26 | Registro de conductores | 3 | ✅ |
| RF-27 | Actualización de datos de conductores | 3 | ✅ |
| RF-28 | Activación e inactivación de conductores | 3 | ✅ |
| RF-29 | Validación de datos mínimos de solicitud | 4 | ✅ |
| RF-30 | Consulta del estado de solicitudes | 4 | ✅ |
| RF-31 | Historial de solicitudes y viajes | 4-6 | ✅ solicitudes (Fase 4) y viajes (Fase 6) |
| RF-32 | Modificación o cancelación de solicitudes | 4 | ✅ |
| RF-33 | Visualización de asignaciones generadas | 5 | ✅ |
| RF-34 | Actualización del estado de asignación | 5-6 | ✅ aceptar, rechazar y vencimiento |
| RF-35 | Recordatorios automáticos | 8 | ✅ |
| RF-36 | Vista centralizada de solicitudes y viajes | 9 | ✅ |
| RF-37 | Monitoreo de servicios en curso | 6 | ✅ sin GPS (ADR-0005 fuera de alcance) |
| RF-38 | Consulta de usuarios, conductores y asignaciones | 3-5 | ✅ |
| RF-39 | Intervención manual en la operación | 5 | ✅ con RF-12 |
| RF-40 | Trazabilidad de cada viaje | 6 | ✅ |
| RF-41 | Búsqueda y filtrado | 3 | ✅ |
| RF-42 | Indicadores básicos | 9 | ✅ |
| RF-43 | Notificación de cambios de estado | 8 | ✅ |
| RF-44 | Notificación de reasignación o novedades | 8 | ✅ |
| RF-45 | Alertas operativas | 8 | ✅ panel (SSE) y correo para las críticas |
| RF-46 | Reporte de viajes realizados | 9 | ✅ |
| RF-47 | Reporte de solicitudes atendidas y pendientes | 9 | ✅ |
| RF-48 | Reporte de conductores asignados | 9 | ✅ |
| RF-49 | Reporte histórico por cliente empresarial | 9 | ✅ |
| RF-50 | Registro de eventos del sistema | 0 | 🟡 log estructurado por solicitud |
| RF-51 | Registro de errores y excepciones | 0 | 🟡 ProblemDetails + log con traceId |
| RF-52 | Auditoría de acciones administrativas | 2 | ✅ auditoría encadenada por hash de las acciones sensibles, verificable con `/api/auditoria/verificacion` |
| RF-53 | Monitoreo técnico de disponibilidad | 0 | 🟡 health checks + métricas OTel |
| RF-54 | Calificación del servicio | 6 | ✅ |
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
| 2026-10-18 | Fase 5 (decisiones del usuario): worker de la cola cada 15 s más "asignar ya"; candidatos con el vehículo habitual o, si no tiene, cada vehículo activo libre; "compatible con servicio crítico" es una marca del conductor; RF-12 completa (reasignar, asignar a mano, devolver a la cola y abandono de turno). |
| 2026-10-18 | La configuración del motor vive en las tablas `motor.*` del modelo ER (no en `ops.parametro`), con la v1 sembrada por migración; la ventana del balance de carga (7 días) es un parámetro de la regla. Las exclusiones de franjas pasan a diferibles para poder reemplazar una asignación en una transacción. |
| 2026-10-18 | Fase 6 (decisiones del usuario): hasta el canal WhatsApp, el Operador logístico ejecuta desde el panel las acciones del conductor y del pasajero; un rechazo devuelve la solicitud a la cola sin volver a proponer a ese conductor; una asignación sin respuesta vence (parámetro, 15 min) y cuenta como rechazo; la calificación tiene plazo configurable (7 días). |
| 2026-10-18 | El viaje nace al aceptar la asignación (estado Programado) para poder registrar paradas y novedades antes de empezar; el candidato manual del Operador logístico no se descarta por un rechazo previo; el monitoreo muestra los programados de las próximas 2 horas. |
| 2026-10-18 | Fase 7 (decisiones del usuario): el webhook guarda en una bandeja de entrada y un worker procesa y responde con reintentos; bot con menú numerado y pasos guiados; los cuatro flujos (pasajero: pedir, consultar, cancelar y calificar; conductor: responder asignaciones, iniciar/finalizar y novedades). Para probar con el celular: número de prueba de Meta + ngrok (dominio estático gratis), con el simulado por defecto. |
| 2026-10-18 | Una solicitud pedida por WhatsApp dura 60 min por defecto (el pasajero no la conoce); el número del remitente se normaliza a E.164 (`+57…`) para encontrar al usuario; la conversación vuelve al menú tras 30 min sin mensajes; los teléfonos del canal se cifran como los de `ops.usuario`. |
| 2026-10-18 | Los teléfonos de WhatsApp se validan y normalizan a E.164 (`+57…`, sin espacios ni guiones) al crear o editar usuarios, pasajeros y conductores (también el primer administrador de la CLI): son la identidad en el canal. Se detectó al probar con Meta, porque la API aceptaba cualquier texto. |
| 2026-10-18 | Fase 8 (decisiones del usuario): todos los avisos propuestos (conductor, pasajero, panel y recordatorios); por correo van las alertas de solicitud urgente sin candidatos, accidente o falla mecánica, turno abandonado y WhatsApp fallido; fuera de la ventana de 24 h, plantilla aprobada o error con alerta (ADR-0016); alertas guardadas y en vivo por SSE. |
| 2026-10-18 | Se agregan los eventos `solicitud.cancelada` y `asignacion.cancelada` al cancelar una solicitud, y `canal.envio_fallido` cuando un WhatsApp no se entrega; el envío en vivo lee de la base cada 2 s (funciona con varias réplicas); Mailpit publica el SMTP 1025 en `docker-compose`. |
| 2026-10-19 | Fase 9 (decisiones del usuario): exportes en CSV, generados en la misma petición (síncronos) con periodo máximo de 93 días; reportes e indicadores para el Administrador y el Operador logístico (las personales siguen sin direcciones según el parámetro); indicadores de volumen, cumplimiento, conductores y por cliente empresarial. |
| 2026-10-19 | La puntualidad usa una tolerancia configurable (`reporte.tolerancia_puntualidad_minutos`, 10 min); una asignación vencida cuenta aparte del rechazo; solo los indicadores de periodos cerrados se cachean (TTL ≤ 10 min), lo demás va siempre a PostgreSQL. |
| 2026-10-19 | Fase 10 (decisiones del usuario): la tabla de ruteo vive en el catálogo de plataforma y la llena la CLI; el borde y el stack validan la firma de Meta (defensa en profundidad); el WAF con OWASP CRS es el gestionado de Azure en la Fase 12; en local, HTTPS con `*.dev.localhost` (lo cubre el certificado de desarrollo de .NET 10). |
| 2026-10-19 | Con una sola app de Meta, el `AppSecret` del stack de cada operador es el mismo de la app de la plataforma. El borde reenvía el cuerpo intacto a cada operador que aparece en el webhook (el stack ya ignora lo que no es de su número); un número sin operador se responde 200 para que Meta no reintente sin fin. |
| 2026-10-20 | Requisitos del profesor (videos): lo que ya pasó se guarda en UTC; lo que va a pasar en un lugar, en hora local + nombre IANA de la zona, con el instante UTC derivado y recalculado al arrancar. Decisiones del usuario: refactor completo de fechas, TOTP obligatorio para el Administrador, reautenticación reciente en acciones sensibles y validación del token también en el borde. |
| 2026-10-20 | El borde responde 401 sin llegar al stack si a `/api/` no llega un token del realm de ese operador; sin claves del realm, 503. La reautenticación usa el desafío estándar de RFC 9470 para que el panel repita el inicio de sesión con `max_age`. El hook de gitleaks bloquea el push si no hay gitleaks ni Docker. |
