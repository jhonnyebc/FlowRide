# FlowRide — Memoria del proyecto

> **Léela al empezar cada sesión y actualízala al terminar.** Complementa a `docs/ROADMAP-V1.md` (qué falta por fase)
> con lo que no está en el código: estado actual, decisiones, problemas del entorno y bitácora.
> Última actualización: 2026-10-16.

## Estado actual
- **Rama de trabajo:** `feature/RF-03-tenancy-silo` (Fase 1 completa, todos los gates en verde), **sin commit todavía**,
  pendiente del OK del usuario para el commit y el PR hacia `develop`.
- **`develop`** contiene: motor de asignación (ADR-0009), ADR revisados + roadmap, Fase 0 (base ejecutable) y el modelo
  de dominio completo (PR #3).
- **Fase actual:** Fase 1 (Tenancy y persistencia Silo) terminada en la rama; la siguiente es la Fase 2 (Identidad,
  usuarios y auditoría).
- **Tests:** 144 unitarios (94,1 % líneas / 91,3 % ramas en Domain + Application), 10 de arquitectura, 46 de integración.
- **Entorno local del usuario:** `tenant-a` (Transportes Andinos SAS) y `tenant-b` (Movilidad del Oriente SAS)
  aprovisionados con la CLI; la API arranca como `tenant-a` y `/health/ready` responde 200.

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
| 2026-10-16 | **Aprovisionamiento por CLI** (`src/FlowRide.Aprovisionamiento`), no por endpoint: no se expone una API con permisos para crear bases; lo opera un administrador técnico. Comandos: `crear-operador`, `migrar`, `migrar-todos`, `listar`. |
| 2026-10-16 | **Secretos locales por operador:** el equivalente local de su Key Vault (ADR-0014) es un almacén user-secrets `flowride-<tenant>`. La CLI escribe ahí `ConnectionStrings:Silo` y la contraseña; la API lo lee en Development, **por debajo** de las variables de entorno y la línea de comandos. El adaptador de Azure Key Vault llega en la Fase 12. |
| 2026-10-16 | El stack resuelve su Silo desde su configuración de despliegue (`FlowRide:TenantId` + `ConnectionStrings:Silo`), no desde la petición. En la Fase 2 se exige además que el claim `tenant_id` del JWT coincida con el `TenantId` del stack. |
| 2026-10-16 | `docker-compose` crea solo el catálogo; los Silos los crea la CLI. Si un rol ya existía (los de la Fase 0), **el secreto manda** y el rol toma su contraseña. |
| 2026-10-16 | Persistencia: esquema `ops` en snake_case (igual que el modelo ER), herencia TPT de `Usuario`, fechas guardadas en UTC (Npgsql solo admite offset 0 en `timestamptz`), enums como texto, y restricciones `EXCLUDE` escritas a mano en la migración. |

## Modelo de dominio (`src/FlowRide.Domain`)
- **Entidades del Silo:** Usuario, Rol, Pasajero, Conductor, Turno, DocumentoConductor, Vehiculo, EmpresaCliente,
  Itinerario, SolicitudViaje, EstadoSolicitudHistorial, CancelacionSolicitud, Asignacion, Viaje, Parada, Incidente,
  Calificacion. **Plataforma:** Operador (catálogo).
- **Value object:** FranjaHoraria (intervalo semiabierto). **Enumeraciones:** las del diagrama de clases.
- `SolicitudViaje`, `Turno` y `Asignacion` tienen un constructor privado sin parámetros **solo para EF Core**
  (el público registra historial o recibe una `FranjaHoraria` que no se enlaza a columnas).
- **Fuera de alcance (no crear):** RegistroUbicacion, DesviacionEta, EstadoAlerta (RF-55/RF-56, ADR-0005).
- `Viaje.EtaProgramada` se eliminó el 2026-10-16 (fuera de alcance, ADR-0005); migración `QuitarEtaProgramada`.
- **Tablas del modelo ER que aún no existen** (llegan en su fase): `ops.parametro` (Fase 3), `motor.*` (Fase 5),
  `notif.*` (Fases 7–8), `audit.evento_auditoria` (Fase 2).

## Entorno local (problemas conocidos y cómo se resolvieron)
- **Docker Desktop** con WSL 2. Si `docker` falla con `dockerDesktopLinuxEngine` o `docker_engine`, Docker no está
  abierto: iniciarlo (`C:\Program Files\Docker\Docker\Docker Desktop.exe`) y esperar a que `docker info` responda
  (≈1 min).
- En PowerShell, `docker` puede no estar en el PATH de la sesión: recargarlo con
  `$env:Path = [Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [Environment]::GetEnvironmentVariable("Path","User")`.
- **Hay un PostgreSQL 18 instalado como servicio de Windows en el puerto 5432.** El de Docker usa el **5433**.
- `.env` (ignorado por git) tiene la contraseña del superusuario y la del catálogo. Las variables `TENANT_A/B_DB_PASSWORD`
  que puedan quedar en el `.env` del usuario ya no se usan.
- La CLI tiene sus secretos en user-secrets `flowride-aprovisionamiento` (ya configurados en el equipo del usuario).
- El user-secret `ConnectionStrings:Silo` del proyecto API (Fase 0) quedó **obsoleto** (contraseña vieja); el almacén
  del operador lo sobrescribe. Se puede borrar con `dotnet user-secrets remove "ConnectionStrings:Silo" --project src/FlowRide.API`.
- Dentro del contenedor, las conexiones a `localhost` son `trust`: para probar autenticación real hay que conectarse
  desde fuera (`host.docker.internal:5433`).
- **No usar `preview_start` de la herramienta de navegador:** toma un `launch.json` de otro proyecto del usuario
  (`constructora-web`, `astro-dev`). Para probar la API, arrancarla, consultarla y detenerla en un solo comando
  (PowerShell: `Start-Process dotnet ... -PassThru`, `Invoke-WebRequest`, y matar `FlowRide.API.exe` al final).
- `gh` (GitHub CLI) no está instalado; los PR se abren con enlaces `compare/develop...<rama>?expand=1`. Antes de fusionar, si GitHub pide "Update branch", pulsarlo (la protección exige la rama al día); no usar "bypass rules". El navegador del usuario traduce GitHub al español (develop = "desarrollar", main = "principal").
- Git Bash convierte `/p:` en ruta: usar `-p:` en los comandos de `dotnet`.
- En Git Bash, los heredocs largos con comillas a veces rompen el comando: crear archivos con la herramienta Write.
- El CI corre las pruebas de integración con `--filter "Category=Integration"`: **toda** clase de `IntegrationTests`
  debe llevar `[Trait("Category", "Integration")]` o no se ejecuta en el CI.

## Lecciones aprendidas
- **Precedencia de configuración en ASP.NET Core:** `WebApplicationBuilder` agrega dos fuentes de variables de entorno
  (`DOTNET_`/`ASPNETCORE_` al principio y la general al final). Para insertar una fuente "por debajo del entorno" hay que
  ubicarla antes de la **general** (sin prefijo). Las pruebas de configuración deben usar el `WebApplicationBuilder`
  real, no un `ConfigurationBuilder` armado a mano: así se detectó el error.
- **ArchUnitNET no analiza el cuerpo del `Main` de nivel superior** (`Program.cs`): una regla de dependencias solo
  detecta violaciones dentro de clases. Comprobar cada regla nueva provocando una violación a propósito.
- **Npgsql + `timestamptz`:** solo acepta `DateTimeOffset` con offset 0; hay un convertidor global a UTC en las
  convenciones de los contextos.
- **`xmin` como token de versión:** la migración lo declara como columna, pero Npgsql lo omite al generar el SQL
  (verificado con `dotnet ef migrations script`).

## Bitácora de sesiones
| Fecha | Qué se hizo |
|---|---|
| 2026-10-13 | Motor de asignación + 63 tests. ADR originales incorporados y revisados. Roadmap V1. Repo en GitHub. Docker instalado. Fase 0 completa (PR #1 y #2 fusionados, CI en verde). Protección de ramas. |
| 2026-10-14 | Modelo de dominio completo según el diagrama de clases (17 entidades) con 51 pruebas nuevas. Creada esta memoria. PR #3 fusionado (se usó "Update branch" porque la protección exige la rama al día con `develop`). |
| 2026-10-16 | Fase 1 completa en `feature/RF-03-tenancy-silo`: catálogo de plataforma, `FlowRideDbContext` con las 17 entidades y migración inicial, caso de uso `AprovisionarOperador` + `MigrarSilos`, CLI de aprovisionamiento, almacén local de secretos por operador, Spike 3 superado. 30 pruebas unitarias, 34 de integración y 1 regla de arquitectura nuevas. Probado de punta a punta en el entorno local del usuario. |

## Sesión 2026-10-16 (continuación): estilo del profesor
- `Viaje.EtaProgramada` **eliminada** (decisión del usuario) + migración `QuitarEtaProgramada`, ya aplicada a tenant-a/b locales.
- Proyecto de referencia del profesor: `C:\Users\Lenovo\IdeaProjects\clase7SF2\uco-test-api` (Java/Spring hexagonal).
- Decisiones del usuario: (1) mantener mapeo fluido de EF (sin entidad JPA + mapper); (2) servicios de dominio `XxxDominio`
  con los puertos de repositorio en Domain (`Domain/Comun/Repositorios` genéricos + `IOperadorRepositorio`);
  (3) módulo primero y dentro `CasosUso/`, `Entrada/{Puertos,Adaptadores,Dto}`, `Salida/Puertos`; (4) aplicado ya en la Fase 1.
- Piezas: `ICasoUsoConRetorno/SinRetorno`, `FlowRideExcepcion` (Domain/Transversal) → 400 vía `ManejadorExcepciones` en la API,
  CLI solo usa puertos de entrada. Nuevas reglas de arquitectura en `EstiloHexagonalTests` (verificadas con violación provocada).
- Gates al cierre: build sin advertencias, formato OK, 173 unitarias (94,5 % / 92,1 %), 15 arquitectura, 47 integración.
- CLI refactorizada probada contra el entorno local: `listar`, `crear-operador` repetido (idempotente), errores amigables
  (NIT duplicado, Id inválido, operador inexistente → código de salida 1) y `migrar-todos`.
- CLAUDE.md tiene la sección "Estilo de puertos y adaptadores (acordado con el profesor)"; ROADMAP actualizado (Fase 1 y
  dos decisiones del 2026-10-16).
- **Pendiente:** commit + PR tras el OK del usuario.

## Próximos pasos
1. **Esperar el OK del usuario** sobre la Fase 1; luego commit (`feat(tenancy): …`) y PR de `feature/RF-03-tenancy-silo`
   hacia `develop`, con el CI en verde.
2. ~~Duda de `Viaje.EtaProgramada`~~ (eliminada).
3. Fase 2 (Identidad, usuarios y auditoría): Keycloak con un realm por operador (agregar el paso "realm" al pipeline de
   aprovisionamiento), claim `tenant_id` validado contra el `TenantId` del stack, RBAC, revocación ≤ 60 s y `AuditService`.
4. Opcional: SonarCloud (crear el proyecto y el secreto `SONAR_TOKEN`).
