# FlowRide

Proyecto académico (Ingeniería del Software 2). Objetivo: construir la **V1 completa** por fases (ver `docs/ROADMAP-V1.md`). RF-10 (motor de asignación) ya está implementado.

## Memoria del proyecto (obligatorio)
- **Al empezar cada sesión**, lee `docs/MEMORIA.md` (estado, decisiones, problemas del entorno y bitácora) y `docs/ROADMAP-V1.md`.
- **Al terminar cada sesión o fase**, actualiza `docs/MEMORIA.md`: estado actual, decisiones nuevas, problemas encontrados, una fila en la bitácora y los próximos pasos.

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
- Panel Web: React + TypeScript en `web/` (ADR-0019).
- IDE: JetBrains Rider.

## Arquitectura
Monolito modular por capas; las dependencias apuntan hacia Domain:
- `FlowRide.Domain`: entidades (`Entidades/`), value objects (`ValoresObjeto/`), enumeraciones y motor de asignación (`Assignment/`, lógica pura). **No depende de nada externo** (ni EF Core, ni ASP.NET). Las entidades validan sus invariantes en el constructor, cambian de estado solo por métodos, se relacionan con otros agregados por Id y reciben el tiempo como parámetro (`ahora`).
- `FlowRide.Application` → Domain. Casos de uso y puertos (interfaces).
- `FlowRide.Infrastructure` → Application. EF Core, Npgsql, implementaciones de puertos. `Persistencia/` (`FlowRideDbContext` del Silo, esquema `ops`, migraciones) y `Tenancy/` (catálogo de plataforma, servidor de Silos, secretos por operador).
- `FlowRide.API` → Application, Infrastructure. Endpoints HTTP y composición (DI). Es el stack de **un** operador: nunca usa el catálogo ni el aprovisionamiento (regla de arquitectura).
- `FlowRide.Aprovisionamiento` → Application, Infrastructure. Herramienta de plataforma (CLI) para aprovisionar y migrar los Silos (RF-03).
- Migraciones: `dotnet ef migrations add <Nombre> --project src/FlowRide.Infrastructure --context FlowRideDbContext --output-dir Persistencia/Migraciones` (catálogo: `--context CatalogoDbContext --output-dir Tenancy/Migraciones`). Las restricciones `EXCLUDE` se escriben a mano con `migrationBuilder.Sql`.
- Módulos = primer namespace dentro de cada capa (p. ej. `FlowRide.API.Plataforma`); sin ciclos entre módulos.
- Tests: `tests/FlowRide.UnitTests` (Domain + Application, con gate de cobertura), `tests/FlowRide.ArchitectureTests` (ArchUnitNET: capas, ciclos y estilo hexagonal), `tests/FlowRide.IntegrationTests` (Testcontainers, `[Trait("Category","Integration")]` en **toda** clase, porque el CI filtra por esa categoría).

## Estilo de puertos y adaptadores (acordado con el profesor)
Referencia: proyecto `uco-test-api` del curso (`C:\Users\Lenovo\IdeaProjects\clase7SF2`). Cada funcionalidad sigue esta cadena; la plantilla es el módulo `Tenancy`:
1. **Adaptador de entrada de infraestructura** (endpoint de la API o comando de la CLI): solo conoce el **puerto de entrada** y sus DTO.
2. **Puerto de entrada** `I<Accion>PuertoEntrada` y **adaptador de entrada** `<Accion>AdaptadorEntrada` en `Application/<Modulo>/Entrada/{Puertos,Adaptadores}`. El adaptador valida los campos obligatorios de la `<Accion>Peticion`, la convierte en el objeto de dominio, ejecuta el caso de uso y arma la `<Accion>Respuesta` (`Entrada/Dto`).
3. **Caso de uso** `<Accion>CasoUso` en `Application/<Modulo>/CasosUso`, que implementa `ICasoUsoConRetorno<TEntrada,TSalida>` o `ICasoUsoSinRetorno<TEntrada>` (`Application/Comun/CasosUso`).
4. **Dominio** por caso de uso en `Domain/<Modulo>/<Accion>/`: el objeto de datos `<Accion>Dominio` y los servicios de dominio `<Entidad>Dominio` con las reglas que consultan repositorios. Las reglas que solo dependen de la entidad viven en la entidad.
5. **Puertos de salida:** los repositorios son interfaces de Domain (`Domain/<Modulo>/Repositorios`, armados con los genéricos de `Domain/Comun/Repositorios`); los demás puertos (servidores, secretos, mensajería) van en `Application/<Modulo>/Salida/Puertos`.
6. **Adaptadores de salida** en Infrastructure (`<Entidad>RepositorioAdaptador`, etc.). EF Core mapea las entidades del dominio directamente con configuración fluida: **no** hay modelo de persistencia aparte ni mapper (decisión del 2026-10-16).
7. Reglas de negocio incumplidas → `FlowRideExcepcion` (`Domain/Transversal`); la API la devuelve como 400 (`API/Plataforma/Errores/ManejadorExcepciones`).
Las pruebas de `EstiloHexagonalTests` hacen cumplir los puntos 1, 2, 3 y 5.

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
- Herramientas locales: `dotnet tool restore` (instala `dotnet-ef` desde `dotnet-tools.json`).
- BD local: `cp .env.example .env`, definir contraseñas y `docker compose up -d`. PostgreSQL queda en el **puerto 5433** del host (5432 lo ocupa un PostgreSQL local). Al iniciar el volumen solo se crea el catálogo (`flowride_catalogo`); los Silos los crea la herramienta.
- Herramienta de aprovisionamiento (secretos de plataforma una sola vez, con los valores de `.env`):
  `dotnet user-secrets set "ConnectionStrings:Catalogo" "Host=localhost;Port=5433;Database=flowride_catalogo;Username=flowride_catalogo;Password=<CATALOGO_DB_PASSWORD>" --project src/FlowRide.Aprovisionamiento`
  `dotnet user-secrets set "ConnectionStrings:AdministracionServidor" "Host=localhost;Port=5433;Database=postgres;Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>" --project src/FlowRide.Aprovisionamiento`
  y luego `dotnet run --project src/FlowRide.Aprovisionamiento -- crear-operador --id tenant-a --razon-social "..." --nit ... --correo ...` (también `migrar --id`, `migrar-todos`, `listar`, `ayuda`). Es idempotente.
- API local: el `TenantId` está en `appsettings.Development.json`; en Development la API lee su cadena de conexión del almacén del operador que creó la herramienta (`%APPDATA%\Microsoft\UserSecrets\flowride-<tenant>\secrets.json`). `dotnet run --project src/FlowRide.API` (http://localhost:5237; pruebas en `src/FlowRide.API/FlowRide.API.http`). Las variables de entorno y la línea de comandos siguen pudiendo sobrescribirla.
- En PowerShell, si `docker` no se encuentra, recargar el PATH o abrir una terminal nueva.

## Alcance
Todo lo definido en los ADR debe funcionar, **excepto**:
- RF-55 (ingesta de telemetría GPS) y RF-56 (desviación de ETA), es decir, ADR-0005. No crear nada de ubicación ni de ETA.
