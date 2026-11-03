# ADR-0019: Construir el Panel Web como SPA en React + TypeScript

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0019 |
| Fecha | 2026-10-13 |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Building block | Frontend del Panel Web |

## Contexto

El Administrador del operador y el Operador logístico trabajan desde el **Panel Web**. Los ADR existentes ya fijan varias de sus condiciones:
- consume la API por HTTP/JSON (ADR-0001);
- se autentica con OIDC Authorization Code + PKCE (ADR-0003);
- sus estáticos se sirven desde el almacenamiento de objetos a través del borde (ADR-0012, ADR-0013);
- usa claves del catálogo de mensajes (ADR-0016);
- recibe alertas por un canal de eventos del servidor que no depende del mapa (ADR-0015).

Ningún ADR fija la tecnología del frontend. Hay escenarios de UX y accesibilidad que dependen de él: OPLOG-ACC-01 (operación solo con teclado), OPLOG-ACC-02 (alertas distinguibles con daltonismo), OPLOG-UX-02, ADM-UX-02 y ADM-ACC-01.

## Decisión

- **SPA en React + TypeScript**, compilada con Vite, como artefacto estático (sin servidor Node en producción).
- **Autenticación:** OIDC Authorization Code + PKCE contra el realm de Keycloak del operador (ADR-0003). El realm se determina por el subdominio del operador (ADR-0012).
- **API:** cliente tipado generado desde el contrato OpenAPI del backend, para que el frontend no se desincronice de la API.
- **Alertas en tiempo real:** Server-Sent Events desde la API del stack del operador (ADR-0015).
- **Textos:** i18n con las claves del catálogo de mensajes, `es-CO` (ADR-0016).
- **Accesibilidad:**
  - WCAG 2.1 AA;
  - navegación completa por teclado (OPLOG-ACC-01);
  - alertas con color, icono y forma (OPLOG-ACC-02).
- **Ubicación:** carpeta `web/` en el mismo repositorio, con sus propios gates en CI (ADR-0018).
- **Despliegue:** se publica en el bucket de plataforma y lo sirve el borde (ADR-0012, ADR-0013). En desarrollo local corre con el servidor de Vite.

## Alternativas consideradas

| Criterio | **React + TypeScript (elegida)** | A. Blazor WebAssembly | B. Angular | C. Razor Pages / MVC en el backend |
|---|---|---|---|---|
| Un solo stack con el backend (ADR-0017) | No | Sí | No | Sí |
| Artefacto estático servible desde el bucket (ADR-0013) | Sí | Sí (descarga inicial pesada) | Sí | No — requiere servidor |
| Ecosistema de accesibilidad y componentes | Muy amplio | Medio | Amplio | Medio |
| Librerías OIDC + PKCE maduras | Sí | Sí | Sí | Sí |
| Separación API / UI (ADR-0001) | Alta | Alta | Alta | Baja |

- **A. Blazor WebAssembly** — viable por compartir el stack .NET, pero descartada por el tamaño de la descarga inicial y por tener un ecosistema de componentes accesibles menor.
- **B. Angular** — viable, pero con más ceremonia para un panel de este tamaño.
- **C. Razor** — descartada: acopla la UI al stack del operador y no se puede servir desde el bucket.

## Consecuencias

**Positivas**
- El Panel Web se despliega y versiona como un artefacto estático independiente del backend.
- Los contratos están tipados de punta a punta gracias a la generación desde OpenAPI.

**Negativas**
- Introduce un segundo lenguaje y una segunda cadena de herramientas (Node/npm) en el repositorio y en CI, a diferencia de lo que buscaba ADR-0017 para el backend.
- Las dependencias npm requieren auditoría de seguridad en CI.

## Tácticas de Bass aplicadas

- **Soportar la iniciativa del usuario:** acciones directas sobre cada servicio (asignar ya, devolver a la cola, aceptar o rechazar en nombre del conductor) y atajos de navegación con teclado (OPLOG-ACC-01).
- **Soportar la iniciativa del sistema / mantener el modelo del sistema:** semáforo de servicios con color, icono y texto y alertas en vivo (OPLOG-UX-02, OPLOG-ACC-02).
- **Limitar la exposición / autenticar actores:** tokens solo en memoria (nunca en cookies ni en almacenamiento del navegador) y reautenticación para las acciones sensibles.
- **Reducir el acoplamiento:** cliente tipado generado desde el contrato OpenAPI; la interfaz no conoce detalles del backend más allá del contrato.

## Escenarios y requisitos relacionados

- RF-36 (vista centralizada), RF-37 (monitoreo), RF-45 (alertas), RF-30, RF-31, RF-39, RF-40 y la administración de RF-04 a RF-08, RF-24 a RF-28.
- OPLOG-UX-02, OPLOG-ACC-01, OPLOG-ACC-02, ADM-UX-02, ADM-ACC-01, ADM-REND-01.

## ADRs relacionados
- **ADR-0003**: OIDC + PKCE con Keycloak.
- **ADR-0012** y **ADR-0013**: servido como estático desde el borde.
- **ADR-0015**: canal de alertas del panel.
- **ADR-0016**: catálogo de mensajes.
- **ADR-0018**: gates del frontend.

## Notas de implementación

### Implementación en FlowRide (estado al 22-10-2026)

- Aplicación en `web/` con Vite 7, React 19, TypeScript 5.9 y Mantine 8; TanStack Query para las consultas y React Router para la navegación. Versiones exactas y con al menos dos semanas de publicadas.
- Inicio de sesión OIDC con código + PKCE (`oidc-client-ts`, `react-oidc-context`) contra el realm del operador; los tokens se guardan solo en memoria. Ante el desafío de RFC 9470 la aplicación pide iniciar sesión de nuevo y repite la acción (ADR-0003).
- Cliente tipado con `openapi-fetch` y tipos generados con `openapi-typescript` desde el contrato de la API; una prueba de integración del backend impide que el contrato publicado y los tipos del frontend se desincronicen.
- Pantallas de operación (tablero con indicadores y gráficas, vista del día, solicitudes, viajes y alertas) y de administración (usuarios, empresas cliente y pasajeros; conductores, vehículos e itinerarios en la Fase 11b-2). Estilo visual en blanco y negro, plano; los colores se reservan para lo que significa algo (semáforo, severidad, estados).
- Pruebas con Vitest y Testing Library; cada pantalla se verifica además con axe (WCAG 2.1 AA). El job «Frontend» del CI corre tipos, lint, pruebas y build.
- En local corre con el servidor de Vite, que reenvía `/api` a la API del operador (un solo origen, sin CORS); en producción lo sirve el borde común.

## Referencias

- IETF. *RFC 7636 — Proof Key for Code Exchange (PKCE)* y *OAuth 2.0 for Browser-Based Applications* (borrador del OAuth WG).
- IETF. *RFC 9470 — OAuth 2.0 Step Up Authentication Challenge Protocol*.
- W3C. *Web Content Accessibility Guidelines (WCAG) 2.1*.
- Deque. *axe-core*. github.com/dequelabs/axe-core.
- Meta. *React documentation*; Mantine. *Mantine documentation*.
