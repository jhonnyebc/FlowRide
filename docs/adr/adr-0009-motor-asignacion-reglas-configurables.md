# ADR-0009: Implementar el motor de asignación con reglas configurables y composición determinista

## Metadatos

| **Campo**          | **Valor**                                                                                                         |
| ------------------ | ----------------------------------------------------------------------------------------------------------------- |
| ID                 | ADR-0009                                                                                                         |
| Fecha              | 2026-09-09                                                                                                        |
| Estado             | **Accepted**                                                                                                      |
| Participantes      | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro                                   |
| Curso              | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026)                                                        |
| Profesor evaluador | Wider Farid Sánchez Garzón                                                                                        |
| Reemplaza          | ADR-04 histórico del tradeoff anterior, conservando el tema del motor de asignación pero actualizando las reglas. |

## Contexto

El principal valor diferencial de FlowRide es reemplazar la asignación manual que actualmente depende del conocimiento y criterio del administrador por un motor capaz de aplicar reglas programadas, configurables, explicables y auditables.

La operación de FlowRide no corresponde a un modelo de *ride-hailing* como Uber o DiDi. Los servicios de transporte corporativo son programados previamente y la información de ubicación GPS se utiliza principalmente para monitoreo operativo, seguimiento y estimación de tiempos de llegada —ETA—, no para realizar reasignaciones dinámicas basadas únicamente en cercanía geográfica.

El motor de asignación debe considerar diferentes factores del dominio, entre ellos:

* Disponibilidad programada del conductor.
* Compatibilidad con los turnos.
* Cambios de turno.
* Balance de carga entre conductores.
* Restricciones regulatorias.
* Características particulares del servicio.
* Priorización de solicitudes críticas.
* Reglas particulares definidas para cada tenant.
* Condiciones especiales de operación.

El motor debe permitir agregar nuevas reglas sin modificar las reglas existentes y debe mantener un orden de evaluación explícito.

También debe ser posible identificar posteriormente por qué un conductor fue seleccionado frente a otros candidatos.

La solución debe cumplir principalmente con los siguientes atributos de calidad:

* Modificabilidad.
* Trazabilidad.
* Auditabilidad.
* Testabilidad.
* Configurabilidad.
* Bajo acoplamiento.
* Alta cohesión.
* Reproducibilidad de las decisiones.

Además, la solución debe mantenerse coherente con la decisión arquitectónica general de FlowRide de utilizar un **monolito modular**, evitando introducir infraestructura externa cuando no exista una necesidad real que la justifique.

---

## Decisión

Implementar el motor de asignación como un componente interno del monolito modular utilizando **reglas encapsuladas detrás de una interfaz común**, siguiendo un enfoque de **Strategy / Policy internas configurables** con una composición determinista.

Cada regla podrá realizar una o varias de las siguientes funciones:

* Filtrar candidatos.
* Validar restricciones.
* Asignar puntuaciones.
* Definir prioridades.
* Participar en el desempate.

Las reglas estarán desacopladas entre sí y serán coordinadas por el motor de asignación.

El orden y la precedencia de ejecución deberán estar explícitamente configurados y ser auditables.

Una nueva regla no deberá requerir modificar las reglas existentes.

La configuración de reglas pertenecerá al tenant o silo correspondiente.

El motor diferenciará, como mínimo, los siguientes tipos de reglas:

1. Reglas eliminatorias.
2. Reglas de prioridad.
3. Reglas de puntuación.
4. Reglas de desempate.

La telemetría GPS quedará fuera del conjunto de criterios utilizados para realizar una reasignación dinámica tipo *ride-hailing*.

La telemetría únicamente podrá aportar información para:

* Monitoreo.
* Seguimiento del servicio.
* ETA.
* Detección de desviaciones.
* Alertas operacionales.

El resultado de una asignación debe poder reconstruirse posteriormente utilizando:

* Solicitud evaluada.
* Candidatos disponibles.
* Reglas activas.
* Orden de ejecución.
* Parámetros configurados.
* Pesos utilizados.
* Versión de configuración.
* Resultado de cada regla.
* Criterio de desempate.

---

# Alternativas consideradas

## Alternativa A — Condicionales hardcodeados dentro del servicio de asignación

Esta alternativa consiste en implementar toda la lógica de asignación directamente dentro de un único servicio utilizando estructuras condicionales como:

* `if`
* `else`
* `switch`
* Validaciones sucesivas

Ejemplo conceptual:

```text
Si conductor está disponible
    Si cumple turno
        Si cumple restricciones regulatorias
            Si puede atender el servicio
                Si tiene menor carga
                    asignar
```

Cada vez que aparezca una nueva regla de negocio sería necesario modificar directamente el servicio principal.

### Ventajas

* Implementación inicial rápida.
* Baja complejidad conceptual.
* No requiere patrones adicionales.
* Fácil de comprender cuando existen pocas reglas.
* Útil para prototipos tempranos.

### Desventajas

* Incrementa progresivamente la complejidad ciclomática.
* Cada nueva regla obliga a modificar código existente.
* Aumenta el riesgo de regresiones.
* Dificulta realizar pruebas unitarias individuales.
* Complica la configuración por tenant.
* Hace difícil identificar qué regla produjo una determinada decisión.
* Reduce la reutilización.
* Puede generar un servicio excesivamente grande.
* Dificulta mantener el principio Open/Closed.
* La lógica termina altamente acoplada.

### Decisión

**Descartada.**

Aunque presenta una implementación inicial sencilla, no resulta adecuada para un motor que debe crecer progresivamente y soportar múltiples reglas de negocio.

La incorporación continua de condiciones terminaría convirtiendo el servicio principal en un componente complejo, difícil de mantener y con alto riesgo de regresión.

---

## Alternativa B — Chain of Responsibility / Pipeline secuencial de reglas

Esta alternativa utiliza el patrón **Chain of Responsibility**.

Cada regla se representa como un eslabón de una cadena y recibe el resultado producido por la regla anterior.

Ejemplo conceptual:

```text
Conductores candidatos
        ↓
Disponibilidad
        ↓
Compatibilidad de turno
        ↓
Restricción regulatoria
        ↓
Compatibilidad del servicio
        ↓
Balance de carga
        ↓
Prioridad
        ↓
Selección
```

Cada elemento puede:

* Filtrar candidatos.
* Transformar información.
* Modificar resultados.
* Detener el procesamiento.
* Entregar el resultado a la siguiente regla.

### Ventajas

* Las reglas se encuentran separadas.
* Facilita agregar nuevas etapas.
* El orden de ejecución es visible.
* Permite realizar pruebas de cada etapa.
* Reduce condicionales dentro del servicio principal.
* Favorece la separación de responsabilidades.
* Se integra fácilmente dentro del monolito.

### Desventajas

* El resultado depende fuertemente del orden de ejecución.
* Una regla puede depender implícitamente del resultado de la anterior.
* Cambiar una posición puede modificar significativamente el comportamiento.
* Las reglas de puntuación no siempre se adaptan correctamente a una cadena secuencial.
* Algunas reglas necesitan comparar todos los candidatos simultáneamente.
* Puede mezclar reglas eliminatorias con reglas de puntuación.
* Puede aparecer acoplamiento implícito entre eslabones.

### Decisión

**Descartada.**

Aunque mejora considerablemente frente al uso de condicionales hardcodeados, el modelo de cadena estrictamente secuencial no representa correctamente la naturaleza de todas las reglas de FlowRide.

El motor necesita diferenciar entre filtrado, prioridad, puntuación y desempate, además de permitir configuraciones más flexibles.

---

## Alternativa C — Tabla de decisión configurable

Esta alternativa consiste en representar las reglas mediante una **Decision Table**.

Las combinaciones de condiciones producen una acción específica.

Ejemplo:

| Disponible | Turno compatible | Habilitado | Servicio crítico | Acción         |
| ---------- | ---------------- | ---------- | ---------------- | -------------- |
| Sí         | Sí               | Sí         | Sí               | Prioridad alta |
| Sí         | Sí               | Sí         | No               | Evaluar carga  |
| Sí         | No               | -          | -                | Descartar      |
| No         | -                | -          | -                | Descartar      |

La tabla podría almacenarse en:

* Base de datos.
* Archivo JSON.
* Configuración.
* Panel administrativo.

El motor evaluaría cada candidato frente a las combinaciones existentes.

### Ventajas

* Las reglas son fáciles de visualizar.
* Alta trazabilidad para decisiones discretas.
* Facilita auditoría.
* Algunas reglas pueden modificarse sin cambiar código.
* Permite cierta configuración por tenant.
* Reduce condicionales dentro del código.
* Puede ser comprensible para perfiles funcionales.

### Desventajas

* El número de combinaciones aumenta rápidamente.
* Puede producir una explosión combinatoria.
* Resulta difícil representar cálculos complejos.
* Es limitada para algoritmos de puntuación.
* Algunas reglas requerirían lógica adicional fuera de la tabla.
* Mantener tablas extensas puede convertirse en un problema.
* Pueden existir combinaciones no contempladas.
* Requiere mecanismos adicionales de validación.

### Decisión

**Descartada.**

Las reglas de FlowRide no se limitan a decisiones booleanas o combinaciones discretas.

El motor necesita manejar simultáneamente:

* Restricciones.
* Priorización.
* Scoring.
* Balance de carga.
* Desempates.

Una tabla de decisión podría utilizarse en casos específicos, pero no resulta suficientemente flexible como mecanismo principal del motor.

---

## Alternativa D — Specification Pattern + función centralizada de scoring

Esta alternativa utiliza el patrón **Specification** para representar restricciones del dominio.

Las especificaciones se pueden combinar mediante operadores como:

* `AND`
* `OR`
* `NOT`

Ejemplo:

```text
Conductores
    ↓
Disponible
    AND
TurnoCompatible
    AND
HabilitaciónRegulatoria
    AND
CompatibilidadServicio
    ↓
Candidatos válidos
    ↓
Scoring central
    ↓
Ranking
    ↓
Asignación
```

Las restricciones se encapsulan como especificaciones.

Luego, una función central calcula la puntuación de los candidatos válidos.

Ejemplo:

```text
Score =
    BalanceCarga
    + PrioridadServicio
    + Compatibilidad
    + CondicionesEspeciales
```

### Ventajas

* Muy buena representación de restricciones booleanas.
* Las condiciones son reutilizables.
* Permite componer reglas.
* Facilita pruebas unitarias.
* Separa restricciones de preferencias.
* Reduce estructuras condicionales.
* Encaja correctamente con conceptos del dominio.

### Desventajas

* El scoring central puede convertirse en un nuevo punto de alta complejidad.
* Agregar una nueva regla de puntuación puede requerir modificar el algoritmo principal.
* La lógica queda dividida entre especificaciones y scoring.
* La trazabilidad puede fragmentarse.
* No todas las reglas se expresan naturalmente mediante especificaciones.
* Tiene mayor extensibilidad para filtros que para scoring.

### Decisión

**Descartada.**

Esta alternativa resulta fuerte para representar restricciones, pero el motor de FlowRide también necesita que las reglas de puntuación y prioridad evolucionen independientemente.

Centralizar el scoring generaría nuevamente un componente que tendría que modificarse de manera frecuente.

---

## Alternativa E — Motor de reglas externo o DSL

Esta alternativa consiste en utilizar un motor especializado de reglas de negocio o un **Domain Specific Language — DSL**.

Las reglas se definirían mediante expresiones declarativas fuera del código principal.

Ejemplo:

```text
rule "Prioridad servicio crítico"
when
    servicio.critico == true
    and conductor.disponible == true
then
    score += 100
end
```

Otro ejemplo:

```text
rule "Excluir conductor fuera de turno"
when
    conductor.turnoActivo == false
then
    excluir conductor
end
```

Las reglas podrían configurarse sin modificar directamente las clases principales del dominio.

### Ventajas

* Alta flexibilidad.
* Excelente capacidad para incorporar nuevas reglas.
* Permite reglas dinámicas.
* Puede proporcionar herramientas avanzadas de auditoría.
* Permite versionamiento.
* Puede facilitar modificación sin recompilar.
* Es adecuado para sistemas con grandes cantidades de reglas.

### Desventajas

* Introduce una nueva tecnología.
* Aumenta la complejidad operacional.
* Requiere aprendizaje de un DSL o plataforma adicional.
* Incrementa la dificultad de diagnóstico.
* Puede dificultar la observabilidad.
* Puede complicar la consistencia transaccional.
* Introduce una dependencia tecnológica adicional.
* Puede aumentar los puntos de fallo.
* Puede ser excesivo para el alcance actual.

### Decisión

**Descartada para V1.**

Aunque ofrece alta flexibilidad, la cantidad y complejidad actual de las reglas de FlowRide no justifican introducir un motor externo.

La solución generaría costos tecnológicos y operacionales adicionales sin aportar un beneficio proporcional en la primera versión.

---

## Alternativa F — Strategy / Policy internas configurables

Esta alternativa consiste en representar cada criterio de asignación mediante una estrategia o política independiente que implemente una interfaz común.

Ejemplo:

```text
IAssignmentRule
    ├── AvailabilityRule
    ├── ShiftCompatibilityRule
    ├── RegulatoryRestrictionRule
    ├── WorkloadBalanceRule
    ├── CriticalServicePriorityRule
    ├── ServiceCompatibilityRule
    └── TenantSpecificRule
```

Cada regla encapsula una única responsabilidad.

El motor se encarga de:

* Obtener las reglas configuradas.
* Clasificarlas.
* Ordenarlas.
* Ejecutarlas.
* Registrar resultados.
* Resolver conflictos.
* Aplicar desempates.

El flujo puede estructurarse de la siguiente forma:

```text
Conductores candidatos
        ↓
Reglas eliminatorias
        ↓
Candidatos válidos
        ↓
Reglas de prioridad
        ↓
Reglas de puntuación
        ↓
Ranking
        ↓
Desempate determinista
        ↓
Asignación
```

### Ventajas

* Cada regla permanece aislada.
* Alta cohesión.
* Bajo acoplamiento.
* Permite agregar reglas sin modificar las existentes.
* Facilita pruebas unitarias independientes.
* Permite configuración distinta por tenant.
* Permite manejar reglas de filtrado.
* Permite manejar reglas de prioridad.
* Permite manejar reglas de puntuación.
* Facilita auditoría.
* Facilita trazabilidad.
* Mantiene la lógica dentro del monolito modular.
* No requiere una plataforma externa.
* Permite definir explícitamente el orden.
* Facilita resultados deterministas.
* Permite versionar configuraciones.
* Facilita la evolución futura.

### Desventajas

* Requiere validar conflictos entre reglas.
* Requiere administrar precedencias.
* Una configuración inválida puede generar resultados incorrectos.
* Deben evitarse dependencias circulares.
* El catálogo de reglas puede crecer.
* Requiere mecanismos de organización.
* Strategy no resuelve directamente persistencia.
* Strategy no resuelve directamente edición dinámica.
* El mecanismo de composición debe diseñarse explícitamente.

### Decisión

**Elegida.**

Es la alternativa que presenta el mejor equilibrio entre:

* Modificabilidad.
* Trazabilidad.
* Auditabilidad.
* Testabilidad.
* Configurabilidad.
* Complejidad técnica.
* Cohesión.
* Bajo acoplamiento.

Además, permite conservar el comportamiento dentro del monolito modular sin introducir infraestructura adicional.

---

## Comparación de alternativas

| Criterio                          | A. Hardcoded | B. Chain of Responsibility | C. Tabla de decisión | D. Specification + Scoring | E. Motor externo / DSL | F. Strategy / Policy |
| --------------------------------- | ------------ | -------------------------- | -------------------- | -------------------------- | ---------------------- | -------------------- |
| Extensibilidad                    | Baja         | Media                      | Media                | Alta                       | Muy alta               | **Alta**             |
| Trazabilidad                      | Baja         | Media                      | Alta                 | Media-Alta                 | Alta                   | **Alta**             |
| Auditabilidad                     | Baja         | Media                      | Alta                 | Media                      | Alta                   | **Alta**             |
| Complejidad operacional           | Baja         | Baja                       | Media                | Media                      | Alta                   | **Baja-Media**       |
| Facilidad de pruebas              | Baja         | Alta                       | Media                | Alta                       | Media                  | **Alta**             |
| Reglas eliminatorias              | Sí           | Sí                         | Sí                   | Excelente                  | Sí                     | **Sí**               |
| Reglas de puntuación              | Limitada     | Media                      | Limitada             | Sí                         | Sí                     | **Sí**               |
| Reglas de prioridad               | Limitada     | Media                      | Media                | Media                      | Alta                   | **Alta**             |
| Configuración por tenant          | Difícil      | Posible                    | Sí                   | Posible                    | Sí                     | **Sí**               |
| Determinismo                      | Medio        | Dependiente del orden      | Alto                 | Alto                       | Configurable           | **Alto**             |
| Dependencia tecnológica adicional | No           | No                         | No                   | No                         | Sí                     | **No**               |
| Complejidad inicial               | Baja         | Media                      | Media                | Media                      | Alta                   | **Media**            |
| Encaje con monolito modular       | Bajo         | Alto                       | Medio                | Alto                       | Medio                  | **Muy alto**         |
| Evolución independiente de reglas | Baja         | Media                      | Media                | Media-Alta                 | Muy alta               | **Alta**             |
| Soporte de scoring                | Bajo         | Medio                      | Bajo                 | Alto                       | Alto                   | **Alto**             |
| Adecuación para FlowRide V1       | Baja         | Media                      | Media                | Alta                       | Baja                   | **Muy alta**         |

---

## Justificación de la alternativa seleccionada

La alternativa **Strategy / Policy internas configurables** fue seleccionada porque FlowRide necesita que las reglas de negocio evolucionen sin que cada nuevo criterio implique modificar el algoritmo principal.

Las reglas no tienen todas la misma naturaleza.

Existen reglas obligatorias como:

```text
Disponibilidad
Turno compatible
Habilitación regulatoria
Compatibilidad con el servicio
```

También existen criterios de preferencia como:

```text
Balance de carga
Prioridad del servicio
Compatibilidad especial
Cantidad de asignaciones recientes
```

Además, pueden existir reglas específicas de un tenant.

Por esta razón, se requiere una solución donde cada criterio pueda evolucionar de forma independiente.

La alternativa seleccionada mantiene el motor como un orquestador y delega el conocimiento específico en políticas separadas.

---

## Funcionamiento esperado del motor

El flujo general será:

```text
Solicitud de asignación
        ↓
Obtener candidatos
        ↓
Obtener configuración del tenant
        ↓
Cargar reglas activas
        ↓
Validar configuración
        ↓
Aplicar reglas eliminatorias
        ↓
Aplicar reglas de prioridad
        ↓
Aplicar reglas de puntuación
        ↓
Ordenar candidatos
        ↓
Aplicar desempate determinista
        ↓
Seleccionar conductor
        ↓
Registrar decisión
```

---

## Clasificación de reglas

### Reglas eliminatorias

Determinan si un candidato puede continuar participando.

Ejemplos:

```text
AvailabilityRule
ShiftCompatibilityRule
RegulatoryRestrictionRule
ServiceCapabilityRule
```

Si un candidato falla una regla eliminatoria, queda fuera del proceso.

---

### Reglas de prioridad

Permiten aumentar la prioridad de determinados candidatos o servicios.

Ejemplos:

```text
CriticalServicePriorityRule
TenantPriorityRule
SpecialServicePriorityRule
```

---

### Reglas de puntuación

Asignan valores utilizados para comparar candidatos.

Ejemplos:

```text
WorkloadBalanceRule
ServiceCompatibilityRule
RecentAssignmentsRule
```

Ejemplo:

```text
Conductor A

WorkloadBalanceRule        +30
ServiceCompatibilityRule   +40
CriticalServicePriority    +20

Total                       90
```

---

### Regla de desempate

Cuando dos candidatos obtienen la misma puntuación deberá utilizarse un criterio estable.

Ejemplo:

```text
1. Menor cantidad de asignaciones recientes.
2. Mayor tiempo desde su última asignación.
3. Identificador estable del conductor.
```

No deberán utilizarse mecanismos aleatorios que impidan reproducir la decisión.

---

## Configuración por tenant

Cada tenant podrá tener diferentes reglas habilitadas.

Ejemplo:

```json
{
  "tenantId": "empresa-01",
  "rules": [
    {
      "name": "AvailabilityRule",
      "enabled": true,
      "order": 1
    },
    {
      "name": "ShiftCompatibilityRule",
      "enabled": true,
      "order": 2
    },
    {
      "name": "WorkloadBalanceRule",
      "enabled": true,
      "order": 3,
      "weight": 0.40
    },
    {
      "name": "CriticalServicePriorityRule",
      "enabled": true,
      "order": 4,
      "weight": 0.60
    }
  ]
}
```

---

## Validación de configuración

Antes de activar una configuración se deben validar:

* Reglas duplicadas.
* Reglas inexistentes.
* Precedencias inválidas.
* Pesos fuera de rango.
* Dependencias incompatibles.
* Reglas obligatorias deshabilitadas.
* Ciclos de dependencia.
* Configuraciones incompletas.

Flujo esperado:

```text
Configuración
      ↓
ConfigurationValidator
      ↓
¿Configuración válida?
      ↓
Sí → activar
No → rechazar
```

---

## Determinismo

La misma combinación de:

* Solicitud.
* Candidatos.
* Estado operacional.
* Configuración.
* Reglas.
* Pesos.

debe producir el mismo resultado.

Formalmente:

```text
Assignment(
    request,
    candidates,
    configuration
)
=
mismo resultado
```

si las entradas son exactamente iguales.

Esto facilita:

* Auditoría.
* Diagnóstico.
* Pruebas.
* Reproducción de errores.
* Explicación de decisiones.

---

## Auditoría

Cada ejecución del motor debe registrar información suficiente para reconstruir la decisión.

Ejemplo:

```text
Solicitud: SRV-1052

Candidatos iniciales:
- Driver-01
- Driver-02
- Driver-03

AvailabilityRule:
- Driver-01 → válido
- Driver-02 → válido
- Driver-03 → descartado

ShiftCompatibilityRule:
- Driver-01 → válido
- Driver-02 → válido

WorkloadBalanceRule:
- Driver-01 → +30
- Driver-02 → +50

ServiceCompatibilityRule:
- Driver-01 → +40
- Driver-02 → +20

Resultado:
Driver-01 = 70
Driver-02 = 70

Desempate:
Driver-02 tiene mayor tiempo desde su última asignación.

Seleccionado:
Driver-02
```

---

## Consecuencias positivas

* Nuevas reglas pueden agregarse con impacto localizado.
* El motor mantiene bajo acoplamiento.
* Se aumenta la cohesión.
* Facilita pruebas unitarias.
* Permite auditoría.
* Permite trazabilidad.
* Permite configuración por tenant.
* Facilita la evolución futura.
* Permite resultados reproducibles.
* Mantiene las decisiones dentro del dominio.
* Evita incorporar infraestructura innecesaria.
* Reduce la complejidad del servicio principal.
* Permite registrar la versión de configuración.
* Facilita diagnóstico de decisiones.
* Evita depender del GPS para reasignaciones dinámicas.

---

## Consecuencias negativas

* Una composición incorrecta puede generar reglas contradictorias.
* Se requiere validación de configuración.
* Se requiere administración de precedencias.
* Deben evitarse ciclos.
* Puede aumentar la cantidad de clases.
* Se requiere organizar el catálogo de reglas.
* Se requiere persistencia para configuraciones.
* La edición dinámica necesita mecanismos adicionales.
* Se debe definir el comportamiento ante conflictos.
* Es necesaria disciplina para evitar dependencias directas entre reglas.

---

## Riesgos

### Conflictos entre reglas

Dos reglas pueden favorecer candidatos diferentes.

#### Mitigación

* Clasificar reglas.
* Definir pesos.
* Definir precedencias.
* Validar configuración.
* Registrar resultados.

---

### Crecimiento del catálogo de reglas

La cantidad de reglas puede aumentar con el tiempo.

#### Mitigación

* Agrupar reglas por dominio.
* Mantener interfaces comunes.
* Documentar políticas.
* Eliminar reglas obsoletas.
* Versionar reglas.

---

### Configuración inválida

Un tenant puede tener parámetros incorrectos.

#### Mitigación

* Validación previa.
* Versionado.
* Rollback.
* Configuraciones aprobadas.

---

### Falta de reproducibilidad

Cambios posteriores podrían dificultar explicar decisiones antiguas.

#### Mitigación

Registrar:

```text
ConfigurationVersion
RuleVersion
ExecutionResult
Timestamp
```

en cada asignación.

---

## Tácticas de Bass aplicadas

### Diferir binding

La selección y orden de las reglas se realiza en tiempo de ejecución mediante configuración.

Esto permite modificar:

* Reglas activas.
* Orden.
* Parámetros.
* Pesos.

sin cambiar el algoritmo principal.

### Reducir acoplamiento

Cada regla implementa un contrato común.

Ejemplo:

```text
IAssignmentRule
```

El motor depende de la interfaz y no de implementaciones concretas.

### Aumentar cohesión

La lógica de asignación permanece dentro del módulo correspondiente.

Los controladores no contienen reglas de negocio.

### Encapsular

Cada regla encapsula su propio comportamiento.

Ejemplo:

```text
WorkloadBalanceRule
```

únicamente evalúa el balance de carga.

### Restringir dependencias

Las reglas no deben depender directamente unas de otras.

Correcto:

```text
AssignmentEngine
   ├── AvailabilityRule
   ├── ShiftCompatibilityRule
   ├── RegulatoryRestrictionRule
   └── WorkloadBalanceRule
```

No deseado:

```text
AvailabilityRule
      ↓
ShiftCompatibilityRule
      ↓
WorkloadBalanceRule
```

La coordinación pertenece al motor.

---

## Escenarios de calidad relacionados

### Modificabilidad

Ante la necesidad de incorporar una nueva regla:

```text
PreferredDriverRule
```

esta debe poder agregarse sin modificar las reglas existentes.

### Auditabilidad

Cuando un administrador consulte por qué un conductor fue seleccionado, el sistema deberá mostrar las reglas y criterios que intervinieron.

### Testabilidad

Cada regla debe poder probarse de manera aislada.

Ejemplo:

```text
AvailabilityRuleTests
ShiftCompatibilityRuleTests
WorkloadBalanceRuleTests
```

### Configurabilidad

Dos tenants pueden utilizar configuraciones diferentes.

Ejemplo:

```text
Tenant A
- Disponibilidad
- Turno
- Balance de carga
```

```text
Tenant B
- Disponibilidad
- Restricción regulatoria
- Prioridad crítica
- Balance de carga
```

---

## Escenarios y requisitos relacionados

* `OPLOG-CAP-01` — el motor sostiene picos operativos del tenant.
* `RF-10` — asignación automática.
* `RF-11` — aplicación de reglas de negocio.
* `RF-13` — priorización de solicitudes críticas.
* `RF-21` — configuración de reglas.

---

## ADRs relacionados

* **ADR-0001** — el motor es un módulo interno del monolito.
* **ADR-0007** — protege consistencia cuando varias asignaciones compiten.
* **ADR-0005** — la telemetría se utiliza únicamente para ETA y monitoreo.

---

## Notas de implementación

* Separar reglas eliminatorias de reglas de puntuación.
* Definir una interfaz común.
* Registrar la versión de configuración.
* Definir un desempate estable.
* Validar configuración antes de activarla.
* Evitar dependencias directas entre reglas.
* Registrar reglas ejecutadas.
* Registrar candidatos descartados.
* Registrar cambios de puntuación.
* Registrar cambios de prioridad.
* Incluir pruebas unitarias por regla.
* Incluir pruebas de integración.
* Realizar una PoC con reglas en conflicto.
* Mantener telemetría fuera de la reasignación dinámica.
* Evitar desempates aleatorios.
* Versionar configuraciones.
* Permitir rollback.

---

## Posible estructura interna

```text
FlowRide.Core
│
├── Assignment
│   │
│   ├── AssignmentEngine
│   ├── AssignmentContext
│   ├── AssignmentResult
│   ├── AssignmentConfiguration
│   │
│   ├── Rules
│   │   │
│   │   ├── IAssignmentRule
│   │   │
│   │   ├── Filtering
│   │   │   ├── AvailabilityRule
│   │   │   ├── ShiftCompatibilityRule
│   │   │   ├── RegulatoryRestrictionRule
│   │   │   └── ServiceCapabilityRule
│   │   │
│   │   ├── Priority
│   │   │   ├── CriticalServicePriorityRule
│   │   │   └── TenantPriorityRule
│   │   │
│   │   ├── Scoring
│   │   │   ├── WorkloadBalanceRule
│   │   │   ├── ServiceCompatibilityRule
│   │   │   └── RecentAssignmentsRule
│   │   │
│   │   └── TieBreaker
│   │       └── StableTieBreaker
│   │
│   └── Audit
│       └── AssignmentDecisionLog
```

---

## PoC propuesta

### Escenario 1 — Regla eliminatoria

```text
Driver A → disponible
Driver B → no disponible
```

Resultado esperado:

```text
Driver B queda excluido.
```

---

### Escenario 2 — Balance de carga

```text
Driver A
Carga actual: 5

Driver B
Carga actual: 2
```

Resultado esperado:

```text
WorkloadBalanceRule favorece Driver B.
```

---

### Escenario 3 — Servicio crítico

```text
Driver A
Score normal: 70

Driver B
Score normal: 60
Compatible con servicio crítico: Sí
```

Resultado esperado:

```text
CriticalServicePriorityRule modifica la prioridad según la configuración.
```

---

### Escenario 4 — Empate

```text
Driver A = 80 puntos
Driver B = 80 puntos
```

Resultado esperado:

```text
Se ejecuta StableTieBreaker.
```

La misma entrada debe producir siempre el mismo resultado.

---

### Escenario 5 — Reglas en conflicto

```text
Regla 1 favorece Driver A.
Regla 2 favorece Driver B.
```

Resultado esperado:

El conflicto debe resolverse utilizando:

* Pesos.
* Prioridad.
* Precedencia.
* Configuración explícita.

Nunca mediante comportamiento implícito.

---

### Escenario 6 — Configuración diferente por tenant

```text
Tenant A
WorkloadBalanceRule = activa

Tenant B
WorkloadBalanceRule = desactivada
```

Resultado esperado:

La misma solicitud puede producir decisiones diferentes entre tenants debido a su configuración.

Sin embargo, para una misma configuración, el resultado deberá mantenerse determinista.

---

## Resultado esperado de la decisión

La arquitectura seleccionada debe permitir que el motor evolucione desde un catálogo pequeño de reglas hasta uno considerablemente mayor sin convertir el servicio de asignación en una estructura condicional difícil de mantener.

Ejemplo:

```text
3 reglas
   ↓
10 reglas
   ↓
20 reglas
```

sin necesidad de reestructurar completamente el algoritmo principal.

El motor actúa como orquestador.

Las reglas encapsulan conocimiento específico.

La configuración define el comportamiento activo por tenant.

---

## Estado final de la decisión

Se acepta la implementación basada en:

**Strategy / Policy internas configurables con composición determinista.**

Las alternativas descartadas son:

1. Condicionales hardcodeados.
2. Chain of Responsibility / Pipeline secuencial.
3. Tabla de decisión configurable.
4. Specification Pattern + scoring centralizado.
5. Motor de reglas externo / DSL.

La alternativa seleccionada es:

6. Strategy / Policy internas configurables.

La elección busca favorecer principalmente:

* Modificabilidad.
* Trazabilidad.
* Auditabilidad.
* Testabilidad.
* Configurabilidad.
* Cohesión.
* Bajo acoplamiento.
* Determinismo.

sin incorporar infraestructura adicional que no se considera necesaria para FlowRide V1.

---

## Referencias

* `FuncionalidadesSignificativas_FlowRide_ACTUALIZADO.xlsx`.
* `tradeoff_FlowRide_ACTUALIZADO.xlsx` — PoC Motor de reglas.
* *Software Architecture in Practice* — tácticas de modificabilidad.
* Patrón Strategy.
* Patrón Policy.
* Patrón Specification.
* Patrón Chain of Responsibility.
* Decision Tables.
