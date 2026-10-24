# ADR-0004: Usar PostgreSQL con EF Core y Npgsql como persistencia transaccional

## Metadatos

| Campo              | Valor                                                                           |
| ------------------ | ------------------------------------------------------------------------------- |
| ID                 | ADR-0004                                                                        |
| Fecha              | 2026-09-09 (revisado 2026-10-13)                                                |
| Estado             | **Accepted**                                                                    |
| Participantes      | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso              | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026)                      |
| Profesor evaluador | Wider Farid Sánchez Garzón                                                      |
| Reemplaza          | ADR-08 histórico de PostgreSQL, actualizado para modelo Silo y telemetría.      |

## Contexto

El núcleo de FlowRide es relacional y transaccional: usuarios, conductores, vehículos, itinerarios, solicitudes, asignaciones y viajes mantienen relaciones e invariantes fuertes.

La implementación actual ya usa PostgreSQL y Npgsql y no existe un driver arquitectónico que justifique migrar el núcleo a otro modelo de persistencia.

El aislamiento Silo significa que cada operador mantiene su propia persistencia, por lo que la operación de migraciones y conexiones debe ser repetible.

FlowRide requiere mantener consistencia sobre operaciones relacionadas con:

* Conductores.
* Vehículos.
* Solicitudes.
* Asignaciones.
* Itinerarios.
* Viajes.
* Estados operacionales.
* Auditoría.
* Mensajes Outbox.

La persistencia debe permitir:

* Transacciones ACID.
* Integridad referencial.
* Restricciones relacionales.
* Control de concurrencia.
* Consultas indexadas.
* Consultas paginadas.
* Ejecución de SQL optimizado.
* Manejo de múltiples Silos.
* Particionamiento cuando el volumen lo requiera.

La telemetría se aborda específicamente en ADR-0005 y podrá utilizar particionamiento dentro del mismo motor para la primera versión.

## Decisión

Usar **PostgreSQL** como motor relacional de cada Silo y **Npgsql** como proveedor principal de acceso desde .NET.

> **Revisión 2026-10-13.** El acceso se hace con **EF Core** sobre el proveedor Npgsql (`Npgsql.EntityFrameworkCore.PostgreSQL`). Así queda alineado con ADR-0007 (token de versión administrado por la capa de persistencia, mapeado a `xmin`), con ADR-0010 (*migration bundle* de EF Core versionado con la imagen) y con ADR-0017. EF Core maneja las migraciones, el mapeo y las transacciones. Para las consultas críticas de lectura sigue permitido el SQL explícito sobre Npgsql (proyecciones con SQL crudo de EF Core o `NpgsqlCommand`), siempre dentro de Infrastructure. Las alternativas E (Dapper) y F (Npgsql directo) dejan de ser el mecanismo principal y se mantienen solo para esas consultas.

Permitir SQL optimizado para consultas específicas cuando exista evidencia de rendimiento y sin saltarse los límites definidos entre aplicación, infraestructura y persistencia.

Organizar datos operativos y auditoría en estructuras claramente separadas dentro del Silo.

La telemetría se resuelve en ADR-0005 y puede utilizar particionamiento dentro del mismo motor.

Las operaciones que modifiquen estados relacionados deberán utilizar transacciones cuando sea necesario preservar consistencia.

El acceso a PostgreSQL deberá mantenerse encapsulado dentro de la capa correspondiente de infraestructura, evitando que detalles específicos de conexión, comandos SQL o transacciones se propaguen hacia el dominio.

---

## Alternativas consideradas

### Alternativa A — SQL Server

Utilizar Microsoft SQL Server como motor principal de persistencia relacional.

#### Ventajas

* Motor relacional maduro.
* Soporte completo de transacciones ACID.
* Amplia integración con tecnologías Microsoft.
* Herramientas administrativas consolidadas.
* Buen soporte para concurrencia.
* Amplia documentación.
* Buen rendimiento para cargas transaccionales.

#### Desventajas

* Puede introducir costos de licenciamiento.
* Requeriría migrar la implementación existente.
* Implicaría cambios de configuración y operación.
* No existe una ventaja arquitectónica necesaria frente a PostgreSQL.
* Reduce continuidad con el conocimiento actual del equipo.

#### Decisión

**Descartada como base principal** por costo/licenciamiento y ausencia de una ventaja necesaria para los drivers vigentes.

---

### Alternativa B — MySQL / MariaDB

Utilizar MySQL o MariaDB como motor relacional principal.

#### Ventajas

* Tecnología madura.
* Amplia adopción.
* Buen rendimiento transaccional.
* Ecosistema amplio.
* Herramientas de administración conocidas.
* Soporte desde diferentes lenguajes y plataformas.

#### Desventajas

* Implicaría migrar la implementación actual.
* Requeriría adaptar consultas y configuraciones.
* No existe un beneficio arquitectónico demostrado.
* Se perdería continuidad con PostgreSQL.
* Algunas capacidades avanzadas de PostgreSQL ya resultan útiles para FlowRide.

#### Decisión

**Viable, pero descartada.**

Cambiar implicaría una migración sin beneficio arquitectónico demostrado.

---

### Alternativa C — PostgreSQL + Npgsql

Utilizar PostgreSQL como motor relacional principal y Npgsql como proveedor de acceso desde .NET.

#### Ventajas

* Continuidad con la implementación actual.
* Capacidades transaccionales ACID.
* Integridad referencial.
* Amplio soporte de índices.
* Soporte para particionamiento.
* SQL avanzado.
* Buen manejo de concurrencia.
* Integración directa con .NET mediante Npgsql.
* Compatible con el patrón Outbox.
* Adecuado para el modelo Silo.
* Permite optimizar consultas específicas.
* Permite aprovechar funcionalidades nativas de PostgreSQL.

#### Desventajas

* Las consultas deben revisarse y optimizarse cuando sea necesario.
* Las migraciones deben gestionarse para múltiples Silos.
* Puede existir dependencia de características específicas de PostgreSQL.
* El manejo directo de SQL requiere disciplina.
* Se debe gestionar correctamente el pool de conexiones.

#### Decisión

**Elegida** por capacidades transaccionales, madurez y continuidad con la implementación.
## Justificación de la decisión

Se selecciona **PostgreSQL + Npgsql** porque FlowRide requiere una persistencia relacional y transaccional capaz de mantener integridad referencial, consistencia entre entidades relacionadas y soporte para operaciones ACID.

PostgreSQL ya forma parte de la implementación actual, por lo que mantenerlo evita una migración tecnológica innecesaria y reduce riesgos, esfuerzo y complejidad operativa.

Npgsql permite integrar PostgreSQL directamente con .NET, manteniendo control explícito sobre consultas, transacciones, conexiones y optimizaciones específicas del motor.

La decisión también es coherente con el modelo Silo adoptado por FlowRide, ya que permite mantener una persistencia independiente por operador, gestionar migraciones de forma repetible y aislar los datos entre tenants.

Por tanto, la elección se fundamenta directamente en:

- Naturaleza relacional del dominio.
- Necesidad de transacciones ACID.
- Integridad referencial.
- Continuidad con la implementación existente.
- Integración directa con .NET mediante Npgsql.
- Compatibilidad con el modelo Silo.
- Soporte para concurrencia, índices y consultas optimizadas.
- Posibilidad de utilizar particionamiento cuando el volumen lo requiera.
- Ausencia de un beneficio arquitectónico suficiente que justifique migrar a otro motor.

No se selecciona PostgreSQL únicamente por preferencia tecnológica, sino porque satisface los drivers actuales de FlowRide con menor impacto de cambio y sin introducir complejidad adicional innecesaria.

---

### Alternativa D — NoSQL como base principal

Utilizar un motor NoSQL documental, clave-valor o equivalente como persistencia principal del núcleo operacional.

Ejemplos:

* MongoDB.
* DynamoDB.
* Couchbase.
* Cassandra.

#### Ventajas

* Flexibilidad de esquema.
* Buen soporte para datos semiestructurados.
* Puede reducir joins en algunos escenarios.
* Algunos motores permiten escalabilidad horizontal.
* Adecuado para modelos documentales.

#### Desventajas

* El núcleo de FlowRide posee relaciones fuertes.
* Las invariantes del dominio son naturalmente relacionales.
* Puede aumentar la duplicación de información.
* La consistencia entre múltiples entidades se vuelve más compleja.
* Requiere un enfoque diferente para las transacciones.
* No existe una necesidad actual que justifique abandonar el modelo relacional.

#### Decisión

**Descartada** porque las relaciones e invariantes del núcleo son naturalmente relacionales.

---

### Alternativa E — PostgreSQL + Dapper

Utilizar PostgreSQL junto con Npgsql e incorporar Dapper como micro-ORM para simplificar el mapeo entre resultados SQL y objetos.

#### Ventajas

* Mantiene SQL explícito.
* Bajo overhead.
* Buen rendimiento.
* Reduce parte del código manual de mapeo.
* Permite consultas optimizadas.
* Integración sencilla con Npgsql.
* Facilita proyecciones específicas.

#### Desventajas

* Introduce una dependencia adicional.
* Sigue siendo necesario diseñar y mantener SQL.
* No resuelve por sí mismo las migraciones.
* Puede producir dispersión de consultas.
* Las transacciones siguen requiriendo manejo explícito.
* Puede no ser necesario si Npgsql directo satisface los requerimientos.

#### Decisión

**Alternativa viable.**

Puede reducir código de mapeo, aunque introduce una dependencia adicional sobre el acceso directo mediante Npgsql.

---

### Alternativa F — Npgsql directo con SQL explícito

Utilizar PostgreSQL accediendo directamente mediante Npgsql, ejecutando consultas y comandos SQL explícitos.

Ejemplo conceptual:

```text
Aplicación
    ↓
Repositorio
    ↓
Npgsql
    ↓
SQL
    ↓
PostgreSQL
```

#### Ventajas

* Control directo sobre las consultas.
* Menor cantidad de abstracciones.
* Permite aprovechar funcionalidades específicas de PostgreSQL.
* Facilita optimización detallada.
* No requiere ORM.
* Integración directa con .NET.
* Mayor visibilidad del SQL ejecutado.

#### Desventajas

* Mayor cantidad de código manual.
* El mapeo debe implementarse explícitamente.
* Requiere administrar conexiones.
* Requiere administrar transacciones.
* Puede aumentar código repetitivo.
* Exige mayor conocimiento de SQL por parte del equipo.

#### Decisión

**Alternativa viable** para mantener control explícito sobre el acceso a PostgreSQL.

---

### Alternativa G — ADO.NET genérico

Utilizar las abstracciones genéricas de acceso a datos de .NET.

Ejemplos:

```text
DbConnection
DbCommand
DbTransaction
DbDataReader
```

#### Ventajas

* Menor dependencia directa de un proveedor específico.
* APIs estándar de .NET.
* Facilita sustituir el proveedor de base de datos.
* Permite control directo de comandos y transacciones.

#### Desventajas

* Mayor cantidad de código manual.
* Puede limitar el uso de funcionalidades específicas de PostgreSQL.
* Introduce abstracciones que pueden no aportar valor.
* El mapeo continúa siendo responsabilidad del equipo.
* La portabilidad de motor no es actualmente un requisito de FlowRide.

#### Decisión

**Alternativa viable**, aunque su principal beneficio sería la portabilidad entre motores.

---

### Alternativa H — Base compartida multi-tenant mediante `TenantId` o `EmpresaId`

Utilizar una única base PostgreSQL para todos los operadores y diferenciar los registros mediante un identificador de tenant.

Ejemplo:

```text
Driver
- Id
- TenantId
- Name

Vehicle
- Id
- TenantId
- Plate

Trip
- Id
- TenantId
- Status
```

#### Ventajas

* Menor cantidad de bases de datos.
* Administración centralizada.
* Migraciones ejecutadas una sola vez.
* Menor complejidad inicial de aprovisionamiento.
* Facilita consultas globales.

#### Desventajas

* Menor aislamiento entre tenants.
* Todas las consultas deben filtrar correctamente.
* Un error puede exponer información de otro operador.
* Mayor impacto ante fallos de la base compartida.
* Recuperar un tenant de manera independiente puede ser más complejo.
* No corresponde con el modelo Silo ya definido.

#### Decisión

**Descartada** por no corresponder con el aislamiento Silo adoptado para FlowRide.

---

### Alternativa I — Schema por tenant

Utilizar una base PostgreSQL compartida, pero asignar un schema diferente a cada operador.

Ejemplo:

```text
tenant_a.drivers
tenant_a.vehicles
tenant_a.trips

tenant_b.drivers
tenant_b.vehicles
tenant_b.trips
```

#### Ventajas

* Mayor separación que utilizar únicamente `TenantId`.
* Mantiene una sola base PostgreSQL.
* Reduce el número de bases independientes.
* Permite identificar físicamente estructuras de cada tenant.

#### Desventajas

* Las migraciones deben aplicarse a múltiples schemas.
* Aumenta el número de objetos dentro de la base.
* La administración se vuelve más compleja a medida que crecen los tenants.
* Puede requerir manejo dinámico de schemas.
* Un fallo de la base continúa afectando múltiples operadores.

#### Decisión

**Alternativa viable**, aunque presenta un nivel de aislamiento menor al de una persistencia Silo completamente independiente.

---

### Alternativa J — Base PostgreSQL independiente por tenant

Asignar una base PostgreSQL separada para cada operador.

Ejemplo:

```text
Tenant A
    ↓
flowride_tenant_a

Tenant B
    ↓
flowride_tenant_b

Tenant C
    ↓
flowride_tenant_c
```

#### Ventajas

* Alto aislamiento de datos.
* Backups independientes.
* Restauración independiente.
* Menor riesgo de exposición entre tenants.
* Permite configuraciones particulares.
* Facilita mantenimiento específico por operador.

#### Desventajas

* Mayor cantidad de bases de datos.
* Las migraciones deben ejecutarse múltiples veces.
* Mayor número de cadenas de conexión.
* Incrementa la complejidad operacional.
* El crecimiento de tenants aumenta el esfuerzo administrativo.

#### Decisión

**Alternativa directamente compatible con el modelo Silo.**

---

### Alternativa K — SQLite por tenant

Utilizar una base SQLite independiente para cada operador.

Ejemplo:

```text
tenant-a.db
tenant-b.db
tenant-c.db
```

#### Ventajas

* Instalación sencilla.
* No necesita servidor.
* Fácil distribución.
* Backups simples.
* Separación física mediante archivos.
* Adecuado para prototipos.

#### Desventajas

* Concurrencia limitada.
* Menor capacidad para cargas multiusuario.
* Escalabilidad limitada.
* Menos capacidades operacionales.
* Menor adecuación para cargas transaccionales concurrentes.
* Puede presentar limitaciones ante crecimiento de telemetría.

#### Decisión

**Descartada para el núcleo operacional** debido a sus limitaciones frente al comportamiento esperado del sistema.

---

### Alternativa L — MongoDB como persistencia principal

Utilizar MongoDB como base principal para representar el dominio mediante documentos.

Ejemplo:

```json
{
  "tripId": "TRIP-001",
  "driver": {
    "id": "DR-01"
  },
  "vehicle": {
    "id": "VH-01"
  },
  "status": "Assigned"
}
```

#### Ventajas

* Esquema flexible.
* Manejo natural de documentos.
* Adecuado para información semiestructurada.
* Permite estructuras anidadas.
* Puede reducir algunos joins.

#### Desventajas

* Las relaciones fuertes requieren tratamiento adicional.
* Puede existir duplicación de información.
* Las invariantes relacionales son más complejas de representar.
* Cambios coordinados entre documentos pueden complicarse.
* El núcleo actual de FlowRide está mejor representado relacionalmente.

#### Decisión

**Descartada como persistencia principal del núcleo.**

---

### Alternativa M — Persistencia políglota

Utilizar diferentes motores de almacenamiento dependiendo del tipo de dato.

Ejemplo:

```text
PostgreSQL → núcleo transaccional

MongoDB → documentos

Redis → estados temporales

Motor de series temporales → telemetría
```

#### Ventajas

* Cada tecnología puede optimizar un tipo específico de carga.
* Mayor flexibilidad.
* Permite escalar diferentes persistencias independientemente.
* Puede mejorar rendimiento en escenarios especializados.

#### Desventajas

* Incrementa el número de tecnologías.
* Mayor complejidad operacional.
* Requiere diferentes estrategias de backup.
* Aumenta la cantidad de puntos de fallo.
* Requiere conocimientos adicionales.
* Puede requerir sincronización entre motores.

#### Decisión

**Descartada para V1** por incrementar la complejidad sin una necesidad demostrada actualmente.

---

### Alternativa N — PostgreSQL + TimescaleDB

Utilizar PostgreSQL incorporando TimescaleDB para capacidades orientadas a series temporales.

#### Ventajas

* Mantiene compatibilidad con PostgreSQL.
* Buen manejo de información temporal.
* Facilita consultas por rangos de tiempo.
* Facilita particionamiento temporal.
* Puede aportar beneficios para telemetría.

#### Desventajas

* Introduce una extensión adicional.
* Requiere conocimiento específico.
* Aumenta dependencia tecnológica.
* El núcleo transaccional no necesita capacidades especializadas de series de tiempo.
* Su beneficio se concentra principalmente en telemetría.

#### Decisión

**Alternativa relacionada principalmente con ADR-0005**, donde se define la persistencia de telemetría.

---

### Alternativa O — CQRS con almacenamiento separado de lectura y escritura

Utilizar persistencias independientes para operaciones de escritura y consultas.

Ejemplo:

```text
Commands
    ↓
Write Database
    ↓
Sincronización
    ↓
Read Database
    ↓
Queries
```

#### Ventajas

* Permite optimizar lectura y escritura de forma independiente.
* Facilita modelos de consulta especializados.
* Permite escalar cada lado de manera separada.
* Puede reducir presión sobre la base transaccional.

#### Desventajas

* Requiere sincronización.
* Puede introducir consistencia eventual.
* Incrementa la infraestructura.
* Aumenta complejidad de despliegue.
* Requiere mecanismos adicionales para actualizar modelos de lectura.

#### Decisión

**Descartada para V1** por incrementar la complejidad de persistencia sin una necesidad actual demostrada.

---

### Alternativa P — Event Sourcing

Persistir eventos como fuente principal de verdad en lugar de almacenar únicamente el estado actual.

Ejemplo:

```text
DriverRegistered
        ↓
DriverAssigned
        ↓
TripStarted
        ↓
TripCompleted
```

#### Ventajas

* Historial completo de cambios.
* Alta trazabilidad.
* Permite reconstruir estados anteriores.
* Favorece auditoría.
* Permite generar diferentes proyecciones.

#### Desventajas

* Mayor complejidad conceptual.
* Requiere versionamiento de eventos.
* Las consultas operativas requieren proyecciones.
* Introduce componentes adicionales.
* Incrementa la complejidad del mantenimiento.
* No existe un driver actual que lo requiera.

#### Decisión

**Descartada.**

Las necesidades de trazabilidad y auditoría de FlowRide no justifican adoptar Event Sourcing como mecanismo principal de persistencia.

---

### Alternativa Q — PostgreSQL administrado en nube

Utilizar PostgreSQL mediante un servicio administrado de un proveedor cloud.

Ejemplo conceptual:

```text
FlowRide
    ↓
Npgsql
    ↓
PostgreSQL administrado
```

#### Ventajas

* Automatización de backups.
* Herramientas de monitoreo.
* Facilita alta disponibilidad.
* Reduce administración directa del servidor.
* Facilita escalamiento de recursos.

#### Desventajas

* Costos recurrentes.
* Dependencia del proveedor.
* Algunas capacidades administrativas pueden estar restringidas.
* Puede existir costo adicional por almacenamiento o transferencia.
* Las características disponibles dependen del servicio contratado.

#### Decisión

**Alternativa válida de despliegue para PostgreSQL**, pero no modifica la elección del motor relacional.

---

### Alternativa R — PostgreSQL autogestionado

Administrar PostgreSQL directamente en infraestructura propia o máquinas virtuales.

#### Ventajas

* Control completo del motor.
* Control sobre configuración.
* Libertad para instalar extensiones.
* Menor dependencia de un servicio administrado.
* Mayor control sobre infraestructura.

#### Desventajas

* El equipo debe gestionar backups.
* Requiere gestionar actualizaciones.
* Requiere implementar recuperación.
* Mayor esfuerzo de monitoreo.
* Mayor responsabilidad sobre seguridad.
* Mayor responsabilidad sobre disponibilidad.

#### Decisión

**Alternativa válida de despliegue**, pero independiente de la decisión del motor de persistencia.

---

## Consecuencias positivas

* Transacciones ACID para el núcleo.
* Integridad referencial.
* Continuidad con código y experiencia ya existentes.
* Control explícito de consultas SQL.
* Particionamiento disponible si el volumen lo exige.
* SQL avanzado disponible para consultas específicas.
* Integración directa con .NET mediante Npgsql.
* Adecuación al modelo Silo.
* Compatibilidad con Outbox.
* Capacidad de implementar índices especializados.
* Permite utilizar capacidades específicas de PostgreSQL.

## Consecuencias negativas

* Las consultas deben revisarse para evitar problemas de rendimiento.
* Las migraciones deben orquestarse por múltiples Silos.
* Existe dependencia del dialecto y capacidades de PostgreSQL en Infrastructure.
* El manejo directo de SQL puede generar código repetitivo.
* Se requiere administración cuidadosa de conexiones.
* El crecimiento del número de Silos aumenta la complejidad operacional.
* El equipo debe mantener conocimiento suficiente de SQL y PostgreSQL.

## Tácticas de Bass aplicadas

* **Mantener consistencia** — transacciones relacionales.
* **Reducir overhead** — índices y consultas optimizadas.
* **Gestionar recursos** — pooling y paginación.

## Escenarios y requisitos relacionados

* `COND-CONF-02` — estado consistente de conductor.
* `PAS-CONF-02` — estado confirmado de solicitud.
* `ADM-CAP-02` — consultas paginadas e indexadas.
* `RF transaccionales` — persistencia del dominio operativo.

## ADRs relacionados

* **ADR-0002** — una persistencia PostgreSQL por Silo.
* **ADR-0007** — Outbox y concurrencia usan la transacción del motor.
* **ADR-0005** — telemetría usa particiones en PostgreSQL para V1.

#

### Implementación en FlowRide (estado al 22-10-2026)

- EF Core 10 con Npgsql 10 sobre PostgreSQL 17. El esquema operativo es `ops`, en snake_case (`EFCore.NamingConventions`), igual al modelo entidad-relación; la herencia de `Usuario` (pasajero, conductor) es TPT.
- Las fechas pasadas se guardan como `timestamptz` en UTC; los eventos futuros guardan además su hora local y su zona horaria (ADR-0020). Los enums se guardan como texto.
- Los traslapes que no deben existir (turnos del mismo conductor y asignaciones del mismo conductor o del mismo vehículo en la misma franja) se protegen además con restricciones `EXCLUDE` en la base.
- Las migraciones son versionadas y las aplica la herramienta de aprovisionamiento (`crear-operador`, `migrar`); su historial vive en `ops.__ef_migraciones`.
- Las pruebas de integración corren contra PostgreSQL real con Testcontainers.

# Notas de implementación

* Versionar migraciones y registrar versión de esquema por tenant.
* Definir índices según consultas reales.
* Evitar índices basados en `empresaId` como separación de tenant.
* Usar transacciones explícitas en casos que afecten varios agregados o estados.
* Revisar consultas críticas con `EXPLAIN` cuando aparezcan problemas medidos.
* Mantener consultas parametrizadas.
* Gestionar correctamente el pool de conexiones.
* Mantener acceso específico a PostgreSQL dentro de Infrastructure.
* Evitar que Domain dependa directamente de Npgsql.
* Mantener auditoría separada de los datos operativos.
* Coordinar migraciones de manera repetible entre Silos.
* Revisar crecimiento de la telemetría según ADR-0005.

## Referencias

* PostgreSQL Documentation — transactions.
* PostgreSQL Documentation — indexes.
* PostgreSQL Documentation — declarative partitioning.
* PostgreSQL Documentation — EXPLAIN.
* Npgsql Documentation.
* `RestriccionesTecnicas_FlowRide_ACTUALIZADO.xlsx`.
* `tradeoff_FlowRide_ACTUALIZADO.xlsx`.
