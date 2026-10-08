# Integridad e índices — diseño físico v1.0

**Diseño físico: IN PROGRESS. OQ-PHYS-007/009: RESOLVED en alcance arquitectónico.** Consolidación autorizada por el responsable del proyecto: 2026-10-07. No hay DDL ni inventario definitivo de CHECK o índices.

## Fuentes y preservación

Se aplican los [seis diccionarios](../logical-model-v1/00-indice.md), [integridad lógica](../logical-model-v1/05-integridad-referencial.md), [dominios](../logical-model-v1/13-dominios-logicos.md), VF-01..05 y las [correspondencias aprobadas](02-decisiones-y-mapeo-fisico.md). Se conservan 32 PK, nueve AK, seis URA y 41 FK, incluidas 13 compuestas, con sus columnas en el orden documentado y los 88 nombres de OQ-PHYS-002. No se reconstruyen claves ni se introducen sustitutos.

La estrategia aprobada respalda la integridad en PostgreSQL donde sea adecuado. El backend coordina operaciones y autorización. Una validación de aplicación aislada no garantiza invariantes ante concurrencia; una FK tampoco garantiza publicación, autorización, completitud o vigencia.

## Mecanismos físicos de integridad y concurrencia

### MP-PHYS-004 — Máximo una versión publicada/activa por catálogo padre

1. **Invariante y problema:**
   - Para un `INSTRUMENT`, debe existir como máximo una `INSTRUMENT_VERSION` en estado `PUBLISHED` concurrentemente.
   - Para una `DIMENSION`, debe existir como máximo una `DIMENSION_VERSION` en estado `ACTIVE` concurrentemente.
   - Un simple chequeo en aplicación (`SELECT ... WHERE status = 'PUBLISHED'`) sufre condiciones de carrera bajo transacciones concurrentes.
2. **Fuentes y decisiones aprobadas:**
   - Diccionarios D02/D03, estados del modelo lógico (L-06), OQ-PHYS-002-D (`uxp_` aprobado) y OQ-PHYS-007.
3. **Mecanismo físico propuesto:**
   - **Índices únicos parciales declarativos en PostgreSQL:**
     * Para InstrumentVersion:
       `CREATE UNIQUE INDEX uxp_instrument_version_single_published ON yusay.instrument_version (instrument_id) WHERE (status = 'PUBLISHED');`
     * Para DimensionVersion:
       `CREATE UNIQUE INDEX uxp_dimension_version_single_active ON yusay.dimension_version (dimension_id) WHERE (status = 'ACTIVE');`
   - **Protocolo de bloqueo transaccional:**
     * En operaciones de publicación (`INSTRUMENT_VERSION_PUBLISHED`) o reemplazo de versión activa (`DIMENSION_VERSION_ACTIVATED`):
       La transacción debe adquirir primero un bloqueo exclusivo a nivel de fila sobre el catálogo padre:
       `SELECT instrument_id FROM yusay.instrument WHERE instrument_id = $1 FOR UPDATE;`
       (o `SELECT dimension_id FROM yusay.dimension WHERE dimension_id = $1 FOR UPDATE;`).
     * Posteriormente, para `DIMENSION_VERSION`, se actualiza la versión activa actual a `RETIRED` y la versión objetivo a `ACTIVE` dentro de la misma transacción atómica. El índice parcial garantiza físicamente que si otra transacción concurrente intenta intercalar un `ACTIVE`, PostgreSQL abortará una de ellas por violación de unicidad (`unique_violation`, código `23505`).
4. **Errores, rollback e idempotencia:**
   - Ante conflicto concurrente, se eleva excepción relacional `23505`. El backend captura el error, realiza rollback limpio y reporta conflicto de estado (`409 Conflict`), impidiendo estados inconsistentes.
5. **Pruebas necesarias:**
   - Positiva: Publicación válida de una versión tras retirar la anterior.
   - Negativa: Intento de insertar o actualizar dos versiones a `PUBLISHED` para el mismo `instrument_id`.
   - Concurrente: Dos transacciones simultáneas intentando promover dos versiones a `PUBLISHED` en el mismo instante; exactamente una triunfa y la otra falla por restricción única parcial.

---

### MP-PHYS-005 — Máximo un intento IN_PROGRESS por usuario e instrumento

1. **Invariante y problema:**
   - Un usuario no puede tener más de un `ASSESSMENT_ATTEMPT` con estado `IN_PROGRESS` para el mismo `INSTRUMENT` concurrentemente, incluso entre distintas versiones (`instrument_version_id`).
   - El atributo `instrument_id` existe en `ASSESSMENT_ATTEMPT` precisamente para soportar esta invariante sin joins cruzados.
2. **Fuentes y decisiones aprobadas:**
   - Diccionario D02, modelo lógico D02 §13, estados del modelo lógico, OQ-PHYS-007.
3. **Mecanismo físico propuesto:**
   - **Índice único parcial declarativo:**
     Dado que `ASSESSMENT_ATTEMPT` incluye `(user_id, instrument_id, status)` en su inventario formal:
     `CREATE UNIQUE INDEX uxp_assessment_attempt_single_in_progress ON yusay.assessment_attempt (user_id, instrument_id) WHERE (status = 'IN_PROGRESS');`
   - **Protocolo de concurrencia y vigencia:**
     * Antes de insertar un nuevo intento, si existe un intento previo `IN_PROGRESS` cuyo `expires_at <= clock_timestamp()`, este se considera lógicamente vencido. La transacción debe transicionar formalmente el intento caducado a `EXPIRED` (`UPDATE yusay.assessment_attempt SET status = 'EXPIRED', ended_at = expires_at WHERE attempt_id = ...`) antes de insertar el nuevo intento, o bien ejecutar dicha actualización en la misma unidad atómica.
     * Si el intento previo sigue vigente, cualquier inserción concurrente de otro intento `IN_PROGRESS` para el mismo `(user_id, instrument_id)` es rechazada directamente a nivel físico por el índice único parcial.
4. **Errores, rollback e idempotencia:**
   - Violación de índice parcial produce error `23505` (`unique_violation`). La transacción realiza rollback y devuelve al cliente el identificador del intento en curso ya existente (idempotencia lógica).
5. **Pruebas necesarias:**
   - Positiva: Inserción de intento en progreso; finalización o expiración; inserción subsiguiente de nuevo intento.
   - Negativa: Intento de insertar segundo intento en progreso mientras el primero está activo (mismo o diferente `instrument_version_id`).
   - Concurrente: Doble click o peticiones paralelas de inicio de evaluación; solo una inserción prospera, la segunda es rechazada por el índice.

---

### MP-PHYS-006 — Envío atómico de evaluación y coherencia estricta SUBMITTED ↔ Result

1. **Invariante y problema de equivalencia:**
   - Invariante lógica: Un `ASSESSMENT_ATTEMPT` se encuentra en estado `SUBMITTED` **si y solo si** existe exactamente un `ASSESSMENT_RESULT` histórico válido correspondiente a dicho intento y su versión.
   - Limitación de las claves relacionales estándar:
     * La clave primaria compartida `pk_assessment_result (attempt_id)` y la FK hacia `ASSESSMENT_ATTEMPT` garantizan que ningún intento tenga más de un resultado ($0 \le Resultados \le 1$).
     * Sin embargo, estas claves **no garantizan** por sí solas la dirección inversa: un intento podría tener `status = 'SUBMITTED'` sin que exista fila alguna en `ASSESSMENT_RESULT`, o un intento `CANCELLED` o `EXPIRED` podría contener erróneamente un resultado.
2. **Fuentes y decisiones aprobadas:**
   - D02-Evaluaciones, L-05 Integridad referencial, L-06 Estados, L-07 Transacciones, OQ-PHYS-007.
3. **Mecanismo físico propuesto:**
   - **A. Protocolo transaccional de envío atómico (`SUBMIT_ASSESSMENT`):**
     1. Bloqueo pesimista del intento en la base de datos:
        `SELECT attempt_id, instrument_version_id, status, expires_at FROM yusay.assessment_attempt WHERE attempt_id = $1 FOR UPDATE;`
     2. Verificación de precondiciones y vigencia:
        - Si `status = 'SUBMITTED'`: Idempotencia inmediata; se retorna el `assessment_result` existente sin recalcular.
        - Si `status != 'IN_PROGRESS'`: Rechazo (`409 Conflict`).
        - Si `clock_timestamp() >= expires_at`: El intento ha caducado; la transacción lo transiciona a `EXPIRED`, elimina respuestas parciales y aborta el envío (`422 Unprocessable Entity`).
     3. Verificación de completitud de respuestas:
        - Se comprueba que el conjunto de respuestas en `yusay.answer` cubra el 100% de las preguntas de la versión histórica requeridas (`QUESTION.required = true`).
     4. Agregación segura:
        - Se calcula la puntuación acumulada mediante `SUM(sc.contribution)`. PostgreSQL acumula internamente en `bigint`, verificándose que el valor resultante esté dentro del rango `integer` $[-2.147.483.648, +2.147.483.647]$.
     5. Resolución de interpretación oficial:
        - Se busca la interpretación cuyo intervalo inclusivo `[lower_bound, upper_bound]` contenga el `score`. La prevalidación de publicación asegura exactamente una coincidencia.
     6. Inserción atómica del resultado y actualización de estado:
        - Se inserta la fila en `yusay.assessment_result` con `(attempt_id, instrument_version_id, score, interpretation_id, calculated_at)`.
        - Se actualiza `yusay.assessment_attempt SET status = 'SUBMITTED', ended_at = calculated_at WHERE attempt_id = $1;`.
        - Ambas mutaciones ocurren dentro de la misma unidad de confirmación atómica (`COMMIT`).
   - **B. Garantía complementaria relacional y de base de datos:**
     - En operaciones fuera del flujo habitual o correcciones administrativas, para impedir a nivel relacional que `status = 'SUBMITTED'` exista sin su resultado, se define una guarda mediante restricción de verificación diferible / trigger de consistencia transaccional (`trg_check_submitted_result`):
       Al momento del `COMMIT`, si `status = 'SUBMITTED'`, se verifica `EXISTS (SELECT 1 FROM yusay.assessment_result r WHERE r.attempt_id = NEW.attempt_id)`. Si no existe resultado, la transacción se aborta con violación de integridad.
     - Asimismo, si `status IN ('IN_PROGRESS', 'EXPIRED', 'CANCELLED')`, se prohíbe terminantemente la existencia de filas en `yusay.assessment_result`.
   - **C. Eliminación autorizada por privacidad:**
     - La supresión autorizada de una evaluación personal elimina la tupla completa en una única transacción: elimina `assessment_result`, `answer` y `assessment_attempt`. La cascada `ON DELETE CASCADE` de `assessment_result` hacia `assessment_attempt` asegura que la supresión del intento limpie automáticamente su resultado, preservando la coherencia sin dejar orfandades.
4. **Errores e idempotencia:**
   - Envíos concurrentes: La segunda transacción que intente enviar el mismo intento esperará en el bloqueo `FOR UPDATE`. Al liberarse, leerá `status = 'SUBMITTED'` y retornará de forma segura e idempotente el resultado ya creado, sin duplicar ni recalcular.
   - Fallos en operaciones intermedias: Cualquier error en la búsqueda de interpretación, cálculo o validación provoca `ROLLBACK` total, asegurando que el intento permanezca `IN_PROGRESS` (si sigue vigente) y nunca pase a `SUBMITTED` sin resultado.
5. **Pruebas necesarias:**
   - Positiva: Envío con respuestas completas; inserción coordinada de `assessment_result` y cambio a `SUBMITTED`.
   - Negativa: Intento de forzar `SUBMITTED` sin fila en `assessment_result` (debe ser rechazado); intento de enviar con intento expirado (`clock_timestamp() >= expires_at`).
   - Concurrente: Solicitudes simultáneas de envío del mismo intento; exactamente una ejecuta el cálculo e inserción, y la segunda retorna el resultado persistido de forma idempotente.

---

## Acciones referenciales de las 41 claves foráneas (FK)

A continuación se revisa y ajusta la política física de integridad referencial para cada una de las 41 FKs normativas, justificando individualmente el comportamiento ante eliminación (`ON DELETE`).
Se corrige el uso de cascadas en catálogos y contenido editorial para proteger la inmutabilidad histórica, manteniendo `CASCADE` exclusivamente para dependencias personales o componentes efímeros de borrador, y `SET NULL` para la desvinculación de auditoría:

| ID FK | Tabla Origen | Columnas Origen | Tabla Destino | Columnas Destino | Acción ON DELETE | Justificación técnica y de integridad histórica |
| --- | --- | --- | --- | --- | --- | --- |
| FK-001 | `user_credential` | `user_id` | `app_user` | `user_id` | `CASCADE` | Dependencia personal 1:1 estricta de la cuenta; se suprime atómicamente al eliminar el usuario. |
| FK-002 | `administrator` | `user_id` | `app_user` | `user_id` | `CASCADE` | Habilitación administrativa de la cuenta; se suprime atómicamente con la cuenta. |
| FK-003 | `email_verification_token` | `user_id` | `app_user` | `user_id` | `CASCADE` | Tokens efímeros dependientes de la cuenta; purga atómica en supresión de usuario. |
| FK-004 | `password_reset_token` | `user_id` | `app_user` | `user_id` | `CASCADE` | Tokens efímeros dependientes de la cuenta; purga atómica en supresión de usuario. |
| FK-005 | `instrument_version` | `instrument_id` | `instrument` | `instrument_id` | `RESTRICT` | Protección estricta de catálogos históricos; no se permite eliminar un instrumento que posea versiones. |
| FK-006 | `instrument_version_reference` | `instrument_version_id` | `instrument_version` | `instrument_version_id` | `RESTRICT` | Referencias bibliográficas de la versión; inmutables tras publicación. Su eliminación solo se permite en DRAFT mediante backend; `RESTRICT` previene borrados accidentales de catálogos. |
| FK-007 | `question` | `instrument_version_id` | `instrument_version` | `instrument_version_id` | `RESTRICT` | Componente de la versión; protegido contra eliminación física destructiva. |
| FK-008 | `answer_option` | `question_id` | `question` | `question_id` | `RESTRICT` | Opciones de respuesta estructuradas; protegidas contra eliminación accidental. |
| FK-009 | `scoring_definition` | `instrument_version_id` | `instrument_version` | `instrument_version_id` | `RESTRICT` | Definición de puntuación de la versión; inmutable tras publicación. |
| FK-010 | `scoring_contribution` | `instrument_version_id` | `scoring_definition` | `instrument_version_id` | `RESTRICT` | Contribución de puntuación asociada a la definición; protegida contra borrado. |
| FK-011 | `scoring_contribution` | `instrument_version_id, question_id` | `question` | `instrument_version_id, question_id` | `RESTRICT` | Integridad compuesta de la versión de la pregunta en la contribución. |
| FK-012 | `scoring_contribution` | `question_id, option_id` | `answer_option` | `question_id, option_id` | `RESTRICT` | Integridad compuesta de la opción evaluada en la contribución. |
| FK-013 | `assessment_attempt` | `user_id` | `app_user` | `user_id` | `CASCADE` | Supresión de cuenta: los intentos del usuario se purgan en la eliminación de la cuenta. |
| FK-014 | `assessment_attempt` | `instrument_id, instrument_version_id` | `instrument_version` | `instrument_id, instrument_version_id` | `RESTRICT` | Prohíbe terminantemente borrar una versión de instrumento si existen evaluaciones históricas. |
| FK-015 | `answer` | `attempt_id, instrument_version_id` | `assessment_attempt` | `attempt_id, instrument_version_id` | `CASCADE` | Dependencia directa del intento; suprimida atómicamente con el intento (o en expiración/cancelación). |
| FK-016 | `answer` | `instrument_version_id, question_id` | `question` | `instrument_version_id, question_id` | `RESTRICT` | Impide borrar preguntas que cuenten con respuestas históricas de usuarios. |
| FK-017 | `answer` | `question_id, option_id` | `answer_option` | `question_id, option_id` | `RESTRICT` | Impide borrar opciones que hayan sido seleccionadas en respuestas históricas. |
| FK-018 | `interpretation` | `instrument_version_id` | `instrument_version` | `instrument_version_id` | `RESTRICT` | Interpretación oficial asociada a la versión; catálogo inmutable publicado. |
| FK-019 | `assessment_result` | `attempt_id` | `assessment_attempt` | `attempt_id` | `CASCADE` | Resultado dependiente 1:1 del intento; suprimido conjuntamente con el intento en borrados autorizados. |
| FK-020 | `assessment_result` | `attempt_id, instrument_version_id` | `assessment_attempt` | `attempt_id, instrument_version_id` | `CASCADE` | Componente compuesto de coherencia de versión del intento; suprimido junto con el intento. |
| FK-021 | `assessment_result` | `instrument_version_id, interpretation_id` | `interpretation` | `instrument_version_id, interpretation_id` | `RESTRICT` | Prohíbe eliminar interpretaciones de catálogo referenciadas por resultados de usuarios. |
| FK-022 | `dimension_version` | `dimension_id` | `dimension` | `dimension_id` | `RESTRICT` | Protección de catálogo; impide eliminar una dimensión con versiones. |
| FK-023 | `dimension_anchor` | `dimension_version_id` | `dimension_version` | `dimension_version_id` | `RESTRICT` | Anclajes descriptivos de escala de la versión; protegidos contra eliminación en cascada de versiones. |
| FK-024 | `check_in` | `user_id` | `app_user` | `user_id` | `CASCADE` | Supresión de cuenta: los CheckIns del usuario se eliminan en la supresión de la cuenta. |
| FK-025 | `measurement` | `check_in_id` | `check_in` | `check_in_id` | `CASCADE` | Mediciones dependientes del CheckIn; suprimidas atómicamente con el CheckIn. |
| FK-026 | `measurement` | `dimension_id, dimension_version_id` | `dimension_version` | `dimension_id, dimension_version_id` | `RESTRICT` | Prohíbe eliminar versiones de dimensiones referenciadas por mediciones históricas. |
| FK-027 | `check_in_context_tag` | `check_in_id` | `check_in` | `check_in_id` | `CASCADE` | Etiquetas asociadas al CheckIn; suprimidas atómicamente con el CheckIn. |
| FK-028 | `check_in_context_tag` | `context_tag_id` | `context_tag` | `context_tag_id` | `RESTRICT` | Prohíbe borrar etiquetas de catálogo referenciadas por CheckIns (se deben retirar a RETIRED). |
| FK-029 | `instrument_version_compatibility` | `instrument_id, version_a_id` | `instrument_version` | `instrument_id, instrument_version_id` | `RESTRICT` | Inmutabilidad de compatibilidad histórica; impide borrar versiones declaradas compatibles. |
| FK-030 | `instrument_version_compatibility` | `instrument_id, version_b_id` | `instrument_version` | `instrument_id, instrument_version_id` | `RESTRICT` | Inmutabilidad de compatibilidad histórica; versión b. |
| FK-031 | `dimension_version_compatibility` | `dimension_id, version_a_id` | `dimension_version` | `dimension_id, dimension_version_id` | `RESTRICT` | Inmutabilidad de compatibilidad histórica de dimensiones. |
| FK-032 | `dimension_version_compatibility` | `dimension_id, version_b_id` | `dimension_version` | `dimension_id, dimension_version_id` | `RESTRICT` | Inmutabilidad de compatibilidad histórica de dimensiones. |
| FK-033 | `resource_topic` | `resource_id` | `resource` | `resource_id` | `RESTRICT` | Asociación editorial; se prohíbe cascada accidental. La gestión de tópicos en recursos se realiza de forma controlada por servicio editorial. |
| FK-034 | `resource_topic` | `topic_id` | `topic` | `topic_id` | `RESTRICT` | Prohíbe borrar Topics de catálogo con recursos asociados. |
| FK-035 | `instrument_topic` | `instrument_id` | `instrument` | `instrument_id` | `RESTRICT` | Vínculo editorial; protegido contra borrados accidentales de instrumentos. |
| FK-036 | `instrument_topic` | `topic_id` | `topic` | `topic_id` | `RESTRICT` | Prohíbe borrar Topics referenciados por instrumentos. |
| FK-037 | `interpretation_topic` | `interpretation_id` | `interpretation` | `interpretation_id` | `RESTRICT` | Vínculo editorial dependiente de la interpretación; inmutable. |
| FK-038 | `interpretation_topic` | `topic_id` | `topic` | `topic_id` | `RESTRICT` | Prohíbe borrar Topics referenciados por interpretaciones. |
| FK-039 | `dimension_topic` | `dimension_id` | `dimension` | `dimension_id` | `RESTRICT` | Vínculo editorial dependiente de la dimensión; inmutable. |
| FK-040 | `dimension_topic` | `topic_id` | `topic` | `topic_id` | `RESTRICT` | Prohíbe borrar Topics referenciados por dimensiones. |
| FK-041 | `audit_event` | `actor_user_id` | `app_user` | `user_id` | `SET NULL` | **Desvinculación por privacidad:** Ante la supresión de la cuenta del usuario actor, `actor_user_id` se desvincula fijándose a NULL, preservando el evento de auditoría sin datos personales. |

---

## OQ-PHYS-009 — Separación de índices de integridad frente a rendimiento (MP-PHYS-013)

1. **Índices de Integridad (Obligatorios para la validez relacional):**
   - Son los índices físicos que respaldan de forma indispensable las restricciones del modelo:
     * 32 índices de respaldo de Claves Primarias (`pk_*`).
     * 9 índices de respaldo de Claves Alternativas (`uq_*`), incluyendo el índice funcional `lower(email) COLLATE "C"`.
     * 6 índices de respaldo de Unicidades Referenciales Adicionales (`ref_*`).
     * 2 índices únicos parciales de versión (`uxp_instrument_version_single_published` y `uxp_dimension_version_single_active`).
     * 1 índice único parcial de intento en curso (`uxp_assessment_attempt_single_in_progress`).
2. **Evaluación de índices para Claves Foráneas (FK):**
   - En PostgreSQL, una clave foránea no crea automáticamente un índice en las columnas de origen.
   - Las FKs hacia `app_user` (`user_id` en `assessment_attempt`, `check_in`, etc.) se benefician de índices para agilizar la cascada y las consultas por usuario, pero su creación formal se supedita a las consultas de acceso comprobadas.
3. **Índices de Rendimiento (MP-PHYS-013):**
   - **Estado: OPEN / DIFERIDO.**
   - Justificación: No existen en esta fase consultas de backend implementadas, volúmenes medidos, planes `EXPLAIN` reales ni parámetros de carga aprobados.
   - **OQ-NFR-001 permanece abierto:** Queda expresamente prohibido proponer o crear índices adicionales de optimización "preventiva" o inventar SLAs de tiempo de respuesta mientras no se disponga de la definición formal de perfiles de carga y consultas frecuentes del producto.

[Índice físico](00-indice.md) · [Transacciones y concurrencia](04-transacciones-y-concurrencia.md) · [Privacidad y operación](05-privacidad-eliminacion-y-operacion.md).
