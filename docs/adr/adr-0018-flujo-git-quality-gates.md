# ADR-0018: Flujo de trabajo Git y quality gates automáticos en la integración

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0018 |
| Fecha | 2026-10-13 |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Origen | Formaliza las restricciones técnicas 18 (*Control de versiones y calidad*) y 19 (*Código y pruebas*) del DAS v2.0 |

## Contexto

Las restricciones técnicas 18 y 19 exigen:
- Git/GitHub con trabajo por ramas;
- revisión mediante Pull Request;
- análisis estático automatizado antes de integrar a `main`;
- pruebas automatizadas sobre las reglas de negocio y los flujos críticos.

Además, los ADR existentes ya dependen de controles que hoy no están formalizados en ningún ADR:
- límites entre capas y módulos (ADR-0001, ADR-0017);
- escaneo de secretos en CI (ADR-0014);
- imágenes inmutables generadas en cada merge a `main` (ADR-0010).

Sin un acuerdo explícito, esos controles dependen de la disciplina de cada integrante y se degradan con el tiempo.

## Decisión

### 1. Ramas
| Rama | Uso | Protegida |
|---|---|---|
| `main` | Versión liberable; cada merge genera la imagen versionada (ADR-0010) | Sí: solo por PR desde `develop` (release) o `hotfix/*` |
| `develop` | Integración continua de las fases | Sí: solo por PR |
| `feature/RF-XX-descripcion` | Una rama por fase o requisito (por ejemplo `feature/RF-10-asignacion-automatica`) | No |
| `fix/…`, `chore/…`, `docs/…` | Correcciones y tareas no funcionales | No |

- PR de `feature/*` hacia `develop`: requiere CI en verde y una revisión.
- PR de *release*, de `develop` hacia `main`: requiere CI en verde y análisis estático (restricción 18).

### 2. Commits
**Conventional Commits**, con el módulo como *scope*:
- `tipo(modulo): descripción` en español, por ejemplo `feat(asignacion): …` o `fix(mensajeria): …`;
- tipos: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `ci`, `perf`;
- scopes: `tenancy`, `identidad`, `operacion`, `solicitudes`, `asignacion`, `mensajeria`, `notificaciones`, `viajes`, `calificacion`, `auditoria`, `plataforma`, `web`.

### 3. Quality gates (GitHub Actions, en cada PR)
| Gate | Criterio de aprobación |
|---|---|
| Compilación | `dotnet build` sin advertencias (`TreatWarningsAsErrors`, ADR-0017) |
| Formato | `dotnet format --verify-no-changes` |
| Pruebas unitarias | 100 % en verde |
| Pruebas de arquitectura | ArchUnitNET: dependencias de capa hacia el dominio y sin ciclos entre módulos (ADR-0001) |
| Cobertura | Domain + Application: al menos **80 % de líneas** y **70 % de ramas** |
| Pruebas de integración | Testcontainers (PostgreSQL y Keycloak reales), marcadas con `[Trait("Category","Integration")]` |
| Secretos | Escaneo de secretos sin hallazgos (ADR-0014) |
| Análisis estático | SonarCloud o equivalente: sin *issues* bloqueantes (restricción 18) |
| Frontend | Lint, pruebas y build del Panel Web (ADR-0019) |

### 4. Definición de terminado por fase
Una fase se cierra cuando:
- sus RF funcionan de punta a punta;
- tiene un archivo `.http` o pasos de prueba documentados;
- `docs/ROADMAP-V1.md` está actualizado;
- el PR hacia `develop` pasa todos los gates.

## Alternativas consideradas

| Criterio | **feature → develop → main (elegida)** | A. GitHub Flow (solo `main`) | B. Trunk-based con feature flags | C. Gitflow completo (release/*, hotfix/*) |
|---|---|---|---|---|
| Separa integración de liberación | Sí | No | Con flags | Sí |
| Cumple "análisis antes de integrar a `main`" | Sí | Sí | Sí | Sí |
| Complejidad para un equipo de 3 | Baja | Muy baja | Media (flags) | Alta |
| Encaje con entregas por fase del curso | Alto | Medio | Medio | Alto, con sobrecosto |

- **A** — descartada: cada merge a `main` generaría una imagen (ADR-0010) incluso para fases a medio integrar.
- **B** — descartada: los *feature flags* agregan una infraestructura que no tiene un driver que la justifique.
- **C** — descartada: las ramas `release/*` no aportan valor con un único entorno académico.

## Consecuencias

**Positivas**
- Los límites de ADR-0001 y ADR-0017 se verifican automáticamente, no solo por revisión humana.
- El historial es legible por módulo y se puede generar un *changelog*.
- `main` siempre es desplegable.

**Negativas**
- El pipeline tarda más, porque las pruebas de integración levantan contenedores.
- El umbral de cobertura puede incentivar pruebas de bajo valor; la revisión del PR debe vigilarlo.

## Tácticas de Bass aplicadas
- **Detectar fallos tempranamente**: gates en cada PR.
- **Restringir dependencias**: pruebas de arquitectura.
- **Limitar exposición**: escaneo de secretos.

## ADRs relacionados
- **ADR-0001** y **ADR-0017**: límites que verifican las pruebas de arquitectura.
- **ADR-0010**: imagen inmutable por cada merge a `main`.
- **ADR-0014**: escaneo de secretos.
- **ADR-0019**: gates del frontend.
