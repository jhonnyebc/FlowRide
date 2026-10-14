# ADR-0008: Integrar WhatsApp mediante Sandbox para el alcance académico y evolucionar a Meta Cloud API

## Metadatos

| Campo              | Valor                                                                                                                                                    |
| ------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ID                 | ADR-0008                                                                                                                                                 |
| Fecha              | 2026-09-09 (revisado 2026-10-13)                                                                                                                         |
| Estado             | **Accepted**                                                                                                                                             |
| Participantes      | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro                                                                          |
| Curso              | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026)                                                                                               |
| Profesor evaluador | Wider Farid Sánchez Garzón                                                                                                                               |
| Reemplaza          | ADR-05 del tradeoff anterior que contemplaba Twilio como BSP intermedio y ajusta la decisión previa de utilizar Meta Cloud API directamente desde la V1. |

---

## Contexto

WhatsApp es el canal funcional definido para la interacción de Pasajeros y Conductores con FlowRide.

A través de este canal se requiere soportar funcionalidades como:

* Recepción de solicitudes.
* Confirmaciones.
* Cambios de estado.
* Notificaciones.
* Interacciones conversacionales.
* Mensajes entrantes.
* Mensajes salientes.

La integración con WhatsApp condiciona atributos como:

* Disponibilidad.
* Interoperabilidad.
* Seguridad.
* Costo.
* Lock-in.
* Manejo de errores.
* Reintentos.
* Idempotencia.
* Observabilidad.
* Administración de mensajes.
* Capacidad de evolución.

Para una implementación productiva, la integración prevista para FlowRide es **Meta Cloud API**, evitando intermediarios innecesarios y manteniendo el control de la integración mediante un adapter.

Sin embargo, el alcance actual corresponde a un proyecto académico del curso **Ingeniería del Software 2**, donde el objetivo principal es demostrar correctamente la arquitectura, integración, procesamiento conversacional y desacoplamiento del proveedor, sin asumir todavía todos los requisitos operacionales necesarios para desplegar WhatsApp Business Platform en un entorno productivo.

Implementar desde el alcance académico toda la configuración productiva asociada a Meta Cloud API puede introducir trabajo adicional relacionado con:

* Configuración de cuentas empresariales.
* Número productivo.
* Verificación y configuración de Meta.
* Plantillas.
* Credenciales productivas.
* Políticas de mensajería.
* Configuración operacional.
* Administración del canal.

Estas actividades no constituyen el objetivo principal del proyecto académico.

Por esta razón, se requiere una solución que permita demostrar técnicamente:

* Entrada de mensajes.
* Salida de mensajes.
* Webhooks.
* Normalización.
* Procesamiento mediante Application.
* Idempotencia.
* Manejo de errores.
* Correlación.
* Desacoplamiento del proveedor.

sin acoplar el núcleo de FlowRide a la tecnología utilizada para la demostración.

---

## Decisión

Para el alcance académico de FlowRide se utilizará un **entorno Sandbox de WhatsApp** como mecanismo de integración y prueba del canal.

El Sandbox será utilizado exclusivamente como entorno de desarrollo, demostración y validación de la integración durante el curso.

> **Revisión 2026-10-13 — Sandbox seleccionado.** El Sandbox es el **número de prueba de Meta Cloud API**: gratuito, admite hasta 5 destinatarios verificados y usa el mismo formato de webhook y la misma firma `X-Hub-Signature-256` que producción (ADR-0012). `WhatsAppSandboxAdapter` habla con ese número. Pasar a producción solo exige un número productivo y plantillas aprobadas: el contrato no cambia. Para las pruebas automatizadas y el desarrollo sin conexión se usa además un adapter simulado en memoria detrás del mismo `IWhatsAppGateway`.

La arquitectura no dependerá directamente del Sandbox.

La comunicación se encapsulará mediante un contrato interno:

```text
IWhatsAppGateway
```

y una implementación ubicada en Infrastructure:

```text
WhatsAppSandboxAdapter
```

La estructura para el alcance académico será:

```text
Pasajero / Conductor de prueba
            ↓
      WhatsApp Sandbox
            ↓
   WhatsAppSandboxAdapter
            ↓
        Application
            ↓
          Domain
```

La evolución prevista para un entorno productivo será:

```text
Pasajero / Conductor
            ↓
         WhatsApp
            ↓
     Meta Cloud API
            ↓
   MetaCloudApiAdapter
            ↓
        Application
            ↓
          Domain
```

De esta forma, el cambio:

```text
WhatsAppSandboxAdapter
        ↓
MetaCloudApiAdapter
```

no deberá requerir modificaciones sobre:

* Domain.
* Casos de uso.
* Reglas de negocio.
* Entidades.
* Servicios de aplicación.
* Lógica conversacional.

Únicamente deberá sustituirse la implementación de infraestructura responsable de comunicarse con el proveedor externo.

---

## Frontera de integración

Application dependerá de una abstracción propia de FlowRide.

Ejemplo conceptual:

```text
Application
      ↓
IWhatsAppGateway
      ↑
      │
WhatsAppSandboxAdapter
```

Para producción:

```text
Application
      ↓
IWhatsAppGateway
      ↑
      │
MetaCloudApiAdapter
```

Application y Domain no deberán conocer:

* Payloads del Sandbox.
* Payloads de Meta.
* URLs externas.
* Tokens.
* SDKs del proveedor.
* Identificadores técnicos propios de la plataforma.
* Formatos de webhook externos.
* Códigos de error específicos del proveedor.

La dependencia deberá mantenerse en esta dirección:

```text
Domain
   ↑
Application
   ↑
IWhatsAppGateway
   ↑
Infrastructure Adapter
   ↑
Proveedor externo
```

y no:

```text
Domain
   ↓
Sandbox API
```

ni:

```text
Application
   ↓
Meta Cloud API
```

---

## Mensajes entrantes

Los mensajes externos deberán convertirse primero a un modelo interno.

Flujo para el alcance académico:

```text
Mensaje Sandbox
       ↓
WhatsAppSandboxAdapter
       ↓
Validación
       ↓
Normalización
       ↓
IncomingWhatsAppMessage
       ↓
Comando FlowRide
       ↓
Application
       ↓
Caso de uso
```

Ejemplo conceptual del modelo interno:

```text
IncomingWhatsAppMessage
- ExternalMessageId
- Sender
- MessageType
- Content
- ReceivedAt
```

El resto del sistema trabajará con este modelo y no con el payload original del Sandbox.

---

## Mensajes salientes

El flujo inverso será:

```text
Application
     ↓
SendWhatsAppMessage
     ↓
IWhatsAppGateway
     ↓
WhatsAppSandboxAdapter
     ↓
Sandbox
     ↓
WhatsApp de prueba
```

En producción:

```text
Application
     ↓
SendWhatsAppMessage
     ↓
IWhatsAppGateway
     ↓
MetaCloudApiAdapter
     ↓
Meta Cloud API
     ↓
WhatsApp
```

---

## Responsabilidades del adapter

El adapter utilizado para la integración deberá encargarse de:

* Recibir eventos externos.
* Traducir formatos externos.
* Normalizar mensajes.
* Enviar mensajes.
* Convertir estados externos a estados internos.
* Manejar identificadores externos.
* Registrar errores técnicos.
* Manejar errores transitorios.
* Aplicar reintentos cuando corresponda.
* Mantener correlación de mensajes.
* Evitar que contratos externos lleguen a Application o Domain.

Estas responsabilidades deberán mantenerse independientemente de que el proveedor concreto sea:

```text
Sandbox
```

o posteriormente:

```text
Meta Cloud API
```

---

## Idempotencia

Los mensajes externos deberán tener un identificador que permita detectar procesamiento duplicado.

Ejemplo:

```text
ExternalMessageId
        ↓
Consultar procesamiento previo
        ↓
 ┌─────────────┬─────────────┐
 ↓             ↓
Existe       No existe
 ↓             ↓
Ignorar      Procesar
```

El Sandbox no será considerado responsable de garantizar la idempotencia funcional de FlowRide.

La responsabilidad permanecerá dentro del sistema.

Esto permite conservar el mismo mecanismo al migrar posteriormente a Meta Cloud API.

---

## Manejo de errores

Los errores de integración se clasificarán conceptualmente en:

### Error transitorio

```text
Error transitorio
       ↓
Reintento
       ↓
Backoff
       ↓
Nuevo intento
```

Ejemplos:

* Timeout.
* Error temporal del proveedor.
* Problema momentáneo de red.

### Error permanente

```text
Error permanente
       ↓
Registrar fallo
       ↓
No realizar reintentos indefinidos
```

Ejemplos:

* Formato inválido.
* Destinatario inválido.
* Configuración incorrecta.
* Credenciales rechazadas.

---

## Correlación

FlowRide deberá mantener correlación entre el mensaje interno y el identificador devuelto por el proveedor.

Ejemplo:

```text
FlowRideMessageId
        ↕
ExternalMessageId
        ↕
Estado externo
```

Esto permitirá conocer qué ocurrió con una interacción independientemente de la tecnología utilizada.

---

## Alcance del Sandbox

El Sandbox se utilizará para demostrar:

* Envío de mensajes.
* Recepción de mensajes.
* Funcionamiento del webhook.
* Normalización.
* Integración con Application.
* Procesamiento conversacional.
* Idempotencia.
* Manejo de errores.
* Correlación.
* Pruebas de integración.
* Desacoplamiento del proveedor.

El Sandbox **no será considerado representación completa del entorno productivo**.

Por tanto, no se utilizará como evidencia definitiva para validar:

* SLA productivo.
* Disponibilidad real del canal.
* Límites productivos de mensajería.
* Costos reales.
* Volúmenes máximos de producción.
* Políticas comerciales.
* Comportamiento completo de plantillas.
* Rendimiento productivo de Meta.

---

## Evolución a Meta Cloud API

La solución objetivo de largo plazo será **Meta Cloud API directa**.

La transición se realizará reemplazando:

```text
WhatsAppSandboxAdapter
```

por:

```text
MetaCloudApiAdapter
```

manteniendo:

```text
IWhatsAppGateway
```

como contrato interno.

Conceptualmente:

```text
                IWhatsAppGateway
                       ↑
              ┌────────┴────────┐
              │                 │
WhatsAppSandboxAdapter   MetaCloudApiAdapter
       Curso                 Producción
```

Para el alcance académico:

```text
IWhatsAppGateway
        ↓
WhatsAppSandboxAdapter
```

Para producción:

```text
IWhatsAppGateway
        ↓
MetaCloudApiAdapter
```

Esta transición no deberá modificar las reglas de negocio.

---

## Justificación de la decisión

Se selecciona **Sandbox para el alcance académico** porque permite demostrar la integración completa del canal WhatsApp sin introducir desde esta etapa todas las actividades operacionales necesarias para configurar un entorno productivo real.

El objetivo del curso es demostrar la arquitectura y comportamiento de FlowRide, especialmente:

* Desacoplamiento.
* Integración.
* Procesamiento conversacional.
* Normalización.
* Idempotencia.
* Manejo de errores.
* Trazabilidad.
* Interacción con los casos de uso.

Estas capacidades pueden validarse utilizando un Sandbox.

La decisión se fundamenta en:

* Alcance académico limitado.
* Menor complejidad inicial.
* Menor esfuerzo de configuración.
* Permite realizar pruebas controladas.
* Permite demostrar mensajes entrantes y salientes.
* Facilita pruebas del webhook.
* Facilita pruebas de idempotencia.
* No obliga a utilizar credenciales productivas.
* Mantiene desacoplado el dominio.
* Permite migrar posteriormente a Meta Cloud API sustituyendo únicamente el adapter.

El Sandbox no se selecciona como solución productiva.

La decisión establece explícitamente dos etapas:

```text
Etapa 1 — Curso / V1 académica
Sandbox
```

y:

```text
Etapa 2 — Producción / evolución
Meta Cloud API
```

Esto permite reducir el alcance operativo durante el curso sin comprometer la dirección arquitectónica de largo plazo.

---

# Alternativas consideradas

## Alternativa A — Meta Cloud API directa desde el alcance académico

Implementar desde el proyecto del curso la integración completa directamente contra Meta Cloud API.

### Ventajas

* La solución del curso sería más cercana al entorno productivo.
* Menos diferencias entre desarrollo y producción.
* Menor trabajo futuro de sustitución del adapter.
* Permite validar directamente contratos de Meta.
* No introduce un entorno intermediario de pruebas.

### Desventajas

* Incrementa el trabajo de configuración durante el curso.
* Puede requerir configuración empresarial adicional.
* Introduce tareas operacionales fuera del objetivo principal.
* Puede consumir tiempo destinado a funcionalidades centrales de FlowRide.
* Hace que una integración externa condicione el avance del proyecto.

### Decisión

**Descartada para el alcance académico.**

Meta Cloud API sigue siendo la solución objetivo de largo plazo, pero implementar toda la integración productiva no es necesario para demostrar los drivers arquitectónicos requeridos durante el curso.

---

## Alternativa B — Twilio Sandbox / BSP

Utilizar Twilio como intermediario para acceder a WhatsApp durante desarrollo y posteriormente mantenerlo como BSP.

### Ventajas

* Facilita pruebas.
* Documentación amplia.
* SDKs disponibles.
* Reduce parte del esfuerzo de integración.
* Ofrece herramientas adicionales de comunicación.

### Desventajas

* Introduce un intermediario comercial.
* Agrega una dependencia externa adicional.
* Puede aumentar lock-in.
* Puede introducir costos posteriores.
* No corresponde con la dirección de largo plazo hacia Meta Cloud API directa.

### Decisión

**Descartada como estrategia principal.**

Aunque un Sandbox basado en BSP puede ser útil para pruebas, FlowRide no busca mantener un BSP como dependencia productiva de largo plazo.

---

## Alternativa C — Callbell

Utilizar Callbell como capa entre FlowRide y WhatsApp.

Ejemplo:

```text
FlowRide
    ↓
Callbell
    ↓
Meta / WhatsApp
```

### Ventajas

* Reduce esfuerzo inicial de integración.
* Ofrece API.
* Ofrece webhooks.
* Gestión de conversaciones.
* Bandeja multiagente.
* Automatizaciones.
* Herramientas de atención.
* Puede acelerar una V1 orientada a atención humana.

### Desventajas

* Introduce intermediación adicional.
* Añade dependencia comercial.
* Aumenta lock-in.
* Puede incorporar costos que no se justifican en el alcance académico.
* Gran parte de las funcionalidades de CRM no forman parte del núcleo de FlowRide.
* La evolución prevista es integración directa con Meta.

### Decisión

**Descartada como integración principal para este alcance.**

Callbell es técnicamente viable, pero aporta principalmente capacidades de CRM y atención multiagente que no constituyen el objetivo central del proyecto.

---

## Alternativa D — Sandbox para el alcance académico + Meta Cloud API a largo plazo

Utilizar un entorno Sandbox durante el curso y mantener una abstracción que permita sustituirlo posteriormente por Meta Cloud API.

Ejemplo:

```text
CURSO

Application
     ↓
IWhatsAppGateway
     ↓
WhatsAppSandboxAdapter
     ↓
Sandbox
```

Posteriormente:

```text
PRODUCCIÓN

Application
     ↓
IWhatsAppGateway
     ↓
MetaCloudApiAdapter
     ↓
Meta Cloud API
```

### Ventajas

* Reduce complejidad durante el curso.
* Permite demostrar la arquitectura.
* Facilita pruebas controladas.
* No compromete Domain.
* Permite validar webhooks.
* Permite validar idempotencia.
* Permite validar envío y recepción.
* Reduce dependencia de configuración productiva.
* Mantiene Meta Cloud API como dirección futura.
* Permite sustitución del proveedor mediante el adapter.

### Desventajas

* Sandbox y producción no son entornos equivalentes.
* Algunas restricciones reales no podrán validarse.
* Será necesario implementar posteriormente `MetaCloudApiAdapter`.
* Los límites productivos deberán validarse posteriormente.
* Parte de la integración deberá volver a probarse antes de producción.

### Decisión

**Elegida.**

Es la alternativa que mejor equilibra el alcance académico actual con la dirección tecnológica esperada para una versión productiva.

---

## Alternativa E — Aplicación móvil propia

Desarrollar una aplicación móvil para Pasajero y Conductor en lugar de utilizar WhatsApp.

### Ventajas

* Control completo de experiencia.
* Integración directa con FlowRide.
* No depende de WhatsApp.
* Posibilidad de agregar funcionalidades específicas.

### Desventajas

* Incrementa significativamente el alcance.
* Requiere instalación.
* Aumenta fricción para el usuario.
* Requiere desarrollo móvil adicional.
* Contradice el objetivo de utilizar WhatsApp como canal de baja fricción.

### Decisión

**Descartada.**

No corresponde con el alcance funcional definido para FlowRide.

---

## Alternativa F — SMS, Telegram o correo electrónico

Utilizar otros canales para Pasajero y Conductor.

### Ventajas

* Reduce dependencia de WhatsApp.
* Puede proporcionar mecanismos alternativos.
* Algunos canales tienen integraciones sencillas.

### Desventajas

* Fragmenta la experiencia.
* Introduce más integraciones.
* Amplía el alcance.
* No corresponde con el canal funcional actualmente definido.

### Decisión

**Descartada como canal funcional principal.**

---

## Comparación de alternativas

| Criterio                          | Meta directa desde curso | Twilio/BSP | Callbell |       Sandbox → Meta | App propia | Otros canales |
| --------------------------------- | -----------------------: | ---------: | -------: | -------------------: | ---------: | ------------: |
| Facilidad para el curso           |                    Media |       Alta |     Alta |         **Muy alta** |       Baja |         Media |
| Cercanía a producción             |             **Muy alta** |       Alta |     Alta |           Media-Alta |       Baja |          Baja |
| Complejidad inicial               |               Media-Alta |      Media |    Media |             **Baja** |   Muy alta |         Media |
| Desacoplamiento posible           |                     Alto |       Alto |     Alto |         **Muy alto** |       Alto |          Alto |
| Mantiene WhatsApp                 |                       Sí |         Sí |       Sí |               **Sí** |         No |            No |
| Requiere intermediario productivo |                       No |         Sí |       Sí | **No a largo plazo** |         No |      Variable |
| Pruebas controladas               |                     Alta |       Alta |     Alta |         **Muy alta** |      Media |         Media |
| Adecuación al alcance académico   |                     Alta |       Alta |     Alta |         **Muy alta** |       Baja |         Media |
| Evolución hacia Meta directa      |                      N/A |      Media |    Media |          **Directa** |       Baja |          Baja |
| Complejidad operacional V1        |               Media-Alta |      Media |    Media |             **Baja** |       Alta |         Media |

---

## Consecuencias positivas

* Reduce la complejidad del alcance académico.
* Permite concentrarse en los drivers principales de FlowRide.
* Facilita demostraciones.
* Facilita pruebas.
* Mantiene Application independiente del proveedor.
* Mantiene Domain independiente del proveedor.
* Permite probar webhooks.
* Permite probar idempotencia.
* Permite probar correlación.
* Permite probar manejo de errores.
* Evita depender de configuración productiva durante el curso.
* Mantiene Meta Cloud API como dirección de evolución.
* Facilita sustituir la infraestructura sin cambiar el dominio.

---

## Consecuencias negativas

* El Sandbox no representa completamente producción.
* La disponibilidad observada no representa un SLA real.
* Los límites de prueba pueden ser diferentes.
* El comportamiento de plantillas puede diferir.
* Será necesario implementar y validar el adapter de Meta posteriormente.
* Deberán repetirse pruebas de integración antes de producción.
* Los costos reales no podrán medirse con precisión usando Sandbox.
* Algunos errores productivos solo aparecerán al utilizar Meta Cloud API real.

---

## Tácticas de Bass aplicadas

### Adaptar interfaz

Se utiliza `IWhatsAppGateway` para evitar que el sistema dependa directamente del proveedor externo.

### Reducir acoplamiento

Los tipos propios del Sandbox o de Meta permanecen exclusivamente en Infrastructure.

### Reemplazar componente

La implementación:

```text
WhatsAppSandboxAdapter
```

podrá sustituirse por:

```text
MetaCloudApiAdapter
```

sin cambiar Application ni Domain.

### Reintentar

Los errores transitorios podrán procesarse mediante reintentos con backoff.

### Encolar

Los mensajes pendientes podrán preservarse cuando existan interrupciones temporales.

### Autenticar actores

Los endpoints externos deberán validar los mecanismos de autenticación disponibles en cada proveedor.

---

## Escenarios y requisitos relacionados

* `COND-DISP-01` — disponibilidad objetivo del canal en producción.
* `PAS-DISP-01` — recepción confiable de solicitudes.
* `PAS-DISP-03` — recuperación ante interrupciones transitorias.
* `COND-ESC-01` — capacidad de absorber picos de mensajes.
* `PAS-UX-01` — interacción sin aplicación móvil adicional.
* `RF-14` — recepción de mensajes.
* `RF-15` — procesamiento conversacional.
* `RF-16` — confirmaciones.
* `RF-17` — notificaciones.
* `RF-18` — cambios y consulta de estados.

Los requisitos de disponibilidad y capacidad productiva deberán validarse nuevamente al realizar la transición desde Sandbox hacia Meta Cloud API.

---

## ADRs relacionados

* **ADR-0003** — identidad y autorización de los usuarios detrás del canal.
* **ADR-0006** — consistencia e idempotencia del procesamiento.
* **ADR-0011** — observabilidad, errores, métricas y trazas.

---

## Notas de implementación

* Definir `IWhatsAppGateway` dentro de una capa independiente del proveedor.
* Implementar `WhatsAppSandboxAdapter` en Infrastructure.
* No exponer payloads del Sandbox hacia Application.
* No exponer payloads de Meta hacia Application.
* Normalizar mensajes entrantes.
* Utilizar identificador externo para idempotencia.
* Registrar correlación de mensajes.
* Mantener secretos fuera del repositorio.
* Instrumentar mensajes enviados.
* Instrumentar mensajes recibidos.
* Instrumentar errores.
* Instrumentar reintentos.
* Instrumentar mensajes duplicados.
* No utilizar Sandbox como fuente de verdad.
* No colocar reglas de negocio dentro del adapter.
* Mantener PostgreSQL como persistencia de estados de FlowRide.
* Mantener la lógica conversacional dentro de FlowRide.
* Utilizar Sandbox exclusivamente para desarrollo, pruebas y demostración.
* Implementar `MetaCloudApiAdapter` antes de una salida productiva.
* Repetir pruebas de integración contra Meta Cloud API antes de producción.
* Validar disponibilidad, seguridad, templates, límites y costos reales durante la transición productiva.

---

## Criterio de evolución

La migración hacia Meta Cloud API deberá realizarse cuando FlowRide avance desde el alcance académico hacia una versión productiva.

El criterio será:

```text
Curso / Demo
      ↓
WhatsApp Sandbox
      ↓
Validación arquitectónica
      ↓
Preparación productiva
      ↓
Meta Cloud API
```

La transición se considerará correcta si:

1. No requiere modificar Domain.
2. No requiere modificar reglas de negocio.
3. No requiere modificar los casos de uso.
4. Solo requiere sustituir la implementación de `IWhatsAppGateway`.
5. Se mantienen idempotencia y correlación.
6. Se mantienen métricas y observabilidad.
7. Se validan nuevamente los requisitos no representativos del Sandbox.

---

## Estado final de la decisión

Para el alcance del curso se adopta:

**WhatsApp Sandbox + Adapter desacoplado.**

Para la evolución productiva se establece como dirección:

**Meta Cloud API directa + MetaCloudApiAdapter.**

La arquitectura será:

```text
HOY — ALCANCE ACADÉMICO

FlowRide
   ↓
IWhatsAppGateway
   ↓
WhatsAppSandboxAdapter
   ↓
Sandbox
```

y evolucionará a:

```text
LARGO PLAZO — PRODUCCIÓN

FlowRide
   ↓
IWhatsAppGateway
   ↓
MetaCloudApiAdapter
   ↓
Meta Cloud API
   ↓
WhatsApp
```

La elección del Sandbox responde exclusivamente al **alcance académico y de demostración**, mientras que Meta Cloud API permanece como la alternativa prevista para una implementación productiva.

El principio arquitectónico principal es que **FlowRide no dependerá directamente ni del Sandbox ni de Meta Cloud API**, sino del contrato interno `IWhatsAppGateway`, permitiendo que el proveedor externo cambie sin afectar el núcleo del sistema.

---

## Referencias

* Meta for Developers — WhatsApp Business Platform / Cloud API.
* Documentación del Sandbox seleccionado para las pruebas del curso.
* `RestriccionesNegocio_FlowRide_ACTUALIZADO.xlsx`.
* `RestriccionesTecnicas_FlowRide_ACTUALIZADO.xlsx`.
* `tradeoff_FlowRide_ACTUALIZADO.xlsx`.
