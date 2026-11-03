# ADR-0003: desacoplar identidad mediante OIDC/OAuth2 y revocación controlada

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0003 |
| Fecha | 2026-09-09 (revisado 2026-10-13, 2026-10-20 y 2026-10-21) |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Reemplaza | ADR-03 histórico de JWT propio HMAC-SHA256. |

## Contexto

FlowRide tiene dos superficies de identidad: el Panel Web usado por Administrador del operador y Operador logístico, y WhatsApp usado por Pasajero y Conductor. La implementación previa generaba JWT propios con HMAC-SHA256 y usaba `empresaId` como claim de aislamiento, lo cual ya no coincide con el modelo Silo.

El requisito vigente es autenticación segura basada en tokens, RBAC y capacidad de revocar acceso en un máximo de 60 segundos. La solución también debe evitar que la lógica de negocio dependa directamente de un proveedor concreto de identidad, de modo que este pueda sustituirse sin modificar el núcleo del dominio.

## Decisión

Adoptar **OpenID Connect (OIDC) / OAuth 2.0** como contrato estándar de identidad y autorización para los usuarios interactivos del Panel Web.

La autenticación será delegada a un proveedor de identidad externo o autoalojado compatible con dichos estándares. FlowRide no administrará directamente la emisión principal de credenciales del Panel Web, sino que recibirá y validará los tokens emitidos por el proveedor seleccionado.

Para evitar el acoplamiento entre el dominio y un proveedor específico, se implementará un `IdentityProviderAdapter` encargado de:

- Validar la información de identidad recibida desde el proveedor.
- Obtener el identificador externo del usuario.
- Mapear dicho identificador hacia el `Usuario` interno de FlowRide.
- Resolver el rol asignado dentro de la plataforma.
- Resolver el tenant correspondiente según el modelo Silo.
- Consultar el estado interno del usuario antes de autorizar acciones.
- Aplicar mecanismos de revocación o invalidación cuando corresponda.

Los tokens externos no se utilizarán como fuente definitiva de autorización sobre los recursos de negocio. El token permitirá identificar al usuario autenticado, pero FlowRide conservará internamente la relación entre usuario, rol, tenant y recursos autorizados.

La autorización se realizará mediante **RBAC** y validaciones contextuales sobre los recursos solicitados. De esta manera, aunque el proveedor entregue claims de identidad, la decisión final sobre qué puede hacer un usuario permanece bajo control de FlowRide.

Para Pasajero y Conductor, el canal WhatsApp utilizará la identidad de la cuenta o número remitente para localizar al usuario interno. Estos usuarios no recibirán ni manipularán directamente tokens OAuth2/OIDC. El backend verificará que la identidad del canal esté asociada con el usuario, servicio o asignación correspondiente antes de ejecutar acciones sensibles.

La revocación de acceso deberá surtir efecto en un máximo de **60 segundos**. Para cumplir este requisito se podrá utilizar una combinación de:

- sesiones o tokens de corta duración;
- validación del estado interno del usuario;
- introspección de tokens cuando el proveedor lo soporte;
- listas de revocación;
- invalidación de sesión;
- mecanismos equivalentes ofrecidos por el proveedor seleccionado.

Adicionalmente, la desactivación de un usuario en FlowRide tendrá prioridad sobre la vigencia del token externo. Un usuario marcado como inactivo no podrá continuar ejecutando operaciones aunque el token emitido por el proveedor todavía no haya expirado.

> **Revisión 2026-10-13 — selección del proveedor.** Se adopta **Keycloak autoalojado, con un realm por operador** (alternativa D). Es coherente con ADR-0010, ADR-0013 y ADR-0014, que ya lo asumían como componente de plataforma. La implementación queda detrás de `IdentityProviderAdapter`, así que cambiar de proveedor no afecta al dominio. Cada realm emite el claim `tenant_id` que exige ADR-0002. En desarrollo local, Keycloak corre en Docker con los realms importados desde archivos versionados que no contienen secretos.

## Alternativas consideradas

### Alternativa A — JWT propio con HMAC-SHA256

Consiste en mantener un mecanismo de autenticación implementado directamente por FlowRide, donde el backend valida las credenciales del usuario y genera tokens JWT firmados mediante una clave simétrica usando HMAC-SHA256.

#### Ventajas

- Implementación relativamente sencilla para un sistema pequeño.
- No depende de servicios externos de identidad.
- Permite controlar directamente la estructura de los claims y el tiempo de expiración de los tokens.
- No genera costos adicionales asociados a proveedores SaaS.
- Puede integrarse fácilmente con la infraestructura actual de ASP.NET Core.

#### Desventajas

- FlowRide debe asumir completamente la administración del ciclo de vida de las credenciales.
- El equipo debe implementar recuperación de contraseña, bloqueo de cuentas, MFA, políticas de contraseña y mecanismos de protección contra ataques.
- La revocación inmediata de tokens requiere desarrollar infraestructura adicional, como listas de revocación o consultas de sesión.
- El uso de una clave simétrica aumenta la criticidad de la protección y rotación del secreto de firma.
- Aumenta la responsabilidad de seguridad del equipo de desarrollo.
- Puede generar mayor acoplamiento entre autenticación, autorización y dominio.

#### Motivo de descarte

Se descarta como solución principal porque obliga a FlowRide a desarrollar y mantener capacidades de gestión de identidad que ya se encuentran disponibles en soluciones especializadas. Además, dificulta el cumplimiento del requisito de revocación en ≤60 segundos sin agregar estado e infraestructura adicional.

---

### Alternativa B — Auth0

Consiste en utilizar **Auth0** como proveedor de identidad SaaS compatible con OpenID Connect y OAuth 2.0, delegando la autenticación y gran parte de la administración de usuarios a la plataforma.

#### Ventajas

- Soporte nativo para OIDC y OAuth 2.0.
- Integración sencilla con aplicaciones web y APIs.
- Incluye administración de usuarios, recuperación de contraseña y políticas de seguridad.
- Permite incorporar MFA sin desarrollarlo directamente en FlowRide.
- Reduce el esfuerzo necesario para implementar funcionalidades de identidad.
- Permite mantener el `IdentityProviderAdapter`, evitando que el dominio dependa directamente de Auth0.

#### Desventajas

- Introduce dependencia de un proveedor SaaS externo.
- Los costos pueden aumentar según cantidad de usuarios, autenticaciones o funcionalidades utilizadas.
- Algunas características avanzadas dependen del plan contratado.
- Una indisponibilidad del proveedor puede afectar el acceso al Panel Web.
- Una migración futura puede requerir trasladar usuarios, configuraciones y políticas de autenticación.

#### Motivo de consideración

Es una alternativa válida cuando se busca reducir el esfuerzo operativo y acelerar la implementación de autenticación segura. Puede utilizarse como proveedor inicial siempre que sus costos y límites sean compatibles con las necesidades de FlowRide.

---

### Alternativa C — Amazon Cognito

Consiste en utilizar **Amazon Cognito** como servicio administrado de identidad, aprovechando sus capacidades de autenticación, emisión de tokens y administración de usuarios dentro del ecosistema AWS.

#### Ventajas

- Soporta OAuth 2.0 y OpenID Connect.
- Permite administrar usuarios y flujos de autenticación.
- Escala automáticamente según la cantidad de usuarios.
- Puede integrarse con otros servicios de AWS.
- Reduce la necesidad de operar infraestructura propia de identidad.
- Permite delegar MFA, recuperación de credenciales y políticas de acceso.

#### Desventajas

- Aumenta la dependencia tecnológica de AWS.
- La configuración puede resultar más compleja que en proveedores orientados exclusivamente a identidad.
- Su principal ventaja se obtiene cuando la solución ya utiliza ampliamente infraestructura AWS.
- La migración a otro proveedor puede requerir ajustes en configuración, usuarios y automatizaciones.
- Algunas capacidades y costos dependen directamente del modelo de servicios de AWS.

#### Motivo de consideración

Es una alternativa adecuada si FlowRide se despliega principalmente sobre AWS. Sin embargo, se evita adoptarla como decisión arquitectónica obligatoria porque la identidad debe permanecer desacoplada del proveedor de infraestructura.

---

### Alternativa D — Keycloak / IdentityServer compatible

Consiste en desplegar y operar un proveedor de identidad compatible con OIDC/OAuth2 bajo control del propio equipo o de la organización, utilizando soluciones como Keycloak o una implementación equivalente basada en estándares.

#### Ventajas

- Mayor control sobre configuración, usuarios, políticas y datos de identidad.
- Reduce la dependencia de proveedores SaaS externos.
- Soporta estándares como OIDC y OAuth 2.0.
- Permite implementar SSO, roles, MFA y administración centralizada de identidad.
- Puede integrarse con el `IdentityProviderAdapter` sin modificar el dominio.
- Permite desplegar la solución dentro de la propia infraestructura.

#### Desventajas

- Requiere administrar infraestructura adicional.
- El equipo debe encargarse de actualizaciones, parches y monitoreo.
- Una mala configuración puede generar vulnerabilidades de seguridad.
- Incrementa la complejidad operativa.
- Requiere definir estrategias de alta disponibilidad, respaldo y recuperación.
- El mantenimiento puede ser más costoso que utilizar un servicio administrado.

#### Motivo de consideración

Es una alternativa válida cuando se requiere mayor control operativo o evitar dependencia de un proveedor SaaS. Sin embargo, implica una carga de administración superior para el equipo.

## Consecuencias positivas

- El dominio no depende de la implementación concreta del proveedor.
- Facilita MFA, recuperación de credenciales y políticas de identidad sin reconstruirlas desde cero.
- Elimina el uso de `empresaId` como mecanismo de aislamiento.
- Permite reemplazar el proveedor manteniendo el contrato basado en OIDC/OAuth2.
- Reduce la responsabilidad directa de FlowRide sobre almacenamiento y administración de credenciales.
- Mantiene la autorización de recursos bajo control interno de FlowRide.
- Facilita incorporar proveedores externos o autoalojados sin modificar la lógica central del sistema.

## Consecuencias negativas

- Existe dependencia operativa del proveedor seleccionado.
- Es necesario mapear correctamente identidad externa, usuario interno, rol y tenant.
- La revocación en ≤60 s puede requerir mantener estado adicional.
- La validación del estado interno del usuario reduce el carácter completamente stateless de la autenticación.
- Una indisponibilidad del proveedor puede impedir nuevos inicios de sesión.
- Se debe controlar cuidadosamente la sincronización entre el estado del usuario externo y el estado interno de FlowRide.

## Tácticas de Bass aplicadas

- **Autenticar actores** — validar la identidad antes de ejecutar acciones.
- **Autorizar actores** — aplicar RBAC y autorización contextual sobre los recursos.
- **Revocar acceso** — garantizar la invalidación del acceso dentro del umbral de 60 segundos.
- **Abstraer plataforma** — utilizar un adapter interno para evitar dependencia directa de un proveedor OIDC/OAuth2.

## Escenarios y requisitos relacionados

- `ADM-SEG-01` — revocación de acceso ≤60 s.
- `COND-SEG-01` — validación de identidad del conductor en WhatsApp.
- `PAS-SEG-01` — limitación de exposición de solicitudes personales.
- `RF-01` — autenticación basada en token.
- `RF-02` — autorización basada en roles.

## ADRs relacionados

- **ADR-0002** — el tenant resuelto determina el Silo, no un claim `empresaId`.
- **ADR-0008** — WhatsApp aporta identidad de canal para Pasajero y Conductor.
- **ADR-0011** — registra eventos de seguridad y auditoría.

#

### Implementación en FlowRide (estado al 22-10-2026)

- Keycloak 26 con un realm por operador, creado e idempotente desde `plantilla-realm.json` (versionada, sin secretos) por la herramienta de aprovisionamiento. La API se conecta con la cuenta de servicio de su propio realm; el administrador de Keycloak de la plataforma solo lo usa la herramienta.
- El dominio solo ve el puerto `IProveedorIdentidad` (implementado por `ProveedorIdentidadKeycloak`): crear la cuenta, enviar el correo para definir la contraseña, exigir doble factor, habilitar, deshabilitar y, desde el 2026-10-21, actualizar nombre y correo (RF-24).
- El rol y el estado del usuario los decide FlowRide: en cada petición la API resuelve el usuario interno por el claim `sub` y lo rechaza si está inactivo. Por eso la revocación surte efecto en la siguiente petición, sin esperar a que expire el token; además se deshabilita la cuenta y se cierran sus sesiones en Keycloak.
- El Panel Web usa el flujo de código con PKCE y guarda los tokens solo en memoria (ADR-0019).

> **Revisión 2026-10-20 — seguridad (requisito del curso sobre OWASP, autenticación rota).** Se agregan a la decisión: (1) **doble factor (TOTP) obligatorio para el Administrador**, como acción requerida al crear la cuenta o al pasar a ese rol; (2) **política de contraseñas** del realm (mínimo 12 caracteres, hash argon2, sin repetir las últimas 5) y **bloqueo por fuerza bruta** tras 5 intentos fallidos; (3) **reautenticación reciente** en las acciones sensibles (gestión de usuarios, configuración del motor, parámetros y plantillas): la API exige que el inicio de sesión sea de los últimos 5 minutos (claim `auth_time`) y, si no, responde `401` con `WWW-Authenticate: Bearer error="insufficient_user_authentication", max_age="300"` (RFC 9470); el Panel Web vuelve a pedir contraseña y TOTP y repite la acción.

> **Revisión 2026-10-21 — cambio de correo (RF-24).** El correo es el usuario de inicio de sesión. Cuando el Administrador lo cambia, la cuenta de Keycloak cambia su correo y su usuario, queda sin verificar y recibe en la nueva dirección el correo para confirmarla (acción `VERIFY_EMAIL`); la auditoría registra qué campos cambiaron, no sus valores.

# Notas de implementación

- El Panel Web debe utilizar Authorization Code Flow con PKCE cuando aplique al tipo de cliente utilizado.
- Las claves, secretos y configuraciones sensibles del proveedor deben almacenarse fuera del repositorio.
- El `IdentityProviderAdapter` debe realizar el mapeo: identificador externo → `Usuario` interno → rol → tenant.
- Los claims externos no deben utilizarse directamente como única fuente de autorización sobre recursos internos.
- Cada solicitud protegida debe validar que el usuario interno permanezca activo.
- Desactivar un usuario en FlowRide debe invalidar su acceso aunque el token externo todavía no haya expirado.
- La selección concreta entre Auth0, Cognito, Keycloak u otro proveedor compatible puede realizarse posteriormente sin cambiar esta decisión arquitectónica.

## Referencias

- Microsoft Learn — OpenID Connect en ASP.NET Core 10.
- `Drivers_Arquitectonicos_FlowRide_FINAL.docx`.
- `RestriccionesTecnicas_FlowRide_ACTUALIZADO.xlsx`.
- `tradeoff_FlowRide_ACTUALIZADO.xlsx` — T-01 y T-09.