# Verificación documental y dictamen — Diseño físico v1.0

**Estado de la fase: APROBADO CON CONDICIONES DE IMPLEMENTACIÓN.**
Fecha de evaluación documental: 2026-10-07.
Autoridad técnica: Arquitecto de Datos Senior (PostgreSQL 18, Integridad, Concurrencia y Privacidad).

---

## 1. Resumen ejecutivo y declaración de honestidad metodológica

Este documento contiene la verificación documental exhaustiva, el balance de consistencia transversal y el dictamen técnico de la fase de diseño físico v1.0 del MVP de Yusay.

> [!IMPORTANT]
> **Distinción metodológica estricta:**
> - **Diseño especificado:** Decisiones, mecanismos, expresiones y arquitecturas completamente definidas a nivel documental en la especificación (`00` a `05`).
> - **Diseño aprobado:** Resoluciones institucionales formalizadas por el responsable del proyecto (ADR-001, OQ-PHYS-001 a 010, C-PHYS-001).
> - **Mecanismo implementado y probado:** Ejecución real de código, DDL, migraciones, benchmarks o tests automatizados sobre una base de datos activa.
> 
> En estricto cumplimiento de las restricciones del encargo, **se declara que no se han ejecutado pruebas sobre un motor PostgreSQL en producción ni se han generado scripts SQL/migraciones ejecutables**. Toda verificación aquí reportada es de carácter **estrictamente documental, lógico, relacional y arquitectónico**.

---

## 2. Verificación de consistencia del modelo normativo

Se ha contrastado de forma exhaustiva el diseño físico con la línea base lógica normativamente congelada (`logical-model-v1`):

| Métrica del Modelo | Línea Base Lógica Congelada | Diseño Físico Verificado | Resultado | Observaciones |
| --- | --- | --- | --- | --- |
| **Módulos funcionales** | 6 | 6 | **100% Coincidente** | Identidad, Evaluaciones, Seguimiento, Compatibilidad, Contenido, Auditoría. Esquema físico unificado `yusay`. |
| **Relaciones / Tablas** | 32 | 32 | **100% Coincidente** | Escenario B aprobado: `USER` → `yusay.app_user`; las otras 31 tablas conservan nombre lógico singular en snake_case. |
| **Atributos / Columnas** | 147 | 147 | **100% Coincidente** | Exactitud verificada de nombres, nulabilidad y orden por tabla. Cero omisiones, cero adiciones no autorizadas. |
| **Claves Primarias (PK)** | 32 | 32 | **100% Coincidente** | 16 raíces propias (UUIDv4), 5 compartidas 1:1 (heredadas sin regenerar), 11 compuestas (conservando orden formal). |
| **Claves Alternativas (AK)** | 9 | 9 | **100% Coincidente** | Unicidad de emails (`uq_app_user_email` funcional `lower(email)`), códigos de catálogos y versiones/posiciones. |
| **Unicidades Referenciales (URA)** | 6 | 6 | **100% Coincidente** | 6 URA compuestas íntegras (`ref_*`), incluyendo las de 61 bytes sin truncamiento ni abreviaturas. |
| **Claves Foráneas (FK)** | 41 (28 simples, 13 compuestas) | 41 (28 simples, 13 compuestas) | **100% Coincidente** | 41 acciones referenciales evaluadas y justificadas individualmente (`RESTRICT`, `CASCADE`, `SET NULL`). |

---

## 3. Matriz de estado de los mecanismos físicos (MP-PHYS-001 a MP-PHYS-016)

A continuación se detalla el estado riguroso y justificado de cada uno de los 16 mecanismos físicos tras la revisión correctiva:

| ID | Mecanismo Físico | Estado Documental | Justificación Técnica del Estado |
| --- | --- | --- | --- |
| **MP-PHYS-001** | Capacidad numérica de enteros y `target_identifier` | **Resuelto documentalmente** | Se descartaron supuestos arbitrarios de 100/1.000 preguntas. Se formuló el cálculo formal de cotas extremas $[Score_{min}, Score_{max}]$ por instrumento a partir de mínimos y máximos de opciones. Se documentó que `SUM(integer)` retorna `bigint` en PostgreSQL pero un acumulador `bigint` también puede desbordarse si la suma excede sus límites, y además devolver `bigint` no garantiza que el resultado quepa en `integer`. Por ello, se especificó la validación segura de cotas obligatoria antes de publicar instrumentos (`InstrumentVersion` DRAFT $\to$ READY/PUBLISHED), rechazando cualquier versión cuyas cotas o interpretaciones desborden el rango de `integer` ($\pm 2.147.483.647$). `target_identifier` resuelto como `text` con serialización canónica UUIDv4. |
| **MP-PHYS-002** | Expresión, colación ASCII y unicidad de email | **Resuelto documentalmente** | Almacenamiento `text`, parte local ASCII simplificada `[A-Za-z0-9._+-]` preservando casing. Prohibición estricta de `%`, espacios, controles, puntos extremos o consecutivos, y Unicode en parte local. Dominio canonicalizado en backend mediante IDNA/Punycode minúsculas, sin exigir terminación exclusivamente alfabética (soporte TLD `xn--...`). Índice funcional de unicidad `lower(email) COLLATE "C"`. `C-PHYS-001` ratificado como inmutable en el MVP. |
| **MP-PHYS-003** | Guardas de auditoría `jsonb` y 7 perfiles N/F/D/C/E/T/P | **Resuelto documentalmente** | Especificada discriminación estricta entre `NULL` SQL y `jsonb null` (prohibido). Definidas guardas relacionales CHECK por perfil, catálogo de `changed_fields`, serialización canónica de IDs y validación cruzada actor-acción-destino. |
| **MP-PHYS-004** | Máximo una versión publicada/activa (Instrument / Dimension) | **Resuelto documentalmente** | Especificados índices únicos parciales `uxp_instrument_version_single_published` y `uxp_dimension_version_single_active`, complementados con protocolo de bloqueo pesimista `SELECT ... FOR UPDATE` en el catálogo padre. |
| **MP-PHYS-005** | Máximo un intento `IN_PROGRESS` por usuario e instrumento | **Resuelto documentalmente** | Especificado índice único parcial declarativo `uxp_assessment_attempt_single_in_progress` sobre `(user_id, instrument_id) WHERE status = 'IN_PROGRESS'`. Guarda temporal en transacción. |
| **MP-PHYS-006** | Envío atómico y coherencia SUBMITTED / Result | **Resuelto documentalmente** | Demostrada la equivalencia bidireccional (*intento SUBMITTED $\iff$ exactamente un resultado válido*). Protocolo atómico: `SELECT ... FOR UPDATE`, verificación al 100% de respuestas obligatorias, cálculo seguro, enlace a interpretación oficial existente, inserción de Result y actualización a `SUBMITTED`. Consistencia garantizada en rollback y reforzada mediante restricción/trigger diferible a COMMIT. Supresión en cascada coordinada ante derecho al olvido. |
| **MP-PHYS-007** | Edición optimista de CheckIn (`revision` y ventana de 168h) | **Resuelto documentalmente** | Especificada sentencia `UPDATE ... SET revision = revision + 1 WHERE revision = $rev AND clock_timestamp() < created_at + 168h`. Preservación inmutable del conjunto de dimensiones/mediciones y escalas. |
| **MP-PHYS-008** | Expiración a 720h, precisión de reloj y limpieza de lotes | **Resuelto documentalmente** | Vigencia evaluada dinámicamente mediante `clock_timestamp() < expires_at` independiente del worker. Transición a `EXPIRED` con purga atómica de respuestas. Lote de purga a los 30 días de `ended_at`. |
| **MP-PHYS-009** | Tokens criptográficos, hashes y consumo único | **Resuelto documentalmente** | Secreto CSPRNG 256 bits en backend (no almacenado); persistencia exclusiva de `token_hash` SHA-256 (64 hex). Consumo atómico mediante bloqueo transaccional pesimista con espera `SELECT ... FOR UPDATE` (se eliminó `SKIP LOCKED` para evitar falsos negativos bajo concurrencia), reevaluación estricta de existencia y vigencia, borrado total de tokens del usuario y revocación de credenciales. |
| **MP-PHYS-010** | Aislamiento de fallos de auditoría en supresión (DP-TRANS-001) | **Resuelto documentalmente** | Supresión atómica de `app_user` (cascada sobre datos personales) y desvinculación de auditoría (`SET NULL`). Aislamiento de fallos al insertar `USER_DELETED` mediante savepoint/backend sin bloquear la supresión. |
| **MP-PHYS-011** | Registro duradero de supresiones independiente de backups | **Resuelto documentalmente** | Diseñado log de supresión externo con `user_id` y `deleted_at` sin datos privados. Retención fijada al ciclo de vida de los backups restaurables (30 días + rotación) y purga segura tras destrucción del último backup. |
| **MP-PHYS-012** | Protocolo de restauración en entorno aislado | **Resuelto documentalmente** | Diseñado protocolo de fallo seguro (*fail-closed*): restauración en red aislada, reaplicación obligatoria de supresiones duraderas y revocaciones, validación de conteo cero de usuarios suprimidos antes de habilitar tráfico. |
| **MP-PHYS-013** | Justificación de índices de rendimiento y OQ-NFR-001 | **OPEN / Diferido** | **Diferido intencionalmente:** No se inventan consultas, volúmenes de prueba, planes `EXPLAIN` ni SLAs. OQ-NFR-001 permanece abierto hasta contar con métricas de carga del producto. Separación estricta de los índices de integridad. |
| **MP-PHYS-014** | Separación de roles técnicos, search_path y RLS selectivo | **Resuelto documentalmente** | Definidos roles con privilegios mínimos (`yusay_app`, `yusay_migrator`, `yusay_worker`, `yusay_backup`), `search_path` fijo `yusay, pg_temp`, y evaluación selectiva de RLS para evitar sobrecargas innecesarias. |
| **MP-PHYS-015** | Revocación efectiva de accesos tras operaciones sensibles | **Parcialmente resuelto documentalmente** | **Resuelto a nivel relacional:** Revocación global de sesiones mediante política de revocación JWT conjunta — regla temporal en segundos (`to_timestamp(token.iat) >= date_trunc('second', password_changed_at)`) acumulada a la huella de versión de credencial `pwd_at` en microsegundos Unix exactos, con rechazo de tokens sin el claim y consulta a PostgreSQL en cada uso —, cambio de credenciales, bloqueo de cuenta (`status = 'BLOCKED'`), supresión de usuario y post-restauración. **Responsabilidad de backend:** La revocación selectiva por dispositivo en `SIGN_OUT` se mantiene formalmente como responsabilidad exclusiva de la capa de backend (denylist en memoria/Redis con TTL) sin modificar el modelo lógico normativo congelado. |
| **MP-PHYS-016** | Contratos de intercambio UUID v4 y no regeneración de claves | **Resuelto documentalmente** | Intercambio en formato canónico de 36 caracteres en minúsculas (RFC 9562). Comparación binaria equivalente a `memcmp` de 16 bytes sin signo. Prohibición de regenerar claves compartidas. |

**Balance de los 16 mecanismos:**
- **14 Resueltos documentalmente** (`MP-PHYS-001` a `012`, `MP-PHYS-014`, `MP-PHYS-016`).
- **1 Parcialmente resuelto documentalmente** (`MP-PHYS-015` — limitación estructural de la línea base lógica sin entidad de sesiones para cierre selectivo).
- **1 Diferido intencionalmente** (`MP-PHYS-013` — dependiente de métricas de carga bajo `OQ-NFR-001`).

---

## 4. Conflictos resueltos y decisiones técnicas consolidadas

1. **Revisión de capacidad numérica (`MP-PHYS-001`):**
   - Se descartaron supuestos arbitrarios de 100/1.000 preguntas. Se formalizó el cálculo de extremos $[Score_{min}, Score_{max}]$. Se aclaró que `SUM(integer)` devuelve `bigint` en PostgreSQL pero un acumulador `bigint` también puede desbordarse si la suma excede sus límites numéricos, y que devolver `bigint` no garantiza que el resultado quepa en la columna `integer` de destino. Se documentó la validación segura de cotas obligatoria antes de publicar instrumentos en la capa editorial.
2. **Revisión de sintaxis y restricciones de correo (`MP-PHYS-002`):**
   - Se eliminaron expresiones que rechazaban TLDs no exclusivamente alfabéticos (permitiendo IDNA/Punycode `xn--...`). Se prohibió terminantemente `%`, espacios, caracteres de control, puntos iniciales/finales/consecutivos y Unicode en parte local. Se formalizó el índice funcional candidato `lower(email) COLLATE "C"`. Se ratificó `C-PHYS-001` como inmutable.
3. **Equivalencia estricta de envíos de evaluación (`MP-PHYS-006`):**
   - Se demostró formalmente la equivalencia entre estado `SUBMITTED` y la presencia de exactamente un resultado válido. Se articuló la transacción atómica con cálculo seguro, asignación obligatoria a una interpretación existente y guarda/trigger de consistencia diferible al COMMIT para evitar estados incongruentes en rollback o concurrencia.
4. **Eliminación de `SKIP LOCKED` en consumo de tokens (`MP-PHYS-009`):**
   - Se sustituyó `FOR UPDATE SKIP LOCKED` por bloqueo pesimista estándar con espera `SELECT ... FOR UPDATE` y reevaluación estricta de existencia y vigencia. Esto previene que peticiones legítimas concurrentes reciban respuestas espurias de «token no encontrado».
5. **Alcance y límites de revocación de sesiones (`MP-PHYS-015`):**
   - Se corrigió la comparación temporal de JWT homogeneizando unidades y escalas (`to_timestamp(token.iat) >= date_trunc('second', password_changed_at)`). Esa regla en segundos, por sí sola, no distingue tokens emitidos antes y después de un mismo cambio de contraseña dentro de un mismo segundo (`iat` es un entero), por lo que la decisión consolidada es la **política conjunta**: la regla temporal precedente acumulada a la huella de versión de credencial `pwd_at`, en microsegundos Unix exactos, con rechazo de los tokens sin el claim. Se ratificó que la revocación individual de sesión en `SIGN_OUT` permanece como responsabilidad exclusiva de la capa de backend/aplicación (denylist efímera con TTL), manteniendo el modelo lógico normativo 100% inalterado.
6. **Revisión exhaustiva de las 41 acciones referenciales (FKs):**
   - Se ajustaron 6 FKs de catálogos y contenido editorial (`RESOURCE_TOPIC`, `INSTRUMENT_TOPIC`, `DIMENSION_ANCHOR`, etc.) cambiando de `CASCADE` a `RESTRICT` para evitar que eliminaciones accidentales de tópicos o anclajes destruyan silenciosamente la integridad referencial histórica.
7. **Inmutabilidad del correo electrónico (`C-PHYS-001`):**
   - Se ratificó la prohibición de cambio de correo durante todo el MVP, manteniendo la línea base lógica 100% intacta.
8. **Diferimiento honesto del rendimiento (`MP-PHYS-013` / `OQ-NFR-001`):**
   - Se mantuvieron abiertos los índices de rendimiento para evitar optimizaciones prematuras o ficticias no sustentadas en métricas de carga reales.

---

## 5. Dictamen técnico de la fase de diseño físico v1.0

### Veredicto: APROBADO CON CONDICIONES DE IMPLEMENTACIÓN (Diseño físico v1.0)

El diseño físico de datos para Yusay en PostgreSQL 18:
1. **Respeta al 100% el contrato normativo congelado:** Mantiene invariantes las 32 relaciones, 147 atributos, 32 PK, 9 AK, 6 URA y 41 FK.
2. **Resuelve con rigor matemático y relacional los mecanismos físicos evaluados:** Capacidad numérica con cálculo de extremos y guarda preventiva de publicación, validación canónica de emails, equivalencia atómica SUBMITTED/Result, consumo atómico pesimista de tokens y análisis de precisión temporal de revocación.
3. **Reconoce con transparencia las fronteras del sistema:**
   - Conserva `MP-PHYS-015` como **Parcialmente resuelto documentalmente**, manteniendo la revocación individual de sesiones como responsabilidad exclusiva del backend, sin modificar el modelo lógico normativo congelado.
   - Conserva `MP-PHYS-013` como **OPEN / Diferido** en estricto apego al criterio de no inventar métricas ni perfiles de carga sin evidencia (`OQ-NFR-001`).
4. **Mantiene la disciplina metodológica:** Cero scripts ejecutables, cero migraciones prematuras, cero pruebas de integración falsamente declaradas como ejecutadas.

### Condiciones previas para la fase de implementación (DDL y migraciones):

**El diseño físico documental v1.0 queda cerrado y Aprobado con condiciones de implementación**, debiendo cumplirse las siguientes directrices técnicas al iniciar la fase de DDL y código de aplicación:
1. **Revocación individual de sesión (`MP-PHYS-015`):** Asumir en la capa de aplicación/caché (Redis o memoria) la lista de revocación efímera de tokens para `SIGN_OUT` con TTL acotado, sin alterar las 32 entidades relacionales.
2. **Guarda de pre-publicación editorial (`MP-PHYS-001`):** Implementar en el servicio de catálogo la validación exhaustiva de cotas $[Score_{min}, Score_{max}]$ antes de transicionar versiones de instrumentos a `READY` o `PUBLISHED`, asegurando que quepan estrictamente en `integer` ($\pm 2.147.483.647$).
3. **Métricas de rendimiento (`MP-PHYS-013` / `OQ-NFR-001`):** Registrar perfiles de carga reales una vez desplegada la base de la aplicación para diseñar y probar los índices de rendimiento necesarios mediante planes `EXPLAIN ANALYZE`.

[Índice de Data](../README.md) · [Índice físico](00-indice.md) · [Contexto y alcance](01-contexto-y-alcance.md).
