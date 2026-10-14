# ADR-0002: aislar cada operador mediante modelo multi-tenant Silo

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0002 |
| Fecha | 2026-09-09 |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Reemplaza | ADR-02 histórico basado en filtro global EF Core por `empresaId`. |

## Contexto

En FlowRide un **tenant es un operador de transporte**, no una empresa empresarial atendida por ese operador. Cada operador puede prestar servicio a aerolíneas, mineras, hospitales u otros clientes empresariales dentro de su propia operación.

El aislamiento de datos entre operadores es una condición contractual y de seguridad. El identificador `empresaId` permanece en el dominio para identificar al cliente empresarial atendido dentro del tenant, pero no puede utilizarse como frontera de seguridad entre tenants.

## Decisión

Adoptar un modelo multi-tenant **Silo**, con persistencia y credenciales de acceso separadas por operador. La sesión de aplicación debe resolver primero el tenant y, a partir de esa resolución, utilizar únicamente la conexión y recursos asignados a ese Silo.

Automatizar aprovisionamiento, migraciones, backups, restauración y configuración por tenant. Ninguna consulta funcional debe depender de un filtro global por `empresaId` para impedir acceso a otro operador.

La resolución del tenant se realiza mediante un claim `tenant_id` incluido en el JWT emitido tras el login. El módulo de identidad autentica al usuario y determina su operador (Silo) **antes** de emitir el token; la aplicación no vuelve a resolver el tenant en cada request por lógica propia — solo lee el claim ya verificado y firmado, y lo usa para seleccionar la cadena de conexión del Silo correspondiente.

## Alternativas consideradas

### Alternativa A — Pool: una única base compartida con tenantId
Descartada. Minimiza costo operativo, pero un error de filtro o autorización puede exponer datos de otro operador y aumenta el blast radius.

### Alternativa B — Bridge: esquema por tenant en la misma base
Descartada para la línea base. Mejora separación lógica, pero comparte motor, credenciales y recursos, por lo que no satisface el aislamiento físico verificable que exige `RF-19` — solo Silo lo cumple sin depender de la corrección de la lógica de esquema.

### Alternativa C — Silo
Elegida por alinearse con la confidencialidad B2B2B, contención de fallos y simplicidad conceptual de la frontera de tenant.

### Comparación de alternativas por atributo de calidad

| Atributo / Criterio | Silo (elegida) | A. Pool | B. Bridge |
|---|---|---|---|
| Aislamiento de datos | Cumple — separación física de credenciales y persistencia | No cumple — depende de un filtro por tenantId en cada consulta; un olvido expone datos de otro operador | Parcial — separación lógica por esquema, pero mismo motor y credenciales compartidas |
| Blast radius de fallos | Cumple — un fallo de un Silo no afecta a otros tenants | No cumple — un fallo del motor compartido afecta a todos los tenants a la vez | Parcial — mismo motor compartido; un incidente del motor afecta a todos los esquemas |
| Cumplimiento contractual (RF-19, confidencialidad B2B2B) | Cumple — aislamiento físico verificable | No cumple — la separación depende de lógica de aplicación, no es verificable físicamente | Parcial — mejor que Pool, pero no cumple aislamiento físico total |
| Rendimiento / interferencia entre tenants | Cumple — sin contención de recursos entre operadores | No cumple — un tenant con picos de carga degrada a los demás (noisy neighbor) | Parcial — mismo motor compartido, mismo riesgo de noisy neighbor a nivel de motor |
| Costo operacional | No cumple del todo — mayor costo por recursos dedicados por tenant | Cumple — menor costo, un solo recurso compartido | Parcial — costo intermedio, un motor con múltiples esquemas |
| Complejidad de aprovisionamiento | Parcial — requiere automatización por tenant, pero repetible | Cumple — alta simplicidad, un solo esquema para todos | Parcial — moderada, gestión de esquemas por tenant en el mismo motor |
| Complejidad de migraciones | Parcial — se ejecutan N veces (una por Silo), de forma aislada | Cumple — una sola migración aplica a todos los tenants | Parcial — se ejecuta por esquema, similar a Silo pero en el mismo motor |
| Backup / restauración por cliente | Cumple — backup/restore independiente por operador | No cumple — restaurar implica afectar a todos los tenants o extracción selectiva compleja | Parcial — backup por esquema posible, restauración requiere coordinación con el motor compartido |

**Lectura:** Silo es la única alternativa que cumple el driver contractual de aislamiento físico (`RF-19`) y contención de fallos sin depender de disciplina de código (filtros de consulta), consistente con `T-02` del tradeoff (aislamiento vs. costo operacional). Pool y Bridge quedan descartadas no por preferencia sino porque ninguna satisface el requisito de aislamiento físico verificable que el negocio exige contractualmente.

## Consecuencias positivas

- Aislamiento físico verificable entre operadores.
- Reduce el impacto de errores de consulta y facilita backup/restauración por cliente.
- Permite adaptar capacidad y mantenimiento por operador.
- El significado de `empresaId` queda correctamente limitado al cliente empresarial interno.

## Consecuencias negativas

- Aumenta la cantidad de recursos y operaciones de aprovisionamiento.
- Las migraciones deben ejecutarse de forma controlada sobre múltiples Silos.
- Puede elevar el costo si el despliegue por tenant no se automatiza.

## Tácticas de Bass aplicadas

- **Separar entidades/recursos** — cada operador obtiene persistencia y credenciales independientes.
- **Limitar acceso** — la resolución de tenant impide seleccionar recursos de otro Silo.
- **Detección de fallos** — la salud se supervisa por instancia/tenant para limitar el blast radius.

## Escenarios y requisitos relacionados

- `OPLOG-SEG-02` — un operador no puede consultar servicios de otro tenant.
- `ADM-SEG-02` — un administrador no puede modificar usuarios o recursos de otro tenant.
- `RF-19` — aislamiento físico entre tenants.
- `RF-20` — configuración independiente por tenant.

## ADRs relacionados

- **ADR-0001** — el Silo es una frontera de despliegue del monolito modular.
- **ADR-0004** — PostgreSQL es la persistencia transaccional de cada Silo.
- **ADR-0010** — automatiza el despliegue y la operación por tenant.

## Notas de implementación

- El tenant debe resolverse a partir de una identidad confiable y no de un parámetro libre enviado por el cliente.
- Las credenciales de conexión nunca se comparten entre operadores.
- `empresaId` puede repetirse en Silos distintos sin implicar colisión global.
- Las migraciones deben registrar versión de esquema por tenant y ser idempotentes/reintentables.
- El claim `tenant_id` debe viajar firmado dentro del JWT; nunca se acepta como header libre, query param o body sin verificación criptográfica previa.
- El módulo de identidad es responsable de resolver el mapeo usuario → operador → Silo antes de emitir el token; la aplicación no debe reimplementar esa resolución en cada endpoint.
- Cualquier request cuyo JWT no traiga un `tenant_id` válido, o cuyo Silo no tenga una cadena de conexión configurada, debe rechazarse antes de tocar cualquier lógica de negocio (falla cerrado, no abierto).
- La cadena de conexión resuelta a partir del claim no debe cachearse entre requests de tenants distintos dentro del mismo contexto de ejecución, para evitar fugas por reutilización incorrecta de conexión.
- Un cambio de operador para un mismo usuario (caso excepcional en B2B2B) exige reautenticación y nuevo token — no hay reasignación en caliente del claim sobre una sesión activa.

## Referencias

- `RestriccionesNegocio_FlowRide_ACTUALIZADO.xlsx`.
- `RestriccionesTecnicas_FlowRide_ACTUALIZADO.xlsx`.
- `tradeoff_FlowRide_ACTUALIZADO.xlsx` — T-02 Aislamiento vs. costo operacional.