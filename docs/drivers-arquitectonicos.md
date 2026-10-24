**UNIVERSIDAD CATÓLICA DE ORIENTE**

**Facultad de Ingenierías**

**Programa de Ingeniería de Sistemas**

**INGENIERÍA DEL SOFTWARE 2**

**Semestre 2-2026**

**PROYECTO EXTRACLASE — FLOWRIDE**

Plataforma SaaS de Gestión de Transporte Corporativo Interno

**DRIVERS ARQUITECTÓNICOS**

**AUTORES:**

**Sebastián Zapata Naranjo**\
**Andrés David Ortiz Mejía**\
**Jhonny Estiven Benítez Caro**

**DOCENTE:**

**Wider Farid Sánchez Garzón**

**Rionegro, Antioquia**

Septiembre de 2026

# Drivers arquitectónicos de FlowRide

> Versión Markdown para la entrega del 28-10-2026 (fuente: `DriversArquitectonicosFlowRide_ACTUALIZADO.md` del 22-09-2026, con las tablas convertidas a formato Markdown estándar). Revisión del 22-10-2026: el canal WhatsApp queda en 99,5 % de disponibilidad (ADR-0010) y la telemetría GPS y la detección de desviación de ETA (RF-55, RF-56) quedan fuera del alcance de V1 (ADR-0005 en estado *Proposed*); los textos afectados lo indican y se conservan por trazabilidad.

Los drivers arquitectónicos son los elementos que condicionan de manera
significativa la arquitectura de FlowRide. Para el proyecto se
consideran cuatro grupos: atributos de calidad priorizados,
funcionalidades arquitectónicamente significativas, restricciones de
negocio y restricciones técnicas.

Este documento utiliza como fuente de verdad para los atributos de
calidad las hojas **Tradeoff QA** y **Priorización QA** del análisis
arquitectónico. Por esta razón se trabaja con un conjunto cerrado de
**11 atributos de calidad**, evitando incorporar atributos adicionales
que no formen parte del análisis vigente.

# Atributos de calidad priorizados

La prioridad arquitectónica global adoptada para FlowRide utiliza una
escala de **11 a 1**, donde **11 representa la mayor prioridad y 1 la
menor prioridad relativa**.

| Prioridad | Atributo de calidad | Aplicación en FlowRide |
| --- | --- | --- |
| **11** | **Disponibilidad** | La operación depende de la continuidad del canal WhatsApp, del motor de asignación y del Panel Web. Se manejan objetivos diferenciados por criticidad: WhatsApp 99,5 % (revisado desde 99,9 % en ADR-0010), motor y operaciones críticas 99,5 % y Panel Web 99 %. |
| **10** | **Seguridad** | Protege identidad, autorización, roles, sesiones y acceso a información. La revocación de acceso debe surtir efecto en ≤ 60 s y los accesos no autorizados deben ser rechazados. |
| **9** | **Rendimiento** | Las operaciones críticas deben responder bajo carga sin degradar la operación. Para operaciones CRUD se adopta P95 \< 500 ms y P99 \< 1,5 s con hasta 200 usuarios concurrentes por instancia. |
| **8** | **Confiabilidad** | La plataforma debe evitar asignaciones duplicadas, pérdida de eventos y estados incoherentes, manteniendo una fuente de verdad transaccional. |
| **7** | **Aislamiento** | Cada operador de transporte se maneja como tenant con aislamiento físico mediante modelo Silo, evitando acceso cruzado entre persistencias. |
| **6** | **Interoperabilidad** | FlowRide debe integrarse con servicios externos como WhatsApp, el proveedor de identidad y, fuera de V1, la telemetría GPS, mediante contratos y adaptadores que eviten acoplar el dominio al proveedor. |
| **5** | **Capacidad** | Una instancia debe soportar el volumen operativo definido, incluidos hasta 200 usuarios concurrentes y, cuando exista telemetría (fuera de V1, ADR-0005), un dimensionamiento base de 100 eventos GPS/min por instancia. |
| **4** | **Capacidad para ser auditado** | Las acciones sensibles y eventos relevantes deben poder reconstruirse mediante evidencia verificable, minimizando datos personales innecesarios. |
| **3** | **Usabilidad - UX** | Pasajero y conductor interactúan principalmente mediante WhatsApp y los roles administrativos mediante Panel Web. Los controles de seguridad no deben introducir fricción innecesaria. |
| **2** | **Escalabilidad** | El crecimiento de la plataforma se resuelve incorporando nuevas instancias Silo por operador, evitando concentrar la carga de todos los tenants en una única instancia. |
| **1** | **Mantenibilidad** | El monolito modular, la separación de responsabilidades y los adaptadores deben permitir cambios localizados sin propagar modificaciones innecesarias por el sistema. |

Los atributos **Accesibilidad** y **Portabilidad** no forman parte del
conjunto priorizado vigente. Aspectos como despliegue, testabilidad,
observabilidad, modificabilidad y reusabilidad pueden seguir siendo
prácticas o propiedades deseables de la solución, pero no se manejan
como drivers de calidad independientes dentro del Tradeoff vigente.

# Priorización por stakeholder

La hoja **Priorización QA** conserva los mismos 11 atributos y el mismo
orden físico. Cada stakeholder utiliza los valores del **11 al 1
exactamente una vez**, de acuerdo con sus necesidades. Cada columna suma
66 puntos.

| Atributo de calidad | Administrador del operador | Conductor | Pasajero | Operador logístico |
| --- | --- | --- | --- | --- |
| Disponibilidad | 9 | 11 | 11 | 11 |
| Seguridad | 11 | 8 | 9 | 7 |
| Rendimiento | 8 | 10 | 10 | 10 |
| Confiabilidad | 10 | 9 | 8 | 9 |
| Aislamiento | 7 | 3 | 4 | 4 |
| Interoperabilidad | 3 | 6 | 5 | 6 |
| Capacidad | 4 | 5 | 3 | 5 |
| Capacidad para ser auditado | 6 | 2 | 2 | 8 |
| Usabilidad - UX | 5 | 7 | 7 | 3 |
| Escalabilidad | 2 | 4 | 6 | 2 |
| Mantenibilidad | 1 | 1 | 1 | 1 |
| **Total** | **66** | **66** | **66** | **66** |

La priorización por stakeholder no sustituye la prioridad arquitectónica
global de la hoja Tradeoff QA. Representa la importancia relativa que
cada actor asigna a cada atributo desde su interacción con FlowRide.

# Tradeoffs de calidad vigentes

| ID | Atributo | Tensión arquitectónica | Criterio vigente / resolución | Impacto aceptado | Escenarios relacionados | Decisión asociada | Prioridad |
| --- | --- | --- | --- | --- | --- | --- | --- |
| T-01 | Disponibilidad | Aumentar redundancia y capacidad de recuperación mejora continuidad, pero incrementa infraestructura, complejidad operativa y costo por tenant. | Se aplican niveles diferenciados: WhatsApp 99,5 % (revisado desde 99,9 % en ADR-0010), motor y operaciones críticas 99,5 % y Panel Web 99 %. La recuperación se concentra en componentes críticos. | Se acepta mayor complejidad y costo en componentes críticos sin sobredimensionar componentes administrativos. | COND-DISP-01, PAS-DISP-01, ADM-DISP-01, OPLOG-DISP-02 | ADR-0010 / ADR-0012 | 11 |
| T-02 | Seguridad | Validar identidad, rol, sesión y límites de acceso agrega procesamiento y complejidad a cada petición. | Se prioriza protección de acceso, autenticación desacoplada, autorización por roles y revocación ≤ 60 s. | Se acepta costo de validación siempre que CRUD mantenga P95 \< 500 ms y P99 \< 1,5 s. | ADM-SEG-01, OPLOG-SEG-02, ADM-SEG-02 | ADR-0002 / ADR-0003 | 10 |
| T-03 | Rendimiento | La caché reduce latencia, pero puede servir información obsoleta. | Solo se cachean datos de baja volatilidad. Solicitudes, asignaciones, viajes en curso y telemetría no dependen de caché como fuente de verdad. | Se acepta menor hit-rate y más lecturas a persistencia en datos críticos para conservar consistencia operacional. | ADM-REND-01, ADM-CAP-02 | ADR-0006 | 9 |
| T-04 | Confiabilidad | Garantizar consistencia añade transacciones, control de concurrencia, idempotencia y reintentos. | La fuente de verdad permanece transaccional; se aplican concurrencia optimista, restricciones de unicidad y mecanismos confiables de procesamiento de eventos. | Se acepta mayor complejidad transaccional antes que duplicar asignaciones, perder eventos o generar estados incoherentes. | OPLOG-REND-01, COND-CONF-02, PAS-CONF-02 | ADR-0007 | 8 |
| T-05 | Aislamiento | El modelo Silo mejora confidencialidad y contención de fallos, pero multiplica recursos y tareas por tenant. | Se mantiene Silo como aislamiento físico por operador y se automatizan aprovisionamiento, migraciones y backups. | Se acepta mayor complejidad operativa a cambio de aislamiento físico verificable. | OPLOG-SEG-02, ADM-SEG-02 | ADR-0002 / ADR-0010 | 7 |
| T-06 | Interoperabilidad | WhatsApp, telemetría e identidad dependen de contratos y proveedores externos fuera del control directo de FlowRide. | Las integraciones se encapsulan mediante adaptadores; se aplican validación, idempotencia, reintentos y recuperación según el canal. | Se acepta dependencia externa a cambio de integración y capacidad de sustitución del proveedor sin acoplar el dominio. | COND-DISP-01, PAS-DISP-03, COND-ACC-01, OPLOG-REND-01 | ADR-0008 / ADR-0012 | 6 |
| T-07 | Capacidad | **Fuera del alcance de V1** (ADR-0005). Mayor frecuencia de GPS mejora frescura del ETA, pero aumenta throughput, almacenamiento y exposición de datos. | Dimensionamiento base de 100 eventos/min por instancia y retención GPS de 6 meses. La ubicación se usa para monitoreo y desviación de ETA, no para reasignación dinámica. | Se limita frecuencia y retención en lugar de almacenar ubicación indefinidamente o a frecuencia extrema. | OPLOG-INTEROP-04, PAS-CONF-01 | ADR-0005 | 5 |
| T-08 | Capacidad para ser auditado | Una auditoría más rica mejora trazabilidad, pero aumenta almacenamiento, costo y exposición de información. | Se conserva la evidencia necesaria para reconstruir acciones sensibles, evitando duplicar datos personales innecesarios. | Se acepta mayor almacenamiento a cambio de evidencia verificable, aplicando minimización de datos. | OPLOG-AUD-01, PAS-AUD-02, OPLOG-AUD-02, PAS-AUD-03 | ADR-0011 / ADR-0013 | 4 |
| T-09 | Usabilidad - UX | WhatsApp reduce fricción, pero las acciones críticas y datos sensibles requieren controles adicionales. | WhatsApp se mantiene como canal de pasajero y conductor, con validación de identidad, permisos y exposición mínima. | Se acepta una interacción adicional cuando protege datos o evita suplantación. | PAS-UX-01, COND-SEG-01, PAS-SEG-01 | ADR-0003 / ADR-0008 | 3 |
| T-10 | Escalabilidad | Crecer mediante instancias Silo evita concentrar tenants, pero aumenta infraestructura y operación conforme crece la plataforma. | La plataforma incorpora nuevas instancias Silo por operador y conserva el dimensionamiento de cada instancia. | Se acepta crecimiento de infraestructura por tenant a cambio de aislamiento y de evitar degradación entre operadores. | Pendiente de vincular con el escenario seleccionado de escalabilidad. | Pendiente de validar al revisar ADR. | 2 |
| T-11 | Mantenibilidad | Separación modular, interfaces y adaptadores agregan estructura inicial, pero reducen el impacto de cambios futuros. | Se mantiene un monolito modular con responsabilidades separadas y contratos explícitos para infraestructura e integraciones. | Se acepta mayor disciplina y estructura interna a cambio de menor acoplamiento y cambios localizados. | Pendiente de vincular con el escenario seleccionado de mantenibilidad. | Pendiente de validar al revisar ADR. | 1 |

Las asociaciones con ADR se conservan como referencias de trabajo y se
validarán en la revisión específica de ADR. No se crean códigos nuevos
de escenario ni decisiones no verificadas para Escalabilidad y
Mantenibilidad.

# Escenarios de calidad

Cada escenario utiliza seis elementos: **fuente, estímulo, artefacto,
entorno, respuesta y medida de respuesta**. Las medidas deben ser
verificables y cuantificables.

## QA-01 — Disponibilidad

| Parte | Contenido |
| --- | --- |
| Fuente | Falla no planeada de un componente de ejecución |
| Estímulo | Una instancia deja de responder durante horario operativo |
| Artefacto | Canal WhatsApp, motor/operaciones críticas o Panel Web |
| Entorno | Producción |
| Respuesta | FlowRide aplica la estrategia de continuidad correspondiente al componente afectado preservando la operación y consistencia de datos |
| Medida | Disponibilidad mensual objetivo: WhatsApp ≥ 99,5 % (revisado desde 99,9 % en ADR-0010); motor y operaciones críticas ≥ 99,5 %; Panel Web ≥ 99 % |

## QA-02 — Seguridad

| Parte | Contenido |
| --- | --- |
| Fuente | Usuario o actor no autorizado |
| Estímulo | Intenta acceder a una operación protegida con credenciales inválidas, expiradas, revocadas o sin el rol requerido |
| Artefacto | Autenticación y autorización de la API |
| Entorno | Producción |
| Respuesta | El sistema rechaza el acceso y no expone información protegida |
| Medida | 100 % de solicitudes no autorizadas de las pruebas de seguridad son rechazadas; la revocación de acceso surte efecto en ≤ 60 s |

## QA-03 — Rendimiento

| Parte | Contenido |
| --- | --- |
| Fuente | Usuario autenticado desde Panel Web o flujo operacional |
| Estímulo | Ejecuta una operación CRUD sobre la API |
| Artefacto | API REST y persistencia transaccional |
| Entorno | Pico de operación con hasta 200 usuarios concurrentes por instancia |
| Respuesta | El sistema procesa y retorna la operación solicitada |
| Medida | P95 \< 500 ms y P99 \< 1,5 s para operaciones CRUD bajo el volumen definido |

## QA-04 — Confiabilidad

| Parte | Contenido |
| --- | --- |
| Fuente | Dos procesos o solicitudes concurrentes |
| Estímulo | Intentan producir una asignación o transición de estado incompatible o duplicada |
| Artefacto | Motor de asignación y persistencia transaccional |
| Entorno | Operación normal o reintento después de una falla transitoria |
| Respuesta | El sistema conserva una única transición válida y evita duplicados |
| Medida | 0 asignaciones duplicadas y 0 transiciones inválidas en las pruebas de concurrencia e idempotencia definidas |

## QA-05 — Aislamiento

| Parte | Contenido |
| --- | --- |
| Fuente | Usuario autenticado perteneciente al operador A |
| Estímulo | Intenta acceder a información persistida en el tenant del operador B |
| Artefacto | Resolución de tenant y persistencias Silo |
| Entorno | Producción multi-tenant |
| Respuesta | La petición no puede acceder ni enrutar operaciones hacia la persistencia de otro operador |
| Medida | 0 fugas de datos entre tenants y 100 % de pruebas automatizadas de acceso cruzado bloqueadas |

## QA-06 — Interoperabilidad

| Parte | Contenido |
| --- | --- |
| Fuente | Servicio externo integrado |
| Estímulo | Envía un webhook o evento válido hacia FlowRide |
| Artefacto | Adaptador de integración correspondiente |
| Entorno | Operación normal |
| Respuesta | El adaptador valida el contrato y procesa o encola el evento sin acoplar el dominio al proveedor |
| Medida | 100 % de casos válidos del contrato de integración superan las pruebas de contrato; para webhook de WhatsApp, reconocimiento o encolado ≤ 500 ms |

## QA-07 — Capacidad

| Parte | Contenido |
| --- | --- |
| Fuente | Fuente de telemetría GPS y usuarios concurrentes |
| Estímulo | La instancia recibe el volumen operativo dimensionado |
| Artefacto | API, ingesta de telemetría y persistencia |
| Entorno | Producción en carga nominal |
| Respuesta | La instancia procesa el volumen sin superar los objetivos de rendimiento definidos |
| Medida | Hasta 200 usuarios concurrentes y 100 eventos GPS/min por instancia manteniendo los umbrales de rendimiento aplicables (la parte de eventos GPS no aplica en V1, ADR-0005) |

## QA-08 — Capacidad para ser auditado

| Parte | Contenido |
| --- | --- |
| Fuente | Administrador, operador logístico o proceso sensible |
| Estímulo | Ejecuta una acción que requiere trazabilidad |
| Artefacto | Mecanismo de auditoría |
| Entorno | Producción |
| Respuesta | Se registra evidencia suficiente para reconstruir actor, acción, instante y resultado sin duplicar datos personales innecesarios |
| Medida | 100 % de las acciones catalogadas como auditables generan un registro verificable; la retención legal definida para auditoría es de 5 años |

## QA-09 — Usabilidad - UX

| Parte | Contenido |
| --- | --- |
| Fuente | Pasajero o conductor |
| Estímulo | Ejecuta un flujo habitual mediante WhatsApp |
| Artefacto | Canal conversacional de FlowRide |
| Entorno | Operación normal |
| Respuesta | El sistema guía el flujo y solicita controles adicionales únicamente cuando la operación sea sensible |
| Medida | 100 % de los flujos críticos definidos para WhatsApp pueden completarse sin instalar una aplicación adicional; los pasos extra de validación se limitan a operaciones sensibles |

## QA-10 — Escalabilidad

| Parte | Contenido |
| --- | --- |
| Fuente | Crecimiento de la plataforma |
| Estímulo | Se incorpora un nuevo operador de transporte |
| Artefacto | Plataforma FlowRide y aprovisionamiento Silo |
| Entorno | Producción |
| Respuesta | Se crea una nueva instancia Silo sin aumentar la carga operacional de las instancias existentes |
| Medida | El nuevo tenant conserva el techo de hasta 200 usuarios concurrentes por instancia y los objetivos de rendimiento vigentes sin degradar las instancias existentes |

## QA-11 — Mantenibilidad

| Parte | Contenido |
| --- | --- |
| Fuente | Equipo de desarrollo |
| Estímulo | Se modifica una regla de asignación o se sustituye una integración externa |
| Artefacto | Monolito modular, motor de reglas y adaptadores |
| Entorno | Desarrollo e integración |
| Respuesta | El cambio permanece localizado en el módulo o adaptador correspondiente y no obliga a modificar módulos no relacionados |
| Medida | 0 modificaciones requeridas en módulos de dominio no relacionados y 100 % de las pruebas existentes del área afectada continúan aprobando después del cambio |

# Criterio de coherencia

A partir de esta versión, cualquier escenario, Building Block, táctica,
PoC/Spike o ADR que se incorpore al análisis debe poder relacionarse con
al menos uno de estos **11 atributos de calidad**. No se deben crear
nuevos atributos en hojas posteriores sin actualizar previamente
Tradeoff QA y Priorización QA.

# Especificación de Requisitos Funcionales

Esta sección consolida la especificación funcional vigente de FlowRide.
El catálogo queda cerrado en 56 requisitos funcionales, alineados con el
alcance actual del producto, el modelo B2B2B, el aislamiento Silo por
operador, WhatsApp como canal de pasajero y conductor, el motor de
asignación programado por reglas, la calificación del servicio y la
telemetría GPS para monitoreo y detección de desviación de ETA. RF-55 y RF-56 quedaron luego fuera del alcance de V1 (ADR-0005).

Los requisitos arquitectónicamente significativos se identifican por su
valor de negocio (VN) o por representar un reto tecnológico (RT). El
catálogo evita duplicidades funcionales y excluye capacidades que no
pertenecen al alcance actual del producto, de modo que la numeración
RF-01 a RF-56 constituye la línea base funcional de este documento.

## **Módulo 1 — Autenticación y Autorización**

RF-01 — Autenticación mediante mecanismo de identidad basado en token

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Todos los usuarios del sistema (Pasajero, Conductor, Administrador del operador, Operador logístico) |
| Descripción | El sistema deberá autenticar y validar la identidad de los usuarios mediante un mecanismo basado en tokens y un proveedor de identidad desacoplado. El sistema usa un proveedor de identidad desacoplado y tokens de acceso estándar; la comparación y selección del proveedor se documenta en ADR-03. |
| Entradas | Correo electrónico y contraseña del usuario |
| Proceso | Valida credenciales y estado del usuario mediante el proveedor de identidad configurado y obtiene/emite un token de acceso con los atributos mínimos necesarios para autorización, sin usar empresaId como mecanismo de aislamiento entre tenants. |
| Salidas | Token de acceso válido + información básica del usuario y rol |
| Precondiciones | El usuario debe estar registrado y activo en el sistema |
| Postcondiciones | El usuario puede realizar peticiones autenticadas mientras su sesión/token sea válido según la política de sesión y expiración configurada |
| Prioridad | Alta |

**RF-02 — Autorización basada en roles**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Todos los usuarios autenticados |
| Descripción | El sistema deberá validar en cada petición a endpoints protegidos que el usuario posea el rol requerido para ejecutar la acción, rechazando la petición si no cumple. |
| Entradas | Token de acceso válido en el encabezado Authorization |
| Proceso | Valida el rol y permisos requeridos para la operación mediante el mecanismo de autorización implementado por la aplicación, manteniendo desacople respecto al proveedor de identidad |
| Salidas | Respuesta HTTP 200 (autorizado) o HTTP 403 (no autorizado) |
| Precondiciones | El usuario debe estar autenticado con un token válido y no expirado |
| Postcondiciones | Solo los usuarios con el rol adecuado pueden ejecutar cada acción del sistema |
| Prioridad | Alta |

## Módulo 2 — Gestión de Operadores / Tenants

RF-03 — Registro de operador de transporte como tenant

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Administrador técnico / proceso de aprovisionamiento SaaS |
| Descripción | El sistema deberá permitir registrar un nuevo operador de transporte como tenant de FlowRide y aprovisionar su entorno de datos aislado físicamente mediante el modelo Silo. |
| Entradas | Nombre o razón social del operador, NIT, correo de contacto y datos mínimos de aprovisionamiento |
| Proceso | Valida la identidad del operador, crea el identificador del tenant y dispara el aprovisionamiento de su persistencia aislada. El aprovisionamiento crea la persistencia aislada del tenant; migraciones y respaldos siguen la estrategia operativa documentada en ADR-02. |
| Salidas | Tenant del operador registrado y entorno Silo preparado para configuración |
| Precondiciones | El operador no debe existir previamente como tenant |
| Postcondiciones | El operador queda habilitado para configurar usuarios, conductores, vehículos, reglas y clientes empresariales dentro de su propia instancia |
| Prioridad | Alta |

RF-04 — Configuración inicial del tenant

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Administrador del operador |
| Descripción | El sistema deberá permitir configurar los parámetros base de operación del tenant: usuarios administrativos, recursos operativos y parámetros necesarios para iniciar la operación. |
| Entradas | Datos de usuarios administrativos, recursos y parámetros operativos del tenant |
| Proceso | Persiste la configuración dentro de la instancia Silo del operador |
| Salidas | Confirmación de configuración aplicada |
| Precondiciones | La empresa debe estar previamente registrada (RF-03) |
| Postcondiciones | El tenant puede operar con sus reglas y recursos sin afectar otros operadores |
| Prioridad | Alta |

## **Módulo 3 — Gestión de Usuarios**

RF-05 — Creación de usuarios por tenant

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Administrador del operador |
| Descripción | El sistema deberá permitir al administrador crear usuarios del tenant (pasajeros, conductores, operadores logísticos y administradores) con el rol y asociación operacional correspondiente. |
| Entradas | Nombre, correo, teléfono, rol, código interno del usuario |
| Proceso | Valida la unicidad aplicable dentro del tenant y persiste el usuario en la instancia del operador. La gestión de credenciales se delega al proveedor de identidad; la aplicación no almacena contraseñas en texto plano ni acopla el dominio a un emisor específico de tokens. |
| Salidas | Usuario creado con su identificador único y credenciales para primer login |
| Precondiciones | El administrador debe estar autenticado y autorizado dentro del tenant |
| Postcondiciones | El usuario puede autenticarse y operar según su rol dentro del tenant |
| Prioridad | Alta |

**RF-06 — Asignación de roles a usuarios**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Administrador del operador |
| Descripción | El sistema deberá permitir asignar o modificar el rol de un usuario existente dentro de la empresa (Pasajero, Conductor, Operador Logístico, Administrador). |
| Entradas | Identificador del usuario, nuevo rol |
| Proceso | Valida que el rol exista en el catálogo, actualiza el rolId del usuario y registra la acción en auditoría |
| Salidas | Usuario con rol actualizado |
| Precondiciones | El usuario y el rol deben existir dentro del mismo tenant; el administrador debe estar autorizado |
| Postcondiciones | Los permisos del usuario se actualizan de acuerdo con la política de sesión, roles y revocación configurada |
| Prioridad | Alta |

## **Módulo 4 — Gestión de Conductores**

**RF-07 — Consulta de disponibilidad de conductores**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Motor de Asignación (proceso interno) + Operador Logístico |
| Descripción | El sistema deberá permitir consultar los conductores disponibles para una franja o servicio dentro del tenant, filtrando por estado y criterios operativos programados. La ubicación GPS no se utiliza para reasignación dinámica por cercanía. |
| Entradas | Identificador del tenant y filtros operativos (estado, turno, vehículo asignado u otros definidos por reglas) |
| Proceso | Consulta conductores activos y disponibles dentro de la instancia del operador, respetando las reglas programadas de disponibilidad |
| Salidas | Listado de conductores disponibles con sus datos operativos |
| Precondiciones | Debe existir al menos un conductor registrado y activo en el tenant |
| Postcondiciones | La consulta no modifica el estado del sistema |
| Prioridad | Media |

## **Módulo 5 — Gestión de Itinerarios y Solicitudes**

**RF-08 — Registro de itinerarios**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Operador Logístico |
| Descripción | El sistema deberá permitir registrar itinerarios de transporte que definen los servicios recurrentes o programados de la empresa (origen, destino, horarios, código de servicio). |
| Entradas | Código de servicio, fecha, origen, destino, hora de salida, hora de llegada |
| Proceso | Valida los datos y persiste el itinerario dentro del tenant. Cuando corresponda, empresaId identifica al cliente empresarial atendido por el operador, no al tenant. |
| Salidas | Itinerario registrado con identificador único |
| Precondiciones | El operador debe estar autenticado y tener rol autorizado |
| Postcondiciones | El itinerario queda disponible como referencia para solicitudes de transporte |
| Prioridad | Alta |

**RF-09 — Recepción de solicitudes de transporte**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Pasajero (vía WhatsApp) |
| Descripción | El sistema deberá recibir solicitudes de transporte de los pasajeros, validarlas y registrarlas en la cola de asignación. |
| Entradas | Identificador del pasajero, itinerario asociado (opcional), dirección origen, dirección destino, fecha y hora, indicador de urgencia y atributo esPersonal cuando aplique |
| Proceso | Valida datos mínimos, crea la solicitud con estado Pendiente dentro del tenant del operador y registra empresaId solo como referencia al cliente empresarial atendido cuando corresponda |
| Salidas | Solicitud registrada con identificador único y confirmación al pasajero |
| Precondiciones | El pasajero debe estar identificado dentro de un tenant activo |
| Postcondiciones | La solicitud queda encolada para asignación automática |
| Prioridad | Alta |

## **Módulo 6 — Motor de Asignación**

**RF-10 — Asignación automática de conductores**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Motor de Asignación (proceso interno disparado por nueva solicitud) |
| Descripción | El sistema deberá asignar automáticamente un conductor y un vehículo a cada solicitud pendiente, aplicando las reglas de negocio programadas y configurables del tenant. |
| Entradas | Solicitud de transporte en estado Pendiente |
| Proceso | Recupera conductores disponibles, aplica las reglas de asignación configuradas por el tenant, selecciona el mejor candidato y crea la asignación asociada |
| Salidas | Asignación creada con conductor y vehículo; solicitud actualizada a estado Asignada |
| Precondiciones | Debe existir al menos un conductor disponible y las reglas del tenant deben estar configuradas |
| Postcondiciones | Se notifica al conductor asignado (RF-17); la solicitud queda en flujo operativo |
| Prioridad | Alta |

**RF-11 — Aplicación de reglas de negocio para la asignación**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Motor de Asignación |
| Descripción | El sistema deberá aplicar reglas de negocio configurables por tenant, tales como disponibilidad programada, minimización de cambios de turno, balance de carga, distancia del servicio origen-destino y restricciones regulatorias. La telemetría GPS se usa para monitoreo y desviación de ETA, no para reasignación dinámica por cercanía. |
| Entradas | Solicitud de transporte, configuración de reglas del tenant y conductores disponibles |
| Proceso | Ejecuta las reglas activas, filtra y pondera candidatos según la configuración del operador y registra las razones de la decisión para trazabilidad |
| Salidas | Conductor seleccionado o notificación de imposibilidad de asignación |
| Precondiciones | Las reglas deben estar configuradas por el Administrador del operador |
| Postcondiciones | La decisión y las reglas aplicadas quedan disponibles para auditoría y trazabilidad |
| Prioridad | Alta |

**RF-12 — Reasignación manual de conductores**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Operador Logístico |
| Descripción | El sistema deberá permitir al Operador logístico reasignar manualmente una asignación cuando exista una contingencia operativa, por ejemplo, cuando un conductor abandone su turno o quede indisponible. |
| Entradas | Identificador de la asignación, nuevo conductor y vehículo |
| Proceso | Valida disponibilidad del nuevo conductor, actualiza la asignación, notifica a ambos conductores (el liberado y el nuevo) y registra la acción en auditoría |
| Salidas | Asignación actualizada; notificaciones enviadas |
| Precondiciones | La asignación debe existir y el operador debe pertenecer al mismo tenant |
| Postcondiciones | La nueva asignación queda vigente; la anterior queda registrada en histórico |
| Prioridad | Alta |

**RF-13 — Priorización de solicitudes urgentes**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Motor de Asignación |
| Descripción | El sistema deberá priorizar solicitudes marcadas como urgentes o críticas de acuerdo con reglas configuradas por el tenant, sin convertir el motor en un esquema de competencia dinámica por proximidad. |
| Entradas | Solicitud con marca urgente = true |
| Proceso | Prioriza la solicitud dentro de la lógica programada del motor y, cuando no exista capacidad suficiente, genera una situación de excepción para intervención del Operador logístico conforme a las reglas vigentes. |
| Salidas | Asignación priorizada o alerta de imposibilidad de asignación |
| Precondiciones | La solicitud debe estar marcada como urgente y el tenant debe tener reglas de priorización activas |
| Postcondiciones | La decisión queda registrada para trazabilidad y, si requiere intervención, se notifica al Operador logístico |
| Prioridad | Media |

## **Módulo 7 — Integración con WhatsApp**

**RF-14 — Recepción de mensajes desde WhatsApp**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Plataforma WhatsApp Business / proveedor seleccionado en ADR-0008 |
| Descripción | El sistema deberá recibir mensajes entrantes de pasajero y conductor vía webhook HTTP desde la plataforma WhatsApp Business. La integración se implementa mediante un adaptador desacoplado del proveedor, preservando el contrato funcional del webhook y del envío de mensajes. |
| Entradas | Payload HTTP POST del proveedor seleccionado con contenido del mensaje, número de origen y metadatos |
| Proceso | Valida autenticidad del webhook según el proveedor seleccionado, identifica al usuario y despacha el mensaje al flujo conversacional correspondiente |
| Salidas | Respuesta HTTP conforme al contrato del proveedor; mensaje disponible para procesamiento |
| Precondiciones | El usuario debe tener número de WhatsApp asociado a su cuenta |
| Postcondiciones | El mensaje queda disponible para procesamiento por el bot conversacional |
| Prioridad | Alta |

**RF-15 — Procesamiento de solicitudes enviadas por WhatsApp**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Bot conversacional interno |
| Descripción | El sistema deberá interpretar los mensajes conversacionales del pasajero, mantener el estado de la conversación y convertirlos en solicitudes de transporte estructuradas. |
| Entradas | Mensaje entrante + estado de conversación previo |
| Proceso | Aplica la máquina de estados conversacional, extrae datos (origen, destino, fecha, urgencia), y cuando la solicitud está completa la persiste vía RF-09 |
| Salidas | Solicitud de transporte creada; respuesta al pasajero solicitando el siguiente dato o confirmando |
| Precondiciones | El mensaje debe provenir de un usuario identificado |
| Postcondiciones | La solicitud queda registrada o la conversación continúa esperando más datos |
| Prioridad | Alta |

**RF-16 — Envío de confirmaciones al usuario por WhatsApp**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Servicio de Notificaciones |
| Descripción | El sistema deberá enviar al pasajero una confirmación por WhatsApp cuando su solicitud haya sido registrada y cuando haya sido asignada. |
| Entradas | Evento de cambio de estado de solicitud (Pendiente → Asignada) |
| Proceso | Genera el mensaje de confirmación con los datos operativos pertinentes y lo envía mediante el adaptador de WhatsApp configurado para FlowRide |
| Salidas | Mensaje entregado al pasajero |
| Precondiciones | La solicitud debe estar en estado Asignada |
| Postcondiciones | El pasajero conoce el estado y detalles de su solicitud |
| Prioridad | Alta |

**RF-17 — Envío de notificaciones al conductor asignado**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Servicio de Notificaciones |
| Descripción | El sistema deberá notificar al conductor por WhatsApp cuando se le asigne un nuevo servicio, incluyendo los datos del pasajero, origen, destino y hora. |
| Entradas | Evento de creación de asignación |
| Proceso | Genera el mensaje con los detalles del servicio y lo envía al número WhatsApp del conductor |
| Salidas | Notificación entregada al conductor |
| Precondiciones | El conductor debe tener número WhatsApp registrado |
| Postcondiciones | El conductor puede aceptar o rechazar la asignación desde WhatsApp |
| Prioridad | Alta |

**RF-18 — Actualización de estado del viaje desde WhatsApp**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Conductor (vía WhatsApp) |
| Descripción | El sistema deberá permitir al conductor actualizar el estado del viaje (inicio, finalización, incidente) enviando mensajes por WhatsApp que se sincronicen con el estado interno del sistema. |
| Entradas | Mensaje del conductor con palabra clave (INICIO, FIN, INCIDENTE) + viajeId implícito por conversación |
| Proceso | Interpreta el comando, valida que el conductor esté asignado al viaje, actualiza el estado y notifica al pasajero y al operador si corresponde |
| Salidas | Estado del viaje actualizado; notificación de confirmación al conductor |
| Precondiciones | El conductor debe tener una asignación activa |
| Postcondiciones | El estado del viaje refleja la realidad operativa; se disparan las notificaciones correspondientes |
| Prioridad | Alta |

## Módulo 8 — Multi-tenancy (modelo Silo)

RF-19 — Aislamiento físico entre tenants (modelo Silo)

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Sistema (transversal a todas las operaciones) |
| Descripción | El sistema deberá garantizar aislamiento físico entre operadores de transporte (tenants) mediante el modelo Silo, evitando que una sesión o proceso de un operador acceda a la persistencia de otro. |
| Entradas | Petición autenticada con contexto de tenant/operador resuelto por la plataforma |
| Proceso | Resuelve el tenant de la petición y enruta el acceso únicamente hacia la instancia y persistencia Silo del operador autorizado. empresaId no actúa como filtro de aislamiento entre tenants; identifica al cliente empresarial dentro de la BD del operador. |
| Salidas | Respuesta obtenida exclusivamente desde el entorno de datos del tenant autorizado |
| Precondiciones | El usuario debe estar autenticado y asociado a un tenant válido |
| Postcondiciones | No existe acceso cruzado entre persistencias de tenants; el aislamiento se verifica con pruebas automatizadas y controles de aprovisionamiento |
| Prioridad | Alta |

RF-20 — Configuración separada de usuarios y recursos por tenant

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Administrador del operador |
| Descripción | El sistema deberá permitir que cada tenant tenga su propia configuración de usuarios, conductores, vehículos, reglas de asignación y parámetros operativos dentro de su instancia Silo. |
| Entradas | Datos de configuración específicos del tenant |
| Proceso | Persiste la configuración dentro del entorno del tenant, garantizando que los cambios no afecten instancias de otros operadores |
| Salidas | Configuración aplicada exclusivamente al tenant del operador |
| Precondiciones | El administrador debe estar autenticado y autorizado en el tenant |
| Postcondiciones | Cada tenant opera con su configuración independiente |
| Prioridad | Alta |

## **Módulo 9 — Configuración Avanzada**

**RF-21 — Configuración avanzada de reglas de asignación**

| **Campo** | **Contenido** |
| --- | --- |
| Actor | Administrador del operador |
| Descripción | El sistema deberá permitir definir, activar, desactivar y ordenar reglas de asignación aplicables al tenant (por ejemplo: disponibilidad programada, minimización de cambios de turno, balance de carga, distancia del servicio y restricciones regulatorias). No se usa cercanía GPS para reasignación dinámica. |
| Entradas | Definición de reglas con parámetros, orden de aplicación y condiciones de activación |
| Proceso | Valida la coherencia de las reglas, las persiste dentro del tenant y las pone a disposición del motor para solicitudes posteriores |
| Salidas | Reglas configuradas y activas para el tenant |
| Precondiciones | El administrador debe pertenecer al tenant que configura |
| Postcondiciones | El motor de asignación aplica las nuevas reglas a partir de la siguiente solicitud |
| Prioridad | Media |

## Capacidades V1 complementarias

RF-54 (Calificación pasajero → conductor) forma parte del alcance V1 y
completa la línea base funcional. RF-55 (Ingesta de telemetría GPS) y
RF-56 (Detección de desviación de ETA) estaban previstas para V1 y
quedaron fuera de su alcance (ADR-0005 en estado *Proposed*); se
conservan documentadas por trazabilidad.

### RF-54 — Calificación del servicio

| Campo | Contenido |
| --- | --- |
| Actor | Pasajero vía WhatsApp |
| Descripción | El sistema deberá permitir que el pasajero califique al conductor al finalizar un Viaje, mediante un puntaje y comentario opcional. |
| Entradas | viajeId, puntaje, comentario opcional |
| Proceso | Valida que el viaje haya finalizado y que el pasajero esté asociado al servicio; registra la Calificacion vinculada al Viaje. |
| Salidas | Confirmación de calificación registrada |
| Precondiciones | Viaje finalizado y pasajero autorizado para calificarlo |
| Postcondiciones | La calificación queda disponible para reportes operacionales agregados por conductor, cliente empresarial u operador |
| Prioridad | Alta |

### RF-55 — Ingesta de telemetría GPS (fuera del alcance de V1)

| Campo | Contenido |
| --- | --- |
| Actor | Fuente de telemetría GPS configurada para el tenant |
| Descripción | El sistema deberá recibir y almacenar eventos de ubicación de los vehículos/conductores para viajes en curso. |
| Entradas | vehiculoId, viajeId cuando aplique, timestamp, latitud, longitud y metadatos de la fuente |
| Proceso | Valida el evento, identifica el tenant y el viaje, y persiste RegistroUbicacion según la estrategia de persistencia de telemetría documentada para FlowRide. |
| Salidas | Evento de ubicación aceptado o rechazado con trazabilidad |
| Precondiciones | Fuente de telemetría autenticada/autorizada y vehículo identificado |
| Postcondiciones | La ubicación queda disponible para monitoreo y análisis de desviación de ETA, no para reasignación dinámica por cercanía |
| Prioridad | Alta |

### RF-56 — Detección de desviación de ETA (fuera del alcance de V1)

| Campo | Contenido |
| --- | --- |
| Actor | Módulo de Telemetría / sistema |
| Descripción | El sistema deberá detectar desviaciones relevantes de ETA durante viajes en curso a partir de la telemetría GPS y generar una alerta operativa. |
| Entradas | Secuencia de RegistroUbicacion + datos programados del Viaje |
| Proceso | Evalúa la ubicación y el progreso del viaje contra la ETA programada y los umbrales configurados para el servicio. |
| Salidas | Estado/alerta de desviación para Administrador del operador y Operador logístico; notificación proactiva cuando corresponda |
| Precondiciones | Viaje en curso con telemetría disponible |
| Postcondiciones | La desviación queda registrada para monitoreo y trazabilidad |
| Prioridad | Alta |

## **Catálogo abreviado — Requisitos no significativos para la arquitectura**

Los siguientes requisitos completan la línea base funcional de FlowRide.
Corresponden a operaciones necesarias del producto que no condicionan
por sí solas la arquitectura, porque se resuelven mediante patrones
conocidos, reutilizan decisiones transversales o tienen menor impacto
arquitectónico. Los reportes operacionales básicos y la observabilidad
técnica permanecen dentro del alcance.

**Autenticación y acceso (2):**

-   RF-22 Cierre de sesión — permite al usuario cerrar su sesión
    activa e invalidar el contexto local.

-   RF-23 Recuperación o restablecimiento de credenciales — permite al
    usuario recuperar el acceso mediante correo electrónico.

**Gestión de usuarios (2):**

-   RF-24 Actualización de información de usuarios — permite modificar
    datos personales de los usuarios existentes.

-   RF-25 Activación e inactivación de usuarios — permite al
    Administrador del operador habilitar o deshabilitar el acceso de un
    usuario.

**Gestión de conductores (3):**

-   RF-26 Registro de conductores — permite dar de alta un nuevo
    conductor con sus datos operativos y licencia.

-   RF-27 Actualización de datos de conductores — permite modificar
    información del conductor.

-   RF-28 Activación e inactivación de conductores — permite habilitar
    o deshabilitar operativamente a un conductor.

**Gestión de itinerarios y solicitudes (4):**

-   RF-29 Validación de datos mínimos de solicitud — verifica que la
    solicitud contenga los campos obligatorios.

-   RF-30 Consulta del estado de solicitudes — permite al pasajero u
    operador ver el estado actual de una solicitud.

-   RF-31 Historial de solicitudes y viajes — permite consultar el
    histórico de solicitudes y viajes por usuario o cliente empresarial.

-   RF-32 Modificación o cancelación de solicitudes — permite cancelar
    o ajustar una solicitud conforme a las reglas del flujo operativo.

**Asignación de transporte (2):**

-   RF-33 Visualización de asignaciones generadas — permite al
    operador ver las asignaciones creadas por el motor.

-   RF-34 Actualización del estado de asignación — permite marcar una
    asignación como aceptada, rechazada, en curso o finalizada.

**Integración con WhatsApp (1):**

-   RF-35 Envío de recordatorios automáticos — envía recordatorios
    programados de viajes próximos al pasajero y al conductor.

**Panel web administrativo (7):**

-   RF-36 Visualización centralizada de solicitudes y viajes — muestra
    una vista unificada de la operación.

-   RF-37 Monitoreo operativo de servicios en curso — permite ver en
    tiempo real los servicios activos.

-   RF-38 Consulta de usuarios, conductores y asignaciones — permite
    consultar el catálogo operativo del tenant.

-   RF-39 Intervención manual en la operación — permite al Operador
    logístico tomar control manual de un servicio cuando exista una
    contingencia.

-   RF-40 Consulta de trazabilidad de cada viaje — muestra el
    histórico completo de eventos de un viaje.

-   RF-41 Búsqueda y filtrado de información — permite filtrar y
    buscar en los listados operativos.

-   RF-42 Visualización de indicadores básicos — muestra métricas
    operacionales simples de la operación diaria.

**Notificaciones (3):**

-   RF-43 Notificación automática de cambios de estado — notifica al
    usuario cuando cambia el estado de su servicio.

-   RF-44 Notificación de reasignación o novedades — informa al
    pasajero y conductor sobre cambios operativos relevantes.

-   RF-45 Alertas operativas para administradores u operadores ---
    notifica eventos que requieren atención.

**Reportes (4):**

-   RF-46 Reporte de viajes realizados — genera reporte de viajes
    completados en un período.

-   RF-47 Reporte de solicitudes atendidas y pendientes — muestra el
    estado agregado de solicitudes.

-   RF-48 Reporte de conductores asignados — muestra actividad y carga
    de trabajo por conductor.

-   RF-49 Reporte histórico de operación por cliente empresarial ---
    genera un reporte agregado histórico dentro del tenant.

**Logging y monitoreo (4):**

-   RF-50 Registro de eventos del sistema — registra eventos
    operativos para trazabilidad técnica.

-   RF-51 Registro de errores y excepciones — registra errores
    técnicos con contexto para diagnóstico.

-   RF-52 Seguimiento de acciones de usuarios administrativos ---
    registra en auditoría las acciones sensibles.

-   RF-53 Monitoreo técnico de disponibilidad del sistema — expone
    métricas de disponibilidad y salud del sistema.

Este filtro se aplica sobre la línea base funcional RF-01 a RF-56
definida en este documento. Una funcionalidad se considera
arquitectónicamente significativa cuando aporta valor de negocio
diferencial o introduce un reto tecnológico que condiciona decisiones de
diseño, integración, persistencia, seguridad, disponibilidad o
despliegue.

# Filtro de significancia arquitectónica

(recordatorio):

Una funcionalidad es significativa arquitectónicamente si cumple al
menos una condición:

Valor de negocio (VN): es lo que diferencia a FlowRide; el negocio no
existe sin esto.

Reto tecnológico (RT): hay que investigar o probar cómo hacerlo; no es
un patrón conocido de escritorio.

Todo lo demás son dependencias: cosas necesarias, pero que no marcan la
arquitectura porque son patrones CRUD estándar o features secundarias.

## Tabla A — Requisitos funcionales SIGNIFICATIVOS para la arquitectura

| **\#** | **Requisito funcional** | **Módulo** | **Tipo** | **Justificación** |
| --- | --- | --- | --- | --- |
| RF-01 | Autenticación mediante mecanismo de identidad basado en token | Autenticación | RT | Reto técnico vigente por selección de proveedor, estrategia de token, revocación y desacople. La autenticación se mantiene desacoplada del proveedor concreto; ADR-03 documenta la decisión tecnológica sin alterar este requisito funcional. |
| RF-02 | Autorización basada en roles | Autenticación | VN | Los cuatro roles operativos (Pasajero, Conductor, Administrador del operador, Operador logístico) son parte del modelo específico de transporte corporativo interno. |
| RF-03 | Registro de operador de transporte como tenant | Gestión de tenants | VN + RT | Es el punto de entrada del SaaS multi-tenant. Cada alta implica aprovisionar un tenant con aislamiento físico Silo, incluyendo su persistencia, configuración operativa y ciclo indesujeto a migraciones y respaldo. |
| RF-04 | Configuración inicial del operador | Gestión de tenants | VN | Habilita que cada tenant configure sus propios recursos y reglas dentro de su instancia aislada. |
| RF-05 | Creación de usuarios por tenant | Gestion de usuarios | VN | Alta de usuarios dentro del tenant. La asociación al operador sustituye el uso de empresaId como mecanismo de aislamiento entre tenants. |
| RF-06 | Asignación de roles a usuarios | Gestión de usuarios | VN | Habilita los cuatro roles operativos que son diferenciador (ver RF-02). |
| RF-07 | Consulta de disponibilidad de conductores | Gestión de conductores | VN | Es el insumo crítico del motor de asignación. Sin esto, no hay asignación automática. |
| RF-08 | Registro de itinerarios | Itinerarios y solicitudes | VN | El itinerario es el objeto central del dominio. Sin itinerario no hay servicio que asignar. |
| RF-09 | Recepción de solicitudes de transporte | Itinerarios y solicitudes | VN | Core del negocio. Es lo que dispara todo el flujo operativo. |
| RF-10 | Asignación automática de conductores | Motor de asignación | VN + RT | Core del negocio: externaliza la lógica de asignación que hoy vive en la cabeza del administrador y la vuelve configurable por tenant. |
| RF-11 | Aplicación de reglas de negocio para la asignación | Motor de asignación | VN + RT | Motor de reglas configurable por operador, orientado a turnos y programación. La telemetría no se usa para competencia dinámica por cercanía. |
| RF-12 | Reasignación manual de conductores | Motor de asignación | VN | Es necesaria para contingencias reales del caso ancla, como un conductor que abandona su turno y obliga a redistribuir servicios. |
| RF-13 | Priorización de solicitudes urgentes | Motor de asignación | VN + RT | Permite priorización operativa según reglas del tenant y gestión de excepciones sin convertir el motor en ride-hailing por proximidad. |
| RF-14 | Recepción de mensajes desde WhatsApp | Integración WhatsApp | VN + RT | Canal principal para pasajero y conductor. Reto técnico: webhooks, contrato de integración, idempotencia, seguridad y desacople del proveedor mediante un adaptador específico. |
| RF-15 | Procesamiento de solicitudes enviadas por WhatsApp | Integración WhatsApp | RT | Parseo conversacional + máquina de estados de conversación. No es CRUD; requiere investigar patrones de bots conversacionales. |
| RF-16 | Envío de confirmaciones al usuario por WhatsApp | Integración WhatsApp | VN | Cierra el flujo. Sin confirmación bidireccional, el pasajero no sabe si su solicitud fue procesada. |
| RF-17 | Envío de notificaciones al conductor asignado | Integración WhatsApp | VN | Cierra el otro extremo del flujo. Sin esto, la asignación no llega al conductor. |
| RF-18 | Actualización de estado del viaje desde WhatsApp | Integración WhatsApp | RT | Reto técnico: mantener sincronización bidireccional entre el estado del sistema y el estado reportado por conductor vía chat. |
| RF-19 | Aislamiento físico entre tenants (Silo) | Multi-tenancy | VN + RT | Diferenciador confirmado: un tenant = un operador de transporte con persistencia físicamente aislada. empresaId identifica al cliente empresarial dentro de la BD del operador y no funciona como filtro entre tenants. |
| RF-20 | Configuración separada de usuarios y recursos por cliente | Multi-tenancy | RT | Configuración independiente por tenant dentro de su instancia Silo, sin compartir persistencia con otros operadores. |
| RF-21 | Configuración avanzada de reglas de asignación | Configuración avanzada | VN + RT | Reglas parametrizables por tenant = diferenciador. Deben soportar turnos, balance de carga y restricciones operativas/regulatorias; no reasignación dinámica por cercanía GPS. |
| RF-54 | Calificación pasajero → conductor | Calificaciones | VN | Capacidad incluida explícitamente en V1. Agrega trazabilidad de calidad operacional sobre Viaje y alimenta reportes básicos. |
| RF-55 | Ingesta de telemetría GPS | Telemetría | VN + RT | Prevista originalmente para V1 y aplazada (ADR-0005). Introduce RegistroUbicacion, tratamiento de datos de ubicación y una estrategia de ingesta/persistencia adecuada al volumen operativo. |
| RF-56 | Detección de desviación de ETA | Telemetría | VN + RT | Fuera del alcance de V1 (ADR-0005). Usa telemetría para monitoreo de viajes en curso y alertas de desviación. No cambia el motor a asignación dinámica por proximidad. |

*VN = Valor de Negocio · RT = Reto Técnico*

## Tabla B — Requisitos funcionales NO significativos (dependencias)

| **Razón de descarte** | **Requisitos funcionales** |
| --- | --- |
| CRUD estándar — patrón conocido, no requiere decisiones arquitectónicas nuevas | Cierre de sesión; Recuperación de credenciales; Actualización de información de usuarios; Activación/inactivación de usuarios; Registro de conductores; Actualización de datos de conductores; Activación/inactivación de conductores; Validación de datos mínimos de solicitud; Consulta del estado de solicitudes; Historial de solicitudes y viajes; Modificación o cancelación de solicitudes; Visualización de asignaciones generadas; Actualización del estado de asignación; Visualización centralizada de solicitudes y viajes; Consulta de usuarios/conductores/asignaciones; Búsqueda y filtrado de información; Visualización de indicadores básicos |
| Feature secundaria — aporta valor pero no moldea la arquitectura | Envío de recordatorios automáticos; Notificación automática de cambios de estado; Notificación de reasignación o novedades; Alertas operativas para administradores u operadores |
| Reportes operacionales — necesarios para el producto, pero no introducen por sí solos una decisión arquitectónica adicional | Reporte de viajes realizados; Reporte de solicitudes atendidas y pendientes; Reporte de conductores asignados; Reporte histórico de operación por cliente empresarial |
| Observabilidad transversal — cubierta por logging, auditoría y monitoreo técnico | Registro de eventos del sistema; Registro de errores y excepciones; Seguimiento de acciones de usuarios administrativos; Monitoreo técnico de disponibilidad |
| Operación estándar del Panel Web — necesaria, pero no toda es driver arquitectónico | Intervención manual estándar; consulta de catálogos; búsqueda y filtrado. El monitoreo en tiempo real asociado a telemetría se cubre por RF-55 y RF-56 (fuera de V1; en V1 el monitoreo se hace por estados y horarios). |
