# FlowRide — Memoria del proyecto

> **Léela al empezar cada sesión y actualízala al terminar.** Complementa a `docs/ROADMAP-V1.md` (qué falta por fase)
> con lo que no está en el código: estado actual, decisiones, problemas del entorno y bitácora.
> Última actualización: 2026-10-14.

## Estado actual
- **Rama de trabajo:** `feature/dominio-entidades` (modelo de dominio completo), pendiente de PR hacia `develop`.
- **`develop`** contiene: motor de asignación (ADR-0009), ADR revisados + roadmap, Fase 0 (base ejecutable).
- **Fase actual:** entre la Fase 0 (✅) y la Fase 1 (Tenancy y persistencia Silo, sin empezar).
- **Tests:** 114 unitarios (94,6 % líneas / 92,2 % ramas en Domain + Application), 9 de arquitectura, 12 de integración.

## Contexto de trabajo
- El equipo del curso (Sebastián, Andrés y Jhonny) trabajó junto **hasta el parcial**: los ADR, el DAS y los diagramas
  siguen vigentes y **no se modifican** por eso. Desde entonces el desarrollo lo hace Jhonny solo (GitHub `jhonnyebc`).
- Repositorio público: https://github.com/jhonnyebc/FlowRide. `develop` es la rama por defecto.
- `main` y `develop` protegidas: PR obligatorio + checks `Backend (build, formato, pruebas y cobertura)` y
  `Escaneo de secretos (ADR-0014)`. Sin aprobaciones obligatorias (trabaja solo). Los administradores pueden saltarlas.
- Copia privada de documentación en `OneDrive - UCO\FLOWRIDE-PRIVADO` (ADR + `CONTEXTO.md`). Los ADR originales del
  equipo están en `OneDrive - UCO\FlowRide\Entregables\doc\adr` y no se tocan.

## Decisiones tomadas (fuera o encima de los ADR)
| Fecha | Decisión |
|---|---|
| 2026-10-13 | ADR-0003: Keycloak con un realm por operador. ADR-0004: EF Core sobre Npgsql. ADR-0008: número de prueba de Meta. |
| 2026-10-13 | ADR-0018 (flujo Git y quality gates) y ADR-0019 (Panel Web React + TypeScript) aceptados. |
| 2026-10-13 | El motor de asignación vive en `FlowRide.Domain/Assignment` (ADR-0009 no exige Application). |
| 2026-10-13 | La BD de catálogo de plataforma guarda solo operadores y ruteo, sin datos operacionales. |
| 2026-10-13 | Pendiente para Fase 5: atributo explícito "compatible con servicio crítico" en el candidato (hoy se usa "turno activo"). |
| 2026-10-14 | Modelo de dominio: entidades con constructores que validan invariantes y métodos de transición; las relaciones entre agregados son por Id (`Guid`). El token de versión (ADR-0007) **no** está en el dominio: se mapea en EF Core como `xmin`. |
| 2026-10-14 | `Conductor` y `Pasajero` heredan de `Usuario` (como en el diagrama). `Conductor.EstaActivo` = usuario activo **y** estado operativo Activo. |
| 2026-10-14 | Turnos y documentos son parte del agregado `Conductor`; paradas e incidentes, del agregado `Viaje`; historial y cancelación, del agregado `SolicitudViaje`. |
| 2026-10-14 | Los registros hijos que crea el propio dominio (historial de solicitud) usan `Guid.CreateVersion7(ahora)`. La regla "nunca aleatoriedad" aplica a las **decisiones del motor**, no a identificadores. |
| 2026-10-14 | El tiempo entra siempre como parámetro (`ahora`); el dominio nunca llama a `DateTime.Now`. |

## Modelo de dominio (`src/FlowRide.Domain`)
- **Entidades:** Usuario, Rol, Pasajero, Conductor, Turno, DocumentoConductor, Vehiculo, EmpresaCliente, Itinerario,
  SolicitudViaje, EstadoSolicitudHistorial, CancelacionSolicitud, Asignacion, Viaje, Parada, Incidente, Calificacion.
- **Value object:** FranjaHoraria (intervalo semiabierto). **Enumeraciones:** las del diagrama de clases.
- **Fuera de alcance (no crear):** RegistroUbicacion, DesviacionEta, EstadoAlerta (RF-55/RF-56, ADR-0005).
- **Tablas del modelo ER que no son entidades del dominio** (llegan en su fase): `ops.parametro` (Fase 3),
  `motor.*` (Fase 5), `notif.*` (Fases 7–8), `audit.evento_auditoria` (Fase 2), `plataforma.operador_tenant` (Fase 1).

## Entorno local (problemas conocidos y cómo se resolvieron)
- **Docker Desktop** con WSL 2. Si las pruebas de integración fallan con `npipe://./pipe/docker_engine`, Docker no está
  abierto: iniciarlo (`C:\Program Files\Docker\Docker\Docker Desktop.exe`) y esperar *Engine running*.
- En PowerShell, `docker` puede no estar en el PATH de la sesión: recargarlo con
  `$env:Path = [Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [Environment]::GetEnvironmentVariable("Path","User")`.
- **Hay un PostgreSQL 18 instalado como servicio de Windows en el puerto 5432.** El de Docker usa el **5433**.
- `.env` (ignorado por git) tiene las contraseñas de los Silos; la cadena de conexión de la API está en user-secrets.
- Dentro del contenedor, las conexiones a `localhost` son `trust`: para probar autenticación real hay que conectarse
  desde fuera (`host.docker.internal:5433`).
- **No usar `preview_start` de la herramienta de navegador:** toma un `launch.json` de otro proyecto del usuario
  (`constructora-web`, `astro-dev`). Para probar la API, arrancarla, consultarla y detenerla en un solo comando.
- `gh` (GitHub CLI) no está instalado; los PR se abren con enlaces `compare/develop...<rama>`.
- Git Bash convierte `/p:` en ruta: usar `-p:` en los comandos de `dotnet`.

## Bitácora de sesiones
| Fecha | Qué se hizo |
|---|---|
| 2026-10-13 | Motor de asignación + 63 tests. ADR originales incorporados y revisados. Roadmap V1. Repo en GitHub. Docker instalado. Fase 0 completa (PR #1 y #2 fusionados, CI en verde). Protección de ramas. |
| 2026-10-14 | Modelo de dominio completo según el diagrama de clases (17 entidades) con 51 pruebas nuevas. Creada esta memoria. |

## Próximos pasos
1. Abrir el PR de `feature/dominio-entidades` hacia `develop` y fusionarlo con el CI en verde.
2. Fase 1 (Tenancy y persistencia Silo). **Decisión pendiente para el usuario:** cómo se dispara el aprovisionamiento
   de un operador nuevo (herramienta de línea de comandos o endpoint protegido).
3. Opcional: SonarCloud (crear el proyecto y el secreto `SONAR_TOKEN`).
