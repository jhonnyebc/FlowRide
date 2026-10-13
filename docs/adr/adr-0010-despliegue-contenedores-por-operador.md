# ADR-0010: Desplegar un stack contenedorizado por operador, con imágenes versionadas e inmutables y un presupuesto explícito de disponibilidad y costo

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0010 |
| Fecha | 2026-09-09 (revisado 2026-09-23) |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Reemplaza | ADR-08 del tradeoff (escalabilidad y despliegue, "por documentar") |

## Contexto

ADR-0002 define un stack completo por operador. Las restricciones vigentes son:

- **SLA por tiers**: canal WhatsApp y motor 99,5 %; Panel Web 99 %. (El objetivo inicial del canal era 99,9 %; la revisión del 22-09-2026 lo bajó a 99,5 % porque el ingreso de mensajes depende del stack del operador y ninguna decisión de este ADR lo hace más disponible que él. Ver el punto 4.)
- **Costo de infraestructura básica**: 20–40 USD/mes por tenant.
- **Dimensionamiento**: hasta 200 usuarios simultáneos y 100 eventos GPS/min por instancia.
- **Retención**: auditoría 5 años, GPS 6 meses (ADR-0011, ADR-0005).

Traducido a tiempo de caída permitido en un mes de unas 730 h:

| Tier | Objetivo | Caída máxima/mes |
|---|---|---|
| Canal WhatsApp (ingreso y procesamiento) | 99,5 % | ≈ 3,6 h |
| Motor y operaciones críticas | 99,5 % | ≈ 3,6 h |
| Panel Web | 99 % | ≈ 7,3 h |

La versión anterior de este ADR no cuantificaba si esos objetivos eran compatibles con el costo. Este ADR lo hace y fija el proveedor: **Microsoft Azure**, elegido por su integración nativa con el stack .NET/ASP.NET Core (identidad de carga de trabajo hacia Key Vault sin credenciales estáticas, SDK oficial de Blob Storage) y por concentrar en una sola cuenta el cómputo, la base de datos, el almacenamiento de objetos y el gestor de secretos, relevante para un equipo pequeño (restricción de equipo). La plataforma concreta se verifica contra el presupuesto de la tabla siguiente con precios reales de Azure (consultados 2026-09-23).

## Decisión

### 1. Unidad de despliegue

Por cada operador, un **stack**:

- 1 contenedor de la aplicación (API + *hosted services*: outbox, recordatorios, retención);
- 1 base de datos PostgreSQL propia (ADR-0004);
- configuración y secretos propios (ADR-0014).

Stateless: todo el estado vive en la base de datos o en el almacenamiento de objetos (ADR-0013).

### 2. Componentes de plataforma compartidos (sin datos operacionales)

- gateway y WAF de borde, más el router de webhooks (ADR-0012), **con 2 réplicas**;
- Keycloak con un realm por operador (ADR-0003);
- registro de contenedores (**Azure Container Registry**, tier Basic);
- *backend* de observabilidad (ADR-0011).

Su costo se prorratea entre los operadores.

### 3. Versionado de contenedores

- Cada *merge* a `main` que pasa CI genera una imagen **inmutable**, etiquetada con versión semántica y SHA del commit (`flowride-api:1.4.2-3f9c2ab`), y se publica en un **registro privado de contenedores**.
- Nunca se despliega `latest`.
- El migration bundle de EF Core (ADR-0004) se versiona con la misma etiqueta.
- **Todos los operadores ejecutan la misma versión** (ADR-0001). El despliegue se hace por oleadas: primero un operador piloto, luego el resto.
- *Rollback* = redeplegar la etiqueta anterior. Por eso las migraciones son compatibles hacia atrás (expandir y contraer).

### 4. Disponibilidad por tier

- **Stack del operador (tiers 99,5 % y 99 %)**: 1 réplica, *health checks* (`/health/live`, `/health/ready`) y reinicio automático por la plataforma; objetivo de reinicio de proceso ≤2 min.
  - Con 44 min al mes de margen en el tier del canal y ≈3,6 h en el del motor, una réplica con reinicio automático cubre los fallos de proceso.
  - Un fallo de host depende de que la plataforma reprograme el contenedor. **Esto debe verificarse con el proveedor elegido.**
  - Si un operador exige más, se le asignan **2 réplicas** (ADR-0006 y ADR-0007 ya soportan varias réplicas), con el costo adicional explícito en su contrato.
- **Canal WhatsApp (tier 99,5 %)**: el canal hereda la disponibilidad del stack del operador, porque el router del borde reenvía cada webhook de forma síncrona y no lo almacena.
  - El borde con 2 réplicas evita ser el eslabón débil, pero no puede hacer el canal más disponible que el destino al que reenvía.
  - Lo que sí se protege por encima de ese 99,5 % es la **pérdida de mensajes**: si el stack no responde, Meta reintenta la entrega (ADR-0008) y el mensaje se procesa al recuperarse, con retraso. El objetivo es 0 mensajes perdidos dentro de la ventana de reintentos de Meta.
  - Se evaluó y se descartó una cola durable en el borde, que sí permitiría 99,9 % de ingreso: obligaría a que un componente compartido entre operadores persista teléfonos y contenido de mensajes, en contra del Silo (ADR-0002) y de la minimización de datos (ADR-0011).
- **Base de datos**: backups automáticos diarios más WAL/PITR si la plataforma lo ofrece (ADR-0013). RPO objetivo ≤15 min; RTO ≤2 h (dentro del margen de 3,6 h del tier del motor y de la ventana de reintentos de Meta, que hay que confirmar).
- **Keycloak**: 1 réplica y punto único de fallo del inicio de sesión del Panel Web de todos los operadores (ADR-0003). RTO objetivo ≤1 h: reinicio automático y, si hace falta, restauración desde `platform/backups/`. No afecta al canal WhatsApp ni a las sesiones ya emitidas.

### 5. Presupuesto por tenant (objetivo de selección de plataforma)

Estos valores no son precios cotizados: la plataforma que se elija debe caber en ellos.

| Partida | Dimensionamiento de referencia | Tope objetivo (USD/mes) |
|---|---|---|
| Cómputo de la aplicación | 1 réplica, ~0,5–1 vCPU, 1–2 GB RAM (Azure Container Apps, precio por confirmar) | 12 |
| Base del operador | **Azure Database for PostgreSQL Flexible Server**, SKU Burstable B1ms + 32 GB, instancia gestionada compartida (ADR-0002 §6). Costo real ≈17 USD/mes por la instancia completa (≈13 cómputo + ≈4 almacenamiento; precios de Azure consultados 2026-09-23), prorrateado entre los operadores que la comparten | 8 (compartida entre 2–3 operadores) |
| Almacenamiento de objetos | **Azure Blob Storage** (tier Hot), backups, archivo de 5 años y exportes (ADR-0013). Costo real ≈0,2–1 USD/mes al volumen del curso | 3 |
| Borde: gateway, WAF y router prorrateados | 2 réplicas compartidas entre operadores (ADR-0012) | 4 |
| Gestor de secretos prorrateado | **Azure Key Vault**, un vault por operador (ADR-0014). Costo real ≈0,15–0,65 USD/mes por operador | 1 |
| Identidad, registro y observabilidad prorrateados | Keycloak, Azure Container Registry y backend de observabilidad | 4 |
| **Total** | | **≤32** |

Con precios reales de Azure para base de datos y almacenamiento (consultados 2026-09-23), el total baja de 39 a ≈32 USD/tenant, dejando margen frente al tope de 40 — el cómputo de la aplicación, el registro de contenedores y el backend de observabilidad todavía no se verificaron contra precios reales de Azure y podrían consumir ese margen. Fuera de este sobre quedan los costos variables de WhatsApp, las consultas al proveedor de rutas (ADR-0005) y la **modalidad de instancia dedicada** de ADR-0002, que se cobra aparte al operador que la pida.

WhatsApp queda fuera del sobre, como costo variable (restricción de *Presupuesto*). Una segunda réplica o una base dedicada con alta disponibilidad **excede el sobre** y se ofrece como opción contractual: una instancia dedicada de Azure Database for PostgreSQL Flexible Server (mismo SKU B1ms) cuesta ≈17 USD/mes adicionales, íntegros para el operador que la contrate.

### 6. Orquestación

**No** se fija Kubernetes como requisito base. Se usa **Azure Container Apps** (o, si su precio de referencia no cabe en el sobre al verificarlo, Docker Compose / systemd sobre una VM de Azure en la etapa del curso). Se reevalúa cuando el número de operadores o réplicas haga que el despliegue por oleadas y el aprovisionamiento sean inmanejables sin un orquestador.

## Alternativas consideradas

| Criterio | **Stack por operador en contenedores (elegida)** | A. VM/proceso manual | B. Kubernetes desde V1 | C. Serverless completo | D. 2 réplicas + BD con HA para todos |
|---|---|---|---|---|---|
| Reproducibilidad | Alta (imagen inmutable) | Baja | Alta | Alta | Alta |
| Cabe en 20–40 USD/tenant | Sí (≤32) | Sí | No con clúster por tenant; con uno compartido, operación alta | Depende de la carga | No |
| Cumple 99,9 % en ingreso | Sí (borde con 2 réplicas + reintento de Meta) | No | Sí | Sí | Sí |
| Cumple 99,5 % del motor | Sí, con reinicio automático (verificar reprogramación en fallo de host) | Difícil | Sí | Parcial | Sí |
| Procesos persistentes (outbox) | Sí | Sí | Sí | No | Sí |
| Carga operativa | Media | Alta (manual) | Alta | Media | Media |

- **A. Manual** — descartada: no es reproducible ni automatizable para N operadores.
- **B. Kubernetes desde V1** — descartada como base: el costo y la operación son desproporcionados para el volumen inicial. Queda como evolución.
- **C. Serverless** — descartada: los procesos persistentes y los webhooks tienen límites de ejecución.
- **D. Alta disponibilidad para todos** — descartada como base por costo; queda disponible como opción por contrato.

## Consecuencias

**Positivas**

- Despliegue reproducible por operador y *rollback* por etiqueta.
- El costo por tenant queda explícito y verificable contra la restricción.
- Los SLA quedan explicados por tier: qué se mide y cómo se protege.

**Negativas**

- Con una réplica, un despliegue implica unos segundos de indisponibilidad del stack. Se despliega en ventana de baja demanda, fuera de la franja 03:00–05:00. El Escenario 12 de Drivers se ajusta a "≤60 s de indisponibilidad del stack; 0 mensajes de WhatsApp perdidos".
- La protección del canal depende de los reintentos de Meta (tradeoff T-04).
- El costo crece linealmente con los operadores; la plataforma compartida es un punto de fallo común (con 2 réplicas).

## Tácticas de Bass aplicadas

- **Detectar fallos** — *health checks* y *heartbeat*.
- **Recuperar: reiniciar / reemplazar instancia** — reinicio automático.
- **Redundancia activa** — solo en el borde (2 réplicas) y como opción contractual por operador.
- **Incrementar recursos** — segunda réplica o más cómputo por operador.
- **Diferir binding** — configuración y secretos inyectados al arrancar.

## Escenarios y requisitos relacionados

- `ADM-DISP-01` — recuperación en la franja crítica.
- `COND-DISP-01`, `PAS-DISP-01` — disponibilidad del canal.
- `OPLOG-DISP-02` — tier del Panel Web.
- `COND-ESC-01`, `OPLOG-CAP-01` — dimensionamiento por instancia.
- Escenario 12 (Desplegabilidad) de Drivers.
- `RF-03`, `RF-53`.

## ADRs relacionados

- **ADR-0001** — artefacto único y versión común.
- **ADR-0002** — stack por operador.
- **ADR-0004** — migration bundle versionado.
- **ADR-0006** / **ADR-0007** — soportan varias réplicas.
- **ADR-0011** — *health checks*, métricas de SLA.
- **ADR-0012** — borde con 2 réplicas.
- **ADR-0013** — backups.
- **ADR-0014** — secretos inyectados.

## Notas de implementación

- Pipeline de aprovisionamiento (RF-03), idempotente: crear base → aplicar migraciones → crear secretos → crear realm → registrar la ruta en el borde → desplegar la imagen vigente.
- Los *jobs* de fondo toleran varias réplicas (`SKIP LOCKED`, ADR-0007); los jobs de retención usan un *lock* consultivo de PostgreSQL.
- Antes de escalar, medir CPU, memoria, backlog de la outbox, P95 y conexiones a la BD.

## Riesgos y validación

- Prueba de recuperación: matar el contenedor en carga simulada. Esperado: reinicio en ≤2 min y 0 mensajes perdidos.
- Prueba de carga con 200 usuarios simultáneos y 100 eventos GPS/min por instancia sobre el dimensionamiento de referencia.

## Referencias

- `RestriccionesNegocio_FlowRide_ACTUALIZADO.xlsx` — Presupuesto, Operativa (SLA).
- `RestriccionesTecnicas_FlowRide_ACTUALIZADO.xlsx` — Disponibilidad, Rendimiento, Costo operacional.
- `tradeoff_FlowRide_ACTUALIZADO.xlsx` — T-03.
