# FlowRide — Memoria del proyecto

> **Léela al empezar cada sesión y actualízala al terminar.** Complementa a `docs/ROADMAP-V1.md` (qué falta por fase)
> con lo que no está en el código: estado actual, decisiones, problemas del entorno y bitácora.
> Última actualización: 2026-10-19.

## Estado actual
- **Rama de trabajo:** `chore/documentacion-desarrollo` (guía de desarrollo y limpieza de archivos locales, SIN commit). Después: Fase 11c.
- **`develop`** contiene: motor de asignación (ADR-0009), ADR revisados + roadmap, Fase 0, modelo de dominio (PR #3),
  Fase 1 Tenancy/Silo (PR #4), Fase 2 Identidad y auditoría (PR #5), Fase 3 Recursos y parámetros (PR #6), Fase 4
  Solicitudes e itinerarios (PR #7), Fase 5 Asignación de punta a punta (PR #8), Fase 6 Viajes y calificación (PR #9), Fase 7 Canal WhatsApp (PR #10), Fase 8 Notificaciones y alertas (PR #11), Fase 9 Reportes y vista operacional (PR #12), Fase 10 Borde común (PR #13), fechas con zona (PR #14), seguridad OWASP
  (PR #15), Fase 11a Panel Web base y Operación (PR #16), Fase 11b-1 usuarios, empresas y pasajeros (PR #17) y la
  documentación de la entrega (PR #18) y la Fase 11b-2 conductores, vehículos e itinerarios (PR #19).
- **Fase actual:** Fase 11b-2 cerrada (PR #19, 2026-10-26). Sigue 11c (configuración y reportes).
- **Tests:** 553 unitarios (94,1 % líneas / 83,2 % ramas en Domain + Application), 17 de arquitectura, 145 de integración
  (Keycloak, Mailpit y Azurite reales con Testcontainers) y 59 del frontend (Vitest + axe).
- **Entorno local del usuario:** `docker compose` levanta PostgreSQL (5433), Keycloak 26.3 (8080), Mailpit (8025) y
  Azurite (10000). Los secretos viven en el AppData **real** del usuario (`flowride-aprovisionamiento` y
  `flowride-tenant-a`, configurados por él el 2026-10-18). `tenant-a` se recreó ese día (base nueva con todas las
  migraciones, clave de cifrado nueva, admin `admin@tenant-a.local` con contraseña ya definida) y tiene una empresa de
  pruebas y un pasajero con el celular del usuario. El canal de tenant-a está en `MetaSandbox` (número de prueba
  +1 555 203 2563, token permanente de un usuario del sistema, webhook por ngrok `chummy-spree-clutter.ngrok-free.dev`).
  `tenant-b` existe en la base pero sus secretos solo estaban en una copia aislada del AppData: para usarlo
  hay que recrearlo igual que tenant-a.
- **Datos de prueba de tenant-a (2026-10-21):** "Conductor de pruebas" con un segundo celular del usuario (autorizado en
  la lista de destinatarios de Meta), turno del 2026-10-21 y vehículo `PRU001`. El número no va en el repo (el `.http`
  usa `+57XXXXXXXXXX`).
- **Choques conocidos:** `uco-test-api-postgres` (otro proyecto del usuario) también usa el puerto 5433: solo uno de los
  dos puede estar encendido. Si `flowride-postgres` arranca sin puerto publicado (`5432/tcp` sin `0.0.0.0:5433`), se
  recrea con `docker compose up -d --force-recreate --no-deps postgres` (los datos están en el volumen). El login de Rider
  regresa a `localhost:5173/callback`, el mismo puerto del panel. La red institucional del usuario bloquea ngrok.

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
| 2026-10-19 | **Fase 9 (respuestas del usuario):** exportes en CSV; generación síncrona con periodo máximo de 93 días; reportes e indicadores para Administrador y Operador logístico; indicadores de volumen, cumplimiento, conductores y por cliente empresarial. |
| 2026-10-19 | CSV para Excel en español: `;`, decimales con coma, UTF-8 con BOM, hora local del operador y apóstrofo delante de los textos que parecen fórmula. Una asignación "aceptada" es la que generó viaje; la vencida se detecta por el evento de auditoría `asignacion.vencer`. |
| 2026-10-19 | **Fase 10 (respuestas del usuario):** rutas del borde en el catálogo (las registra la CLI); la firma de Meta la validan el borde y el stack; WAF gestionado en la Fase 12; HTTPS local con `*.dev.localhost`. |
| 2026-10-19 | Una sola app de Meta para todos los operadores: el `AppSecret` y el `VerifyToken` del borde son los de esa app, y el `AppSecret` de cada stack es el mismo. El borde lee el catálogo con la cadena de `flowride_catalogo` (rol de solo lectura propio: Fase 12). |
| 2026-10-20 | **Videos del profesor:** fechas (UTC para lo pasado; hora local + nombre IANA para eventos futuros en un lugar), secretos y `.env`, CSRF, autenticación rota (OWASP) y reverse proxy vs load balancer vs API gateway. Decisiones del usuario: refactor completo de fechas, TOTP para el Administrador, reautenticación reciente en acciones sensibles, el borde también valida el token. Los videos se revisaron extrayendo cuadros y transcribiendo el audio en local (faster-whisper en un venv temporal). |

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
- **Fin de línea:** `dotnet format` marca un archivo que mezcla CRLF y LF. Al editar con `sed`/`perl` un archivo CRLF,
  dejarlo entero en un solo formato (con `perl -pi` cambiando cada salto de línea a CRLF).
- **Docker Desktop cerrado:** las pruebas de integración fallan al conectar; iniciarlo y esperar a que `docker info` responda.
  Testcontainers usa el pipe `docker_engine`, que a veces tarda más que `docker info` en aparecer.
- **AppData aislado:** lo que escriben en `%APPDATA%` los procesos lanzados desde una app empaquetada (no desde la
  terminal) puede quedar en una copia aislada (`AppData/Local/Packages/<app>/LocalCache`). Los `dotnet user-secrets set`
  y la CLI de aprovisionamiento se corren siempre desde la terminal del usuario.
- **Meta Cloud API:** el token temporal vence en horas (usar uno de usuario del sistema); sin `subscribed_apps` el webhook
  se verifica pero no llegan mensajes; ngrok gratis exige agente ≥ 3.20 (`ngrok update`).

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
| 2026-10-18 | Fase 5 cerrada (PR #8). Fase 6 completa en `feature/RF-14-viajes`: aceptar crea el viaje programado, rechazo y vencimiento devuelven a la cola sin ese conductor, ciclo iniciar/finalizar, paradas, incidentes, calificación con plazo, historial, monitoreo y trazabilidad. 400 + 16 + 111 pruebas en verde. |
| 2026-10-18 | Fase 6 cerrada (PR #9). Fase 7 completa en `feature/RF-14-whatsapp`: webhook firmado con bandeja de entrada idempotente, worker del canal con reintentos y correlación, bot de menú numerado para pasajero y conductor, adaptador del número de prueba de Meta y simulado, guía Meta + ngrok. 448 + 16 + 126 pruebas en verde. |
| 2026-10-18 | Fase 7 cerrada (PR #10). Fase 8 completa en `feature/RF-43-notificaciones`: worker de la Outbox con SKIP LOCKED, gateway de notificaciones (WhatsApp con ventana de 24 h y plantillas, alertas del panel por SSE, correo para las críticas), recordatorios y catálogo es-CO. 480 + 16 + 130 pruebas en verde. |

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
- La guía de desarrollo (hoy `docs/DESARROLLO.md`) tiene la sección "Estilo de puertos y adaptadores (acordado con el profesor)"; ROADMAP actualizado (Fase 1 y
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
- Commit `3e76e89` → PR #8 fusionada en `develop` el 2026-10-18 (merge `68153c7`) con el CI en verde. **Fase 5 cerrada.**

## Sesión 2026-10-18 (continuación): Fase 6 (Viajes y calificación)
- Rama `feature/RF-14-viajes` (llevó también la documentación que cerraba la Fase 5).
- Decisiones del usuario (roadmap 2026-10-18): el Operador logístico actúa desde el panel en nombre del conductor y del
  pasajero hasta la Fase 7; rechazo → cola sin ese conductor; vencimiento por parámetro (15 min); calificación con plazo
  configurable (7 días). Decisiones propias anotadas: el viaje nace al aceptar; el candidato manual no se descarta por un
  rechazo previo; monitoreo con horizonte de 2 h.
- Dominio: `Asignacion.Aceptar(viajeId)` devuelve el viaje; `Iniciar/Finalizar(viaje, ahora)`; `Viaje.RegistrarLlegada`;
  `Candidato.RechazoLaSolicitud` y `AvailabilityRule` v3; módulo `Viajes` (`ViajeDominio`, `ReglasViaje`,
  `IViajeRepositorio`, `ICalificacionRepositorio`); parámetros `asignacion.plazo_respuesta_minutos` y `calificacion.plazo_dias`.
- Aplicación: Aceptar, Rechazar, ConsultarSinRespuesta, Vencer, Iniciar, Finalizar, RegistrarParada, RegistrarLlegada,
  ReportarIncidente, Calificar; Buscar, Consultar, Monitorear y Trazabilidad (`IConsultasViajes`). Reasignar, abandonar
  turno y cancelar solicitud ahora cancelan el viaje programado.
- Infraestructura: `ConsultasViajesEf` (filtra sobre filas con inicializadores y arma el registro al final), worker vence
  antes de cada vuelta. API: `/api/asignaciones/{id}/aceptar|rechazar`, `/api/viajes`, `/api/viajes/monitoreo`,
  `/api/viajes/{id}` y sus acciones, `/api/solicitudes/{id}/trazabilidad`. Sin migración.
- Entorno local: la API arranca como `tenant-a` y el worker corre el vencimiento y la cola sin errores.
- Gates al cierre: build sin advertencias, formato OK, 400 unitarias (94,7 % / 86,8 %), 16 arquitectura, 111 integración;
  Spike 2 en verde 5 veces seguidas.
- Commit `71fd5e7` → PR #9 fusionada en `develop` el 2026-10-18 (merge `bc7d822`) con el CI en verde. **Fase 6 cerrada.**

## Sesión 2026-10-18 (continuación): Fase 7 (Canal WhatsApp)
- Rama `feature/RF-14-whatsapp` (llevó también la documentación que cerraba la Fase 6).
- Decisiones del usuario (roadmap 2026-10-18): bandeja de entrada + worker; menú numerado con pasos guiados; los cuatro
  flujos; para probar con el celular, número de prueba de Meta + ngrok (el usuario ya tiene cuenta de desarrollador de
  Meta; la configuración real queda como paso opcional con `docs/GUIA-WHATSAPP-META.md`). Decisiones propias anotadas:
  duración por defecto de 60 min, teléfono E.164, 30 min de inactividad, teléfonos del canal cifrados.
- Dominio `Canal`: `MensajeEntrante`, `MensajeSaliente` (reintentos y correlación), `Conversacion` (paso + datos),
  `EntradaConversacion` (opciones, fechas "hoy/mañana/dd/mm hh:mm", E.164), repositorios.
- Aplicación: `IWhatsAppGateway`, `BotWhatsApp` (llama a los casos de uso de solicitudes y viajes), Recibir webhook,
  Simular, Procesar entrante, Registrar fallo, Enviar saliente, Pendientes y Conversación. `IUnidadTrabajo.DescartarCambios`.
- Infraestructura: `FormatoWebhookMeta`, `WhatsAppSandboxAdapter` (Graph API, errores transitorios/permanentes),
  `WhatsAppSimuladoAdapter`, `WorkerCanalWhatsApp` (cada 2 s), migración `CanalWhatsApp`. API: `/api/whatsapp/webhook`
  (GET verificación, POST con firma), `/api/whatsapp/conversaciones/{telefono}` y el simulador en Development.
- Entorno local: `migrar-todos` aplicó `CanalWhatsApp` a tenant-a/b; con la API en Development, un mensaje simulado de un
  número no registrado recibió el aviso (teléfono cifrado en la base). Los datos de esa prueba se borraron.
- **Prueba real con WhatsApp (2026-10-18):** número de prueba de Meta + ngrok + webhook firmado; el bot respondió en el
  celular del usuario. Hizo falta: actualizar ngrok (≥ 3.20), suscribir la cuenta de WhatsApp a la app
  (`subscribed_apps`), un token permanente de usuario del sistema y reiniciar la API tras cambiar secretos. La guía
  `docs/GUIA-WHATSAPP-META.md` quedó con esos pasos.
- **Incidente:** los secretos que la CLI había creado desde un proceso que no era la terminal del usuario estaban en una
  copia aislada del AppData (virtualización de una app empaquetada); al borrarla se perdió la clave de cifrado de tenant-a y se recreó tenant-a con
  la CLI corrida por el usuario. Desde entonces los secretos y el aprovisionamiento los corre el usuario.
- Al probar se vio que la API aceptaba cualquier texto como teléfono: ahora se valida y normaliza a E.164
  (`TelefonosWhatsApp.Normalizar` en los adaptadores de entrada de usuarios, pasajeros y conductores).
- `FlowRide.API.http`: la variable de host se llama `host` (Rider no admite puntos en los nombres) y el entorno `local`
  (`http-client.env.json`) inicia sesión en Keycloak con PKCE para obtener el token sin Panel Web.
- Gates al cierre: build sin advertencias, formato OK, 456 unitarias (91,9 % / 82,6 %), 16 arquitectura, 126 integración.
- Commit `9ff280f` → PR #10 fusionada en `develop` el 2026-10-18 (merge `7a7bc80`) con el CI en verde. **Fase 7 cerrada.**

## Sesión 2026-10-18 (continuación): Fase 8 (Notificaciones y alertas)
- Rama `feature/RF-43-notificaciones` (llevó también la documentación que cerraba la Fase 7).
- Decisiones del usuario (roadmap 2026-10-18): todos los avisos de la matriz; correo para urgente sin candidatos, accidente
  o falla mecánica, turno abandonado y WhatsApp fallido; plantilla aprobada o error con alerta fuera de la ventana; alertas
  guardadas + SSE.
- Dominio `Notificaciones`: `Alerta` (secuencia identity para el envío en vivo), `CorreoSaliente`, `PlantillaWhatsApp`,
  `VentanaConversacion`; `MensajeSaliente` admite plantilla, clave del mensaje y evento de origen.
- Aplicación: `CatalogoMensajes` (recurso embebido `mensajes.es-CO.json`, `WithCulture=false` para que no vaya a un
  ensamblado satélite), `GatewayNotificaciones`, `ProcesarEventoCasoUso`, correos, alertas y plantillas.
  `IBandejaSalida.ProgramarAsync`; nuevos eventos `solicitud.cancelada`, `asignacion.cancelada`, `canal.envio_fallido`.
- Infraestructura: `WorkerNotificaciones` (SKIP LOCKED, misma transacción), `EnviadorCorreoSmtp`, `UnidadTrabajoEf` admite
  transacción ya abierta, migración `Notificaciones` (siembra 11 plantillas Pendientes). API: `/api/alertas`
  (lista, `/en-vivo` SSE, `/leida`) y `/api/notificaciones/plantillas`. `docker-compose`: Mailpit publica 1025.
- Gates al cierre: build sin advertencias, formato OK, 480 unitarias (91,9 % / 81,8 %), 16 arquitectura, 130 integración.
- **Pendiente en el entorno local (lo corre el usuario):** `docker compose up -d` (publica el SMTP de Mailpit) y
  `dotnet run --project src/FlowRide.Aprovisionamiento -- migrar --id tenant-a` (migración `Notificaciones`).
- Commit `dd22b39` → PR #11 fusionada en `develop` el 2026-10-18 (merge `6694d26`) con el CI en verde. **Fase 8 cerrada.**

## Sesión 2026-10-19: Fase 9 (Reportes y vista operacional)
- Rama `feature/RF-46-reportes` (lleva también la documentación que cerraba la Fase 8).
- Decisiones del usuario: CSV, síncrono, Administrador y Operador logístico, los cuatro grupos de indicadores.
- Dominio `Reportes`: `PeriodoReporte` (≤ 93 días), `ReglasReportes` (grupo de la solicitud, puntualidad, porcentajes,
  semáforo de la vista centralizada, máximo 50 000 filas), datos de los casos de uso. Parámetro nuevo
  `reporte.tolerancia_puntualidad_minutos` (10) y acción de auditoría `reporte.exportar`.
- Aplicación: `IConsultasReportes`, `ICacheReportes`, `GeneradorReportes`, casos de uso de la vista, indicadores, los
  cuatro reportes y el exporte (`ArchivoCsv`). `IConsultasViajes.ListarAsync` (sin paginar).
- Infraestructura: `ConsultasReportesEf` (en dos pasos, sin subconsultas por fila), `CacheReportesMemoria`, `AddReportes`.
  API: `/api/reportes/…` y `POST /api/reportes/{tipo}/exportar`. Sin migración.
- Gates al cierre: build sin advertencias, formato OK, 516 unitarias (93,9 % / 83,0 %), 16 arquitectura, 133 integración
  (la nueva `ReportesApiTests` usa PostgreSQL y Azurite reales y descarga el CSV con el enlace firmado).
- Commit `91cd691` → PR #12 fusionada en `develop` el 2026-10-19 (merge `9e0c74e`) con el CI en verde. **Fase 9 cerrada.**

## Sesión 2026-10-19 (continuación): Fase 10 (Borde común)
- Rama `feature/RF-19-borde-comun` (lleva la documentación que cerraba la Fase 9).
- Decisiones del usuario: rutas en el catálogo, firma en borde y stack, WAF gestionado en la Fase 12, `*.dev.localhost` con HTTPS.
- Dominio: `RutaOperador` y `TablaRutas` (Plataforma), datos `Borde`. Aplicación: `RegistrarRutaOperadorCasoUso` y
  `ConsultarRutasOperadorCasoUso` (Tenancy, para la CLI), `CargarTablaRutasCasoUso` y `EnrutarWebhookCasoUso` (Borde),
  puerto de salida `IVerificadorWebhook`. Infraestructura: tabla `plataforma.ruta_operador` (migración `RutasBorde`),
  `RutaOperadorRepositorioAdaptador`, `VerificadorWebhookMeta`, `FormatoWebhookMeta.NumerosDestino`, `AddBordeComun`.
- Proyecto `FlowRide.Borde` (YARP 2.3.0): `ProveedorRutasBorde` + `RecargaRutasBorde`, router de webhooks, límites de
  tasa y cabeceras de seguridad. CLI: `registrar-ruta` y `listar-rutas`. Guía de Meta §7 (ngrok apunta al borde).
- Problema: el SDK web de .NET 10 hace público el `Program` de nivel superior y chocaba con el de la API en las pruebas;
  el borde usa `ProgramaBorde` con `Main` explícito.
- Gates al cierre: build sin advertencias, formato OK, 532 unitarias (94,1 % / 83,2 %), 17 arquitectura, 139 integración
  (`BordeApiTests`: borde real → API real del stack → Silo).
- **Pendiente en el entorno local (lo corre el usuario):** cualquier comando de la CLI migra el catálogo; después,
  `registrar-ruta` y los secretos del borde (guía §7).
- Commit `3ec3a87` → PR #13 fusionada en `develop` el 2026-10-19 (merge `21cc8bb`) con el CI en verde. **Fase 10 cerrada.**

## Sesión 2026-10-20: requisitos de los videos del profesor — fechas con zona
- El profesor envió 5 videos: fechas y horas, `.env` y secretos, CSRF, autenticación rota (serie OWASP "las 5
  vulnerabilidades") y reverse proxy vs load balancer vs API gateway. Revisión punto por punto en ROADMAP ("Requisitos
  del profesor").
- Rama `feature/RF-09-fechas-con-zona` (lleva la documentación que cerraba la Fase 10).
- Error encontrado y corregido: el día del servicio salía de la hora UTC (`SolicitudViaje.FechaServicio` y
  `FiltrarDisponibles`), así que la regla regulatoria del motor descartaba licencias o SOAT que vencían esa noche.
- `FechaHoraLocal` (Domain, hora local al minuto + nombre IANA; rechaza desfases y horas que no existen; en horas
  repetidas toma la primera). Solicitud, turno, parada e itinerario guardan hora local + zona; el instante UTC es
  derivado. `RecalcularHorariosCasoUso` + `RecalculoHorariosAlIniciar` (Infrastructure) lo ponen al día al arrancar;
  las asignaciones vigentes siguen a su servicio. `HorasLocales` (Application) toma la zona del operador por defecto.
- API: horas locales sin desfase + `zonaHoraria`; respuestas con instante y hora local. Migración `FechasConZona` con
  relleno de datos (America/Bogota). En pruebas, `HorasDePrueba` (`.Local()`, `.HoraLocal()`).
- Gates: build sin advertencias, formato OK, 547 unitarias (94,1 % / 83,2 %), 17 arquitectura, 140 integración.
- **Pendiente en el entorno local (lo corre el usuario):** `dotnet run --project src/FlowRide.Aprovisionamiento -- migrar --id tenant-a`
  (migraciones `Notificaciones` si faltaba y `FechasConZona`).
- Commit `46cb4bd` → PR #14 fusionada en `develop` el 2026-10-20 (merge `18039d9`) con el CI en verde. **Fechas cerradas.**

## Sesión 2026-10-20 (continuación): seguridad OWASP
- Rama `feature/RF-02-seguridad-owasp` (lleva la documentación que cerraba las fechas).
- Hook `.githooks/pre-push` con gitleaks (local o Docker, imagen fija v8.30.1) y `.gitattributes` con LF para los hooks.
  Probado en un repo desechable: bloquea un token falso. `core.hooksPath` ya activado en el clon del usuario.
- Keycloak: política de contraseñas (12+, argon2, historial 5), bloqueo tras 5 fallos, TOTP; `CONFIGURE_TOTP` para el
  Administrador al crearse o al cambiar a ese rol (`RolesSistema.ExigeDobleFactor`, `IProveedorIdentidad.ExigirDobleFactorAsync`).
- API: `Politicas.AutenticacionReciente` (claim `auth_time` ≤ 5 min) con el desafío 401 de RFC 9470 en las acciones
  sensibles del Administrador. Hallazgo: el token del grant de contraseña directa no trae `auth_time`; se agregó el mapper
  `auth_time` al cliente del panel y un inicio de sesión tipo navegador (código + PKCE) en `KeycloakFixture`; Keycloak
  marca sus cookies como Secure y por http se reenvían a mano.
- Borde: `ValidadorTokensBorde` + `UseTokenDelOperador` en el pipeline de YARP (metadato `operador` en cada ruta).
- Gates: build sin advertencias, formato OK, 548 unitarias (94,1 % / 83,2 %), 17 arquitectura, 142 integración.
- **Pendiente en el entorno local (lo corre el usuario):** volver a correr `crear-operador` de tenant-a con las opciones
  `--admin-*` (aplica la política nueva al realm y le exige TOTP al administrador existente) y, para el borde, nada nuevo
  salvo que `FlowRide:Identidad:UrlKeycloak` ya está en `appsettings.Development.json` del borde.
- Commit `a57bf8e` → PR #15 fusionada en `develop` el 2026-10-20 (merge `eb6c879`) con el CI en verde. **Seguridad cerrada.**

## Sesión 2026-10-21: Fase 11a (base del Panel Web + Operación)
- Decisiones del usuario: Mantine, tres entregas (11a operación, 11b administración, 11c configuración y reportes),
  pruebas unitarias con axe, tema claro y oscuro. Decidido por mí: oidc-client-ts con tokens en memoria, openapi-fetch +
  openapi-typescript, proxy de Vite a la API (sin CORS).
- Backend para el contrato: `TypedResults` en los endpoints, `JsonStringEnumConverter` en los 26 enums, números estrictos
  y OpenAPI también en Testing; `ContratoOpenApiTests` genera o compara `web/src/api/openapi.json`.
- Versiones: las majors más nuevas (TS 7, Vite 8, React Router 8, Mantine 9, Vitest 5) se evitaron por compatibilidad; se
  fijaron versiones exactas con al menos dos semanas de publicadas. npm 11 bloquea scripts de instalación (esbuild funciona sin él).
- axe encontró que las X de cerrar de Mantine no tenían nombre: se corrigió en el tema. En pruebas, `env="test"` de Mantine.
- Prueba local del usuario: login, solicitud por WhatsApp visible en el panel y asignación tras crear un conductor de prueba (`.http`).
  El pasajero no recibió confirmación porque el aviso "conductor asignado" sale con `asignacion.aceptada`, no con
  `asignacion.creada` (diseño de la Fase 8), y el conductor de prueba tiene un número falso (alerta `whatsapp.fallido`).
- Rediseño pedido por el usuario (más elaborado): tablero `/` con indicadores (`/api/reportes/indicadores`), gráficas
  por hora y por estado, listas de lo pendiente; la vista del día pasa a `/dia`. Marco con logo, avatar y contadores
  por sección (`aria-description`). Detalle de solicitud con conductor asignado, aceptar o rechazar en nombre del
  conductor (Operador logístico), decisiones del motor y estados; detalle de viaje con etapas, puntualidad, paradas,
  incidentes y calificación. Dependencias nuevas: `@mantine/charts` 8.3.18 y `recharts` 2.15.4 (chunk `graficas`).
  Pruebas del frontend: 41.
- Estilo visual pedido por el usuario (referencia "quiosco de transporte" en blanco y negro): primario negro en claro y
  blanco en oscuro (`virtualColor`), cabecera negra, tarjetas gris niebla `#f6f6f6` sin borde ni sombra, radio 8 px,
  píldora solo en selectores de modo, pestaña activa `#9dcdd6`, Inter autohospedada (`@fontsource-variable/inter`
  5.2.8), títulos grandes en peso 400. Se mantienen los colores con significado (semáforo, severidad, estados) por
  OPLOG-UX-02 y ACC-02, y el tema oscuro. No se copió marca, nombre ni tipografía propietaria de la referencia.
- Commit `f2988bf` → PR #16 fusionada en `develop` el 2026-10-21 (merge `67083ef`) con el CI en verde (Backend,
  Frontend y escaneo de secretos). **Fase 11a cerrada.**

## Sesión 2026-10-21 (continuación): Fase 11b-1 (administración: usuarios, empresas y pasajeros)
- Decisiones del usuario: RF-24 edita nombre, WhatsApp y correo (el correo se sincroniza con Keycloak y se vuelve a
  confirmar); pantallas con lista + panel lateral; el Operador logístico consulta y programa turnos, Usuarios es solo del
  Administrador; la 11b se entrega en dos PR (11b-1 usuarios, empresas y pasajeros; 11b-2 conductores, vehículos e
  itinerarios).
- Backend RF-24: `ActualizarUsuarioCasoUso` (proveedor primero para que el reintento sea idempotente; la auditoría
  `usuario.actualizar` guarda qué campos cambiaron, no sus valores, ADR-0013), `IProveedorIdentidad.ActualizarCuentaAsync`
  (cambia `email` y `username` juntos porque el realm usa el correo como usuario, `emailVerified=false`, acción
  `VERIFY_EMAIL` y correo a la nueva dirección con lo pendiente) y `PUT /api/usuarios/{id}` con autenticación reciente.
  Pruebas: unitarias del caso de uso y del adaptador, Keycloak real (cambio de correo) y autorización de la API.
- Panel: `web/src/administracion/` (piezas comunes `PanelLateral`, `Confirmacion`, `MenuAcciones`, `PiePagina`),
  `PaginaUsuarios` (cifras por rol, crear, editar, cambiar rol y activar o desactivar con confirmación; uno mismo no se
  cambia rol ni estado), `PaginaEmpresas` y `PaginaPasajeros` (solo lectura para el Operador logístico). El menú tiene el
  grupo Administración y la ruta de Usuarios está protegida para otros roles.
- Al correr la integración completa Docker Desktop estaba apagado (DockerUnavailableException); las pruebas de RF-24
  (Keycloak y autorización) ya habían pasado antes con Docker encendido.
- Commit `8e6933e` → PR #17 fusionada en `develop` el 2026-10-21 (merge `ff42931`). Con Docker de nuevo arriba, la
  integración completa pasó (145) y el CI de la rama quedó en verde. **Fase 11b-1 cerrada.**

## Sesión 2026-10-22 a 2026-10-24: documentación para la entrega (rama `docs/entrega-documentacion`)
- El usuario debe entregar el 2026-10-28 toda la documentación en Markdown y pidió priorizarla sobre la 11b-2. Decisiones:
  documentación en `docs/` del repo; diagramas en Mermaid dentro de los `.md`; no se hacen las secciones que la plantilla
  del profesor (`Downloads/Plantilla Documento de Arquitectura de Software (DAS).docx`) marca «(NO)» (propósito, vista
  funcional con la incepción ágil, diagramas de clases, objetos y colaboración); ADR-0020 nuevo para fechas y revisiones
  fechadas en ADR-0003 (seguridad OWASP, RF-24), ADR-0010 (aprovisionamiento por CLI) y ADR-0018 (gitleaks).
- Conversión sin pandoc con convertidores propios (en el scratchpad, fuera del repo): DAS `.docx` → `DAS.md` (títulos
  numerados como en Word, 67 tablas), los 4 Excel de `OneDrive - UCO/FlowRide/Entregables` → `.md`, y las 40 tablas de
  ancho fijo de los drivers → tablas Markdown (GitHub no dibuja las de pandoc).
- Correcciones: canal WhatsApp en 99,5 % (ADR-0010); RF-55/RF-56 y todo lo de GPS/ETA marcado fuera de V1 (se conserva
  por trazabilidad); nombres reales de adaptadores; línea base reescrita con lo implementado; PoC/Spikes con su evidencia;
  hoja de ADR del trade-off (0003/0004/0008 aceptados, 0018–0020 agregados). Las hojas del taller del trade-off (lluvia de
  escenarios, votos, banco de 164) no se tocaron.
- 8 diagramas Mermaid validados con el parser oficial (mermaid 11.17.2): componentes, paquetes (referencias reales de los
  `.csproj`), 5 secuencias (WhatsApp → asignación, aceptación del conductor, abandono de turno, login con reautenticación,
  alta de operador) y despliegue.
- Los 20 ADR tienen las 10 secciones y una subsección «Implementación en FlowRide (estado al 22-10-2026)»; índice de la
  entrega en `docs/README.md`. ROADMAP: RF-20, RF-22, RF-23 y RF-52 pasan a ✅; se agregan la entrega de documentación y
  la demostración con HTTPS local (Fase 12, basta con local según el usuario).
- 2026-10-24: modelo entidad-relación nuevo (`docs/diagramas/modelo-entidad-relacion.md`, 6 erDiagram) generado del
  esquema real de PostgreSQL con las FK reales; reemplaza el modelo de diseño del 13-10, que estaba desactualizado. Se
  borraron del repo los diagramas viejos (PNG de clases/ER, PDF, drawio y su generador, figuras de la v2.0 del DAS); siguen
  en el historial de git. Cada diagrama se dibujó con Edge sin interfaz y se revisó con un detector propio de líneas que
  atraviesan cajas; se rehicieron paquetes (flechas por grupo) y despliegue (un solo stack representativo) y se
  ajustaron las secuencias (pasos por el borde, lectura del mensaje saliente).
- PR #18 fusionada en `develop` (merge `412dc65`, CI en verde); rama borrada. Fuera del repo quedó en el escritorio la
  carpeta de la entrega con el DAS también en Word (con el modelo entidad-relación como Anexo A) y los 14 diagramas en
  PNG; esos dos se generan con scripts del scratchpad, no se versionan.

## Sesión 2026-10-24 (continuación): Fase 11b-2 (conductores, vehículos e itinerarios en el panel)
- Rama `feature/RF-07-panel-recursos`. Solo panel: el backend de la Fase 3 y la 4 ya tenía todo lo necesario.
- Decisiones del usuario: turnos con programar y registrar abandono (sin cancelar turnos programados); itinerarios solo
  registrar y consultar (sin editar ni eliminar); las tres pantallas en el grupo Administración.
- `PaginaConductores` (lista, alta, edición y cambio de estado con confirmación) con `DetalleConductor` (turnos vigentes y
  los últimos 5 terminados; programar en hora local sin desfase, ADR-0020; abandono con motivo solo para el Operador
  logístico porque el endpoint exige ese rol; documentos: cargar PDF/PNG/JPG ≤ 5 MB, descargar con el enlace firmado en
  otra pestaña y eliminar con confirmación, solo el Administrador carga y elimina). `PaginaVehiculos` (vencimientos de
  RTM y SOAT, activar/desactivar, conductor habitual: el motor le asigna siempre ese vehículo y no lo da a otros).
  `PaginaItinerarios` (filtros por empresa, código y fechas; registro por rango y días; las horas viajan como `HH:mm:ss`).
- Comunes nuevos: `InsigniaVencimiento` (vencido / vence pronto a 30 días, con texto), `InsigniaConductor`,
  `useConductoresActivos`, `fechaCorta` y `diasHasta`. El botón de cerrar de los paneles laterales ahora tiene nombre
  accesible (axe lo marcaba).
- Pruebas: 7 nuevas de pantallas y 2 de fechas (59 en total). Hallazgos del entorno de pruebas: las notificaciones de
  Mantine son un almacén global y, pasadas cinco, las nuevas quedan en cola (se limpian después de cada prueba en
  `preparacion.ts`); en jsdom el cuerpo multipart no se puede leer de vuelta (FormData de jsdom con Request de Node),
  así que la prueba de carga verifica los campos que el panel agrega al formulario.
- 2026-10-26: el usuario revisó las tres pantallas en vivo con el stack local (programó un turno desde Conductores).
- **Prueba de punta a punta por WhatsApp cerrada (pendiente desde la 11a):** con ngrok desde la red de la casa del
  usuario, el pasajero de pruebas pidió y confirmó un servicio por WhatsApp, el motor lo asignó automáticamente, el
  conductor de pruebas lo aceptó desde su celular (mismo número del bot; el bot distingue el rol por el número que
  escribe) y el mensaje `solicitud.pasajero.conductor_asignado` salió a Meta sin errores; el viaje quedó Programado.
- Hallazgo pendiente: un turno del 2026-10-21 que ya terminó sigue en estado `Programado` (no pasa a Finalizado). No
  afecta la asignación (el motor exige que el turno cubra la franja), pero hay que revisar quién actualiza ese estado.
- Pregunta del usuario sobre «America/Bogota»: es el nombre IANA de la zona (sale de `FlowRide:ZonaHoraria` del
  operador), no la ciudad del servicio; se deja así. Mostrar un nombre amigable sería un cambio de presentación a
  anotar en ADR-0020.
- PR #19 fusionada en `develop` el 2026-10-26 (merge `74f04fc`, CI en verde). **Fase 11b-2 cerrada.**

## Sesión 2026-10-26 (continuación): guía de desarrollo y archivos locales fuera del repo
- Rama `chore/documentacion-desarrollo`. A pedido del usuario, los archivos de configuración del asistente salen del
  repositorio: se quitan del índice (`git rm --cached`) y se ignoran con `.git/info/exclude` (local, para que ni el
  `.gitignore` los nombre); siguen en el disco del usuario. La historia anterior no se reescribe.
- La documentación técnica que vivía en esa configuración pasa a `docs/DESARROLLO.md` (producto, arquitectura, estilo de
  puertos y adaptadores, módulos por fase, convenciones, flujo de trabajo, comandos y entorno local), enlazada desde
  `docs/README.md`. Se quitaron las menciones al asistente en esta memoria y en `FlowRide.API.http`.
- Contexto: tras reescribir la historia (sin líneas `Co-authored-by`), la barra «Colaboradores» de GitHub seguía
  mostrando un segundo colaborador aunque la API de colaboradores ya devuelve solo la cuenta del usuario. Soporte de
  GitHub no atiende esa limpieza en cuentas gratuitas; queda esperar a que la caché se recalcule.

## Próximos pasos
1. Revisar el turno que queda `Programado` después de terminar (hallazgo de la prueba del 2026-10-26).
   **Recordatorio (2026-10-18):** itinerarios en PDF o Word por WhatsApp quedan fuera de V1; el bot responde que solo
   entiende texto y los itinerarios se registran desde el panel (RF-08). Opciones para después: recibir el archivo y
   alertar al operador, o extracción automática (requeriría ADR nuevo).
2. Fase 11c: parámetros, plantillas de WhatsApp, configuración del motor, indicadores y reportes con exporte.
3. Pendiente de la 11a: marcar el job "Frontend" como requerido en `develop` y actualizar las herramientas de pruebas
   con alertas de `npm audit` (vitest, postcss, tinypool; solo desarrollo).
4. Fase 12: credencial de objetos limitada al contenedor de cada operador (en local se comparte la cuenta de Azurite).
5. Opcional: SonarCloud (crear el proyecto y el secreto `SONAR_TOKEN`).
