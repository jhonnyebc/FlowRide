# ADR-0012: Exponer FlowRide a través de un borde común con API Gateway, WAF y router de webhooks (CDN opcional)

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0012 |
| Fecha | 2026-09-22 |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Building blocks | API Gateway, WAF, CDN (opcional) |

## Contexto

Con un stack por operador (ADR-0002, ADR-0010) hay N aplicaciones que exponer a Internet, y dos tipos de tráfico:

- **Panel Web**: navegadores de administradores y operadores logísticos, en el subdominio de cada operador.
- **Webhooks de WhatsApp**: llegan desde Meta con una URL de *callback* por app de Meta, así que todos los operadores comparten un único punto de entrada. El evento trae el `phone_number_id` del operador (ADR-0008).

Además:

- la restricción técnica *Seguridad de datos* exige cifrado en tránsito y defensa en profundidad;
- los webhooks son un endpoint público que atacantes pueden sondear (Escenario 3);
- el canal debe sostener el tier de 99,9 % en el ingreso (ADR-0010).

Exponer cada stack directamente obligaría a repetir en N lugares TLS, protección, límites de tasa y validación de webhooks, y no resolvería el enrutamiento de un único *callback* de Meta hacia N operadores.

## Decisión

Un **borde común de plataforma**, con 2 réplicas y **sin estado ni datos operacionales**, que concentra:

1. **API Gateway / reverse proxy**:
   - terminación TLS con certificados gestionados;
   - enrutamiento por subdominio (`{operador}.flowride…`) al stack correspondiente;
   - límites de tasa por IP y por ruta;
   - límites de tamaño de *request*;
   - cabeceras de seguridad.

   Se implementa con un reverse proxy programable en .NET (YARP), para compartir *stack* tecnológico con el backend (restricción técnica *Plataforma*). Un gateway gestionado equivalente de la plataforma elegida es un sustituto válido.
2. **Router de webhooks de WhatsApp**, un módulo del mismo borde:
   - responde el *handshake* de verificación de Meta;
   - valida `X-Hub-Signature-256` con el *app secret*, que obtiene del gestor de secretos (ADR-0014);
   - extrae el `phone_number_id` y reenvía el evento al stack del operador, según una tabla de configuración.

   No persiste mensajes. Si el stack no responde, devuelve error para que Meta reintente.
3. **WAF**:
   - reglas OWASP Core Rule Set frente al Panel Web y a la API;
   - en la ruta de webhooks, solo validación de firma, método y tamaño (el cuerpo lo genera Meta).

   Puede ser un WAF gestionado de la plataforma o un motor compatible con OWASP CRS (por ejemplo Coraza) delante del proxy.
4. **CDN (opcional)**: los recursos estáticos del Panel Web (SPA) se sirven desde el almacenamiento de objetos (ADR-0013). La CDN **no se activa en V1**: los usuarios del panel son pocos y están en Colombia. Se activa si la latencia medida de carga del panel lo justifica, sin cambiar la aplicación.

## Alternativas consideradas

| Criterio | **Borde común con gateway + WAF + router (elegida)** | A. Exponer cada stack directamente | B. Gateway gestionado del proveedor cloud | C. Service mesh | D. Una app de Meta y un webhook por operador |
|---|---|---|---|---|---|
| Resuelve el *callback* único de Meta | Sí | No | Parcial (requiere lógica de firma y ruteo) | No aplica | Sí, pero con N apps de Meta que gestionar |
| TLS/WAF/límites en un solo lugar | Sí | No (N veces) | Sí | Solo tráfico interno | — |
| Costo por tenant | Bajo (prorrateado) | Bajo, pero sin protección homogénea | Variable por solicitudes | Alto | — |
| Encaje con monolito por operador | Sí | Sí | Sí | No — orientado a microservicios | — |
| Datos operacionales en componente compartido | No | — | No | — | — |

- **A** — descartada: repite la seguridad en N lugares y no resuelve el ruteo de webhooks.
- **B** — válida como implementación del mismo rol. No se fija proveedor en este ADR; la firma de Meta y el ruteo por `phone_number_id` siguen siendo lógica propia.
- **C. Service mesh** (building block propuesto) — **no se adopta**: un monolito modular por operador no tiene tráfico entre servicios que gobernar. El mTLS interno se resuelve con TLS entre el borde y los stacks.
- **D** — descartada como base: multiplica la administración en Meta. Si Meta permite configurar un *callback* por número o por cuenta (verificar), el router se simplifica, pero el borde se mantiene por TLS y WAF.

## Consecuencias

**Positivas**

- Un único punto con TLS, WAF, límites de tasa y validación de firma.
- Los stacks del operador no quedan expuestos directamente a Internet.
- El canal alcanza el tier de 99,9 % en el ingreso con 2 réplicas compartidas (ADR-0010).

**Negativas**

- Es un componente compartido y crítico: su caída afecta a todos los operadores. Se mitiga con 2 réplicas, *health checks* y despliegue controlado.
- La tabla de ruteo (subdominio / `phone_number_id` → stack) debe mantenerse sincronizada con el aprovisionamiento (RF-03).
- Añade un salto de red (latencia de pocos ms, dentro de los P95).

## Tácticas de Bass aplicadas

- **Detectar ataques / limitar acceso** — WAF y límites de tasa.
- **Autenticar actores** — firma HMAC de Meta.
- **Limitar exposición** — los stacks no son públicos.
- **Redundancia activa** — 2 réplicas del borde.

## Escenarios y requisitos relacionados

- Escenario 3 (Seguridad) y Escenario 7 (Interoperabilidad) de Drivers.
- `COND-DISP-01`, `PAS-DISP-01` — ingreso del canal.
- `OPLOG-SEG-02`, `ADM-SEG-02` — un subdominio lleva únicamente a su stack.
- `RF-14`, `RF-19`.

## ADRs relacionados

- **ADR-0002** — tabla de ruteo por operador.
- **ADR-0008** — webhook de Meta.
- **ADR-0010** — 2 réplicas y costo prorrateado.
- **ADR-0013** — estáticos del panel.
- **ADR-0014** — *app secret* y certificados.

## Notas de implementación

- La tabla de ruteo se genera en el aprovisionamiento y se recarga sin reiniciar.
- Responder a Meta en ≤500 ms: el router solo valida y reenvía.
- Registrar métricas de webhooks rechazados por firma inválida (posible ataque).

## Referencias

- OWASP Core Rule Set.
- YARP (Yet Another Reverse Proxy) — documentación de Microsoft.
- Meta for Developers — Webhooks de WhatsApp Cloud API.
