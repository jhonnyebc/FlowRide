# FlowRide — Memoria del proyecto

> **Léela al empezar cada sesión y actualízala al terminar.** Complementa a `docs/ROADMAP-V1.md` (qué falta por fase)
> con lo que no está en el código: estado actual, decisiones, problemas del entorno y bitácora.
> Última actualización: 2026-10-17.

## Estado actual
- **Rama de trabajo:** `feature/RF-10-asignacion` (Fase 5 completa, todos los gates en verde), **sin commit todavía**,
  pendiente del OK del usuario para el commit y el PR hacia `develop`.
- **`develop`** contiene: motor de asignación (ADR-0009), ADR revisados + roadmap, Fase 0, modelo de dominio (PR #3),
  Fase 1 Tenancy/Silo (PR #4), Fase 2 Identidad y auditoría (PR #5), Fase 3 Recursos y parámetros (PR #6) y Fase 4
  Solicitudes e itinerarios (PR #7).
- **Fase actual:** Fase 5 (Asignación de punta a punta) terminada en la rama; la siguiente es la Fase 6 (Viajes y
  calificación).
- **Tests:** 361 unitarios (94,4 % líneas / 87,5 % ramas en Domain + Application), 16 de arquitectura, 107 de integración
  (Keycloak, Mailpit y Azurite reales con Testcontainers).
- **Entorno local del usuario:** `docker compose` levanta PostgreSQL (5433), Keycloak 26.3 (8080), Mailpit (8025) y
  Azurite (10000). `tenant-a` y `tenant-b` tienen Silo (migración `ParametrosYCamposCifrados` aplicada), realm, bucket
  `flowride-<tenant>`, clave de cifrado y primer administrador (`admin@tenant-a.local`, `admin@tenant-b.local`, datos de
  prueba, todavía sin contraseña: correo "Actualiza tu cuenta" en Mailpit). Sus teléfonos ya están cifrados (`v1.`). La
  API arranca como `tenant-a`: `/health/ready` 200 y `/api/*` 401 sin token.

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

| 2026-10-17 | **Fase 2 (respuestas del usuario):** FlowRide crea las cuentas del Panel Web en Keycloak; la CLI crea el realm y el primer Administrador; la contraseña la define el usuario con el correo de Keycloak (Mailpit en local); revocación consultando el estado interno en cada petición, sin caché. |
| 2026-10-17 | El realm sale de `plantilla-realm.json` (versionada, sin secretos, recurso embebido), aplicada paso a paso por la Admin API para que reaprovisionar sea idempotente (los mappers se sincronizan por nombre). |
| 2026-10-17 | La API usa la cuenta de servicio de su realm (`flowride-api`, secreto `FlowRide:Identidad:SecretoCliente` en el almacén del operador); el admin de Keycloak de la plataforma solo lo usa la CLI (regla de arquitectura). |
| 2026-10-17 | Autorización: `/api` exige token (grupo con `RequireAuthorization`), no una política *fallback*, para no convertir los 404 en 401. Roles fijos en `RolesSistema` (Id sembrados por migración). |
| 2026-10-17 | Auditoría: repositorios del Silo sin `SaveChanges`; `IUnidadTrabajo` confirma cambio + evento en una transacción, con candado consultivo y la cadena SHA-256. Fecha truncada a microsegundos y JSON canónico para que el hash sobreviva a timestamptz y jsonb. |

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
- **Configuración en memoria con valor null pisa el valor por defecto** de una propiedad de opciones al enlazar: no
  agregar claves con null (se vio en `FabricaStackOperador` con `UrlPanel`).
- **Una política *fallback* de autorización también aplica a las rutas inexistentes** (404 → 401): se exige token con un
  grupo `/api` en su lugar.
- **Hash de auditoría y PostgreSQL:** `timestamptz` guarda microsegundos y `jsonb` reordena claves; sin truncar la fecha
  y canonizar el JSON, la verificación fallaría al releer.
- **Tipos generados por el compilador** (expresiones de colección) no tienen namespace en ArchUnitNET: la prueba de ciclos
  entre módulos debe ignorarlos.
- **Keycloak en pruebas:** contenedor genérico con su puerto de gestión 9000 (`/health/ready`) y Mailpit en la misma red
  de Testcontainers (alias `mailpit`); para obtener tokens sin navegador se habilita el flujo de contraseña directa en el
  cliente del panel solo dentro de la prueba.
- **Reaprovisionar con un almacén de secretos nuevo genera una clave de cifrado nueva**: los datos cifrados con la clave
  anterior quedan ilegibles (`AuthenticationTagMismatchException`). En pruebas, un tenant por prueba; en operación, el
  almacén del operador nunca se pierde (Key Vault).
- **EF Core y conversiones con estado:** el modelo se cachea por tipo de contexto; con un convertidor que captura una
  clave hay que cachearlo por cifrador (`IModelCacheKeyFactory`), o dos operadores en el mismo proceso compartirían clave.
- **`sed` con `xargs` sobre `grep -rl` incluye binarios** (`bin/`, `obj/`): limitar con `--include=*.cs` o hacer
  `dotnet clean` después.
- **Minimal APIs y `IFormFile`:** exigen antiforgery; con autenticación solo por bearer se desactiva por endpoint.

## Bitácora de sesiones
| Fecha | Qué se hizo |
|---|---|
| 2026-10-13 | Motor de asignación + 63 tests. ADR originales incorporados y revisados. Roadmap V1. Repo en GitHub. Docker instalado. Fase 0 completa (PR #1 y #2 fusionados, CI en verde). Protección de ramas. |
| 2026-10-14 | Modelo de dominio completo según el diagrama de clases (17 entidades) con 51 pruebas nuevas. Creada esta memoria. PR #3 fusionado (se usó "Update branch" porque la protección exige la rama al día con `develop`). |
| 2026-10-16 | Fase 1 completa en `feature/RF-03-tenancy-silo`: catálogo de plataforma, `FlowRideDbContext` con las 17 entidades y migración inicial, caso de uso `AprovisionarOperador` + `MigrarSilos`, CLI de aprovisionamiento, almacén local de secretos por operador, Spike 3 superado. 30 pruebas unitarias, 34 de integración y 1 regla de arquitectura nuevas. Probado de punta a punta en el entorno local del usuario. |
| 2026-10-17 | Fase 2 completa en `feature/RF-02-identidad-auditoria`: Keycloak con un realm por operador desde una plantilla versionada, primer administrador desde la CLI con correo de contraseña (Mailpit), autenticación OIDC + `tenant_id` + usuario interno por petición, RBAC, revocación inmediata y auditoría con cadena de hashes. 235 + 16 + 78 pruebas en verde. |
| 2026-10-17 | Fase 3 completa en `feature/RF-04-recursos-parametros`: empresas cliente, pasajeros, conductores, turnos, documentos (Azurite), vehículos, disponibilidad (RF-07), búsqueda paginada, parámetros versionados con caché ≤ 60 s y cifrado determinista de teléfono y licencia. 293 + 16 + 92 pruebas en verde. |
| 2026-10-17 | Fase 4 completa en `feature/RF-09-solicitudes-itinerarios`: solicitudes desde el panel con validación, ventana de cancelación, modificación sin asignar, cancelación con motivo (y de su asignación), visibilidad de personales, CQRS ligero e itinerarios recurrentes. 329 + 16 + 99 pruebas en verde. |
| 2026-10-18 | Fase 5 completa en `feature/RF-10-asignacion`: asignación automática transaccional con Outbox y worker de la cola, brechas del motor cerradas, configuración versionada con rollback, decision log persistente, reasignación manual completa y Spike 2 (0 dobles asignaciones). 361 + 16 + 107 pruebas en verde. |

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
- Commit `ad2341c` → PR #4 fusionada en `develop` el 2026-10-17 con el CI en verde. **Fase 1 cerrada.**

## Sesión 2026-10-17: Fase 2 (Identidad, usuarios y auditoría)
- Rama `feature/RF-02-identidad-auditoria` desde `develop`. Decisiones del usuario en la tabla de decisiones (2026-10-17).
- Dominio: `RolesSistema`, `UsuarioDominio`, datos por caso de uso en `Domain/Identidad/GestionUsuarios`, `EventoAuditoria`
  (hash SHA-256) y `CadenaAuditoria`; repositorios `IUsuarioRepositorio` e `IEventoAuditoriaRepositorio`.
- Aplicación: casos de uso Crear, AsegurarAdministradorInicial, CambiarEstado, CambiarRol, ConsultarUsuarios,
  ResolverIdentidad y VerificarAuditoria, con sus puertos y adaptadores de entrada; puertos `IProveedorIdentidad`,
  `IServidorIdentidad` (Tenancy) e `IUnidadTrabajo`.
- Infraestructura: `ClienteKeycloak` (Admin API), `ServidorIdentidadKeycloak` (realm desde la plantilla),
  `ProveedorIdentidadKeycloak` (cuentas, correo, habilitar/deshabilitar + cerrar sesiones), `UnidadTrabajoEf`, migración
  `IdentidadYAuditoria` (roles sembrados, esquema `audit`, trigger solo INSERT).
- API: JwtBearer contra el realm del operador + validación de `tenant_id` y usuario interno en `OnTokenValidated`;
  `/api/usuarios` y `/api/auditoria/verificacion`. CLI: `crear-operador --admin-*`, `appsettings.json` (URL de Keycloak y SMTP).
- Entorno local: `.env` y los user-secrets de la CLI recibieron el administrador de Keycloak (contraseña aleatoria, sin
  imprimirla). tenant-a y tenant-b reaprovisionados con realm y primer administrador; el catálogo quedó con correos de
  contacto `contacto@tenant-x.local`.
- Gates al cierre: build sin advertencias, formato OK, 235 unitarias (95,4 % / 92,7 %), 16 arquitectura, 78 integración.
- Commit `dfe049a` → PR #5 fusionada en `develop` el 2026-10-17 con el CI en verde. **Fase 2 cerrada.**

## Sesión 2026-10-17 (continuación): Fase 3 (Recursos operativos y parámetros)
- Rama `feature/RF-04-recursos-parametros`. Decisiones del usuario en el roadmap (2026-10-17): cifrado determinista de
  teléfono y licencia, empresas cliente y pasajeros incluidos, parámetros por defecto (60/30/sin franjas/pasajero y
  Operador logístico), documentos por la API (PDF/PNG/JPG ≤ 5 MB).
- Dominio: `Paginacion`, `Domain/Operacion` (repositorios, servicios `EmpresaClienteDominio`, `PasajeroDominio`,
  `ConductorDominio` con `FiltrarDisponibles`, `VehiculoDominio`; datos por caso de uso), `Domain/Parametros`
  (`CatalogoParametros`, `DefinicionParametro`, `Parametro`). `Conductor.QuitarDocumento`.
- Aplicación: casos de uso de empresas, pasajeros, conductores (turnos, disponibilidad, documentos), vehículos,
  parámetros y `AsegurarCifrado`; un puerto de entrada por recurso. Pipeline de aprovisionamiento con clave de cifrado y
  bucket.
- Infraestructura: `CifradorCampos` + `ModeloPorCifrador` + `RecifradorCamposSilo`, repositorios de Operación,
  `ParametroRepositorioAdaptador` (cache-aside), `AlmacenObjetosAzureBlob`/`ServidorObjetosAzureBlob`, migración
  `ParametrosYCamposCifrados`. Azurite en docker-compose; la CLI lee `ConnectionStrings:ServidorObjetos` de su
  appsettings (`UseDevelopmentStorage=true`, no es secreto).
- Gates al cierre: build sin advertencias, formato OK, 293 unitarias (95,3 % / 87,7 %), 16 arquitectura, 92 integración.
- Commit `676e28c` → PR #6 fusionada en `develop` el 2026-10-17 con el CI en verde. **Fase 3 cerrada.**

## Sesión 2026-10-17 (continuación): Fase 4 (Solicitudes e itinerarios)
- Rama `feature/RF-09-solicitudes-itinerarios` (llevó también los cambios de documentación que cerraban la Fase 3).
- Decisiones del usuario (roadmap 2026-10-17): ventana para el pasajero y quien registra por él, el Operador logístico
  cancela fuera de ella con motivo; modificar solo sin asignar; sin solicitudes activas solapadas; itinerarios por rango
  de fechas y días. Decisión propia anotada: `FlowRide:ZonaHoraria` (por defecto America/Bogota).
- Dominio: `SolicitudViaje.Modificar`/`EsModificable`/`EstaActiva` y `registradaPorId`; `Domain/Solicitudes`
  (`SolicitudDominio`, `ItinerarioDominio`, `VisibilidadSolicitudes`, repositorios); `ParametrosDominio` (lectura tipada
  de parámetros); `ZonaHorariaOperador`; `IAsignacionRepositorio.ConsultarVigentePorSolicitudAsync`.
- Aplicación: casos de uso Registrar/Modificar/Cancelar/Consultar/Buscar solicitudes y Registrar/Buscar itinerarios;
  lado de lectura `IConsultasSolicitudes` (CQRS ligero). API: `/api/solicitudes` y `/api/itinerarios` (Panel Web);
  `ManejadorExcepciones` devuelve 409 ante `DbUpdateConcurrencyException`. Sin migración (solo setters privados).
- Gates al cierre: build sin advertencias, formato OK, 329 unitarias (95,6 % / 87,5 %), 16 arquitectura, 99 integración.
  Docker Desktop estaba cerrado al empezar: se inició y Testcontainers tardó unos segundos más en verlo.
- Commit `4cd269c` → PR #7 fusionada en `develop` el 2026-10-18 con el CI en verde. **Fase 4 cerrada.**

## Sesión 2026-10-18: Fase 5 (Asignación de punta a punta)
- Rama `feature/RF-10-asignacion` (llevó también la documentación que cerraba la Fase 4).
- Decisiones del usuario (roadmap 2026-10-18): worker cada 15 s + "asignar ya"; vehículo habitual o cada vehículo libre;
  marca "apto para servicio crítico" en el conductor; RF-12 completa. Decisiones propias anotadas: configuración en
  `motor.*` con v1 sembrada, ventana de carga de 7 días como parámetro de regla, exclusiones de franjas diferibles.
- Motor: `IAssignmentRule.Version/Obligatoria/Dependencias`; `AvailabilityRule` v2 (ocupación) y
  `CriticalServicePriorityRule` v2 (marca del conductor); validador con obligatorias, dependencias y ciclos;
  `AssignmentContext.EvaluadaEn` y `AssignmentDecisionLog.EvaluatedAt`; `RuleOutcome.RuleVersion`. Pruebas del motor
  actualizadas a la nueva semántica.
- Dominio `Asignaciones`: `ConfiguracionAsignacion`/`ReglaConfigurada`, `DecisionAsignacion` (+ evaluaciones y
  resultados, snapshot JSON de la entrada), `AsignacionDominio` (candidatos y validación manual).
- Aplicación: Asignar, Cola, AsignarManual, Reasignar, DevolverACola, AbandonarTurno, configuración (crear/activar/
  consultar), decisiones y asignaciones (lectura); `IBandejaSalida` y `ConflictoConcurrenciaExcepcion`.
- Infraestructura: migración `MotorYOutbox` (tablas `motor.*` y `notif.outbox_mensaje`, siembra v1, exclusiones
  diferibles, una sola configuración activa, triggers solo INSERT del decision log), `WorkerColaAsignacion`, `UnidadTrabajoEf`
  traduce conflictos. API: `/api/solicitudes/{id}/asignar|decisiones|asignacion-manual|devolver-a-cola`,
  `/api/asignaciones`, `/api/conductores/{id}/turnos/{turno}/abandonar`, `/api/motor/configuraciones`; política
  `OperadorLogistico`.
- Pruebas de integración: el PostgreSQL de Testcontainers se quedó sin conexiones (`53300`) al crecer la suite → 
  `max_connections=500` en `PostgresFixture`. La prueba de caché entre réplicas ahora usa TTL de 5 s (era frágil con 1 s).
- Entorno local: `migrar-todos` aplicó `MotorYOutbox` a tenant-a/b; la API arranca con el worker (cola vacía, sin errores).
- Gates al cierre: build sin advertencias, formato OK, 361 unitarias (94,4 % / 87,5 %), 16 arquitectura, 107 integración;
  Spike 2 en verde 5 veces seguidas.
- **Pendiente:** OK del usuario para commit + PR.

## Próximos pasos
1. **Esperar el OK del usuario** sobre la Fase 5; luego commit (`feat(asignacion): …`) y PR de `feature/RF-10-asignacion`
   hacia `develop`.
2. Fase 6 (Viajes y calificación): `Viaje`, `Parada`, `Incidente` y `Calificacion` (aceptar/rechazar asignación, iniciar y
   finalizar viaje, incidentes, calificación RF-54); completar RF-31 con los viajes.
3. Panel Web: sigue en la Fase 11. Opción conversada el 2026-10-17: adelantar un panel básico después de la Fase 5 si hay una entrega o demostración intermedia (el usuario aún no lo decidió).
4. RF-23: probar "Olvidé mi contraseña" con la pantalla de login cuando exista el Panel Web (Fase 11).
5. Fase 12: credencial de objetos limitada al contenedor de cada operador (en local se comparte la cuenta de Azurite).
6. Opcional: SonarCloud (crear el proyecto y el secreto `SONAR_TOKEN`).
