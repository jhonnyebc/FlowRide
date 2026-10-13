# ADR-0005: Ingerir telemetría GPS mediante adapter y almacenarla particionada en PostgreSQL

## Metadatos

| Campo              | Valor                                                                           |
| ------------------ | ------------------------------------------------------------------------------- |
| ID                 | ADR-0005                                                                        |
| Fecha              | 2026-09-09                                                                      |
| Estado             | **Proposed**                                                                    |
| Participantes      | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso              | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026)                      |
| Profesor evaluador | Wider Farid Sánchez Garzón                                                      |
| Reemplaza          | —                                                                               |

## Contexto

La V1 de FlowRide incluye telemetría GPS para monitorear viajes en curso y detectar desviaciones respecto al ETA esperado.

El dimensionamiento vigente considera al menos **100 eventos de ubicación por minuto por instancia**, con hasta 50 vehículos o conductores reportando aproximadamente cada 30 segundos y una política de retención de 6 meses.

La telemetría debe permitir identificar, como mínimo:

* Vehículo o conductor asociado.
* Viaje relacionado cuando aplique.
* Timestamp de origen.
* Timestamp de recepción.
* Latitud.
* Longitud.
* Fuente o proveedor.
* Calidad del dato.
* Estado de procesamiento.

Los operadores de FlowRide pueden utilizar diferentes proveedores físicos de GPS.

Por esta razón, no conviene que Application o Domain dependan directamente de proveedores concretos como:

* Satrack.
* WhatsApp Live Location.
* Dispositivos GPS propietarios.
* APIs específicas de fabricantes.
* Plataformas externas de rastreo.

Los distintos proveedores pueden presentar diferencias en:

* Formatos de payload.
* Frecuencia de envío.
* Protocolos.
* Precisión.
* Calidad del dato.
* Identificadores.
* Convenciones de tiempo.
* Métodos de autenticación.
* Estrategias de entrega.

Por lo tanto, se requiere una frontera que normalice estas diferencias antes de que la información ingrese al núcleo de FlowRide.

Adicionalmente, no existe evidencia de volumen que justifique introducir desde V1 una base especializada de series temporales adicional.

La estrategia de persistencia debe mantenerse coherente con la decisión de utilizar PostgreSQL como motor principal del Silo.

La telemetría tiene como objetivo soportar:

* Monitoreo de viajes.
* Seguimiento de posición.
* Detección de retrasos.
* Cálculo o actualización de ETA.
* Detección de desviaciones.
* Alertas operativas.

La telemetría **no forma parte de la lógica de reasignación dinámica de conductores por cercanía**.

---

## Decisión

Se decide implementar la ingesta de telemetría GPS mediante una interfaz común denominada:

```text
ITelemetryProviderAdapter
```

Cada integración externa deberá implementar este contrato y será responsable de transformar el formato particular del proveedor al modelo interno de FlowRide.

El modelo normalizado se representará mediante:

```text
RegistroUbicacion
```

conceptualmente con información como:

```text
RegistroUbicacion
- Id
- VehiculoId
- ConductorId
- ViajeId
- TimestampOrigen
- TimestampRecepcion
- Latitud
- Longitud
- Calidad
- Fuente
```

La estructura conceptual de integración será:

```text
Proveedor GPS
      ↓
ITelemetryProviderAdapter
      ↓
Normalización
      ↓
RegistroUbicacion
      ↓
Validación
      ↓
Persistencia
      ↓
Monitoreo / ETA
```

El proveedor externo no deberá ser conocido directamente por Application o Domain.

La dependencia quedará concentrada en Infrastructure.

Ejemplo:

```text
Domain
   ↑
Application
   ↑
Telemetry Adapter
   ↑
Proveedor GPS
```

y no:

```text
Domain
   ↓
Satrack API
```

ni:

```text
Application
   ↓
Payload proveedor GPS
```

La entrada inicial podrá soportar:

* HTTP.
* Webhook.
* Polling.

El mecanismo concreto dependerá del proveedor disponible para cada operador.

Todos estos mecanismos deberán permanecer encapsulados dentro de Infrastructure.

El adapter será responsable de:

* Recibir información externa.
* Autenticar o validar la fuente cuando aplique.
* Transformar el payload externo.
* Normalizar identificadores.
* Normalizar timestamps.
* Normalizar coordenadas.
* Identificar el vehículo.
* Identificar el conductor cuando aplique.
* Identificar el viaje cuando aplique.
* Registrar la fuente.
* Enviar el evento normalizado al procesamiento interno.

El evento normalizado deberá incluir al menos:

```text
VehiculoId o ConductorId
TimestampOrigen
TimestampRecepcion
Latitud
Longitud
Fuente
Calidad
```

y, cuando sea posible:

```text
ViajeId
```

---

## Validación de eventos

Antes de persistir un evento de telemetría deberán verificarse condiciones mínimas de validez.

Ejemplos:

* Latitud válida.
* Longitud válida.
* Timestamp válido.
* Vehículo conocido.
* Fuente identificada.
* Evento dentro de una ventana temporal razonable.
* Duplicados.
* Formato correcto.

Eventos inválidos no deberán detener completamente el proceso de ingesta.

Conceptualmente:

```text
Evento recibido
      ↓
Validación
   ↙        ↘
Válido     Inválido
  ↓           ↓
Persistir   Aislar / registrar
```

La existencia de eventos incorrectos no deberá impedir el procesamiento de eventos posteriores válidos.

---

## Manejo de duplicados

La integración deberá contemplar que determinados proveedores pueden reenviar información.

Cuando exista un identificador externo confiable, podrá utilizarse como parte de la detección de duplicados.

Cuando no exista, podrán utilizarse combinaciones como:

```text
Proveedor
+
VehiculoId
+
TimestampOrigen
```

La estrategia deberá evitar almacenar múltiples veces una misma ubicación cuando pueda determinarse que se trata del mismo evento.

---

## Manejo de timestamps

Se deberán conservar al menos dos marcas de tiempo:

```text
TimestampOrigen
TimestampRecepcion
```

`TimestampOrigen` representa el momento en que el dispositivo o proveedor generó la ubicación.

`TimestampRecepcion` representa el momento en que FlowRide recibió el evento.

La diferencia entre ambos permitirá medir retrasos de ingesta.

Ejemplo:

```text
TimestampOrigen:    10:00:00
TimestampRecepcion: 10:00:08

Retraso: 8 segundos
```

Esta información podrá utilizarse para:

* Monitoreo.
* Detección de proveedores retrasados.
* Métricas.
* Evaluación de calidad.
* Análisis de ETA.

---

## Persistencia

La telemetría de V1 se almacenará en **PostgreSQL** dentro del Silo correspondiente al operador.

La tabla de telemetría utilizará **particionamiento declarativo por tiempo**.

La partición será preferentemente mensual.

Ejemplo conceptual:

```text
registro_ubicacion
│
├── registro_ubicacion_2026_01
├── registro_ubicacion_2026_02
├── registro_ubicacion_2026_03
├── registro_ubicacion_2026_04
└── ...
```

La columna utilizada para particionar será el timestamp del evento.

Esto permite que la retención pueda gestionarse eliminando particiones antiguas en lugar de ejecutar eliminaciones masivas fila por fila.

---

## Índices

Los índices deberán responder a las consultas operacionales reales.

Inicialmente se consideran combinaciones como:

```text
(VehiculoId, Timestamp)
```

y:

```text
(ViajeId, Timestamp)
```

Estos índices permiten responder consultas como:

* Última ubicación de un vehículo.
* Historial reciente de un vehículo.
* Recorrido de un viaje.
* Ubicaciones de un viaje dentro de un rango temporal.
* Última posición conocida.

Los índices definitivos deberán validarse utilizando métricas reales y planes de ejecución.

---

## Retención

Los eventos de telemetría tendrán una retención de **6 meses**.

La eliminación deberá automatizarse.

Conceptualmente:

```text
Partición actual
      ↓
¿Antigüedad > 6 meses?
   ↓             ↓
  Sí             No
   ↓              ↓
Eliminar       Mantener
```

La estrategia de particionamiento facilita eliminar información antigua sin ejecutar operaciones masivas sobre millones de registros individuales.

---

## Consumo de la telemetría

La telemetría será utilizada principalmente por:

```text
Monitoreo
    ↓
Seguimiento del viaje
    ↓
Cálculo / actualización ETA
    ↓
Detección de desviación
    ↓
Alerta
```

La detección de desviación corresponde a `RF-56`.

Ejemplo conceptual:

```text
ETA esperado: 10:30

ETA actualizado: 10:37

Desviación: +7 minutos

Umbral permitido: 5 minutos

Resultado:
Generar alerta
```

La telemetría no participará como señal para realizar reasignaciones dinámicas basadas en cercanía.

---

## Justificación de la decisión

Se selecciona **Adapter + PostgreSQL particionado** porque FlowRide necesita desacoplar la telemetría del proveedor físico sin introducir desde V1 una tecnología adicional de almacenamiento.

El uso de `ITelemetryProviderAdapter` permite que diferentes operadores utilicen distintas fuentes GPS sin modificar Application ni Domain.

PostgreSQL ya forma parte de la arquitectura de persistencia de FlowRide y el volumen previsto de telemetría no demuestra actualmente la necesidad de utilizar una base especializada de series temporales.

El particionamiento temporal permite manejar:

* Crecimiento de datos.
* Consultas por rango de tiempo.
* Retención de seis meses.
* Eliminación eficiente de información antigua.

La decisión se fundamenta directamente en:

* Posibilidad de múltiples proveedores GPS.
* Necesidad de evitar lock-in con un proveedor.
* Necesidad de normalizar formatos.
* Volumen previsto de al menos 100 eventos por minuto.
* Retención limitada a 6 meses.
* Continuidad con PostgreSQL.
* Necesidad de consultar posiciones por vehículo, viaje y tiempo.
* Ausencia de evidencia que justifique una base especializada adicional.
* Necesidad de aislar eventos inválidos sin detener la ingesta.

La solución permite mantener la V1 simple y dejar abierta una futura reevaluación si el volumen real de telemetría crece significativamente.

---

# Alternativas consideradas

## Alternativa A — Proveedor GPS fijo directamente en el dominio

Integrar directamente un proveedor determinado dentro de Application o Domain.

Ejemplo:

```text
Domain
   ↓
Satrack
```

### Ventajas

* Implementación inicial sencilla.
* Menor cantidad de abstracciones.
* Acceso directo a funcionalidades específicas del proveedor.
* Menor esfuerzo inicial.

### Desventajas

* Alto acoplamiento.
* Lock-in con el proveedor.
* Dificulta cambiar de proveedor.
* Diferentes operadores pueden requerir integraciones distintas.
* Payloads externos podrían contaminar el modelo interno.
* Complica pruebas.
* Reduce modificabilidad.

### Decisión

**Descartada.**

Distintos operadores pueden utilizar proveedores diferentes y FlowRide no debe depender directamente de uno de ellos.

---

## Alternativa B — TimescaleDB desde V1

Utilizar TimescaleDB como extensión especializada para el almacenamiento de telemetría.

Ejemplo:

```text
Telemetry Adapter
      ↓
TimescaleDB
```

### Ventajas

* Especializada en series temporales.
* Manejo eficiente de datos temporales.
* Consultas optimizadas por tiempo.
* Facilita retención.
* Mantiene compatibilidad con PostgreSQL.
* Puede manejar volúmenes elevados de telemetría.

### Desventajas

* Introduce una extensión adicional.
* Aumenta conocimiento requerido.
* Añade complejidad operacional.
* Incrementa dependencia tecnológica.
* No existe evidencia de que PostgreSQL particionado sea insuficiente para V1.

### Decisión

**Descartada como requisito inicial.**

Puede reevaluarse si el volumen real supera significativamente el dimensionamiento previsto.

---

## Alternativa C — Base NoSQL o de series temporales separada

Utilizar una persistencia independiente especializada en telemetría.

Ejemplos:

* InfluxDB.
* MongoDB.
* ClickHouse.
* Cassandra.
* DynamoDB.

Ejemplo conceptual:

```text
PostgreSQL
    ↓
Datos operativos

InfluxDB
    ↓
Telemetría
```

### Ventajas

* Separación de cargas.
* Posibilidad de optimizar telemetría.
* Escalamiento independiente.
* Motores especializados pueden manejar grandes volúmenes.

### Desventajas

* Introduce una segunda tecnología de persistencia.
* Mayor complejidad operacional.
* Requiere backups adicionales.
* Aumenta monitoreo.
* Introduce más puntos de fallo.
* Requiere sincronización con información operacional.
* Incrementa costos de infraestructura.
* No existe evidencia actual que lo justifique.

### Decisión

**Descartada para V1.**

La complejidad adicional no se justifica con el volumen estimado.

---

## Alternativa D — Adapter + PostgreSQL particionado

Utilizar adapters intercambiables para normalizar proveedores y almacenar los eventos en PostgreSQL mediante particiones temporales.

Ejemplo:

```text
Satrack ───────────────┐
                      ↓
Proveedor B ─→ ITelemetryProviderAdapter
                      ↓
WhatsApp Location ────┘
                      ↓
              RegistroUbicacion
                      ↓
          PostgreSQL particionado
```

### Ventajas

* Desacopla proveedor y dominio.
* Permite diferentes proveedores por operador.
* Mantiene una sola tecnología de persistencia.
* Compatible con modelo Silo.
* Facilita retención.
* Permite consultas por vehículo, viaje y tiempo.
* Reduce complejidad operacional.
* Aprovecha conocimiento existente de PostgreSQL.
* Permite reevaluar almacenamiento en el futuro.

### Desventajas

* Requiere desarrollar adapters.
* Los formatos de proveedores deben normalizarse.
* PostgreSQL podría necesitar reevaluación ante un crecimiento considerable.
* Requiere administrar particiones.
* La calidad de datos depende parcialmente del proveedor.
* Deben controlarse duplicados y datos retrasados.

### Decisión

**Elegida.**

Ofrece simplicidad, desacoplamiento del proveedor y capacidad suficiente para el volumen previsto de V1.

---

## Alternativa E — Ingesta directa proveedor → PostgreSQL sin adapter

Permitir que cada integración transforme y persista directamente sus datos en PostgreSQL.

Ejemplo:

```text
Satrack
   ↓
Integración Satrack
   ↓
PostgreSQL
```

y:

```text
Proveedor B
   ↓
Integración B
   ↓
PostgreSQL
```

### Ventajas

* Menor cantidad inicial de abstracciones.
* Cada integración puede adaptarse libremente a su proveedor.
* Implementación rápida para un único proveedor.
* Acceso directo a la persistencia.

### Desventajas

* Cada integración puede producir modelos distintos.
* La normalización queda duplicada.
* Aumenta acoplamiento entre proveedor y persistencia.
* Dificulta incorporar nuevos proveedores.
* Puede generar inconsistencias entre formatos almacenados.
* Aumenta duplicación de lógica.
* Complica el mantenimiento.

### Decisión

**Descartada.**

La ausencia de un contrato común dificultaría mantener una representación uniforme de telemetría entre operadores y proveedores.

---

## Alternativa F — Broker de mensajes entre proveedor y procesamiento

Incorporar un broker como punto intermedio para recibir telemetría antes de procesarla.

Ejemplo:

```text
Proveedor GPS
      ↓
Adapter
      ↓
Kafka / RabbitMQ / Pulsar
      ↓
Telemetry Consumer
      ↓
PostgreSQL
```

### Ventajas

* Permite absorber picos.
* Desacopla recepción y procesamiento.
* Facilita procesamiento asíncrono.
* Puede mejorar tolerancia a fallos.
* Permite múltiples consumidores.
* Facilita replay en algunas tecnologías.

### Desventajas

* Introduce infraestructura adicional.
* Aumenta complejidad operacional.
* Requiere monitoreo del broker.
* Requiere manejo de offsets o acknowledgements.
* Introduce otra dependencia.
* El volumen esperado de V1 puede no justificarlo.

### Decisión

**Descartada como requisito inicial.**

Podría reevaluarse si las métricas reales muestran picos de ingesta que no puedan ser absorbidos adecuadamente por la solución inicial.

---

## Alternativa G — Polling centralizado sin adapter

Implementar un proceso genérico que consulte periódicamente APIs de proveedores externos.

Ejemplo:

```text
Scheduler
   ↓
API GPS
   ↓
FlowRide
```

### Ventajas

* Flujo de ejecución controlado por FlowRide.
* No depende de webhooks del proveedor.
* Puede funcionar con proveedores que solo exponen APIs de consulta.

### Desventajas

* Diferentes proveedores tienen contratos distintos.
* Puede generar consultas innecesarias.
* Mayor latencia entre posición real y recepción.
* Puede alcanzar límites de API.
* Sin adapter seguiría existiendo acoplamiento con proveedores.
* No todos los proveedores soportan polling de forma eficiente.

### Decisión

**Descartada como estrategia general.**

Polling podrá utilizarse dentro de un adapter cuando un proveedor específico lo requiera, pero no será el modelo común del dominio.

---

## Consecuencias positivas

* Proveedor GPS sustituible por tenant.
* El dominio no depende de APIs externas.
* No añade un motor adicional en V1.
* La retención de 6 meses puede gestionarse eficientemente por particiones.
* Permite correlacionar telemetría con viajes.
* Mantiene PostgreSQL como tecnología principal.
* Permite distinguir timestamp de origen y recepción.
* Facilita medir retraso de proveedores.
* Permite aislar eventos inválidos.
* Facilita detección de duplicados.
* Permite incorporar nuevos proveedores con impacto localizado.
* Mantiene la telemetría separada de la lógica de asignación.

## Consecuencias negativas

* PostgreSQL puede requerir reevaluación si el volumen crece varios órdenes de magnitud.
* Los proveedores pueden entregar diferentes formatos.
* Las frecuencias de reporte pueden variar.
* La calidad de coordenadas puede variar.
* Los eventos pueden llegar retrasados.
* Pueden existir eventos duplicados.
* Se debe administrar creación y eliminación de particiones.
* Se requiere mantener adapters por proveedor.
* El cálculo de ETA puede verse afectado por GPS ruidoso.

## Tácticas de Bass aplicadas

### Adaptar interfaz

Cada proveedor se transforma al contrato interno definido por:

```text
ITelemetryProviderAdapter
```

Esto evita que formatos externos se propaguen al dominio.

### Gestionar recursos

Se utilizarán:

* Particionamiento temporal.
* Índices.
* Retención de seis meses.
* Eliminación automatizada.
* Consultas por ventana temporal.

### Detectar condiciones

La información GPS permitirá detectar condiciones como:

```text
ETA actualizado - ETA esperado > 5 minutos
```

y generar las alertas correspondientes.

### Aislar fallos

Eventos inválidos deberán aislarse sin detener la ingesta completa.

Ejemplo:

```text
Evento 1 válido     → Procesar
Evento 2 inválido   → Aislar
Evento 3 válido     → Procesar
```

### Reducir acoplamiento

Application y Domain no conocerán proveedores específicos.

---

## Escenarios y requisitos relacionados

* `TEL-INTEROP-01` — ≥100 eventos/min y ≥99% de eventos válidos aceptados.
* `PAS-CONF-01` — detectar desviación >5 min y alertar en <60 s.
* `RF-55` — ingesta de telemetría.
* `RF-56` — detección de desviación de ETA.

---

## ADRs relacionados

* **ADR-0002** — telemetría pertenece al Silo del operador.
* **ADR-0004** — PostgreSQL es el motor de almacenamiento de V1.
* **ADR-0011** — métricas y alertas de ingesta y ETA.

---

## Notas de implementación

* Particionar por `Timestamp` del evento.
* Crear las particiones futuras de forma anticipada.
* Mantener una política automática de retención.
* Eliminar datos superiores a seis meses.
* Indexar inicialmente por `(VehiculoId, Timestamp)`.
* Evaluar `(ViajeId, Timestamp)` según consultas observadas.
* Registrar timestamp del proveedor.
* Registrar timestamp de recepción.
* Calcular retraso entre origen y recepción.
* Validar latitud.
* Validar longitud.
* Validar timestamps.
* Detectar duplicados.
* Aislar coordenadas imposibles.
* Aislar eventos fuera de la ventana esperada.
* Mantener el proveedor externo encapsulado dentro de Infrastructure.
* No exponer payloads del proveedor hacia Domain.
* Registrar métricas por proveedor.
* Registrar cantidad de eventos recibidos.
* Registrar cantidad de eventos válidos.
* Registrar eventos rechazados.
* Registrar retraso promedio de ingesta.
* Monitorear crecimiento de particiones.
* Reevaluar almacenamiento únicamente cuando existan métricas reales que lo justifiquen.

---

## Referencias

* PostgreSQL Documentation — Declarative Partitioning.
* PostgreSQL Documentation — Indexes.
* `FuncionalidadesSignificativas_FlowRide_ACTUALIZADO.xlsx` — RF-55 y RF-56.
* `RestriccionesNegocio_FlowRide_ACTUALIZADO.xlsx` — retención GPS.
* `tradeoff_FlowRide_ACTUALIZADO.xlsx` — TEL-INTEROP-01 y PAS-CONF-01.
