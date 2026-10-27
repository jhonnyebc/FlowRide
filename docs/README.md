# Documentación de FlowRide

FlowRide es una plataforma SaaS de gestión de transporte corporativo interno con modelo B2B2B: se vende al operador de transporte (un tenant = un operador), que presta el servicio a un cliente empresarial. Pasajeros y conductores usan WhatsApp; el Administrador del operador y el Operador logístico usan el Panel Web.

Esta carpeta es la entrega de la documentación del proyecto (28-10-2026), toda en Markdown. Todos los diagramas están escritos en Mermaid dentro de los propios documentos: GitHub los dibuja al abrir el archivo.

**Curso:** ISS0666 — Ingeniería del Software 2, Universidad Católica de Oriente, semestre 2-2026. **Profesor:** Wider Farid Sánchez Garzón.

## Documentos de la entrega

| Documento | Contenido |
|---|---|
| [DAS.md](DAS.md) | Documento de Arquitectura de Software, versión 3.0: motivadores (restricciones, atributos de calidad con sus 33 escenarios, funcionalidades críticas), tácticas, modelo de contexto, arquetipo y arquitectura de solución, línea base implementada, estilos y patrones, justificación y vistas (componentes, paquetes, secuencias y despliegue). |
| [drivers-arquitectonicos.md](drivers-arquitectonicos.md) | Drivers arquitectónicos: atributos de calidad priorizados, priorización por stakeholder, trade-offs, escenarios generales QA-01 a QA-11 y especificación de los requisitos funcionales RF-01 a RF-56 con su filtro de significancia. |
| [restricciones-tecnicas.md](restricciones-tecnicas.md) | Las 19 restricciones técnicas con su tipo, categoría y justificación. |
| [restricciones-negocio.md](restricciones-negocio.md) | Las 15 restricciones de negocio con su justificación y plan de acción. |
| [funcionalidades-significativas.md](funcionalidades-significativas.md) | Catálogo de requisitos funcionales y los arquitectónicamente significativos, con su tipo y justificación. |
| [tradeoff-atributos-calidad.md](tradeoff-atributos-calidad.md) | Análisis de atributos de calidad: trade-offs, priorización, stakeholders, lluvia de escenarios y votación, banco de 164 escenarios, 33 escenarios seleccionados, tácticas por escenario, ADR, building blocks, PoC y spikes y catálogo de tácticas. |
| [diagramas/modelo-entidad-relacion.md](diagramas/modelo-entidad-relacion.md) | Modelo entidad-relación de las bases de datos tal como están implementadas: seis diagramas (usuarios y recursos, solicitudes, asignaciones y viajes, motor, canal y notificaciones, catálogo de plataforma) generados del esquema real de PostgreSQL. |
| [adr/](adr/README.md) | Los 20 Architecture Decision Records, con su estado, sus revisiones y lo que se implementó de cada uno. |

### Decisiones de arquitectura (ADR)

| ADR | Decisión | Estado |
|---|---|---|
| [ADR-0001](adr/adr-0001-adoptar-monolito-modular.md) | Monolito modular con límites internos explícitos | Accepted |
| [ADR-0002](adr/adr-0002-aislamiento-multitenant-silo.md) | Multi-tenancy Silo por operador | Accepted |
| [ADR-0003](adr/adr-0003-identidad-oidc-oauth2.md) | Identidad OIDC/OAuth2 con Keycloak (un realm por operador), TOTP y reautenticación reciente | Accepted |
| [ADR-0004](adr/adr-0004-postgresql-efcore-persistencia.md) | PostgreSQL con EF Core y Npgsql | Accepted |
| [ADR-0005](adr/adr-0005-telemetria-gps-postgresql-particionado.md) | Telemetría GPS con adaptador y PostgreSQL particionado | Proposed — fuera del alcance de V1 |
| [ADR-0006](adr/adr-0006-cache-aside-datos-baja-volatilidad.md) | Cache-Aside local para datos de baja volatilidad | Accepted |
| [ADR-0007](adr/adr-0007-consistencia-concurrencia-outbox.md) | Concurrencia optimista y Transactional Outbox | Accepted |
| [ADR-0008](adr/adr-0008-whatsapp-sandbox-meta-cloud-api.md) | WhatsApp con el número de prueba de Meta, evolución a número propio | Accepted |
| [ADR-0009](adr/adr-0009-motor-asignacion-reglas-configurables.md) | Motor de asignación con reglas configurables y composición determinista | Accepted |
| [ADR-0010](adr/adr-0010-despliegue-contenedores-por-operador.md) | Stack contenedorizado por operador y aprovisionamiento por línea de comandos | Accepted |
| [ADR-0011](adr/adr-0011-observabilidad-auditoria-opentelemetry.md) | OpenTelemetry y auditoría persistente por tenant | Accepted |
| [ADR-0012](adr/adr-0012-borde-api-gateway-waf.md) | Borde común: API Gateway (YARP), WAF y router de webhooks | Accepted |
| [ADR-0013](adr/adr-0013-almacenamiento-objetos.md) | Almacenamiento de objetos por operador | Accepted |
| [ADR-0014](adr/adr-0014-gestion-secretos.md) | Secretos con un ámbito por operador | Accepted |
| [ADR-0015](adr/adr-0015-notification-gateway.md) | Notification Gateway | Accepted |
| [ADR-0016](adr/adr-0016-catalogos-mensajes-parametros.md) | Catálogo de mensajes y de parámetros | Accepted |
| [ADR-0017](adr/adr-0017-dotnet-lenguaje-runtime-backend.md) | C# sobre .NET 10 | Accepted |
| [ADR-0018](adr/adr-0018-flujo-git-quality-gates.md) | Flujo Git, quality gates y escaneo de secretos | Accepted |
| [ADR-0019](adr/adr-0019-panel-web-react-typescript.md) | Panel Web en React + TypeScript | Accepted |
| [ADR-0020](adr/adr-0020-fechas-hora-local-zona-horaria.md) | Fechas: instantes pasados en UTC, eventos futuros en hora local con su zona | Accepted |

## Cómo leer la documentación

1. **Qué condiciona la arquitectura:** sección 2 del [DAS](DAS.md) o, con más detalle, los [drivers](drivers-arquitectonicos.md), las [restricciones técnicas](restricciones-tecnicas.md) y [de negocio](restricciones-negocio.md) y el [trade-off](tradeoff-atributos-calidad.md).
2. **Cómo se respondió:** secciones 3 a 8 del DAS (tácticas, contexto, arquetipo, solución, línea base y justificación) y los [ADR](adr/README.md).
3. **Cómo es el sistema:** sección 9 del DAS, con los diagramas de componentes, paquetes, secuencias de los flujos críticos y despliegue, y el [modelo entidad-relación](diagramas/modelo-entidad-relacion.md) de las bases de datos.

## Alcance y estado

- **Alcance de V1:** todos los requisitos funcionales salvo RF-55 (ingesta de telemetría GPS) y RF-56 (detección de desviación de ETA), que quedaron fuera del alcance (ADR-0005 en estado *Proposed*). Los documentos los conservan por trazabilidad y lo indican.
- **Implementación al 24-10-2026:** el plan de implementación ([ROADMAP-V1.md](ROADMAP-V1.md)) tiene cerradas las fases 0 a 10 y las entregas 11a (operación) y 11b-1 (administración de usuarios, empresas y pasajeros) del Panel Web; siguen 11b-2, 11c y la Fase 12 (operación y demostración con HTTPS local).
- **Pruebas:** 553 unitarias, 17 de arquitectura, 145 de integración (con PostgreSQL, Keycloak, Mailpit y Azurite reales) y 50 del Panel Web con verificación de accesibilidad.
- **Secciones de la plantilla marcadas «(NO)»** (propósito del proyecto, vista funcional, incepción ágil, diagramas de clases, de objetos y de colaboración): aparecen en el DAS con la nota de que no aplican en esta entrega.

## Otros archivos de esta carpeta

| Archivo | Para qué sirve |
|---|---|
| [ROADMAP-V1.md](ROADMAP-V1.md) | Plan de implementación por fases y estado de cada requisito funcional. |
| [GUIA-WHATSAPP-META.md](GUIA-WHATSAPP-META.md) | Cómo conectar el número de prueba de Meta y el webhook en local. |
| [DESARROLLO.md](DESARROLLO.md) | Guía técnica para desarrollar: arquitectura, convenciones, comandos y entorno local. |
