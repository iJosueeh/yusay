# Yusay — Especificación maestra del modelo lógico v1.0

## Instrucción

La siguiente especificación corresponde al modelo lógico de Yusay desarrollado y revisado mediante AJ-01 a AJ-04 y VF-01 a VF-05.

Utilízala como fuente para completar la matriz de correspondencia y, posteriormente, los diccionarios de datos.

No reconstruyas el modelo a partir del conceptual v0.1. No agregues entidades, atributos ni claves que no estén documentados. Si detectas una ambigüedad, regístrala para revisión.

**Inventario obligatorio: 32 relaciones distribuidas en seis módulos.**

## Convenciones

- PK: clave primaria.
- AK: clave alternativa o unicidad candidata.
- FK: clave foránea.
- URA: unicidad adicional requerida para permitir referencias compuestas.
- `?`: atributo opcional.
- Las FKs compuestas deben conservar sus componentes obligatorios.
- Los dominios lógicos no implican todavía una elección de tipos físicos de PostgreSQL.

## A. Identidad

### 1. USER

- `user_id` PK
- `email` AK, canónico, único
- `email_verified_at`?
- `created_at`
- `adult_confirmed_at` obligatorio
- `status`: ACTIVE | BLOCKED

### 2. USER_CREDENTIAL

- `user_id` PK, FK → USER
- `password_hash`
- `password_changed_at`

### 3. ADMINISTRATOR

- `user_id` PK, FK → USER

### 4. EMAIL_VERIFICATION_TOKEN

- `verification_token_id` PK
- `user_id` FK → USER
- `token_hash`
- `created_at`
- `expires_at`

### 5. PASSWORD_RESET_TOKEN

- `reset_token_id` PK
- `user_id` FK → USER
- `token_hash`
- `created_at`
- `expires_at`

Los tokens no contienen `consumed_at`, conforme a VF-05.

## B. Evaluaciones

### 6. INSTRUMENT

- `instrument_id` PK
- `code` AK
- `name`
- `description`
- `purpose`

### 7. INSTRUMENT_VERSION

- `instrument_version_id` PK
- `instrument_id` FK → INSTRUMENT
- `version` entero positivo
- `status`: DRAFT | READY | PUBLISHED | RETIRED
- `source_description`
- `population`
- `administration_conditions`?
- `license_information`?
- `limitations`

Restricciones:

- AK (`instrument_id`, `version`)
- URA (`instrument_id`, `instrument_version_id`)
- Máximo una versión PUBLISHED por instrumento.

### 8. INSTRUMENT_VERSION_REFERENCE

- `instrument_version_id` FK → INSTRUMENT_VERSION
- `reference_order`
- `citation`
- `url`?

PK (`instrument_version_id`, `reference_order`).

### 9. QUESTION

- `question_id` PK
- `instrument_version_id` FK → INSTRUMENT_VERSION
- `position`
- `prompt`
- `required`

Restricciones:

- AK (`instrument_version_id`, `position`)
- URA (`instrument_version_id`, `question_id`)

### 10. ANSWER_OPTION

- `option_id` PK
- `question_id` FK → QUESTION
- `position`
- `label`

Restricciones:

- AK (`question_id`, `position`)
- URA (`question_id`, `option_id`)

### 11. SCORING_DEFINITION

- `instrument_version_id` PK, FK → INSTRUMENT_VERSION
- `method`: SUM

### 12. SCORING_CONTRIBUTION

- `option_id` PK
- `question_id`
- `instrument_version_id`
- `contribution`: entero con signo

Restricciones:

- FK `instrument_version_id` → SCORING_DEFINITION
- FK (`instrument_version_id`, `question_id`) → QUESTION
- FK (`question_id`, `option_id`) → ANSWER_OPTION

### 13. ASSESSMENT_ATTEMPT

- `attempt_id` PK
- `user_id` FK → USER
- `instrument_id`
- `instrument_version_id`
- `status`: IN_PROGRESS | SUBMITTED | EXPIRED | CANCELLED
- `started_at`
- `expires_at`
- `ended_at`?

Restricciones:

- FK (`instrument_id`, `instrument_version_id`) → INSTRUMENT_VERSION
- URA (`attempt_id`, `instrument_version_id`)
- Máximo un Attempt IN_PROGRESS por usuario e instrumento.
- `expires_at = started_at + 720 horas`.

### 14. ANSWER

- `attempt_id`
- `instrument_version_id`
- `question_id`
- `option_id`

Restricciones:

- PK (`attempt_id`, `question_id`)
- FK (`attempt_id`, `instrument_version_id`) → ASSESSMENT_ATTEMPT
- FK (`instrument_version_id`, `question_id`) → QUESTION
- FK (`question_id`, `option_id`) → ANSWER_OPTION

### 15. INTERPRETATION

- `interpretation_id` PK
- `instrument_version_id` FK → INSTRUMENT_VERSION
- `label`
- `description`
- `limitations`?
- `lower_bound` inclusivo
- `upper_bound` inclusivo

Restricción:

- URA (`instrument_version_id`, `interpretation_id`)

Los rangos de interpretación deben cubrir exactamente una vez cada puntuación alcanzable.

### 16. ASSESSMENT_RESULT

- `attempt_id` PK, FK → ASSESSMENT_ATTEMPT
- `instrument_version_id`
- `score`: entero con signo
- `interpretation_id` obligatorio
- `calculated_at`

Restricciones:

- FK (`attempt_id`, `instrument_version_id`) → ASSESSMENT_ATTEMPT
- FK (`instrument_version_id`, `interpretation_id`) → INTERPRETATION

Un Attempt SUBMITTED tiene exactamente un Result, generado atómicamente.

## C. Seguimiento

### 17. DIMENSION

- `dimension_id` PK
- `code` AK
- `name`
- `description`

### 18. DIMENSION_VERSION

- `dimension_version_id` PK
- `dimension_id` FK → DIMENSION
- `version` entero positivo
- `definition`
- `min_value` entero
- `max_value` entero
- `step` entero positivo
- `status`: DRAFT | ACTIVE | RETIRED

Restricciones:

- AK (`dimension_id`, `version`)
- URA (`dimension_id`, `dimension_version_id`)
- `min_value < max_value`
- `(max_value - min_value)` divisible entre `step`
- Máximo una versión ACTIVE por Dimension.

### 19. DIMENSION_ANCHOR

- `dimension_version_id` FK → DIMENSION_VERSION
- `value` entero
- `label`

PK (`dimension_version_id`, `value`).

El valor debe pertenecer a la escala de su versión.

### 20. CHECK_IN

- `check_in_id` PK
- `user_id` FK → USER
- `recorded_at`
- `created_at`
- `updated_at`?
- `revision` entero positivo
- `note`?

Restricciones:

- `revision` inicia en 1.
- Se incrementa con cada edición confirmada del CheckIn o sus dependencias editables.
- Todo CheckIn requiere al menos una Measurement.
- La creación es atómica.
- `recorded_at` inicial pertenece al intervalo de los siete días anteriores a `created_at`.
- Las ediciones se permiten estrictamente antes de `created_at + 168 horas`.

### 21. MEASUREMENT

- `check_in_id`
- `dimension_id`
- `dimension_version_id`
- `value` entero

Restricciones:

- PK (`check_in_id`, `dimension_id`)
- FK `check_in_id` → CHECK_IN
- FK (`dimension_id`, `dimension_version_id`) → DIMENSION_VERSION
- El valor debe pertenecer a la escala correspondiente.
- La versión debe estar ACTIVE al crear la Measurement.
- Las correcciones conservan la versión original.

### 22. CONTEXT_TAG

- `context_tag_id` PK
- `code` AK
- `name`
- `description`?
- `status`: ACTIVE | RETIRED

### 23. CHECK_IN_CONTEXT_TAG

- `check_in_id` FK → CHECK_IN
- `context_tag_id` FK → CONTEXT_TAG

PK (`check_in_id`, `context_tag_id`).

Solo pueden agregarse ContextTags ACTIVE; los vínculos históricos pueden conservarse.

## D. Compatibilidad

### 24. INSTRUMENT_VERSION_COMPATIBILITY

- `version_a_id`
- `version_b_id`
- `instrument_id`
- `rationale`
- `reference`?

Restricciones:

- PK (`version_a_id`, `version_b_id`)
- FK (`instrument_id`, `version_a_id`) → INSTRUMENT_VERSION
- FK (`instrument_id`, `version_b_id`) → INSTRUMENT_VERSION
- `version_a_id < version_b_id`

### 25. DIMENSION_VERSION_COMPATIBILITY

- `version_a_id`
- `version_b_id`
- `dimension_id`
- `rationale`
- `reference`?

Restricciones:

- PK (`version_a_id`, `version_b_id`)
- FK (`dimension_id`, `version_a_id`) → DIMENSION_VERSION
- FK (`dimension_id`, `version_b_id`) → DIMENSION_VERSION
- `version_a_id < version_b_id`

La compatibilidad es explícita por pares, simétrica y no transitiva.

## E. Contenido

### 26. TOPIC

- `topic_id` PK
- `code` AK
- `name`
- `description`?

### 27. RESOURCE

- `resource_id` PK
- `type`: ARTICLE | EXTERNAL_LINK
- `status`: DRAFT | PUBLISHED | RETIRED
- `title` obligatorio y no vacío desde DRAFT
- `summary`?
- `body`?
- `external_url`?

Reglas:

- ARTICLE publicado requiere body válido y no utiliza external_url.
- EXTERNAL_LINK publicado requiere external_url HTTPS válido y no utiliza body.
- Un Resource publicado requiere al menos un Topic.
- `type` es inmutable desde su primera publicación.
- RETIRED es terminal.

### 28. RESOURCE_TOPIC

- `resource_id` FK → RESOURCE
- `topic_id` FK → TOPIC

PK (`resource_id`, `topic_id`).

### 29. INSTRUMENT_TOPIC

- `instrument_id` FK → INSTRUMENT
- `topic_id` FK → TOPIC

PK (`instrument_id`, `topic_id`).

### 30. INTERPRETATION_TOPIC

- `interpretation_id` FK → INTERPRETATION
- `topic_id` FK → TOPIC

PK (`interpretation_id`, `topic_id`).

### 31. DIMENSION_TOPIC

- `dimension_id` FK → DIMENSION
- `topic_id` FK → TOPIC

PK (`dimension_id`, `topic_id`).

## F. Auditoría

### 32. AUDIT_EVENT

- `audit_event_id` PK
- `actor_user_id`? FK → USER
- `actor_kind`
- `action`
- `target_type`
- `target_identifier`?
- `occurred_at`
- `metadata`?

Reglas:

- Eventos administrativos y de seguridad únicamente.
- No almacenar información privada de bienestar.
- No registrar identificadores de Attempts, CheckIns o Results personales.
- `actor_user_id` debe poder desvincularse al eliminar una cuenta.
- Desvincular referencias personales en target_identifier y metadata.
- Retención máxima de 180 días.
- Catálogos cerrados de actor_kind, action y target_type.
- Combinaciones válidas y metadata permitida por acción.

Los valores exactos de estos catálogos siguen pendientes de definición documental.

## Instrucciones finales

1. Contrasta esta especificación con los documentos existentes.
2. Elabora la matriz de correspondencia entre el conceptual v0.1 y las 32 relaciones.
3. Identifica contradicciones comprobables sin corregirlas silenciosamente.
4. Distingue atributos aprobados de detalles físicos todavía abiertos.
5. No agregues ni elimines relaciones.
6. No generes migraciones ni código.
7. No declares congelado el modelo.
8. Antes de redactar los seis diccionarios, presenta la matriz de correspondencia y los conflictos detectados para revisión.

Esta especificación es la fuente lógica aprobada para la siguiente etapa documental de Yusay.