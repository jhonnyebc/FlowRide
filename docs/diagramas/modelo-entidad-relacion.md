# Modelo entidad-relación de FlowRide

Modelo físico de las bases de datos de FlowRide **tal como están implementadas**. Se generó el 24-10-2026 leyendo el esquema real de PostgreSQL 17 (tablas, columnas, llaves primarias, llaves foráneas e índices únicos) después de aplicar todas las migraciones; reemplaza al modelo de diseño del 13-10-2026, anterior a la implementación.

- **Base del operador (Silo):** cada operador tiene su propia base con los esquemas `ops` (operación), `motor` (motor de asignación), `canal` (WhatsApp), `notif` (Outbox, alertas y plantillas) y `audit` (auditoría legal). Secciones 1 a 5.
- **Catálogo de plataforma:** una base aparte, compartida y sin datos operacionales. Sección 6.

## Convenciones

- **Marcas:** `PK` llave primaria, `FK` llave foránea, `UK` columna que forma parte de un índice único; `"NULL"` indica que la columna admite nulos.
- **Cardinalidad:** se deduce de cada llave foránea. `||` es obligatoria y `|o` opcional del lado del padre; `o{` son muchos y `o|` a lo sumo uno del lado del hijo (la columna es única, como en la herencia de `pasajero` y `conductor`, que comparten la llave con `usuario`).
- **Identificadores:** `uuid` generados por la aplicación (versión 7), salvo la auditoría (`bigint` creciente) y el catálogo (identificador del operador).
- **Fechas:** los instantes se guardan en UTC (`timestamptz`); los eventos futuros guardan además su hora local y su zona horaria (ADR-0020).
- **Concurrencia:** la columna de sistema `xmin` de PostgreSQL es el token de versión de las entidades que compiten (ADR-0007); por ser de sistema no aparece en los diagramas.
- **Restricciones `EXCLUDE`** (no se ven en el diagrama): un conductor no puede tener turnos que se traslapen, ni un conductor o un vehículo asignaciones vigentes que se traslapen en la misma franja; y solo puede haber una configuración del motor activa.
- **Referencias lógicas sin llave foránea** (a propósito):
  - `audit.evento_auditoria` (`actor_id`, `recurso_id`): la auditoría legal no depende de que el registro auditado siga existiendo.
  - `motor.decision_asignacion` y `motor.evaluacion_candidato` (`conductor_seleccionado_id`, `vehiculo_seleccionado_id`, `conductor_id`, `vehiculo_id`, `turno_id`) y `ops.asignacion.decision_id`: el registro de decisiones es una evidencia histórica e inmutable.
  - `notif.alerta` (`referencia_id`, `event_id`, `leida_por_id`), `notif.outbox_mensaje.event_id`, `notif.plantilla_whatsapp.modificada_por_id` y `canal.mensaje_entrante.usuario_id`: eventos y mensajes que pueden referirse a distintos tipos de registro o llegar antes de conocer al usuario.
  - `id_externo` de los mensajes, `ops.usuario.identidad_externa_id` y `plataforma.ruta_operador.phone_number_id` son identificadores de sistemas externos (Meta y Keycloak).
- Las tablas de una sección que referencian tablas de otra sección muestran esas tablas solo con su llave primaria.

## 1. Usuarios, clientes y recursos operativos

Usuarios del operador (con herencia TPT para pasajero y conductor), clientes empresariales, recursos del conductor (turnos, documentos, vehículo habitual), itinerarios y parámetros.

```mermaid
erDiagram
    ops_pasajero["ops.pasajero"] {
        uuid id PK,FK
        varchar codigo_interno UK
        uuid empresa_cliente_id FK,UK
    }
    ops_empresa_cliente["ops.empresa_cliente"] {
        uuid id PK
        varchar nombre
        varchar nit UK
        varchar correo_contacto
        bool activa
    }
    ops_itinerario["ops.itinerario"] {
        uuid id PK
        uuid empresa_cliente_id FK,UK
        varchar codigo_servicio UK
        date fecha UK
        varchar origen
        varchar destino
        time hora_salida
        time hora_llegada
        varchar zona_horaria
    }
    ops_rol["ops.rol"] {
        uuid id PK
        varchar nombre UK
        varchar descripcion
    }
    ops_usuario["ops.usuario"] {
        uuid id PK
        varchar nombre
        varchar correo UK "NULL"
        varchar telefono_whats_app UK
        varchar identidad_externa_id UK "NULL"
        uuid rol_id FK
        bool activo
        timestamptz creado_en
    }
    ops_turno["ops.turno"] {
        uuid id PK
        uuid conductor_id FK
        varchar estado
        varchar motivo_abandono "NULL"
        timestamptz fin
        timestamptz inicio
        timestamp fin_local
        timestamp inicio_local
        varchar zona_horaria
    }
    ops_parametro["ops.parametro"] {
        varchar clave PK
        varchar tipo
        jsonb valor
        int version
        uuid modificado_por_id FK "NULL"
        timestamptz modificado_en
    }
    ops_documento_conductor["ops.documento_conductor"] {
        uuid id PK
        uuid conductor_id FK
        varchar tipo
        varchar referencia_blob
        date fecha_vencimiento "NULL"
    }
    ops_conductor["ops.conductor"] {
        uuid id PK,FK
        varchar numero_licencia UK
        varchar categoria_licencia
        date vencimiento_licencia
        varchar estado
        timestamptz fecha_ultima_asignacion "NULL"
        bool apto_servicio_critico
    }
    ops_vehiculo["ops.vehiculo"] {
        uuid id PK
        varchar placa UK
        varchar marca
        varchar modelo
        int capacidad
        date vencimiento_rtm
        date vencimiento_soat
        bool activo
        uuid conductor_habitual_id FK,UK "NULL"
    }
    ops_usuario |o--o{ ops_parametro : "modificado_por_id"
    ops_usuario ||--o| ops_conductor : "id"
    ops_conductor |o--o| ops_vehiculo : "conductor_habitual_id"
    ops_rol ||--o{ ops_usuario : "rol_id"
    ops_usuario ||--o| ops_pasajero : "id"
    ops_empresa_cliente ||--o{ ops_pasajero : "empresa_cliente_id"
    ops_empresa_cliente ||--o{ ops_itinerario : "empresa_cliente_id"
    ops_conductor ||--o{ ops_documento_conductor : "conductor_id"
    ops_conductor ||--o{ ops_turno : "conductor_id"
```

## 2. Solicitudes de viaje

La solicitud con su historial de estados y su cancelación. Las tablas de otras secciones aparecen solo con su llave.

```mermaid
erDiagram
    ops_estado_solicitud_historial["ops.estado_solicitud_historial"] {
        uuid id PK
        uuid solicitud_id FK
        varchar estado_anterior "NULL"
        varchar estado_nuevo
        timestamptz fecha_hora
        uuid actor_id FK "NULL"
        text motivo "NULL"
    }
    ops_pasajero["ops.pasajero"] {
        uuid id PK,FK
    }
    ops_itinerario["ops.itinerario"] {
        uuid id PK
    }
    ops_cancelacion_solicitud["ops.cancelacion_solicitud"] {
        uuid id PK
        uuid solicitud_id FK,UK
        text motivo
        timestamptz fecha_hora
        uuid cancelada_por_id FK
    }
    ops_empresa_cliente["ops.empresa_cliente"] {
        uuid id PK
    }
    ops_usuario["ops.usuario"] {
        uuid id PK
    }
    ops_solicitud_viaje["ops.solicitud_viaje"] {
        uuid id PK
        uuid pasajero_id FK
        uuid empresa_cliente_id FK "NULL"
        uuid itinerario_id FK "NULL"
        varchar direccion_origen
        varchar direccion_destino
        timestamptz fecha_hora_servicio
        interval duracion_estimada
        int cantidad_pasajeros
        bool urgente
        bool es_personal
        varchar canal
        timestamptz registrada_en
        varchar estado
        timestamp hora_local_servicio
        varchar zona_horaria
    }
    ops_usuario |o--o{ ops_estado_solicitud_historial : "actor_id"
    ops_pasajero ||--o{ ops_solicitud_viaje : "pasajero_id"
    ops_itinerario |o--o{ ops_solicitud_viaje : "itinerario_id"
    ops_solicitud_viaje ||--o{ ops_estado_solicitud_historial : "solicitud_id"
    ops_solicitud_viaje ||--o| ops_cancelacion_solicitud : "solicitud_id"
    ops_usuario ||--o{ ops_cancelacion_solicitud : "cancelada_por_id"
    ops_empresa_cliente |o--o{ ops_solicitud_viaje : "empresa_cliente_id"
```

## 3. Asignaciones y viajes

La asignación (que puede reemplazar a otra) y el viaje con sus paradas, incidentes y calificación. Las tablas de otras secciones aparecen solo con su llave.

```mermaid
erDiagram
    ops_asignacion["ops.asignacion"] {
        uuid id PK
        uuid solicitud_id FK
        uuid conductor_id FK
        uuid vehiculo_id FK
        varchar origen
        timestamptz asignada_en
        uuid decision_id "NULL"
        uuid asignacion_reemplazada_id FK "NULL"
        varchar estado
        varchar motivo_cierre "NULL"
        timestamptz franja_fin
        timestamptz franja_inicio
    }
    ops_viaje["ops.viaje"] {
        uuid id PK
        uuid asignacion_id FK,UK
        varchar estado
        timestamptz fecha_hora_inicio "NULL"
        timestamptz fecha_hora_fin "NULL"
    }
    ops_parada["ops.parada"] {
        uuid id PK
        uuid viaje_id FK,UK
        int orden UK
        varchar direccion
        timestamptz hora_estimada
        timestamptz hora_real "NULL"
        timestamp hora_estimada_local
        varchar zona_horaria
    }
    ops_incidente["ops.incidente"] {
        uuid id PK
        uuid viaje_id FK
        varchar tipo
        text descripcion
        timestamptz fecha_hora
        uuid reportado_por_id FK
    }
    ops_calificacion["ops.calificacion"] {
        uuid id PK
        uuid viaje_id FK,UK
        uuid pasajero_id FK,UK
        int puntaje
        text comentario "NULL"
        timestamptz fecha_hora
    }
    ops_conductor["ops.conductor"] {
        uuid id PK,FK
    }
    ops_solicitud_viaje["ops.solicitud_viaje"] {
        uuid id PK
    }
    ops_vehiculo["ops.vehiculo"] {
        uuid id PK
    }
    ops_pasajero["ops.pasajero"] {
        uuid id PK,FK
    }
    ops_usuario["ops.usuario"] {
        uuid id PK
    }
    ops_asignacion |o--o{ ops_asignacion : "asignacion_reemplazada_id"
    ops_conductor ||--o{ ops_asignacion : "conductor_id"
    ops_solicitud_viaje ||--o{ ops_asignacion : "solicitud_id"
    ops_vehiculo ||--o{ ops_asignacion : "vehiculo_id"
    ops_asignacion ||--o| ops_viaje : "asignacion_id"
    ops_pasajero ||--o{ ops_calificacion : "pasajero_id"
    ops_viaje ||--o{ ops_calificacion : "viaje_id"
    ops_usuario ||--o{ ops_incidente : "reportado_por_id"
    ops_viaje ||--o{ ops_incidente : "viaje_id"
    ops_viaje ||--o{ ops_parada : "viaje_id"
```

## 4. Motor de asignación

Configuración versionada del motor con sus reglas y el registro de decisiones (decision log): cada decisión guarda los candidatos evaluados y el resultado de cada regla.

```mermaid
erDiagram
    motor_configuracion_asignacion["motor.configuracion_asignacion"] {
        uuid id PK
        int version UK
        varchar estado
        uuid creada_por_id FK "NULL"
        timestamptz creada_en
        timestamptz activada_en "NULL"
    }
    motor_regla_configurada["motor.regla_configurada"] {
        uuid id PK
        uuid configuracion_id FK,UK
        varchar codigo_regla UK
        varchar tipo UK
        bool habilitada
        smallint orden UK
        numeric peso
        jsonb parametros "NULL"
    }
    motor_decision_asignacion["motor.decision_asignacion"] {
        uuid id PK
        uuid solicitud_id FK
        uuid configuracion_id FK
        int version_configuracion
        varchar origen
        timestamptz evaluada_en
        varchar resultado
        uuid conductor_seleccionado_id "NULL"
        uuid vehiculo_seleccionado_id "NULL"
        text motivo
        jsonb desempate "NULL"
        jsonb entrada_snapshot
    }
    motor_evaluacion_candidato["motor.evaluacion_candidato"] {
        uuid id PK
        uuid decision_id FK,UK
        uuid conductor_id UK
        uuid vehiculo_id UK
        uuid turno_id
        bool elegible
        text motivo_descarte "NULL"
        numeric prioridad
        numeric puntaje_total
        int posicion_ranking "NULL"
    }
    motor_resultado_regla["motor.resultado_regla"] {
        uuid id PK
        uuid evaluacion_id FK,UK
        varchar codigo_regla UK
        int version_regla
        varchar tipo
        smallint orden
        bool aprobado "NULL"
        numeric valor "NULL"
        numeric peso "NULL"
        numeric valor_ponderado "NULL"
        text motivo
    }
    ops_usuario["ops.usuario"] {
        uuid id PK
    }
    ops_solicitud_viaje["ops.solicitud_viaje"] {
        uuid id PK
    }
    ops_usuario |o--o{ motor_configuracion_asignacion : "creada_por_id"
    motor_configuracion_asignacion ||--o{ motor_decision_asignacion : "configuracion_id"
    ops_solicitud_viaje ||--o{ motor_decision_asignacion : "solicitud_id"
    motor_configuracion_asignacion ||--o{ motor_regla_configurada : "configuracion_id"
    motor_decision_asignacion ||--o{ motor_evaluacion_candidato : "decision_id"
    motor_evaluacion_candidato ||--o{ motor_resultado_regla : "evaluacion_id"
```

## 5. Canal WhatsApp, notificaciones y auditoría

Conversaciones y mensajes del bot, la Outbox de eventos, las alertas del Panel Web con sus correos, las plantillas de WhatsApp y la auditoría legal encadenada.

```mermaid
erDiagram
    canal_conversacion["canal.conversacion"] {
        uuid id PK
        varchar telefono UK
        varchar paso
        timestamptz ultima_actividad
        text datos
    }
    canal_mensaje_entrante["canal.mensaje_entrante"] {
        uuid id PK
        varchar id_externo UK
        varchar remitente
        varchar tipo
        text contenido
        timestamptz recibido_en
        varchar estado
        int intentos
        timestamptz procesado_en "NULL"
        uuid usuario_id "NULL"
        text ultimo_error "NULL"
    }
    canal_mensaje_saliente["canal.mensaje_saliente"] {
        uuid id PK
        varchar destinatario
        text contenido
        timestamptz creado_en
        uuid en_respuesta_a FK "NULL"
        varchar estado
        int intentos
        timestamptz proximo_intento
        timestamptz enviado_en "NULL"
        varchar id_externo "NULL"
        varchar estado_externo "NULL"
        text ultimo_error "NULL"
        varchar clave_mensaje "NULL"
        uuid event_id "NULL"
        varchar idioma_plantilla "NULL"
        jsonb parametros_plantilla "NULL"
        varchar plantilla "NULL"
    }
    notif_outbox_mensaje["notif.outbox_mensaje"] {
        uuid id PK
        uuid event_id UK
        varchar tipo_evento
        jsonb payload
        timestamptz ocurrido_en
        timestamptz proximo_intento
        smallint intentos
        timestamptz procesado_en "NULL"
        text ultimo_error "NULL"
    }
    notif_alerta["notif.alerta"] {
        uuid id PK
        bigint secuencia UK
        varchar tipo
        varchar severidad
        varchar titulo
        text detalle
        timestamptz creada_en
        uuid referencia_id "NULL"
        uuid event_id "NULL"
        timestamptz leida_en "NULL"
        uuid leida_por_id "NULL"
    }
    notif_correo_saliente["notif.correo_saliente"] {
        uuid id PK
        varchar destinatario
        varchar asunto
        text cuerpo
        timestamptz creado_en
        uuid alerta_id FK "NULL"
        varchar estado
        int intentos
        timestamptz proximo_intento
        timestamptz enviado_en "NULL"
        text ultimo_error "NULL"
    }
    notif_plantilla_whatsapp["notif.plantilla_whatsapp"] {
        varchar clave PK
        varchar nombre
        varchar idioma
        varchar estado
        uuid modificada_por_id "NULL"
        timestamptz modificada_en
    }
    audit_evento_auditoria["audit.evento_auditoria"] {
        bigint id PK
        uuid actor_id "NULL"
        varchar accion
        varchar recurso
        varchar recurso_id
        timestamptz fecha_hora
        varchar resultado
        text motivo "NULL"
        jsonb valores_antes "NULL"
        jsonb valores_despues "NULL"
        character_64_ hash_previo
        character_64_ hash UK
    }
    canal_mensaje_entrante |o--o{ canal_mensaje_saliente : "en_respuesta_a"
    notif_alerta |o--o{ notif_correo_saliente : "alerta_id"
```

## 6. Catálogo de plataforma (base aparte)

Base compartida de la plataforma, fuera de los Silos y sin datos operacionales: los operadores registrados y la tabla de ruteo del borde.

```mermaid
erDiagram
    plataforma_operador["plataforma.operador"] {
        varchar id PK
        varchar razon_social
        varchar nit UK
        varchar correo_contacto
        varchar estado
        timestamptz creado_en
    }
    plataforma_ruta_operador["plataforma.ruta_operador"] {
        varchar operador_id PK,FK
        varchar subdominio UK
        varchar url_stack
        varchar phone_number_id UK "NULL"
        timestamptz actualizada_en
    }
    plataforma_operador ||--o| plataforma_ruta_operador : "operador_id"
```

