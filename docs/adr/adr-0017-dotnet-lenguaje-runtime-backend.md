# ADR-0017: Adoptar C# sobre .NET 10 como lenguaje y runtime del backend

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0017 |
| Fecha | 2026-09-23 |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Building block | Lenguaje y runtime del backend |

## Contexto

FlowRide se implementa como un monolito modular con límites internos explícitos (ADR-0001), desplegado como un stack contenedorizado por operador bajo el modelo Silo (ADR-0002, ADR-0010), con persistencia en PostgreSQL vía Npgsql (ADR-0004), identidad desacoplada mediante OIDC/OAuth2 (ADR-0003), un motor de asignación con reglas configurables (ADR-0009), integración con WhatsApp por webhooks (ADR-0008), ingesta de telemetría GPS (ADR-0005) y un borde común de plataforma (API Gateway + WAF, ADR-0012).

El backend debe cumplir, entre otros:

- **Rendimiento**: P95 < 500 ms y P99 < 1,5 s para operaciones CRUD, con hasta 200 usuarios concurrentes por instancia (Escenario 1).
- **Mantenibilidad**: estructura por capas (Domain, Application, Infrastructure, API) con dependencias dirigidas hacia el dominio y bajo acoplamiento (Escenario 5, ADR-0001).
- **Modelo de dominio**: DDD con lenguaje ubicuo y límites claros entre responsabilidades operativas (restricción técnica *Dominio*).
- **Concurrencia asíncrona**: ingesta de webhooks de WhatsApp, eventos de telemetría GPS y consultas concurrentes sin bloquear hilos.
- **Costo de infraestructura**: aproximadamente 20–40 USD/mes por tenant (ADR-0010), lo que exige un runtime con consumo de memoria predecible al escalar réplicas por operador.
- **Restricción de equipo**: el proyecto se desarrolla en el marco de un curso universitario con cronograma fijo (semestre 2-2026); el equipo no dispone de tiempo para adoptar un ecosistema nuevo sin curva de aprendizaje.

Actualmente, `RestriccionesTecnicas_FlowRide_ACTUALIZADO.xlsx` ya fija como restricción de plataforma que "el backend de FlowRide se desarrolla en C# sobre .NET 10". Este ADR formaliza esa restricción como decisión arquitectónica, documentando las alternativas evaluadas y las razones para mantenerla en lugar de tratarla como un supuesto implícito.

## Decisión

Adoptar **C# sobre .NET 10** como lenguaje y runtime único del backend de FlowRide, con **ASP.NET Core** como framework web para exponer la API REST, integrado con **EF Core/Npgsql** para el acceso a PostgreSQL (ADR-0004).

El mismo stack (.NET) se reutiliza en el borde de plataforma mediante **YARP** como reverse proxy programable (ADR-0012), evitando introducir un segundo lenguaje/runtime solo para ese componente.

La organización interna del código seguirá la separación por capas ya establecida (`FlowRide.Domain`, `FlowRide.Application`, `FlowRide.Infrastructure`, `FlowRide.API`), aprovechando el sistema de tipos estático de C# para reforzar los límites entre capas definidos en ADR-0001.

## Alternativas consideradas

| Criterio | **.NET 10 / C# (elegida)** | A. Node.js / TypeScript (NestJS) | B. Java / Spring Boot | C. Python (Django / FastAPI) | D. Go |
|---|---|---|---|---|---|
| Tipado estático fuerte (soporta DDD y límites entre capas) | Sí | Parcial (TypeScript, erosionable en runtime) | Sí | No (tipado dinámico; type hints no forzados) | Sí, pero sin abstracciones de dominio ricas |
| Rendimiento asíncrono bajo concurrencia (P95/P99, 200 usuarios/instancia) | Alto | Alto en I/O, limitado en CPU-bound (single-thread event loop) | Alto | Medio-bajo (GIL limita CPU-bound; FastAPI mejora I/O) | Muy alto |
| Integración nativa con PostgreSQL vía ORM maduro (EF Core, ADR-0004) | Sí (Npgsql/EF Core) | Parcial (Prisma/TypeORM, menos maduro para Outbox/concurrencia optimista) | Sí (JPA/Hibernate) | Parcial (SQLAlchemy/Django ORM) | Limitado (ecosistema ORM menos maduro) |
| Consumo de memoria por instancia (afecta costo 20–40 USD/tenant) | Moderado | Moderado-bajo | Alto (runtime JVM) | Moderado-bajo | Muy bajo |
| Conocimiento previo del equipo (driver real: cronograma académico) | Sí | No | No | Parcial | No |
| Ejecución multiplataforma en contenedores Linux (ADR-0010) | Sí | Sí | Sí | Sí | Sí |
| Reutilizable como stack del borde/gateway (ADR-0012) | Sí (YARP) | Requiere stack adicional | Requiere stack adicional | Requiere stack adicional | Requiere stack adicional |

- **A. Node.js/TypeScript** — descartada como base del backend: el modelo de concurrencia de un único hilo de evento favorece cargas I/O-bound (webhooks), pero el motor de asignación incluye evaluación de reglas y scoring con carga de cómputo no trivial, donde .NET rinde mejor bajo concurrencia real. El tipado de TypeScript, además, se pierde en tiempo de ejecución.
- **B. Java/Spring Boot** — alternativa viable técnicamente (tipado fuerte, JPA maduro, buen rendimiento), pero descartada por mayor consumo de memoria en runtime JVM por instancia (impacta el presupuesto por tenant del modelo Silo) y por no ser el stack que el equipo domina dentro del cronograma del curso.
- **C. Python (Django/FastAPI)** — descartada: el tipado dinámico es una mala combinación con la restricción de dominio (DDD con límites internos explícitos, ADR-0001), y el rendimiento bajo concurrencia real es inferior al requerido para sostener P95 < 500 ms con 200 usuarios concurrentes.
- **D. Go** — técnicamente atractiva por rendimiento y bajo consumo de memoria, pero descartada: su ecosistema de acceso a datos es menos maduro para soportar el patrón Outbox con concurrencia optimista (ADR-0007), obliga a más código repetitivo para modelar el dominio, y el equipo no tiene experiencia previa.

## Consecuencias

**Positivas**

- Un único stack tecnológico de punta a punta (backend, motor de asignación, adapters de integración y borde/gateway vía YARP), lo que reduce la superficie tecnológica total a operar y aprender.
- El tipado estático de C# refuerza en tiempo de compilación los límites entre `Domain`, `Application`, `Infrastructure` y `API` definidos en ADR-0001.
- Integración nativa y madura con PostgreSQL/Npgsql (ADR-0004) y con el patrón Outbox/concurrencia optimista (ADR-0007), sin capas de traducción adicionales.
- Soporte async/await nativo adecuado para las cargas I/O-bound del sistema (webhooks de WhatsApp, ingesta de telemetría GPS) sin bloquear hilos.
- Aprovecha el conocimiento previo del equipo, reduciendo el riesgo de cronograma dentro del alcance académico.

**Negativas**

- Acopla al proyecto a un único ecosistema; introducir un módulo en otro lenguaje en el futuro (si apareciera un driver real) tendría un costo de integración mayor que en una arquitectura poliglota desde el inicio.
- El consumo de memoria del runtime .NET por instancia debe monitorearse conforme se agregan réplicas por operador, dentro del presupuesto de 20–40 USD/mes por tenant (ADR-0010).
- Limita la disponibilidad de talento externo al equipo actual a quienes conozcan o estén dispuestos a aprender C#/.NET.

## Tácticas de Bass aplicadas

- **Reducir acoplamiento / aumentar cohesión** — tipado estático que refuerza la separación por capas del monolito modular.
- **Mantener la integridad de la interfaz** — contratos explícitos entre capas y con los adapters, verificados en tiempo de compilación.
- **Introducir concurrencia** — soporte async/await nativo para operaciones I/O-bound (webhooks, telemetría, consultas concurrentes).
- **Encapsular variación** — un runtime único facilita aplicar Strategy/Policy en el motor de asignación (ADR-0009) sin fricción entre lenguajes.

## Escenarios y requisitos relacionados

- Escenario 1 (Rendimiento) y Escenario 5 (Mantenibilidad) de Drivers.
- Escenario 9 (Testabilidad) — la suite de tests corre sobre el mismo runtime en CI.
- RF-01 a RF-56 — transversal: todos los requisitos funcionales dependen del backend.

## ADRs relacionados

- **ADR-0001** — el lenguaje sostiene la organización modular del monolito.
- **ADR-0004** — Npgsql/EF Core se integran nativamente con .NET.
- **ADR-0007** — Outbox y concurrencia optimista se apoyan en el soporte transaccional de EF Core.
- **ADR-0010** — el runtime se empaqueta en contenedores inmutables por operador.
- **ADR-0012** — el borde de plataforma reutiliza el mismo stack mediante YARP.

## Notas de implementación

- Fijar la versión exacta del SDK/runtime de .NET 10 en el Dockerfile de cada imagen, para builds reproducibles (coherente con ADR-0010, imágenes inmutables).
- Habilitar Nullable Reference Types y analizadores estáticos (Roslyn analyzers) para reforzar en compilación el tipado fuerte que exige la restricción de dominio (DDD, lenguaje ubicuo).
- Aplicar async/await de punta a punta (Domain → Application → Infrastructure → API) para evitar bloqueos de hilos bajo carga concurrente.
- Revisar el consumo de memoria por instancia en pruebas de carga antes de fijar el número de réplicas por operador.

## Referencias

- Microsoft — documentación oficial de .NET 10.
- Microsoft — documentación de ASP.NET Core.
- `RestriccionesTecnicas_FlowRide_ACTUALIZADO.xlsx` — restricción "Plataforma tecnológica".
