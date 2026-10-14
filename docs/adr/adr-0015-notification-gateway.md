# ADR-0015: Centralizar las notificaciones en un Notification Gateway (WhatsApp principal, correo administrativo, alertas en panel), sin omnicanalidad en V1

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0015 |
| Fecha | 2026-09-22 |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Building blocks | Notification gateway / omnicanalidad |

## Contexto

Muchos módulos necesitan avisar a alguien:

- **Operación**: confirmaciones al pasajero (RF-16), asignación al conductor (RF-17), recordatorios (RF-35), cambios de estado y novedades (RF-43, RF-44).
- **Monitoreo**: alertas operativas al panel (RF-45) y alertas de desviación de ETA (RF-56).
- **Identidad**: correo para recuperar credenciales (RF-23), que lo envía Keycloak (ADR-0003).

Además, WhatsApp impone reglas por mensaje: la ventana de 24 h y las plantillas aprobadas (ADR-0008). Si cada módulo llamara directamente a `IWhatsAppGateway`, esas reglas, junto con la idempotencia y los reintentos, se repetirían en todas partes.

La restricción de negocio *Operativa* fija **WhatsApp como único canal** para pasajero y conductor, y limita el correo a funciones administrativas. Por eso la omnicanalidad completa (SMS, push, Telegram) no es un requisito.

## Decisión

1. Un módulo **Notificaciones** con un contrato de Application `INotificationGateway.Send(NotificationRequest)`:
   - `NotificationRequest` = destinatario (id interno), tipo de notificación, clave de mensaje (ADR-0016), variables y `EventId`.
2. El gateway resuelve **canal y formato** con una política por tipo de destinatario:
   - **Pasajero y Conductor → WhatsApp.** Dentro de la ventana de 24 h envía un mensaje de sesión; fuera de ella, la **plantilla** asociada a la clave del mensaje.
   - **Administrador y Operador logístico → alerta en el Panel Web** (canal de eventos dedicado del servidor, independiente del renderizado del mapa; `OPLOG-DISP-02`). Para alertas críticas se envía además **correo**.
3. Consume los eventos de la **outbox** (ADR-0007):
   - **deduplica por `EventId`**, de modo que un reintento no genera un mensaje duplicado (`COND-CONF-03`);
   - usa backoff ante errores transitorios;
   - si el error es permanente (número inválido, plantilla rechazada), registra la falla y alerta al panel del operador.
4. Los **recordatorios programados** (RF-35) se generan como eventos futuros en la outbox (`ProximoIntento` = hora del recordatorio), sin un scheduler aparte.
5. Envío de correo tras un `IEmailSender` (SMTP o API transaccional genérica), sin fijar proveedor.
6. **Extensibilidad**: agregar un canal (por ejemplo SMS como contingencia contractual) sería implementar un `INotificationChannel` más y ajustar la política, sin tocar los módulos emisores. **No se implementa en V1.**

## Alternativas consideradas

| Criterio | **Gateway propio con política de canal (elegida)** | A. Cada módulo llama al canal directamente | B. Plataforma omnicanal externa (CPaaS) | C. Omnicanalidad propia (WhatsApp + SMS + push + email) |
|---|---|---|---|---|
| Reglas de ventana 24 h y plantillas en un solo lugar | Sí | No | Sí | Sí |
| Idempotencia y reintento centralizados | Sí | No | Parcial | Sí |
| Cumple la restricción de canal único | Sí | Sí | Excede lo necesario | No — contradice la restricción |
| Costo | Bajo | Bajo | Suscripción + margen por mensaje | Alto (desarrollo y proveedores) |
| Añadir un canal en el futuro | Localizado | Disperso | Configuración | Ya incluido |

- **A** — descartada: duplica reglas y reintentos en cada módulo.
- **B** — descartada: añade un intermediario y un costo sin driver (misma lógica que descartar un BSP en ADR-0008).
- **C. Omnicanalidad** (building block propuesto) — **no se adopta en V1** porque contradice la restricción de canal único. El gateway deja preparada la extensión.

## Consecuencias

**Positivas**

- Las reglas de Meta, la idempotencia y los reintentos se implementan una sola vez.
- Los módulos emisores solo expresan intención, no canal.
- Las alertas del panel no dependen de que cargue el mapa.

**Negativas**

- Es un módulo más, y depende del catálogo de mensajes (ADR-0016).
- Las plantillas deben estar aprobadas antes de usarse; una plantilla rechazada bloquea ese tipo de notificación fuera de la ventana.

## Tácticas de Bass aplicadas

- **Reducir acoplamiento / intermediario** — los emisores no conocen el canal.
- **Reintentar** y **encolar** — sobre la outbox.
- **Detectar fallos** — métricas de entregas fallidas y alertas.

## Escenarios y requisitos relacionados

- `PAS-CONF-01`, `PAS-DISP-03`, `COND-REND-01`, `OPLOG-DISP-02`, `COND-CONF-03`.
- `RF-16`, `RF-17`, `RF-35`, `RF-43`, `RF-44`, `RF-45`, `RF-56`.

## ADRs relacionados

- **ADR-0007** — outbox.
- **ADR-0008** — canal WhatsApp.
- **ADR-0011** — métricas.
- **ADR-0016** — catálogo de mensajes y plantillas.
