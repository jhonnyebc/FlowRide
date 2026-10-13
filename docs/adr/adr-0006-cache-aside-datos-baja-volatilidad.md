# ADR-0006: Usar Cache-Aside local solo para datos de baja volatilidad

## Metadatos

| Campo              | Valor                                                                                     |
| ------------------ | ----------------------------------------------------------------------------------------- |
| ID                 | ADR-0006                                                                                  |
| Fecha              | 2026-09-09                                                                                |
| Estado             | **Accepted**                                                                              |
| Participantes      | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro           |
| Curso              | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026)                                |
| Profesor evaluador | Wider Farid Sánchez Garzón                                                                |
| Reemplaza          | ADR-07 histórico que fijaba IMemoryCache + clave por empresaId para multi-tenancy lógico. |

## Contexto

FlowRide tiene lecturas repetidas de catálogos, configuración y reportes agregados, pero también datos que cambian continuamente: solicitudes, asignaciones, viajes y telemetría.

El modelo Silo reduce la necesidad de una caché global compartida entre tenants.

Una caché distribuida por defecto elevaría costo y operación. Una caché local puede mejorar lecturas sin convertirse en fuente de verdad siempre que su alcance esté restringido.

No todos los datos tienen el mismo nivel de volatilidad.

Existen datos relativamente estables como:

* Catálogos.
* Parámetros.
* Configuración.
* Datos de referencia.
* Indicadores agregados.
* Resultados de reportes.

También existen datos altamente dinámicos y sensibles a inconsistencia como:

* Solicitudes.
* Asignaciones.
* Viajes en curso.
* Disponibilidad de conductores.
* Estado operacional.
* Telemetría.

La estrategia de caché debe diferenciar ambos grupos para evitar que una optimización de rendimiento termine afectando consistencia o decisiones funcionales.

La caché no debe sustituir PostgreSQL como fuente de verdad.

---

## Decisión

Aplicar **Cache-Aside** utilizando caché local en proceso mediante `IMemoryCache` como optimización inicial de cada instancia Silo.

La caché se utilizará exclusivamente para datos de **baja volatilidad** y lecturas donde sea aceptable una obsolescencia temporal acotada.

La fuente de verdad continuará siendo PostgreSQL.

La estrategia general será:

```text
Solicitud de lectura
        ↓
Buscar en caché
   ↓             ↓
Existe          No existe
   ↓                ↓
Retornar       Consultar PostgreSQL
                    ↓
              Guardar temporalmente
                    ↓
                 Retornar
```

La caché no deberá utilizarse como fuente de verdad para:

* Solicitudes activas.
* Asignaciones.
* Viajes en curso.
* Disponibilidad crítica de conductores.
* Estado operacional sensible.
* Telemetría.
* Información cuya inconsistencia temporal pueda producir una decisión incorrecta.

La escritura continuará realizándose sobre PostgreSQL.

Cuando una operación modifique información que pueda encontrarse en caché, la invalidación deberá realizarse **después de un commit exitoso**.

El flujo será:

```text
Modificar dato
      ↓
Persistir en PostgreSQL
      ↓
Commit exitoso
      ↓
Invalidar caché relacionada
```

No se deberá invalidar una entrada antes de confirmar la persistencia, ya que una operación fallida podría generar una invalidación innecesaria.

Cada entrada tendrá un TTL explícito.

El TTL deberá definirse teniendo en cuenta:

* Volatilidad del dato.
* Frecuencia de lectura.
* Costo de reconstrucción.
* Tolerancia a obsolescencia.
* Impacto de una lectura temporalmente desactualizada.

La caché se aplicará preferentemente a:

* Catálogos.
* Configuración de baja frecuencia de cambio.
* Parámetros.
* Datos de referencia.
* Indicadores agregados.
* Reportes precalculados.
* Información de consulta repetitiva con bajo nivel de cambio.

Las claves deberán representar claramente el recurso y sus parámetros funcionales.

Ejemplos:

```text
catalog:vehicle-types

configuration:assignment-rules

dashboard:operator-summary

report:daily-summary:2026-09-09
```

No se utilizará `empresaId` como frontera principal de aislamiento, ya que el aislamiento del tenant se encuentra definido mediante el modelo Silo.

La caché local tendrá alcance únicamente dentro de la instancia en la que fue creada.

Ejemplo:

```text
Instancia A
   ↓
Cache A

Instancia B
   ↓
Cache B
```

No se asumirá consistencia inmediata entre cachés locales pertenecientes a diferentes instancias.

Si un tenant requiere múltiples instancias activas y esta inconsistencia temporal deja de ser aceptable, la estrategia de caché deberá reevaluarse.

En ese escenario podrá analizarse una caché distribuida como Redis u otro mecanismo equivalente mediante una revisión futura del ADR.

La caché local no deberá utilizarse para:

* Locks distribuidos.
* Coordinación entre instancias.
* Manejo de concurrencia.
* Persistencia crítica.
* Control de integridad.
* Estado que deba sobrevivir a un reinicio.
* Fuente de verdad.

La estrategia de caché deberá ser eliminable.

Esto significa que, si `IMemoryCache` se deshabilita:

```text
Application
      ↓
PostgreSQL
```

el comportamiento funcional del sistema deberá mantenerse, aunque pueda existir un impacto en rendimiento.

La efectividad de cada caché deberá medirse mediante:

* Hit rate.
* Miss rate.
* Latencia.
* Cantidad de consultas evitadas.
* Tiempo de reconstrucción.
* Uso de memoria.

Las entradas o mecanismos de caché que no demuestren un beneficio suficiente podrán eliminarse sin modificar reglas de negocio.

---

## Justificación de la decisión

Se selecciona **Cache-Aside local mediante IMemoryCache** porque FlowRide requiere reducir lecturas repetitivas sobre información relativamente estable sin introducir desde V1 una infraestructura distribuida adicional.

La estrategia Cache-Aside permite mantener PostgreSQL como fuente de verdad y utilizar la caché exclusivamente como optimización de lectura.

La decisión se fundamenta en:

* Existencia de lecturas repetidas sobre datos poco volátiles.
* Necesidad de evitar consultas innecesarias a PostgreSQL.
* Modelo Silo que reduce la necesidad de una caché global.
* Ausencia de una necesidad actual de coordinación mediante caché.
* Bajo costo operacional de una caché en proceso.
* Facilidad para invalidar o eliminar la caché sin afectar el dominio.
* Necesidad de evitar que datos críticos dependan de información potencialmente obsoleta.

La estrategia permite mejorar rendimiento sin convertir la caché en un componente obligatorio para la consistencia funcional de FlowRide.

---

## Alternativas consideradas

### Alternativa A — Sin caché

No utilizar ninguna estrategia de caché y ejecutar todas las consultas directamente sobre PostgreSQL.

#### Ventajas

* Máxima simplicidad.
* No existe riesgo de obsolescencia por caché.
* No requiere invalidación.
* No requiere administración de memoria adicional.
* PostgreSQL permanece como única fuente de lectura.

#### Desventajas

* Mayor cantidad de consultas repetidas.
* Puede incrementar carga sobre PostgreSQL.
* Consultas agregadas costosas deben ejecutarse repetidamente.
* Mayor latencia para información que cambia poco.
* Puede desaprovechar oportunidades simples de optimización.

#### Decisión

**Descartada como política absoluta.**

Algunas lecturas agregadas, catálogos y configuraciones pueden beneficiarse de copias temporales sin afectar consistencia crítica.

---

### Alternativa B — Redis distribuido desde V1

Utilizar Redis como caché distribuida compartida entre las instancias de FlowRide.

Ejemplo:

```text
Instancia A ─┐
             ├──→ Redis
Instancia B ─┘
```

#### Ventajas

* Caché compartida entre instancias.
* Facilita escalamiento horizontal.
* Reduce inconsistencias entre nodos.
* Permite invalidación compartida.
* Amplio soporte para estructuras de datos.
* Puede utilizarse para cargas elevadas de lectura.

#### Desventajas

* Introduce infraestructura adicional.
* Incrementa costo operacional.
* Requiere monitoreo.
* Añade una dependencia externa.
* Introduce latencia de red.
* Requiere políticas adicionales de disponibilidad.
* Puede ser innecesario para una instancia Silo única.

#### Decisión

**Descartada como requisito base para V1.**

Podrá reevaluarse si existen múltiples instancias activas y la inconsistencia entre cachés locales deja de ser aceptable.

---

### Alternativa C — IMemoryCache + Cache-Aside restringido

Utilizar `IMemoryCache` dentro de cada instancia y aplicar el patrón Cache-Aside únicamente sobre información de baja volatilidad.

Ejemplo:

```text
Request
   ↓
IMemoryCache
 ↓        ↓
Hit      Miss
 ↓         ↓
Return   PostgreSQL
            ↓
          Cache
            ↓
          Return
```

#### Ventajas

* Implementación simple.
* Bajo costo operacional.
* Acceso en memoria.
* No requiere infraestructura adicional.
* Fácil de eliminar.
* Reduce consultas repetidas.
* Compatible con el modelo Silo.
* Permite TTL por entrada.
* Mantiene PostgreSQL como fuente de verdad.

#### Desventajas

* Cada instancia tiene su propia copia.
* Puede existir obsolescencia temporal.
* Las entradas desaparecen al reiniciar la instancia.
* No puede utilizarse para coordinación.
* No sirve para locks distribuidos.
* En múltiples instancias puede existir inconsistencia temporal entre cachés.

#### Decisión

**Elegida.**

Proporciona una optimización sencilla, de bajo costo y sustituible para lecturas de baja volatilidad.

---

### Alternativa D — Redis distribuido obligatorio

Utilizar Redis como caché compartida obligatoria para todas las instancias desde el inicio.

Ejemplo:

```text
Instancia A ─┐
             │
Instancia B ─┼──→ Redis
             │
Instancia C ─┘
```

#### Ventajas

* Estado de caché compartido.
* Invalidación centralizada.
* Menor inconsistencia entre instancias.
* Facilita escalamiento horizontal.
* Evita duplicar determinados datos en cada proceso.
* Puede manejar grandes volúmenes de entradas.

#### Desventajas

* Introduce infraestructura obligatoria.
* Aumenta costo de operación.
* Requiere alta disponibilidad.
* Se convierte en otra dependencia crítica.
* Añade latencia de red.
* Requiere monitoreo y mantenimiento.
* Puede ser innecesario para el dimensionamiento inicial.

#### Decisión

**Descartada como estrategia inicial.**

No existe un driver actual que justifique hacer obligatoria una caché distribuida en todos los Silos.

---

### Alternativa E — Write-Through Cache

Utilizar una estrategia donde las escrituras actualicen PostgreSQL y la caché como parte del mismo flujo.

Ejemplo:

```text
Escritura
    ↓
Application
    ↓
 ┌──────────────┬──────────────┐
 ↓              ↓
PostgreSQL     Cache
```

#### Ventajas

* La caché permanece actualizada después de una escritura.
* Reduce misses posteriores.
* Disminuye la ventana de obsolescencia.
* Puede mejorar lecturas posteriores de datos recientemente modificados.

#### Desventajas

* Aumenta complejidad de las escrituras.
* Se deben mantener sincronizados dos destinos.
* Una falla parcial puede producir diferencias.
* Incrementa acoplamiento entre persistencia y caché.
* Puede aumentar latencia de escritura.
* No aporta una ventaja clara para información de baja volatilidad.

#### Decisión

**Descartada.**

FlowRide utiliza la caché como optimización de lectura, no como componente obligatorio del proceso de escritura.

---

### Alternativa F — Refresh-Ahead / precarga periódica

Mantener determinadas entradas actualizadas antes de su expiración mediante procesos periódicos.

Ejemplo:

```text
Scheduler
    ↓
Buscar entradas próximas a expirar
    ↓
Consultar PostgreSQL
    ↓
Actualizar caché
```

#### Ventajas

* Reduce cache misses.
* Evita que el primer usuario después del TTL reconstruya la entrada.
* Puede reducir latencia en dashboards.
* Mantiene información muy consultada preparada.

#### Desventajas

* Puede ejecutar consultas innecesarias.
* Consume recursos incluso sin usuarios consultando.
* Requiere scheduler o proceso periódico.
* Incrementa complejidad.
* Requiere decidir qué información debe precargarse.
* Puede ocultar consultas de base de datos costosas.

#### Decisión

**Descartada como estrategia general.**

Podría evaluarse únicamente para consultas muy frecuentes y costosas si las métricas demuestran que el beneficio justifica la complejidad.

---

## Consecuencias positivas

* Bajo costo operacional.
* Menos lecturas repetidas a PostgreSQL.
* No introduce infraestructura compartida que complique el modelo Silo.
* La caché puede eliminarse sin cambiar reglas de negocio.
* Acceso rápido a datos de baja volatilidad.
* Permite TTL adaptado a cada recurso.
* Mantiene PostgreSQL como fuente de verdad.
* Evita depender de caché para decisiones críticas.
* Facilita una evolución futura hacia caché distribuida si fuera necesario.

## Consecuencias negativas

* Cada instancia mantiene su propia copia.
* Puede existir obsolescencia temporal hasta invalidación o expiración.
* Las entradas se pierden al reiniciar la aplicación.
* No sirve para coordinación.
* No sirve para locks distribuidos.
* No debe utilizarse para estado crítico.
* En múltiples instancias puede existir inconsistencia temporal.
* Requiere definir correctamente TTL e invalidación.

## Tácticas de Bass aplicadas

### Mantener múltiples copias

La caché mantiene temporalmente copias de datos cuya fuente de verdad está en PostgreSQL.

### Gestionar recursos

La caché reduce consultas repetidas y uso innecesario del motor de persistencia.

### Acotar tiempo de vida

Cada entrada utiliza TTL para limitar el tiempo máximo de obsolescencia.

### Reducir overhead

Las lecturas repetitivas pueden resolverse directamente en memoria sin acceder continuamente a PostgreSQL.

### Detectar condiciones

Las métricas de hit rate, miss rate y latencia permiten identificar si una caché realmente aporta beneficio.

---

## Escenarios y requisitos relacionados

* `ADM-REND-01` — dashboard y reportes con caché opcional.
* `ADM-CAP-02` — listados paginados e indexados.
* `RF-42` — indicadores.
* `RF-46` — reportes.
* `RF-47` — reportes.
* `RF-48` — reportes.
* `RF-49` — reportes.

---

## ADRs relacionados

* **ADR-0002** — cada caché pertenece al Silo o instancia del operador.
* **ADR-0004** — PostgreSQL continúa siendo fuente de verdad.
* **ADR-0010** — múltiples instancias pueden obligar a revisar la estrategia.

---

## Notas de implementación

* Utilizar `IMemoryCache`.
* Aplicar Cache-Aside solo donde exista beneficio.
* Definir TTL explícito.
* No utilizar caché para solicitudes activas.
* No utilizar caché para asignaciones.
* No utilizar caché para viajes en curso.
* No utilizar caché para disponibilidad crítica.
* No utilizar caché para telemetría.
* Invalidar únicamente después de commits exitosos.
* Las claves deben incorporar recurso y parámetros funcionales.
* No usar `empresaId` como frontera principal de tenant.
* Mantener PostgreSQL como fuente de verdad.
* Instrumentar hit rate.
* Instrumentar miss rate.
* Medir latencia.
* Medir cantidad de consultas evitadas.
* Controlar uso de memoria.
* Eliminar cachés que no demuestren beneficio.
* No utilizar IMemoryCache para coordinación entre instancias.
* No utilizar IMemoryCache para locks.
* No utilizar IMemoryCache para persistencia.
* Reevaluar Redis solo si existen múltiples instancias y se demuestra la necesidad.

---

## Referencias

* `RestriccionesTecnicas_FlowRide_ACTUALIZADO.xlsx`.
* `tradeoff_FlowRide_ACTUALIZADO.xlsx` — T-06.
