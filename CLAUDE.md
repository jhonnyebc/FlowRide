# FlowRide

Proyecto académico (Ingeniería del Software 2). Objetivo: construir la **V1 completa** por fases (ver `docs/ROADMAP-V1.md`). RF-10 (motor de asignación) ya está implementado.

## Producto
- SaaS B2B2B de transporte corporativo programado por turnos; **no es ride-hailing**.
- Multi-tenant, modelo **Silo**: una base de datos por operador (ADR-0002).
- Pasajeros y conductores usan **solo WhatsApp**. El Administrador del operador y el Operador logístico usan el **Panel Web**.

## Decisiones de arquitectura
Los ADR están en `docs/adr` (copia de los originales del equipo, con las revisiones del 2026-10-13 marcadas dentro de cada uno). Léelos antes de cada fase que los toque. Si una decisión no está en los ADR, pregunta antes de elegir.

## Stack
- Backend: C# / .NET 10, ASP.NET Core, EF Core + Npgsql, PostgreSQL 17 (docker-compose). Tests: xUnit.
- Identidad: Keycloak, un realm por operador (ADR-0003).
- WhatsApp: número de prueba de Meta Cloud API detrás de `IWhatsAppGateway` (ADR-0008).
- Panel Web: React + TypeScript en `web/` (ADR-0019, Proposed).
- IDE: JetBrains Rider.

## Arquitectura
Monolito modular por capas; las dependencias apuntan hacia Domain:
- `FlowRide.Domain`: entidades, reglas de dominio y motor de asignación (`Assignment/`, lógica pura). **No depende de nada externo** (ni EF Core, ni ASP.NET).
- `FlowRide.Application` → Domain. Casos de uso y puertos (interfaces).
- `FlowRide.Infrastructure` → Application. EF Core, Npgsql, implementaciones de puertos.
- `FlowRide.API` → Application, Infrastructure. Endpoints HTTP y composición (DI).
- Módulos = primer namespace dentro de cada capa (p. ej. `FlowRide.API.Plataforma`); sin ciclos entre módulos.
- Tests: `tests/FlowRide.UnitTests` (Domain + Application, con gate de cobertura), `tests/FlowRide.ArchitectureTests` (ArchUnitNET: capas y ciclos), `tests/FlowRide.IntegrationTests` (Testcontainers, `[Trait("Category","Integration")]`).

## RF-10
El sistema asigna automáticamente un conductor y un vehículo a cada solicitud pendiente, aplicando las reglas configurables del tenant.

## Motor de Asignación (ADR-0009)
- Patrón Strategy/Policy con composición determinista.
- Cuatro tipos de regla: **eliminatorias**, **de prioridad**, **de puntuación** y **de desempate**.
- Las reglas son independientes entre sí; `AssignmentEngine` las orquesta.
- **Nunca usar aleatoriedad** (ni `Random`, ni orden no determinista de colecciones/consultas).
- Toda decisión debe poder reconstruirse: *decision log* con candidatos, resultado de cada regla, puntajes, desempate y versión de configuración.

## Consistencia (ADR-0007)
Transacciones PostgreSQL, concurrencia optimista con token de versión, restricciones de unicidad en BD y Transactional Outbox para los efectos asíncronos.

## Lenguaje ubicuo
- Entidades de dominio en español: `SolicitudViaje`, `Conductor`, `Vehiculo`, `Turno`, `Asignacion`.
- Clases del motor en inglés, como en el ADR: `AssignmentEngine`, `AvailabilityRule`, etc.

## Convenciones
- `Nullable` y `TreatWarningsAsErrors` habilitados (Directory.Build.props).
- async/await de punta a punta.
- Sin secretos en `appsettings.json` ni en el código (usar user-secrets / variables de entorno; `.env` no se versiona).

## Flujo de trabajo (ADR-0018)
- Una rama `feature/RF-XX-…` por fase → PR hacia `develop`. Conventional Commits con el módulo como scope.
- Al cerrar cada fase: actualizar `docs/ROADMAP-V1.md`, entregar RF funcionales, cómo probarlos, tests y commit, y **esperar el OK** antes de seguir.

## Comandos
- Compilar: `dotnet build` (sin advertencias)
- Formato: `dotnet format FlowRide.slnx --verify-no-changes`
- Unit + cobertura: `dotnet test tests/FlowRide.UnitTests -p:CollectCoverage=true` (≥ 80 % líneas, ≥ 70 % ramas)
- Arquitectura: `dotnet test tests/FlowRide.ArchitectureTests`
- Integración (requiere Docker): `dotnet test tests/FlowRide.IntegrationTests`
- BD local: `cp .env.example .env`, definir contraseñas y `docker compose up -d`. PostgreSQL queda en el **puerto 5433** del host (5432 lo ocupa un PostgreSQL local), con las bases `flowride_catalogo`, `flowride_tenant_a` y `flowride_tenant_b`, cada una con su propio rol.
- API local: el `TenantId` está en `appsettings.Development.json`; la cadena de conexión es un secreto:
  `dotnet user-secrets set "ConnectionStrings:Silo" "Host=localhost;Port=5433;Database=flowride_tenant_a;Username=flowride_tenant_a;Password=<de .env>" --project src/FlowRide.API`
  y luego `dotnet run --project src/FlowRide.API` (http://localhost:5237; pruebas en `src/FlowRide.API/FlowRide.API.http`).
- En PowerShell, si `docker` no se encuentra, recargar el PATH o abrir una terminal nueva.

## Alcance
Todo lo definido en los ADR debe funcionar, **excepto**:
- RF-55 (ingesta de telemetría GPS) y RF-56 (desviación de ETA), es decir, ADR-0005. No crear nada de ubicación ni de ETA.
