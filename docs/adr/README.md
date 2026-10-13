# ADRs — FlowRide

Este directorio contiene los Architecture Decision Records vigentes de FlowRide. Fuente original: carpeta del equipo en OneDrive (`Entregables/doc/adr`). Las revisiones del 2026-10-13 están marcadas dentro de cada ADR.

| ADR | Decisión | Estado |
|---|---|---|
| ADR-0001 | Monolito modular con límites internos explícitos | Accepted |
| ADR-0002 | Multi-tenancy Silo por operador | Accepted |
| ADR-0003 | Identidad OIDC/OAuth2 — Keycloak con un realm por operador | Accepted (rev. 2026-10-13) |
| ADR-0004 | PostgreSQL con EF Core y Npgsql | Accepted (rev. 2026-10-13) |
| ADR-0005 | Telemetría GPS con adapter + PostgreSQL particionado | Proposed — **fuera del alcance del proyecto** (RF-55, RF-56) |
| ADR-0006 | Cache-Aside local para baja volatilidad | Accepted |
| ADR-0007 | Consistencia con concurrencia optimista + Outbox | Accepted |
| ADR-0008 | WhatsApp: número de prueba de Meta (curso) → Meta Cloud API (producción) | Accepted (rev. 2026-10-13) |
| ADR-0009 | Motor de asignación con reglas configurables | Accepted |
| ADR-0010 | Stack contenedorizado por operador, imágenes inmutables | Accepted |
| ADR-0011 | OpenTelemetry + auditoría persistente por tenant | Accepted |
| ADR-0012 | Borde común: API Gateway (YARP), WAF y router de webhooks | Accepted |
| ADR-0013 | Almacenamiento de objetos por operador | Accepted |
| ADR-0014 | Secretos con un ámbito por operador | Accepted |
| ADR-0015 | Notification Gateway | Accepted |
| ADR-0016 | Catálogo de mensajes y de parámetros | Accepted |
| ADR-0017 | C# sobre .NET 10 | Accepted |
| ADR-0018 | Flujo Git y quality gates | Accepted (2026-10-13) |
| ADR-0019 | Panel Web en React + TypeScript | Accepted (2026-10-13) |

Todos los ADR usan la misma estructura de referencia: Metadatos, Contexto, Decisión, Alternativas, Consecuencias, Tácticas de Bass, Escenarios y requisitos relacionados, ADR relacionados, Notas de implementación y Referencias.

## Inconsistencias conocidas (solo de documentación)
- ADR-0012 cita un ingreso de 99,9 % para el canal WhatsApp; ADR-0010 lo revisó a 99,5 %. Vale ADR-0010.
- ADR-0010 menciona "ADR-0002 §6 (instancia compartida/dedicada)", pero esa sección no existe en ADR-0002.
