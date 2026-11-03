# Diseño detallado: diagramas de secuencia

**FlowRide** — Documento de Arquitectura de Software v3.1 (28-10-2026). La numeración de las secciones es la del DAS completo ([../DAS.md](../DAS.md)).

## 9.4.1 Diagrama de secuencia

El diagrama de secuencia muestra el intercambio de mensajes entre participantes ordenado en el tiempo para un escenario concreto. Le aporta al diseño la validación de responsabilidades y del orden de las interacciones.

El contexto v10 (Capa 4) identifica como flujos críticos la solicitud por WhatsApp, la asignación por reglas, el abandono de turno de un conductor y la detección de desviación de ETA. Se documentan los tres primeros (la asignación forma parte del primero) más la aceptación del conductor, el inicio de sesión con reautenticación y el alta de un operador. La desviación de ETA no se diagrama porque quedó fuera del alcance de V1 (ADR-0005).

### 9.4.1.1 Solicitud por WhatsApp hasta la asignación

Motivación: es el flujo central del negocio (RF-09, RF-10, RF-14, RF-15, RF-17). Muestra cómo la recepción se separa del procesamiento para no perder mensajes (PAS-DISP-01) y cómo la asignación y su aviso quedan en una sola transacción con la Outbox (ADR-0007).

#### 9.4.1.1.1 Diagrama

```mermaid
sequenceDiagram
    autonumber
    actor P as Pasajero
    participant M as WhatsApp Cloud API
    participant B as Borde
    participant W as API — webhook
    participant DB as PostgreSQL del Silo
    participant WC as Proceso del canal
    participant WA as Proceso de la cola de asignación
    participant E as Motor de asignación
    participant WN as Proceso de notificaciones
    actor C as Conductor

    P->>M: Mensaje con la solicitud
    M->>B: POST /api/whatsapp/webhook (firmado)
    B->>B: Valida la firma y busca el stack por phone_number_id
    B->>W: Reenvía el evento
    W->>W: Valida X-Hub-Signature-256
    W->>DB: Guarda el mensaje en la bandeja de entrada (idempotente)
    W-->>M: 200 OK
    WC->>DB: Lee los mensajes entrantes pendientes
    WC->>WC: Bot: avanza la conversación del pasajero
    WC->>DB: Crea la SolicitudViaje (Pendiente) y la respuesta al pasajero
    WC->>M: Envía la confirmación de recepción
    M-->>P: «Recibimos tu solicitud»
    WA->>DB: Lee solicitudes pendientes (urgentes primero)
    WA->>E: Evalúa candidatos con la configuración activa del operador
    E-->>WA: Conductor y vehículo elegidos + decision log
    WA->>DB: Una transacción: Asignacion (Asignada) + decisión + evento asignacion.creada en la Outbox
    WN->>DB: Toma el evento de la Outbox
    WN->>DB: Mensaje saliente para el conductor + recordatorio programado
    WC->>DB: Lee el mensaje saliente pendiente
    WC->>M: Envía el servicio al conductor
    M-->>C: «Tienes un servicio nuevo…»
```

> Imagen del diagrama: [05-secuencia-solicitud-whatsapp-asignacion.png](imagenes/05-secuencia-solicitud-whatsapp-asignacion.png)

#### 9.4.1.1.2 Documentación

- La respuesta HTTP al webhook no espera el procesamiento: el mensaje se guarda primero y se procesa después. Si Meta reintenta, la bandeja de entrada descarta el duplicado.
- Los tres procesos en segundo plano (canal, cola de asignación y notificaciones) corren dentro del mismo stack y se comunican solo a través de la base del Silo; ninguno llama a otro directamente.
- Si dos réplicas intentan asignar la misma solicitud, la concurrencia optimista hace fallar a la segunda y nada se guarda (spike de doble asignación, sección 3).
- Si el motor no encuentra candidatos, la solicitud queda «Sin candidatos» y el evento `solicitud.sin_candidatos` genera una alerta en el Panel Web.

### 9.4.1.2 El conductor acepta y se avisa al pasajero

Motivación: cierra el flujo de la asignación (RF-16, RF-34). El pasajero recibe el nombre del conductor y la placa solo cuando el conductor acepta, no cuando el motor asigna, para no prometer un servicio que el conductor puede rechazar.

#### 9.4.1.2.1 Diagrama

```mermaid
sequenceDiagram
    autonumber
    actor C as Conductor
    participant M as WhatsApp Cloud API
    participant WC as Proceso del canal
    participant DB as PostgreSQL del Silo
    participant WN as Proceso de notificaciones
    actor P as Pasajero

    C->>M: «menu» y luego 1 (aceptar)
    Note over M,WC: Entra por el borde y el webhook como en 9.4.1.1
    WC->>DB: Asignacion pasa a Aceptada y nace el Viaje (Programado)
    WC->>DB: Evento asignacion.aceptada en la Outbox (misma transacción)
    WN->>DB: Toma el evento
    WN->>DB: Mensaje saliente para el pasajero (conductor y placa)
    WC->>DB: Lee el mensaje saliente pendiente
    WC->>M: Envía el mensaje
    M-->>P: «Tu servicio ya tiene conductor: … placa …»
    Note over C,P: Si el conductor no responde a tiempo, la cola de asignación vence la asignación y la solicitud vuelve a buscar conductor
```

> Imagen del diagrama: [06-secuencia-conductor-acepta.png](imagenes/06-secuencia-conductor-acepta.png)

#### 9.4.1.2.2 Documentación

- El Operador logístico puede aceptar o rechazar en nombre del conductor desde el Panel Web (`POST /api/asignaciones/{id}/aceptar` o `/rechazar`); el resto del flujo es el mismo.
- Un rechazo cierra la asignación y la solicitud vuelve a la cola; el motor no vuelve a proponer esa solicitud al conductor que la rechazó.

### 9.4.1.3 Abandono de turno de un conductor

Motivación: es la contingencia principal del caso ancla (RF-12, OPLOG-REND-01). Muestra que un abandono redistribuye los servicios sin interrumpir los viajes que ya están en curso.

#### 9.4.1.3.1 Diagrama

```mermaid
sequenceDiagram
    autonumber
    actor O as Operador logístico
    participant UI as Panel Web
    participant A as API
    participant DB as PostgreSQL del Silo
    participant WN as Proceso de notificaciones
    participant WA as Proceso de la cola de asignación

    O->>UI: Registra el abandono con un motivo
    UI->>A: POST /api/conductores/{id}/turnos/{turnoId}/abandonar (a través del borde)
    A->>DB: Una transacción: turno Abandonado, asignaciones no iniciadas<br/>y viajes programados cancelados, solicitudes de vuelta a Pendiente,<br/>auditoría y evento turno.abandonado en la Outbox
    A-->>UI: Solicitudes devueltas a la cola
    WN->>DB: Toma el evento y crea una alerta crítica
    WN-->>UI: Alerta en vivo (SSE)
    WA->>DB: Lee las solicitudes pendientes y las reasigna con el motor
```

> Imagen del diagrama: [07-secuencia-abandono-de-turno.png](imagenes/07-secuencia-abandono-de-turno.png)

#### 9.4.1.3.2 Documentación

- Un viaje ya iniciado no se interrumpe: solo se cancelan las asignaciones que aún no comenzaron.
- La reasignación la hace el mismo motor con las mismas reglas; si no hay candidatos, el Operador logístico puede asignar manualmente (RF-39).

### 9.4.1.4 Inicio de sesión en el Panel Web y reautenticación

Motivación: muestra cómo se cumplen ADM-SEG-01 (revocación ≤ 60 s) y la protección de las acciones sensibles. El rol y el estado del usuario los decide FlowRide en cada petición, no el token.

#### 9.4.1.4.1 Diagrama

```mermaid
sequenceDiagram
    autonumber
    actor U as Administrador
    participant UI as Panel Web
    participant K as Keycloak (realm del operador)
    participant B as Borde
    participant A as API
    participant DB as PostgreSQL del Silo

    U->>UI: Abre el panel
    UI->>K: Autorización con código + PKCE
    K->>U: Pide contraseña y código TOTP
    U->>K: Credenciales
    K-->>UI: Código de autorización
    UI->>K: Canjea el código por tokens
    K-->>UI: Token de acceso (solo en memoria)
    UI->>B: GET /api/usuarios/yo con el token
    B->>B: Valida que el token sea del realm de este subdominio
    B->>A: Reenvía
    A->>DB: Resuelve el usuario interno (activo) y su rol
    A-->>UI: Usuario y rol
    Note over UI,A: Desde aquí cada petición pasa por el borde igual que en los pasos 8 a 10
    U->>UI: Acción sensible (por ejemplo, cambiar un rol)
    UI->>A: PUT /api/usuarios/{id}/rol
    A-->>UI: 401 insufficient_user_authentication, max_age=300
    UI->>K: Nueva autorización pidiendo iniciar sesión otra vez
    K->>U: Pide contraseña y código TOTP
    U->>K: Credenciales
    K-->>UI: Token con auth_time reciente
    UI->>A: Repite la acción
    A->>DB: Cambio + auditoría en una transacción
    A-->>UI: 200 OK
```

> Imagen del diagrama: [08-secuencia-inicio-sesion-reautenticacion.png](imagenes/08-secuencia-inicio-sesion-reautenticacion.png)

#### 9.4.1.4.2 Documentación

- Desactivar a un usuario deshabilita su cuenta en Keycloak, cierra sus sesiones y, sobre todo, hace que la API lo rechace en la siguiente petición aunque su token siga vigente (el usuario interno ya no está activo).
- Las acciones sensibles (gestión de usuarios, configuración del motor, parámetros) exigen que el inicio de sesión sea de los últimos 5 minutos (RFC 9470); el Administrador siempre usa doble factor.

### 9.4.1.5 Alta de un operador (aprovisionamiento del Silo)

Motivación: RF-03 y el riesgo principal del modelo Silo (OPLOG-SEG-02, ADM-SEG-02): que un operador nuevo quede aislado de los demás. El procedimiento es idempotente para poder repetirse si falla a mitad.

#### 9.4.1.5.1 Diagrama

```mermaid
sequenceDiagram
    autonumber
    actor T as Administrador técnico
    participant CLI as FlowRide.Aprovisionamiento
    participant CAT as Catálogo de plataforma
    participant S as Gestor de secretos del operador
    participant PG as Servidor PostgreSQL
    participant K as Keycloak
    participant OB as Almacenamiento de objetos

    T->>CLI: crear-operador --id ... --admin-...
    CLI->>CAT: Valida el NIT y registra el operador
    CLI->>S: Obtiene o genera la contraseña del Silo
    CLI->>PG: Crea la base y el rol propios del operador
    CLI->>S: Guarda la cadena de conexión
    CLI->>PG: Aplica las migraciones del esquema ops
    CLI->>K: Crea o actualiza el realm desde la plantilla versionada
    CLI->>S: Obtiene o genera la clave de cifrado del operador
    CLI->>OB: Crea el contenedor privado del operador
    CLI->>CAT: Marca el operador como activo
    CLI->>K: Cuenta del primer Administrador (con TOTP obligatorio)
    K-->>T: Correo para definir la contraseña
    T->>CLI: registrar-ruta --id ... --url ...
    CLI->>CAT: Ruta del subdominio para el borde
```

> Imagen del diagrama: [09-secuencia-alta-de-operador.png](imagenes/09-secuencia-alta-de-operador.png)

#### 9.4.1.5.2 Documentación

- Cada paso reutiliza lo que ya existe (secretos, base, realm, contenedor), así que repetir el comando después de un fallo continúa donde quedó sin duplicar recursos.
- La herramienta es la única con permisos para crear bases y realms; la API del operador solo usa la cuenta de servicio de su propio realm.
