# FlowRide — Backend

Proyecto académico (Ingeniería del Software 2). Objetivo actual: implementar y demostrar **RF-10: Asignación automática de conductores**.

## Producto
- SaaS de transporte corporativo interno (B2B2B).
- Multi-tenant, modelo **Silo**: una base de datos por operador.

## Stack
- C# / .NET 10, ASP.NET Core, EF Core + Npgsql, PostgreSQL 17 (docker-compose).
- IDE: JetBrains Rider. Tests: xUnit.

## Arquitectura
Monolito modular por capas; las dependencias apuntan hacia Domain:
- `FlowRide.Domain`: entidades, reglas de dominio y motor de asignación (`Assignment/`, lógica pura). **No depende de nada externo** (ni EF Core, ni ASP.NET).
- `FlowRide.Application` → Domain. Casos de uso y puertos (interfaces).
- `FlowRide.Infrastructure` → Application. EF Core, Npgsql, implementaciones de puertos.
- `FlowRide.API` → Application, Infrastructure. Endpoints HTTP y composición (DI).
- `tests/FlowRide.Tests` → Domain, Application.

## RF-10
El sistema asigna automáticamente un conductor y un vehículo a cada solicitud pendiente, aplicando las reglas configurables del tenant.

## Motor de Asignación (ADR-0009)
- Patrón Strategy/Policy con composición determinista.
- Cuatro tipos de regla: **eliminatorias**, **de prioridad**, **de puntuación** y **de desempate**.
- Las reglas son independientes entre sí; `AssignmentEngine` las orquesta.
- **Nunca usar aleatoriedad** (ni `Random`, ni orden no determinista de colecciones/consultas).
- Toda decisión debe poder reconstruirse: *decision log* con candidatos, resultado de cada regla, puntajes, desempate y versión de configuración.

## Consistencia (ADR-0007)
Concurrencia optimista con token de versión + restricciones de unicidad en BD.

## Lenguaje ubicuo
- Entidades de dominio en español: `SolicitudViaje`, `Conductor`, `Vehiculo`, `Turno`, `Asignacion`.
- Clases del motor en inglés, como en el ADR: `AssignmentEngine`, `AvailabilityRule`, etc.

## Convenciones
- `Nullable` y `TreatWarningsAsErrors` habilitados (Directory.Build.props).
- async/await de punta a punta.
- Sin secretos en `appsettings.json` ni en el código (usar user-secrets / variables de entorno; `.env` no se versiona).

## Comandos
- Compilar: `dotnet build`
- Tests: `dotnet test`
- BD local: `cp .env.example .env` y luego `docker compose up -d`

## Fuera de alcance (no implementar hasta que se pida)
Keycloak, Outbox, WhatsApp, OpenTelemetry, YARP, Key Vault, frontend.
