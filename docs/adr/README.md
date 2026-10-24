# ADRs — FlowRide

Este directorio contiene los Architecture Decision Records vigentes de FlowRide. Fuente original: carpeta del equipo en OneDrive (`Entregables/doc/adr`). Las revisiones posteriores (2026-10-13, 2026-10-16, 2026-10-20, 2026-10-21 y 2026-10-22) están marcadas dentro de cada ADR, y cada uno tiene en sus notas de implementación lo que se construyó (estado al 22-10-2026).

| ADR | Decisión | Estado |
|---|---|---|
| ADR-0001 | Monolito modular con límites internos explícitos | Accepted |
| ADR-0002 | Multi-tenancy Silo por operador | Accepted |
| ADR-0003 | Identidad OIDC/OAuth2 — Keycloak con un realm por operador; TOTP del Administrador y reautenticación reciente | Accepted (rev. 2026-10-13, 2026-10-20 y 2026-10-21) |
| ADR-0004 | PostgreSQL con EF Core y Npgsql | Accepted (rev. 2026-10-13) |
| ADR-0005 | Telemetría GPS con adapter + PostgreSQL particionado | Proposed — **fuera del alcance del proyecto** (RF-55, RF-56) |
| ADR-0006 | Cache-Aside local para baja volatilidad | Accepted |
| ADR-0007 | Consistencia con concurrencia optimista + Outbox | Accepted |
| ADR-0008 | WhatsApp: número de prueba de Meta (curso) → Meta Cloud API (producción) | Accepted (rev. 2026-10-13) |
| ADR-0009 | Motor de asignación con reglas configurables | Accepted |
| ADR-0010 | Stack contenedorizado por operador, imágenes inmutables; aprovisionamiento por línea de comandos | Accepted (rev. 2026-10-16) |
| ADR-0011 | OpenTelemetry + auditoría persistente por tenant | Accepted |
| ADR-0012 | Borde común: API Gateway (YARP), WAF y router de webhooks | Accepted (rev. 2026-10-22) |
| ADR-0013 | Almacenamiento de objetos por operador | Accepted |
| ADR-0014 | Secretos con un ámbito por operador | Accepted |
| ADR-0015 | Notification Gateway | Accepted |
| ADR-0016 | Catálogo de mensajes y de parámetros | Accepted |
| ADR-0017 | C# sobre .NET 10 | Accepted |
| ADR-0018 | Flujo Git y quality gates; escaneo de secretos antes del push | Accepted (2026-10-13, rev. 2026-10-20) |
| ADR-0019 | Panel Web en React + TypeScript | Accepted (2026-10-13) |
| ADR-0020 | Fechas: instantes pasados en UTC y eventos futuros como hora local con su zona horaria | Accepted (2026-10-20) |

Todos los ADR usan la misma estructura de referencia: Metadatos, Contexto, Decisión, Alternativas, Consecuencias, Tácticas de Bass, Escenarios y requisitos relacionados, ADR relacionados, Notas de implementación y Referencias.

## Inconsistencias corregidas el 2026-10-22
- ADR-0012 citaba un ingreso de 99,9 % para el canal WhatsApp; ahora remite al tier de 99,5 % que fijó ADR-0010.
- ADR-0010 mencionaba "ADR-0002 §6", una sección que no existe; ahora explica por qué compartir el servidor PostgreSQL es compatible con ADR-0002 (cada operador conserva su base y sus credenciales), y ADR-0002 lo documenta en sus notas de implementación.
- ADR-0014, ADR-0015, ADR-0016, ADR-0018 y ADR-0019 no tenían todas las secciones de la estructura de referencia; se completaron.
