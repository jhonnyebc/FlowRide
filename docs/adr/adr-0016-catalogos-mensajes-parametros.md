# ADR-0016: Externalizar textos en un catálogo de mensajes (i18n) y la configuración en un catálogo de parámetros por operador

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0016 |
| Fecha | 2026-09-22 |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Building blocks | Message catalog / i18n, Parameter catalog |

## Contexto

**Textos.** FlowRide emite textos al usuario desde el bot de WhatsApp, las notificaciones, las plantillas de Meta y el Panel Web. Las plantillas de Meta se registran y aprueban **por cuenta de WhatsApp Business**. Como cada operador tiene su propio número y su propia cuenta (ADR-0002, ADR-0008), cada operador necesita su propio registro de plantillas aprobadas. V1 opera en español de Colombia, pero el producto no debe tener textos incrustados en el código.

**Parámetros.** Hay valores operativos que cambian por operador y no deben requerir un despliegue:

- configuración de reglas y pesos del motor (RF-21; ADR-0009);
- umbral de desviación de ETA (5 min por defecto; ADR-0005);
- anticipación de los recordatorios (RF-35);
- franjas críticas de operación;
- ventanas de cancelación (RF-32);
- políticas de visibilidad de solicitudes personales (`PAS-SEG-01`).

## Decisión

### Catálogo de mensajes (i18n)

1. Cada texto tiene una **clave estable** (`asignacion.conductor.nueva`), con traducciones por *locale* (`es-CO` por defecto) en archivos de recursos versionados en el repositorio, junto al código.
2. Para WhatsApp, cada clave que pueda enviarse fuera de la ventana de 24 h tiene un **registro de plantilla** (nombre, idioma, variables y estado de aprobación). El registro se guarda en la base de cada operador y se llena al aprovisionarlo, al someter las plantillas a su cuenta de WhatsApp Business.
3. El Notification Gateway (ADR-0015) usa la clave para elegir el texto de sesión o la plantilla. Si la plantilla del operador no está aprobada, lo informa como error permanente.
4. El Panel Web usa las mismas claves para sus textos de interfaz. V1 solo incluye `es-CO`; agregar un idioma es añadir recursos, no cambiar código.

### Catálogo de parámetros

5. Tabla `ops.parametros` en la base de cada operador: clave, tipo, valor, versión, quién y cuándo lo modificó. Cada clave tiene un **valor por defecto de producto** y **validaciones** (tipo, rango) definidas en código.
6. Solo el Administrador del operador modifica los parámetros desde el panel. Cada cambio incrementa la versión, se registra en auditoría (ADR-0011) y se refleja en todas las réplicas en ≤60 s (caché con TTL máximo, ADR-0006).
7. La configuración del motor de asignación (ADR-0009) es un parámetro estructurado de este catálogo, con su propio validador.

## Alternativas consideradas

| Criterio | **Recursos versionados + registro de plantillas + parámetros en BD del operador (elegida)** | A. Textos y valores en el código | B. Archivos de configuración por despliegue | C. Servicio externo de configuración / feature flags |
|---|---|---|---|---|
| Cambiar un parámetro sin desplegar | Sí | No | No (requiere reinicio/despliegue) | Sí |
| Plantillas por cuenta de cada operador | Sí | No | Parcial | No resuelve plantillas |
| Auditoría de cambios | Sí | Vía Git | Vía Git | Depende |
| Respeta el Silo | Sí | — | Sí | No, si es compartido |
| Costo | Nulo | Nulo | Nulo | Suscripción o componente adicional |

## Consecuencias

**Positivas**

- Textos y plantillas consistentes entre el bot, las notificaciones y el panel.
- Los operadores ajustan su operación sin intervención del equipo.
- Preparado para otros idiomas.

**Negativas**

- Hay que mantener sincronizadas las claves del código con las plantillas aprobadas en cada cuenta de Meta.
- Validar bien los parámetros es crítico: un valor extremo, como un umbral de ETA de 0 min, generaría ruido operativo. Por eso existen los rangos en código.

## Tácticas de Bass aplicadas

- **Diferir binding** — textos y parámetros en tiempo de ejecución.
- **Localizar cambios** — un texto o un umbral se cambia en un solo lugar.
- **Validar entrada** — rangos y tipos de los parámetros.

## Escenarios y requisitos relacionados

- `PAS-UX-01`, `ADM-UX-02`, `PAS-CONF-01`, `PAS-SEG-01`.
- Escenario 8 (Modificabilidad) de Drivers.
- `RF-04`, `RF-20`, `RF-21`, `RF-35`.

## ADRs relacionados

- **ADR-0005**, **ADR-0006**, **ADR-0008**, **ADR-0009**, **ADR-0011**, **ADR-0015**.
