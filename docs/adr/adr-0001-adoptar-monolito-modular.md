# ADR-0001: adoptar monolito modular con límites internos explícitos

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0001 |
| Fecha | 2026-09-09 |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |

## Contexto

FlowRide es un SaaS B2B2B para operadores de transporte corporativo con servicios programados, horarios estrictos y flota propia o de uso exclusivo. El sistema debe soportar hasta 200 usuarios simultáneos por instancia, mantener una operación transaccional consistente y evolucionar sin introducir complejidad de distribución que no esté respaldada por un driver real.


## Decisión

Construir FlowRide como un **monolito modular**, con límites de módulo explícitos y dependencias dirigidas hacia el núcleo del negocio.

Mantener la solución organizada en proyectos, sin interpretarse como microservicios. Los módulos internos comparten proceso de despliegue, pero no deben acoplar su lógica de negocio mediante acceso directo a implementaciones internas de otros módulos.

Como SaaS B2B2B, FlowRide es un único producto licenciado a múltiples operadores, no un desarrollo a medida por cliente. Esta naturaleza de producto refuerza la elección: cada tenant despliega la misma versión del mismo artefacto , lo que mantiene un ciclo de release uniforme y evita que la topología interna del sistema diverja entre operadores 

El Panel Web consume la API directamente por HTTP/JSON. WhatsApp, telemetría y proveedores externos se integran mediante adapters.

## Alternativas consideradas

Se evaluaron tres alternativas frente a la elegida. El veredicto resumido de cada una:

- **A. Microservicios** — descartada: sin driver que exija escalado o despliegue diferenciado por módulo, solo añade costo de coordinación distribuida.
- **B. Monolito sin modularización** — descartada: sin límites internos, favorece acoplamiento transversal y dificulta evolucionar el motor de asignación.
- **C. Serverless por función** — descartada: dispersa el dominio en funciones aisladas y complica consistencia y depuración.

El detalle de la comparación por atributo de calidad está en la tabla siguiente.

### Comparación de alternativas por atributo de calidad

| Atributo / Criterio | Monolito modular (elegida) | A. Microservicios | B. Monolito sin modularizar | C. Serverless por función |
|---|---|---|---|---|
| Testabilidad | Cumple — límites de módulo permiten aislar pruebas sin infraestructura distribuida | Cumple, con costo de mocks/contratos entre servicios | No cumple — dependencias cruzadas impiden aislar módulos | Parcial — testeable por función, pero el flujo end-to-end se complica |
| Despliegue / operación | Cumple — un solo artefacto, apto para instancia por tenant (Silo) | No cumple sin driver — exige orquestación y observabilidad distribuida | Cumple en simplicidad, sin aislar fallos internos | Parcial — gestionado por el proveedor, pero dispersa el dominio |
| Escalabilidad | Parcial — escala la instancia completa por tenant, suficiente para 200 usuarios concurrentes | Cumple — escala por servicio, sin necesidad demostrada en V1 | Parcial — igual al monolito modular sin aislar el módulo más pesado | Cumple — escala por función, sin driver que lo justifique |
| Costo operacional | Cumple — un único proceso reduce costo frente a múltiples servicios | No cumple — CI/CD y coordinación multi-servicio no justificados por el volumen actual | Cumple a corto plazo, acumula deuda técnica | Parcial — económico a bajo volumen, añade costo de coordinación distribuida |
| Localización de cambios | Cumple — los límites de módulo concentran el impacto | Cumple mejor por servicio, sin justificar el costo de coordinación entre equipos | No cumple — favorece dependencias transversales | No cumple — dominio disperso dificulta rastrear un cambio |
| Consistencia transaccional | Cumple — transacciones locales sin coordinación distribuida | No cumple sin driver — requiere consistencia eventual y compensación | Cumple, mismo motivo que el monolito modular | No cumple — complica garantizar consistencia |
| OPLOG-CAP-01 (picos operativos del tenant) | Cumple — el Silo por tenant absorbe la carga sin requerir distribución | Cumple, a costo de complejidad no requerida | Parcial — soporta la carga sin aislamiento interno de fallos | Parcial — depende de límites y cold-starts del proveedor |
| PAS-REND-02 (consultas simples) | Cumple — los límites de módulo evitan atravesar el motor completo | Cumple, con latencia de red adicional | Parcial — sin límites claros, invoca lógica no relacionada | Cumple, pero el cold-start puede penalizar |

**Lectura:** ninguna alternativa domina en todos los atributos, pero el monolito modular es la única que cumple de forma consistente los drivers vigentes (`OPLOG-CAP-01`, `PAS-REND-02`, mantenibilidad) sin incurrir en costos de coordinación distribuida que hoy no tienen respaldo. Microservicios y serverless quedan como extracción futura si aparece un driver real de escalado o despliegue diferenciado (ver ADR-0010).

## Consecuencias positivas

- Un único artefacto de aplicación simplifica despliegue, diagnóstico y pruebas end-to-end.
- Los módulos permiten localizar cambios sin introducir comunicación distribuida.
- Encaja con el aislamiento Silo: cada operador ejecuta una instancia lógica independiente del mismo producto.
- Permite evolucionar hacia extracción de servicios solo si un driver futuro lo justifica.

## Consecuencias negativas

- El ciclo de despliegue es compartido entre módulos.
- Un fallo de proceso puede afectar varios módulos de un mismo tenant si no se aplican límites y aislamiento internos.
- Requiere disciplina para impedir dependencias cruzadas que degraden el monolito modular en un monolito acoplado.

## Tácticas de Bass aplicadas

- **Reducir acoplamiento** — interfaces y adapters separan dominio de infraestructura y proveedores.
- **Aumentar cohesión** — cada módulo concentra responsabilidades de negocio relacionadas.
- **Localizar cambios** — las reglas y dependencias externas se encapsulan detrás de límites explícitos.

## Escenarios y requisitos relacionados

- `OPLOG-CAP-01` — el motor debe sostener picos operativos del tenant sin requerir distribución por microservicios.
- `PAS-REND-02` — las consultas simples no deben invocar lógica innecesaria del motor.
- `Escenario de mantenibilidad` — los cambios funcionales deben permanecer localizados.

## ADRs relacionados

- **ADR-0002** — define la frontera Silo por operador.
- **ADR-0009** — define la estructura extensible del motor de asignación.
- **ADR-0010** — define despliegue y escalado de cada instancia.

## Notas de implementación

- Las referencias entre proyectos deben mantener `Domain` sin dependencias hacia infraestructura.
- Los módulos no comparten detalles de proveedor; consumen interfaces de Application o contratos de dominio.
- Un módulo solo se extraerá como servicio independiente cuando exista evidencia de escalado, disponibilidad o despliegue diferenciado.

## Referencias

- Software Architecture in Practice — Bass, Clements y Kazman.