# ADR-0013: Usar almacenamiento de objetos por operador para backups, archivo histórico, exportes y estáticos

## Metadatos

| Campo | Valor |
|---|---|
| ID | ADR-0013 |
| Fecha | 2026-09-22 (revisado 2026-09-23) |
| Estado | **Accepted** |
| Participantes | Sebastián Zapata Naranjo, Andrés David Ortiz Mejía, Jhonny Estiven Benítez Caro |
| Curso | ISS0666 — Ingeniería del Software 2 (UCO, semestre 2-2026) |
| Profesor evaluador | Wider Farid Sánchez Garzón |
| Building block | Storage account / almacenamiento de objetos |

## Contexto

Varias necesidades no encajan en la base transaccional:

- **Backups** de la base de cada operador, con restauración independiente (ADR-0002, ADR-0010; RPO ≤15 min, RTO ≤2 h).
- **Archivo** de históricos operacionales y de auditoría a partir de los 24 meses, conservando la auditoría hasta 5 años (restricción *Datos*; ADR-0011).
- **Exportes** de reportes que el operador descarga (RF-46…RF-49).
- **Documentos del conductor** (soporte de licencia y habilitaciones, RF-26), si se cargan.
- **Recursos estáticos** del Panel Web (ADR-0012).

Guardar archivos en el disco del contenedor los perdería al reemplazar la instancia (ADR-0010: stacks sin estado). Guardarlos en PostgreSQL haría crecer backups y costos sin necesidad.

## Decisión

1. Usar **Azure Blob Storage** (tier Hot para lo reciente; Cool/Archive para el archivo histórico), accedido desde Infrastructure a través de `IObjectStorage` con el SDK `Azure.Storage.Blobs`. No es compatible con la API de S3 — se descarta esa abstracción porque el proveedor ya quedó fijado en Azure (ADR-0010) y mantenerla habría sido una capa de portabilidad que nadie va a usar.
2. **Un bucket por operador**, con credenciales de acceso exclusivas del stack del operador. En Azure el recurso equivalente es un *blob container*; este documento sigue llamándolo "bucket" para no confundirlo con los contenedores Docker del resto de los ADR. El cifrado en reposo y la gestión de claves se deciden en ADR-0014; aquí solo se exige que esté activo. Es coherente con el Silo: ningún stack puede leer el bucket de otro.
   Además, un bucket de plataforma con el prefijo `platform/backups/` guarda los backups de los componentes compartidos (Keycloak y su base, ADR-0003), sin datos operacionales de los operadores.
3. **Prefijos y políticas de ciclo de vida**:

   | Prefijo | Contenido | Política |
   |---|---|---|
   | `backups/` | Dumps diarios + WAL/PITR (Azure Database for PostgreSQL Flexible Server, ADR-0010) | 35 días en caliente; bloqueo de escritura (WORM) vía política de inmutabilidad de Azure Blob Storage |
   | `archive/ops/` | Históricos operacionales > 24 meses | Nivel frío/archivo |
   | `archive/audit/` | Auditoría > 24 meses con su cadena de hashes | Nivel archivo + retención inmutable hasta 5 años, luego borrado |
   | `exports/` | Reportes generados | Enlaces firmados de corta duración; borrado a los 7 días |
   | `documents/` | Soportes del conductor | Acceso por enlace firmado; se eliminan con el conductor (supresión). **Sin retención inmutable**, justamente para poder borrarlos |

4. Los estáticos del Panel Web se publican en un bucket de plataforma (sin datos de operadores), servido por el borde (ADR-0012).
5. Las descargas usan **URLs firmadas** con expiración de ≤15 min. Los buckets nunca son públicos.

## Alternativas consideradas

| Criterio | **Objetos, un bucket por operador (elegida)** | A. Disco del contenedor/VM | B. Archivos en PostgreSQL (`bytea`) | C. Bucket compartido con prefijo por operador |
|---|---|---|---|---|
| Sobrevive al reemplazo de instancia | Sí | No | Sí | Sí |
| Costo por GB | Bajo, con niveles fríos | Bajo | Alto (crecen la BD y los backups) | Bajo |
| Aislamiento Silo | Sí (credencial por bucket) | Sí | Sí | No — una credencial ve a todos |
| Ciclo de vida / retención inmutable | Sí | No | No | Sí |
| Restauración por operador | Sí | Manual | Sí | Sí |

## Consecuencias

**Positivas**

- Backups y archivo fuera de la instancia, con retención automatizada y bajo costo.
- Cumple las retenciones de 24 meses y 5 años sin inflar la base transaccional.
- El aislamiento por operador se mantiene también en archivos.

**Negativas**

- Hay un bucket y unas credenciales más que aprovisionar por operador.
- Restaurar desde el nivel de archivo puede tardar horas; aplica solo al archivo histórico, no a los backups recientes.
- La supresión de datos personales abarca `documents/` y `exports/`, que por eso no llevan retención inmutable. Los backups conservan copias hasta que expiran (35 días) y el archivo de auditoría no guarda datos personales en claro, porque ADR-0011 los sustituye por hashes antes de escribirlos. Ese desfase es el procedimiento acordado de supresión, no un hueco.

## Tácticas de Bass aplicadas

- **Recuperación: rollback / resincronización de estado** — restauración desde backup.
- **Separar entidades** — un bucket por operador.
- **Limitar exposición** — URLs firmadas y buckets privados.

## Escenarios y requisitos relacionados

- `ADM-DISP-01` — recuperación.
- `OPLOG-AUD-01`, `PAS-AUD-02` — conservación de evidencia.
- `RF-26`, `RF-46`…`RF-49`.

## ADRs relacionados

- **ADR-0002** — un bucket por operador, coherente con el Silo.
- **ADR-0003** — backup de Keycloak en `platform/backups/`.
- **ADR-0004** — backups de la base de cada operador.
- **ADR-0010** — RPO/RTO y costo de la partida de almacenamiento.
- **ADR-0011** — archivo de auditoría y alcance de la supresión.
- **ADR-0012** — estáticos del Panel Web servidos desde el borde.
- **ADR-0014** — cifrado en reposo y credenciales del bucket.

## Notas de implementación

- Probar la restauración de un backup por operador al menos una vez por mes (un backup no probado no cuenta).
- El job de archivo corre mensualmente con un *lock* consultivo y registra en auditoría lo archivado.

## Referencias

- `RestriccionesNegocio_FlowRide_ACTUALIZADO.xlsx` — Datos (retenciones).
