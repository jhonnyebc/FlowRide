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

## ADRs relacionados
- **ADR-0003**: OIDC + PKCE con Keycloak.
- **ADR-0012** y **ADR-0013**: servido como estático desde el borde.
- **ADR-0015**: canal de alertas del panel.
- **ADR-0016**: catálogo de mensajes.
- **ADR-0018**: gates del frontend.
