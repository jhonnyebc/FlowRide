# ADR-0014: Gestionar secretos y claves en un gestor de secretos con un ámbito por operador

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0014 |
| Fecha | 2026-09-22 (revisado 2026-09-23) |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Building block | Key Vault / gestor de secretos |

## Contexto

Cada stack de operador necesita credenciales propias (ADR-0002):

- usuario y contraseña de su base;
- credenciales de su bucket (ADR-0013);
- *access token* de su número de WhatsApp (ADR-0008);
- claves de cifrado de datos sensibles.

La plataforma compartida necesita las suyas:

- *app secret* y *verify token* de Meta para el router (ADR-0012);
- secretos de administración de Keycloak (ADR-0003);
- certificados TLS.

Si una credencial de un operador se filtrara o se compartiera, el aislamiento Silo quedaría anulado. La restricción técnica *Seguridad de datos* exige protección según el riesgo. Además, los secretos no pueden vivir en el repositorio ni en la imagen (ADR-0010: imagen inmutable, configurada desde fuera).

## Decisión

1. Usar **Azure Key Vault**, con:
   - **un Key Vault por operador**, al que solo accede la identidad de carga de trabajo (*managed identity*) de su stack — ninguna otra identidad tiene permiso de lectura;
   - **un Key Vault de plataforma** para el borde, Keycloak y el pipeline, con acceso restringido a esos componentes compartidos.
2. Los stacks leen sus secretos **al arrancar**, autenticándose con identidad de carga de trabajo (sin credenciales estáticas para acceder al gestor). Refrescan en caliente los que rotan (por ejemplo, el token de Meta).
3. **Rotación**:
   - contraseñas de BD cada 90 días o ante un incidente;
   - token de Meta según su vigencia;
   - claves de cifrado versionadas (se descifra con la versión registrada, se cifra con la vigente).
4. **Cifrado en reposo** (decisión única para todo FlowRide; los demás ADR solo la referencian):
   - Azure Database for PostgreSQL Flexible Server (ADR-0010) y Azure Blob Storage (ADR-0013) cifran en reposo por defecto con clave administrada por Microsoft. Es suficiente para la línea base y no añade costo;
   - encima de eso, los campos más sensibles del dominio (documento de identidad, teléfono) se cifran **a nivel de aplicación** con una clave propia del operador guardada en su Key Vault. Así, ni Azure ni un backup filtrado exponen esos campos;
   - una clave administrada por el cliente (*customer-managed key*, respaldada en el Key Vault del operador) para toda la base queda como opción contractual, junto con la instancia dedicada de ADR-0002.
5. **Pipeline**: las credenciales que necesita GitHub Actions para desplegar (por ejemplo, el token de acceso a Azure) se guardan en los secretos cifrados del repositorio, no en Key Vault. En ejecución, cada stack lee sus propios secretos directamente de su Key Vault con *managed identity*, sin pasar por archivos montados. **Nunca** en `appsettings.json`, en `.env` versionados ni en variables impresas en logs.
6. Detección preventiva: escaneo de secretos en CI (junto al análisis estático de la restricción *Control de versiones y calidad*).

## Alternativas consideradas

| Criterio | **Gestor de secretos con ámbito por operador (elegida)** | A. Variables de entorno en archivos de configuración | B. Secretos en la base de datos | C. Un único ámbito para todos los operadores |
|---|---|---|---|---|
| Fuera del repositorio y de la imagen | Sí | Riesgo alto de filtrarse | Sí | Sí |
| Aislamiento entre operadores | Sí | Depende de la disciplina | No (¿quién guarda la credencial de la BD?) | No |
| Rotación y auditoría de acceso | Sí | No | Parcial | Sí |
| Costo | Bajo (~0,15–0,65 USD/mes por operador en Azure Key Vault) | Nulo | Nulo | Bajo |

## Consecuencias

**Positivas**

- Una credencial del ámbito de operador comprometida afecta solo a ese operador. No ocurre lo mismo con el ámbito de plataforma (`app secret` de Meta, administración de Keycloak, certificados TLS): comprometer uno de esos afecta a todos, así que su rotación y su auditoría de acceso son más estrictas.
- Rotación sin redeplegar la imagen.
- Auditoría de quién leyó qué secreto.

**Negativas**

- El gestor es una dependencia de arranque: si no está disponible, un stack no puede iniciar. Los stacks ya en ejecución siguen funcionando con los secretos cargados.
- El aprovisionamiento (RF-03) añade la creación del ámbito y los secretos del operador.

## Tácticas de Bass aplicadas

- **Limitar acceso** — cada stack solo alcanza su ámbito.
- **Separar entidades** — un ámbito de secretos por operador.
- **Cifrar datos** — en reposo y a nivel de campo.
- **Revocar el acceso** — rotación de credenciales y claves versionadas.

## Escenarios y requisitos relacionados

- `OPLOG-SEG-02`, `ADM-SEG-02`, `COND-SEG-01`; Escenario 3 de Drivers; `RF-03`, `RF-19`.

## Notas de implementación

- Ningún secreto se escribe en `appsettings.json`, en archivos `.env` versionados ni en variables que terminen en logs (ADR-0011).
- El arranque del stack falla de forma explícita si falta un secreto: nunca se sigue con un valor por defecto.
- Las claves de cifrado de campo se versionan: se descifra con la versión registrada en la fila y se cifra siempre con la vigente.
- La rotación queda registrada en auditoría con quién la ejecutó y cuándo, sin registrar el valor.

## ADRs relacionados

- **ADR-0002** — credenciales y cadena de conexión por operador.
- **ADR-0003** — secretos del cliente OIDC y de administración de Keycloak.
- **ADR-0008** — `app secret`, *verify token* y *access token* de Meta.
- **ADR-0010** — los secretos se inyectan al arrancar la imagen inmutable.
- **ADR-0012** — secretos del borde y certificados TLS.
- **ADR-0013** — cifrado y credenciales del bucket.
