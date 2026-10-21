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
- `FlowRide.Infrastructure` → Application. EF Core, Npgsql, implementaciones de puertos. `Persistencia/` (`FlowRideDbContext` del Silo, esquemas `ops` y `audit`, migraciones, `UnidadTrabajoEf`), `Tenancy/` (catálogo de plataforma, servidor de Silos, secretos por operador), `Identidad/` (repositorio de usuarios y adaptadores de Keycloak con la plantilla `plantilla-realm.json`) y `Auditoria/`.
- `FlowRide.API` → Application, Infrastructure. Endpoints HTTP y composición (DI). Es el stack de **un** operador: nunca usa el catálogo, el aprovisionamiento ni la cuenta de administración de Keycloak (regla de arquitectura). Todo lo que cuelga de `/api` exige un token del realm del operador.
- `FlowRide.Aprovisionamiento` → Application, Infrastructure. Herramienta de plataforma (CLI) para aprovisionar y migrar los Silos y crear sus realms (RF-03). El primer administrador lo crea con la composición del stack del operador (`FabricaStackOperador`).
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
8. En el Silo, los repositorios dejan los cambios pendientes y el caso de uso confirma con `IUnidadTrabajo`: el cambio y su `EventoAuditoria` se guardan en la misma transacción, que sella la cadena de hashes.
Las pruebas de `EstiloHexagonalTests` hacen cumplir los puntos 1, 2, 3 y 5.

## Identidad y auditoría (Fase 2)
- Keycloak, un realm `flowride-<tenant>` por operador, creado por la CLI desde `plantilla-realm.json`: cliente público `flowride-panel` (PKCE) con los claims `tenant_id` y audiencia `flowride-api`; cliente de servicio `flowride-api` con el que la API gestiona las cuentas de su realm (su secreto está en el almacén del operador).
- Cada petición: token válido (firma, emisor, audiencia `flowride-api`, vigencia de 5 min) → `tenant_id` igual al del stack → usuario interno activo buscado por `sub`, **sin caché** (revocación inmediata, ADM-SEG-01). El rol sale de FlowRide (`RolesSistema`, Id fijos sembrados por migración), no del token.
- Políticas: `Administrador` (gestión de usuarios y auditoría) y `PanelWeb` (Administrador u Operador logístico).
- Auditoría en `audit.evento_auditoria`: solo INSERT (trigger), cadena SHA-256, sin datos personales en los valores; `GET /api/auditoria/verificacion` la comprueba.

## Recursos operativos, parámetros y cifrado (Fase 3)
- Módulos `Operacion` (empresas cliente, pasajeros, conductores, turnos, documentos, vehículos, disponibilidad) y `Parametros`. En Operación hay un puerto de entrada por recurso (`I<Recurso>PuertoEntrada`) con varias operaciones; la API los registra con `AddOperacion` (requiere `AddIdentidad`).
- Cifrado de campos (ADR-0014): `CifradorCampos` (AES-256-GCM determinista, IV por HMAC, claves derivadas con HKDF de la clave maestra del operador `FlowRide:Cifrado:Claves:v<N>` + `FlowRide:Cifrado:Vigente`). Lo aplica EF con conversiones sobre `Usuario.TelefonoWhatsApp` y `Conductor.NumeroLicencia`; las búsquedas por valor exacto siguen funcionando. Un modelo de EF por cifrador (`ModeloPorCifrador`). Si se agrega un campo cifrado: conversión en `FlowRideDbContext` + columna en `RecifradorCamposSilo.Campos`.
- Documentos (ADR-0013): `IAlmacenObjetos` sobre Azure Blob (Azurite en local), bucket `flowride-<tenant>`, prefijo `documents/`, URL firmada ≤ 15 min; el adaptador de entrada valida tipo, tamaño (≤ 5 MB) y firma del archivo.
- Parámetros (ADR-0016): catálogo en código (`CatalogoParametros`), `ops.parametro` solo guarda los cambiados (versión = token de concurrencia), caché `IMemoryCache` con TTL ≤ 60 s (`FlowRide:Cache:TtlParametrosSegundos`) invalidada tras confirmar.
- Pruebas: cada prueba que reaprovisiona un operador usa un tenant propio (un entorno nuevo genera una clave de cifrado nueva).

## Solicitudes e itinerarios (Fase 4)
- Módulo `Solicitudes` con **CQRS ligero**: escribir pasa por el agregado `SolicitudViaje` (`ISolicitudRepositorio`, puerto del dominio); leer, por `IConsultasSolicitudes` (Application, salida), con proyecciones sin rastreo a `SolicitudLectura`. La API lo registra con `AddSolicitudes` (requiere `AddOperacion`).
- Reglas: datos mínimos y servicio futuro (RF-29); sin solicitudes activas solapadas del mismo pasajero; modificar solo sin asignar y dentro de la ventana (`solicitud.ventana_cancelacion_minutos`); cancelar con motivo, y fuera de la ventana solo el Operador logístico; cancelar una asignada cancela su asignación vigente.
- Visibilidad (PAS-SEG-01, `solicitud.visibilidad_personales`): las personales llegan sin direcciones (`DetalleRestringido`) a quien no puede verlas; el Administrador nunca las ve.
- Itinerarios: un rango de fechas y días crea un itinerario por fecha; su hora es local del operador (`FlowRide:ZonaHoraria`, por defecto `America/Bogota`, `ZonaHorariaOperador`).
- Los conflictos de concurrencia optimista (`DbUpdateConcurrencyException`) se devuelven como 409.

## Asignación de punta a punta (Fase 5)
- Módulo `Asignaciones` (Spanish) sobre el motor `Assignment` (English, puro). `AsignarSolicitudCasoUso`: candidatos (`AsignacionDominio.Candidatos`: vehículo habitual o cada vehículo libre), motor con la configuración activa, y en una transacción decision log (`motor.*`) + asignación o sin candidatos + evento en la Outbox (`notif.outbox_mensaje`, `IBandejaSalida`).
- Concurrencia (ADR-0007): las exclusiones de franjas de `ops.asignacion` y la de configuración activa son `DEFERRABLE INITIALLY DEFERRED`; `UnidadTrabajoEf` traduce token de versión y 23P01 a `ConflictoConcurrenciaExcepcion` (la API responde 409; el worker reintenta en la siguiente vuelta).
- Worker `WorkerColaAsignacion` (BackgroundService del stack, cada 15 s, urgentes primero); `FlowRide:Asignacion:Worker:Habilitado=false` lo apaga (las pruebas lo apagan y llaman `ProcesarAsync`).
- Reglas: `IAssignmentRule` tiene `Version`, `Obligatoria` y `Dependencias`; las cuatro eliminatorias son obligatorias; `ConfigurationValidator` revisa obligatorias, dependencias y ciclos. Cambiar la lógica de una regla = subir su `Version`.
- Configuración del motor versionada (RF-21): `ConfiguracionAsignacion` (Borrador → Activa → Archivada; v1 sembrada por migración), rollback = activar una versión anterior. Parámetros por regla en `regla_configurada.parametros` (p. ej. `ventanaDias` del balance de carga).
- RF-12 (solo Operador logístico): reasignar, asignar a mano o devolver a la cola, con motivo y auditoría; el candidato manual pasa por el mismo motor (decision log manual). Abandonar un turno devuelve sus solicitudes a la cola.

## Viajes y calificación (Fase 6)
- Módulo `Viajes`: el viaje nace al **aceptar** la asignación (`Asignacion.Aceptar(viajeId)` → `Viaje` Programado); `Iniciar`/`Finalizar` mueven viaje, asignación y solicitud juntos. Paradas e incidentes viven en el agregado `Viaje`; la calificación (RF-54) es una por viaje y pasajero, dentro de `calificacion.plazo_dias`.
- El conductor y el pasajero lo hacen por WhatsApp (Fase 7); el Operador logístico puede hacerlo también desde el panel en su nombre: aceptar/rechazar, iniciar/finalizar, paradas, incidentes y calificación. Ambos canales usan los mismos casos de uso.
- Rechazo: la asignación queda Rechazada, la solicitud vuelve a la cola y `AvailabilityRule` v3 descarta a quien la rechazó (el candidato manual no). Sin respuesta en `asignacion.plazo_respuesta_minutos` = rechazo del sistema (`asignacion.vencer`); lo dispara `WorkerColaAsignacion` antes de cada vuelta de la cola.
- Cancelar la solicitud, reasignar o abandonar el turno cancela también el viaje programado (`CancelarProgramadoDeAsync`).
- Lectura (`IConsultasViajes`): historial (RF-31), monitoreo (RF-37, en curso + programados de las próximas 2 h, retrasados marcados, sin GPS) y trazabilidad (RF-40: historial de la solicitud + decision log + asignaciones + auditoría + paradas, incidentes y calificación, sin direcciones). Misma visibilidad de personales que las solicitudes.

## Canal WhatsApp (Fase 7)
- Módulo `Canal` (ADR-0008): `IWhatsAppGateway` (Application) con dos adaptadores en Infrastructure: `WhatsAppSimuladoAdapter` (por defecto, pruebas y desarrollo) y `WhatsAppSandboxAdapter` (número de prueba de Meta Cloud API). Ambos leen el webhook con `FormatoWebhookMeta` (firma `X-Hub-Signature-256` y payload de Meta); Application solo ve `MensajeRecibido` y `EstadoEntrega`.
- Webhook `POST /api/whatsapp/webhook` (sin token del realm, autenticado por la firma): guarda los mensajes en `canal.mensaje_entrante` (único por id externo, PoC 4) y responde de inmediato. `WorkerCanalWhatsApp` (cada 2 s) los procesa con `BotWhatsApp` y envía `canal.mensaje_saliente` con reintentos (5 s, 30 s, 2 min, 10 min; 5 intentos) y correlación por id externo y estado de entrega.
- Bot: menú numerado y pasos guiados (`Conversacion`, una por teléfono, vuelve al menú tras 30 min). Identifica al usuario por su teléfono E.164 (pasajero o conductor activo) y usa los mismos casos de uso del panel; si una regla falla, `IUnidadTrabajo.DescartarCambios` y responde el motivo. Los teléfonos del canal van cifrados como los de `ops.usuario`.
- Configuración `FlowRide:WhatsApp` (`Proveedor`, `PhoneNumberId`) y secretos del operador (`AppSecret`, `VerifyToken`, `AccessToken`). Probar con el celular: `docs/GUIA-WHATSAPP-META.md`. En Development con el simulado existe `POST /api/whatsapp/simulado/mensajes`.

## Notificaciones y alertas (Fase 8)
- Módulo `Notificaciones` (ADR-0015, ADR-0016). `WorkerNotificaciones` toma cada evento de `notif.outbox_mensaje` con `FOR UPDATE SKIP LOCKED` y lo marca procesado en la misma transacción que guarda sus avisos (un reintento no duplica; varias réplicas no se pisan). Errores: reintento con espera creciente, 10 intentos.
- `ProcesarEventoCasoUso` aplica la matriz del 2026-10-18: conductor (asignación nueva, retirada, cancelada), pasajero (conductor asignado, cambio, inicio, fin, retraso, cancelación por el operador), recordatorios (`recordatorio.servicio` programado N min antes con `IBandejaSalida.ProgramarAsync`) y alertas del panel.
- `GatewayNotificaciones`: pasajero/conductor → WhatsApp (bandeja `canal.mensaje_saliente`); dentro de la ventana de 24 h, texto; fuera, la plantilla **aprobada** de la clave (`notif.plantilla_whatsapp`, sembrada Pendiente); si no hay, alerta crítica. Alertas → `notif.alerta` (+ correo a los administradores si es crítica, `notif.correo_saliente`, SMTP `FlowRide:Correo`, Mailpit 1025 en local).
- Textos en `Application/Notificaciones/Catalogo/mensajes.es-CO.json` (recurso embebido; las alertas son `título|detalle`). Panel: `/api/alertas` (lista, `/en-vivo` por SSE, `/{id}/leida`) y `/api/notificaciones/plantillas`.

## Reportes y vista operacional (Fase 9)
- Módulo `Reportes` (RF-36, RF-42, RF-46 a RF-49), para Administrador y Operador logístico (política `PanelWeb`). `IConsultasReportes` lee filas planas del periodo (solicitudes con su última asignación y viaje; asignaciones con su viaje y si vencieron) y `IConsultasViajes.ListarAsync`; `GeneradorReportes` agrega en memoria con `ReglasReportes` (dominio) y aplica la visibilidad de las personales.
- Periodo `PeriodoReporte`: desde (incluido) y hasta (excluido) la hora programada, máximo 93 días y 50 000 filas (ADM-REND-01). Puntualidad con el parámetro `reporte.tolerancia_puntualidad_minutos` (10). Vista centralizada: un día en la zona del operador, semáforo `Critico`/`Atencion`/`Normal` (OPLOG-UX-02).
- Caché (ADR-0006): solo los indicadores de periodos **cerrados**, `ICacheReportes` en memoria con TTL ≤ 10 min (`FlowRide:Cache:TtlIndicadoresSegundos`, 300). Vista, reportes y periodos abiertos van siempre a PostgreSQL.
- Exporte CSV síncrono (`;`, decimales con coma, UTF-8 con BOM, hora local, protección contra inyección de fórmulas) a `exports/<tipo>/<id>/reporte-….csv` del bucket, enlace firmado de 15 min y evento `reporte.exportar`; si la auditoría falla, se borra el archivo. API: `/api/reportes/{vista-centralizada, indicadores, viajes, solicitudes, conductores, empresas}` y `POST /api/reportes/{tipo}/exportar`.

## Borde común (Fase 10)
- Proyecto `src/FlowRide.Borde` (ADR-0012): YARP sin estado ni datos operacionales. Tabla de ruteo en el catálogo (`plataforma.ruta_operador`: subdominio, URL del stack, `phone_number_id`), registrada con `registrar-ruta` de la CLI y releída cada 30 s sin reiniciar (`RecargaRutasBorde`); solo publica operadores activos. Cada host `{subdominio}.{Borde:DominioBase}` lleva únicamente a su stack; un host sin ruta da 404.
- Router de webhooks en `/api/whatsapp/webhook` (cualquier host): handshake, firma `X-Hub-Signature-256` con la app de Meta de la plataforma (`Borde:WhatsApp:AppSecret` / `VerifyToken`), destino por `phone_number_id` (`TablaRutas`, dominio) y reenvío del cuerpo intacto; el stack vuelve a validar la firma. Stack caído → 502 para que Meta reintente; número sin operador → 200 y métrica `flowride.borde.webhooks_sin_operador`; firma inválida → 401 y `flowride.borde.webhooks_rechazados`.
- Seguridad: límites de tasa por IP (panel 300/min, webhook 1200/min, 429 con `Retry-After`), tamaño máximo (10 MB; webhook 1 MB), solo métodos GET/HEAD/POST/PUT/PATCH/DELETE/OPTIONS, cabeceras (`nosniff`, `DENY`, CSP, `Referrer-Policy`, HSTS en HTTPS) y sin `Server`. El WAF (OWASP CRS) es el gestionado de Azure en la Fase 12.
- El `Program` del borde es `ProgramaBorde` con `Main` explícito: el `Program` de nivel superior del SDK web en .NET 10 es público y chocaría con el de la API en las pruebas.

## Seguridad OWASP (requisitos del profesor, 2026-10-20)
- Secretos: hook versionado `.githooks/pre-push` con gitleaks (local o la imagen `zricethezav/gitleaks:v8.30.1` en Docker) que escanea solo lo que se va a publicar; bloquea el push si encuentra algo. Activar una vez por clon: `git config core.hooksPath .githooks`. El CI sigue escaneando todo el historial.
- Keycloak (`plantilla-realm.json`): contraseña de 12+ con mayúscula, minúscula y número, distinta del usuario y el correo, historial de 5 y hash argon2; fuerza bruta: bloqueo tras 5 fallos (espera creciente hasta 15 min); TOTP de 6 dígitos. El Administrador (`RolesSistema.ExigeDobleFactor`) configura su TOTP al crearse o al pasar a ese rol (acción requerida `CONFIGURE_TOTP`).
- Reautenticación (RFC 9470): política `Politicas.AutenticacionReciente` (claim `auth_time` ≤ `FlowRide:Identidad:MinutosAutenticacionReciente`, 5) en crear, activar, desactivar y cambiar rol de usuarios, parámetros, plantillas y configuración del motor. Sin sesión reciente: 401 con `WWW-Authenticate: Bearer error="insufficient_user_authentication", max_age="300"`. El cliente del panel tiene el mapper `auth_time` (el grant de contraseña directa no lo trae: en las pruebas con Keycloak real se usa `IniciarSesionConNavegadorAsync`).
- Borde (parte AUTH del API gateway): a `/api/` de un subdominio solo pasa un token vigente del realm de ese operador (`ValidadorTokensBorde`, claves del realm en caché, `tenant_id` igual al de la ruta); si no, 401 sin llegar al stack. El stack vuelve a validar.
- CSRF: la API solo acepta `Authorization: Bearer` (sin cookies de sesión), así que no aplica; el Panel Web no guardará tokens en cookies ni secretos en `VITE_*`.

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
- **Fechas y horas** (requisito del profesor, 2026-10-20): lo que ya pasó se guarda como instante UTC (`timestamptz`) y se convierte a la zona del usuario solo al mostrarlo. Lo que va a pasar en un lugar (servicio de la solicitud, turnos, hora estimada de paradas, itinerarios) se guarda como hora local + **nombre** IANA de la zona (`FechaHoraLocal`; columnas `timestamp without time zone` + `zona_horaria`), nunca el desfase. El instante UTC de esos eventos es derivado (para consultar e indexar) y `RecalculoHorariosAlIniciar` lo recalcula al arrancar. La API recibe la hora local sin desfase y `zonaHoraria` opcional (por defecto la del operador); una hora con desfase o `Z` da 400. El día de un servicio es el día local (`SolicitudViaje.FechaServicio`), nunca el día UTC.
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
- Hooks de Git (una vez por clon): `git config core.hooksPath .githooks` (gitleaks antes de cada push; necesita gitleaks o Docker en ejecución).
- Entorno local: `cp .env.example .env`, definir contraseñas y `docker compose up -d`. PostgreSQL queda en el **puerto 5433** del host (5432 lo ocupa un PostgreSQL local), Keycloak en **8080** (consola: usuario `KEYCLOAK_ADMIN_USER`), Mailpit en **8025** (ahí llegan los correos de Keycloak) y Azurite en **10000** (documentos; cuenta de desarrollo `UseDevelopmentStorage=true`). Al iniciar el volumen solo se crea el catálogo (`flowride_catalogo`); los Silos y los realms los crea la herramienta.
- Herramienta de aprovisionamiento (secretos de plataforma una sola vez, con los valores de `.env`):
  `dotnet user-secrets set "ConnectionStrings:Catalogo" "Host=localhost;Port=5433;Database=flowride_catalogo;Username=flowride_catalogo;Password=<CATALOGO_DB_PASSWORD>" --project src/FlowRide.Aprovisionamiento`
  `dotnet user-secrets set "ConnectionStrings:AdministracionServidor" "Host=localhost;Port=5433;Database=postgres;Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>" --project src/FlowRide.Aprovisionamiento`
  `dotnet user-secrets set "FlowRide:Keycloak:Administrador:Usuario" "<KEYCLOAK_ADMIN_USER>" --project src/FlowRide.Aprovisionamiento` (y `...:Contrasena` con `<KEYCLOAK_ADMIN_PASSWORD>`)
  y luego `dotnet run --project src/FlowRide.Aprovisionamiento -- crear-operador --id tenant-a --razon-social "..." --nit ... --correo ... --admin-nombre "..." --admin-correo ... --admin-telefono ...` (también `migrar --id`, `migrar-todos`, `listar`, `ayuda`). Es idempotente: además crea la clave de cifrado y el bucket del operador y deja cifrados sus campos sensibles. La URL de Keycloak, el SMTP de los realms y la cuenta de Azurite están en `src/FlowRide.Aprovisionamiento/appsettings.json`.
- API local: el `TenantId` y la URL de Keycloak están en `appsettings.Development.json`; en Development la API lee su cadena de conexión y el secreto de su cliente de servicio del almacén del operador que creó la herramienta (`%APPDATA%\Microsoft\UserSecrets\flowride-<tenant>\secrets.json`). `dotnet run --project src/FlowRide.API` (http://localhost:5237; pruebas en `src/FlowRide.API/FlowRide.API.http`). Las variables de entorno y la línea de comandos siguen pudiendo sobrescribirla.
- Borde común local: `registrar-ruta --id tenant-a --url http://localhost:5237 [--phone-number-id <id>]` con la herramienta; secretos en `src/FlowRide.Borde` (`ConnectionStrings:Catalogo`, `Borde:WhatsApp:AppSecret`, `Borde:WhatsApp:VerifyToken`) y `dotnet run --project src/FlowRide.Borde --launch-profile https` (https://localhost:8443; el operador en https://tenant-a.dev.localhost:8443). Detalle en `docs/GUIA-WHATSAPP-META.md` §7.
- Primer inicio de sesión: abrir el correo "Actualiza tu cuenta" en Mailpit (http://localhost:8025), seguir el enlace y definir la contraseña.
- En PowerShell, si `docker` no se encuentra, recargar el PATH o abrir una terminal nueva.

## Alcance
Todo lo definido en los ADR debe funcionar, **excepto**:
- RF-55 (ingesta de telemetría GPS) y RF-56 (desviación de ETA), es decir, ADR-0005. No crear nada de ubicación ni de ETA.
