# Decisiones y mapeo físico — consolidación arquitectónica

**Estado vigente: OQ-PHYS-001..010 RESOLVED en sus alcances documentados; diseño físico IN PROGRESS.** C-PHYS-001 y los mecanismos MP-PHYS permanecen OPEN. La aprobación de OQ-PHYS-004 no habilita cambios de correo ni modifica la línea base congelada.

**Diseño físico: IN PROGRESS. OQ-PHYS-002: RESOLVED.** Revisión previa y fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto. Alcance aprobado: organización y nomenclatura del diseño físico v1.0, incluidas OQ-PHYS-002-A..F. No constituye aprobación global. La consolidación posterior de OQ-PHYS-003..010 se registra en este documento y en 03..05, sin implementar el esquema.

La revisión previa se conserva como antecedente. Sus candidatos del escenario B son ahora nombres objetivo aprobados para 32 tablas, 147 columnas y 88 claves/referencias. Alternativas A/C, esquemas por módulo, prefijos de módulo y patrones no seleccionados quedan como historial; los CHECK ilustrativos no son un inventario físico aprobado.

## Fuentes, alcance y método

Se contrastaron el [inventario](../logical-model-v1/02-inventario-relaciones.md), la [especificación maestra](../logical-model-v1/especificacion-maestra-v1.0.md), los seis diccionarios D01..D06, [integridad referencial](../logical-model-v1/05-integridad-referencial.md), [dominios](../logical-model-v1/13-dominios-logicos.md), [índice físico](00-indice.md), [contexto](01-contexto-y-alcance.md) y [ADR-001](../../06-decisions/ADR-001-database-engine.md). Atributos y componentes ordenados de claves se toman de sus fuentes; no se reconstruyen desde el conceptual.

Las matrices son correspondencias de nombres, no nuevos diccionarios. Los dominios, nulabilidad y políticas completas siguen en las fuentes. R/C/PK/AK/URA/FK identifican filas de revisión, no nuevos requisitos. No se modifican relaciones, columnas, claves, reglas ni módulos; no se generan SQL, DDL, migraciones o código. Las decisiones arquitectónicas de OQ-PHYS-003..010 están RESOLVED conforme a la aprobación posterior; los mecanismos concretos y las validaciones indicadas en el contexto siguen pendientes.

## Cobertura comprobada

| Elemento | Fuente | Resultado |
| --- | --- | --- |
| Relaciones / módulos | Inventario, especificación, D01..D06 | 32 / 6; ninguna omisión ni adición |
| Columnas | Bloques de atributos fuente contra tablas de D01..D06 | 147; nombres y orden coinciden |
| PK / AK / URA | Inventario de claves, especificación y diccionarios | 32 / 9 / 6 |
| FK | Inventario de referencias contra columnas y claves destino | 41: 28 simples y 13 compuestas |
| Correspondencia referencial | Existencia y orden de componentes origen/destino | 41/41 verificadas; cada destino posee la clave aprobada |
| CHECK e índices físicos | No existe inventario físico aprobado | NO VERIFICADO como objetos; se evalúan patrones y etiquetas de reglas existentes |
| Instalación real | No se inspeccionó una base ni se ejecutó DDL | NO VERIFICADO: objetos preexistentes, privilegios y configuración |


| Módulo | Relaciones | Columnas | Fuente |
| --- | --- | --- | --- |
| identidad | 5 | 20 | [Diccionario](../logical-model-v1/04-diccionario-datos/01-identidad.md) |
| evaluaciones | 11 | 57 | [Diccionario](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) |
| seguimiento | 7 | 33 | [Diccionario](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) |
| compatibilidad | 2 | 10 | [Diccionario](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) |
| contenido | 6 | 19 | [Diccionario](../logical-model-v1/04-diccionario-datos/05-contenido.md) |
| auditoria | 1 | 8 | [Diccionario](../logical-model-v1/04-diccionario-datos/06-auditoria.md) |


## Convenciones aprobadas — escenario B

Se aprueba un único esquema de aplicación `yusay`, sin esquemas ni prefijos de módulo. Los seis módulos se mantienen lógicos y documentales. Tablas en singular; identificadores en minúsculas y snake_case, no entrecomillados; las 147 columnas se conservan literalmente.

Las matrices consolidan el **escenario B aprobado**: USER → `yusay.app_user`, con `user_id` intacto, y minúsculas literales para las otras 31 relaciones. Es una excepción física, no cambio lógico. A se conserva en la matriz como alternativa histórica no seleccionada; no es un segundo mapeo vigente. Los 88 nombres de PK/AK/URA/FK se aprueban exactamente como constaban en B, sin autorizar su implementación.

| Objeto | Patrón aprobado | Criterio |
| --- | --- | --- |
| Esquema | `yusay` | Un esquema de aplicación; no el único esquema del motor. |
| Tabla | `<nombre_logico_en_minusculas>` | Singular; excepción USER → app_user aprobada. |
| Columna | `<nombre_documentado>` | Correspondencia literal sin renombres inferidos. |
| PK | `pk_<tabla>` | Composición conservada. |
| AK / URA | `uq_<tabla>_<proposito>` | Componentes para AK; `ref_` en URA; categoría lógica explícita. |
| FK | `fk_<tabla>_<referencia_o_proposito>` | Destino si es inequívoco; rol o pertenencia cuando sea necesario. |
| CHECK | `ck_<tabla>_<regla>` | Solo etiqueta de regla documentada; protección física no seleccionada. |
| Índice independiente general | `idx_<tabla>_<proposito>` | Ningún índice de rendimiento propuesto. |
| Índice único parcial | `uxp_<tabla>_<proposito>` | Prefijo aprobado; ningún índice/predicado seleccionado. |


Los patrones no son identificadores literales. Los 88 nombres expandidos de claves/referencias ya están aprobados como objetivos; expansiones futuras requieren revisión. Nombres estables, explícitos y por propósito, sin depender del truncamiento. Las seis URA no se abrevian ni reciben prefijos/sufijos. Cualquier cambio futuro de sus nombres requiere documentación y aprobación expresa; no se aprueba un algoritmo de abreviación.

## Verificación PostgreSQL 18 y espacios de nombres

Los nombres aprobados B utilizan ASCII minúsculo, empiezan por letra y cumplen la forma léxica sin comillas. El motor normaliza los nombres no entrecomillados a minúsculas. El límite estándar es **63 bytes por identificador**, no por referencia calificada; esquema y tabla se comprueban por separado. Aquí bytes y caracteres coinciden. Los nombres superiores se truncarían. [Estructura léxica](https://www.postgresql.org/docs/18/sql-syntax-lexical.html).

Se contrastó el catálogo completo del [parser de la rama 18](https://raw.githubusercontent.com/postgres/postgres/REL_18_STABLE/src/include/parser/kwlist.h). Solo `user` es reservado entre los candidatos naturales. `name`, `version`, `label`, `method`, `value`, `type` y `action` son no reservados; `position` pertenece a la categoría admisible de nombres de columna. No hay motivo sintáctico para renombrar esas columnas. [Clasificación oficial](https://www.postgresql.org/docs/18/sql-keywords-appendix.html).

| Espacio | Comprobación | Resultado |
| --- | --- | --- |
| Esquemas | `yusay`: forma, palabra y 5 bytes | Válido; existencia previa NO VERIFICADO. |
| Relaciones de `yusay` | 32 tablas + 47 nombres PK/AK/URA que podrían nombrar su índice de respaldo | 79 nombres distintos, sin colisión ni truncamiento; no son 47 índices adicionales seleccionados. |
| Tipos del esquema | Tablas y sus tipos compuestos implícitos | 32 nombres distintos; otros tipos preexistentes NO VERIFICADO. |
| Columnas por tabla | 147 ocurrencias, normalización y unicidad local | Sin colisiones; repetir nombres en tablas diferentes es válido. |
| Restricciones por tabla | 88 nombres de claves/referencias | Sin colisiones por tabla ni en el registro completo B. |
| CHECK futuros | Etiquetas ilustrativas de reglas existentes | Sin colisión con las 88; inventario definitivo NO VERIFICADO. |
| Índices independientes futuros | Patrones `idx_` / `uxp_` | Sin objetos concretos; unicidad por comprobar al expandirlos. |


Los índices comparten espacio con tablas, secuencias y vistas del esquema; por ello se compararon tablas y nombres de posibles índices de respaldo. [Nombres de índices](https://www.postgresql.org/docs/18/sql-createindex.html). Las restricciones se identifican por tabla y nombre; su nombre no tiene que ser globalmente único. [pg_constraint](https://www.postgresql.org/docs/18/catalog-pg-constraint.html). Crear una tabla genera un tipo compuesto y una restricción UNIQUE genera un índice con su nombre, por lo que deben comprobarse esos espacios. [Reglas del motor](https://www.postgresql.org/docs/18/sql-createtable.html).

## Matriz de 32 relaciones

A: transformación literal histórica no seleccionada. B: nombre objetivo aprobado, usado en las matrices de claves. Bytes del componente tabla de B; `yusay` tiene 5. Todos los componentes B son válidos sin comillas.

| ID | Relación lógica | Módulo / fuente | A: histórico no seleccionado | B: nombre objetivo aprobado | Bytes B |
| --- | --- | --- | --- | --- | --- |
| R-001 | USER | identidad / [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md#user) | `yusay.user` | `yusay.app_user` | 8 |
| R-002 | USER_CREDENTIAL | identidad / [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md#user_credential) | `yusay.user_credential` | `yusay.user_credential` | 15 |
| R-003 | ADMINISTRATOR | identidad / [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md#administrator) | `yusay.administrator` | `yusay.administrator` | 13 |
| R-004 | EMAIL_VERIFICATION_TOKEN | identidad / [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md#email_verification_token) | `yusay.email_verification_token` | `yusay.email_verification_token` | 24 |
| R-005 | PASSWORD_RESET_TOKEN | identidad / [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md#password_reset_token) | `yusay.password_reset_token` | `yusay.password_reset_token` | 20 |
| R-006 | INSTRUMENT | evaluaciones / [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md#instrument) | `yusay.instrument` | `yusay.instrument` | 10 |
| R-007 | INSTRUMENT_VERSION | evaluaciones / [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md#instrument_version) | `yusay.instrument_version` | `yusay.instrument_version` | 18 |
| R-008 | INSTRUMENT_VERSION_REFERENCE | evaluaciones / [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md#instrument_version_reference) | `yusay.instrument_version_reference` | `yusay.instrument_version_reference` | 28 |
| R-009 | QUESTION | evaluaciones / [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md#question) | `yusay.question` | `yusay.question` | 8 |
| R-010 | ANSWER_OPTION | evaluaciones / [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md#answer_option) | `yusay.answer_option` | `yusay.answer_option` | 13 |
| R-011 | SCORING_DEFINITION | evaluaciones / [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md#scoring_definition) | `yusay.scoring_definition` | `yusay.scoring_definition` | 18 |
| R-012 | SCORING_CONTRIBUTION | evaluaciones / [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md#scoring_contribution) | `yusay.scoring_contribution` | `yusay.scoring_contribution` | 20 |
| R-013 | ASSESSMENT_ATTEMPT | evaluaciones / [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md#assessment_attempt) | `yusay.assessment_attempt` | `yusay.assessment_attempt` | 18 |
| R-014 | ANSWER | evaluaciones / [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md#answer) | `yusay.answer` | `yusay.answer` | 6 |
| R-015 | INTERPRETATION | evaluaciones / [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md#interpretation) | `yusay.interpretation` | `yusay.interpretation` | 14 |
| R-016 | ASSESSMENT_RESULT | evaluaciones / [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md#assessment_result) | `yusay.assessment_result` | `yusay.assessment_result` | 17 |
| R-017 | DIMENSION | seguimiento / [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md#dimension) | `yusay.dimension` | `yusay.dimension` | 9 |
| R-018 | DIMENSION_VERSION | seguimiento / [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md#dimension_version) | `yusay.dimension_version` | `yusay.dimension_version` | 17 |
| R-019 | DIMENSION_ANCHOR | seguimiento / [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md#dimension_anchor) | `yusay.dimension_anchor` | `yusay.dimension_anchor` | 16 |
| R-020 | CHECK_IN | seguimiento / [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md#check_in) | `yusay.check_in` | `yusay.check_in` | 8 |
| R-021 | MEASUREMENT | seguimiento / [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md#measurement) | `yusay.measurement` | `yusay.measurement` | 11 |
| R-022 | CONTEXT_TAG | seguimiento / [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md#context_tag) | `yusay.context_tag` | `yusay.context_tag` | 11 |
| R-023 | CHECK_IN_CONTEXT_TAG | seguimiento / [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md#check_in_context_tag) | `yusay.check_in_context_tag` | `yusay.check_in_context_tag` | 20 |
| R-024 | INSTRUMENT_VERSION_COMPATIBILITY | compatibilidad / [D04](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md#instrument_version_compatibility) | `yusay.instrument_version_compatibility` | `yusay.instrument_version_compatibility` | 32 |
| R-025 | DIMENSION_VERSION_COMPATIBILITY | compatibilidad / [D04](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md#dimension_version_compatibility) | `yusay.dimension_version_compatibility` | `yusay.dimension_version_compatibility` | 31 |
| R-026 | TOPIC | contenido / [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md#topic) | `yusay.topic` | `yusay.topic` | 5 |
| R-027 | RESOURCE | contenido / [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md#resource) | `yusay.resource` | `yusay.resource` | 8 |
| R-028 | RESOURCE_TOPIC | contenido / [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md#resource_topic) | `yusay.resource_topic` | `yusay.resource_topic` | 14 |
| R-029 | INSTRUMENT_TOPIC | contenido / [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md#instrument_topic) | `yusay.instrument_topic` | `yusay.instrument_topic` | 16 |
| R-030 | INTERPRETATION_TOPIC | contenido / [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md#interpretation_topic) | `yusay.interpretation_topic` | `yusay.interpretation_topic` | 20 |
| R-031 | DIMENSION_TOPIC | contenido / [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md#dimension_topic) | `yusay.dimension_topic` | `yusay.dimension_topic` | 15 |
| R-032 | AUDIT_EVENT | auditoria / [D06](../logical-model-v1/04-diccionario-datos/06-auditoria.md#audit_event) | `yusay.audit_event` | `yusay.audit_event` | 11 |


## Matriz de 147 columnas

Se cuenta cada atributo de cada relación. Relación y fila R remiten al diccionario. Original y propuesto coinciden con la especificación. OK: admisible sin comillas, ≤63 bytes, sin duplicado local; NR: palabra no reservada o de categoría admisible de columna. Estos resultados no aprueban el diseño.

| ID | Relación lógica | Original | Propuesto | Bytes | Resultado |
| --- | --- | --- | --- | --- | --- |
| C-01-01 | USER | `user_id` | `user_id` | 7 | OK |
| C-01-02 | USER | `email` | `email` | 5 | OK |
| C-01-03 | USER | `email_verified_at` | `email_verified_at` | 17 | OK |
| C-01-04 | USER | `created_at` | `created_at` | 10 | OK |
| C-01-05 | USER | `adult_confirmed_at` | `adult_confirmed_at` | 18 | OK |
| C-01-06 | USER | `status` | `status` | 6 | OK |
| C-02-01 | USER_CREDENTIAL | `user_id` | `user_id` | 7 | OK |
| C-02-02 | USER_CREDENTIAL | `password_hash` | `password_hash` | 13 | OK |
| C-02-03 | USER_CREDENTIAL | `password_changed_at` | `password_changed_at` | 19 | OK |
| C-03-01 | ADMINISTRATOR | `user_id` | `user_id` | 7 | OK |
| C-04-01 | EMAIL_VERIFICATION_TOKEN | `verification_token_id` | `verification_token_id` | 21 | OK |
| C-04-02 | EMAIL_VERIFICATION_TOKEN | `user_id` | `user_id` | 7 | OK |
| C-04-03 | EMAIL_VERIFICATION_TOKEN | `token_hash` | `token_hash` | 10 | OK |
| C-04-04 | EMAIL_VERIFICATION_TOKEN | `created_at` | `created_at` | 10 | OK |
| C-04-05 | EMAIL_VERIFICATION_TOKEN | `expires_at` | `expires_at` | 10 | OK |
| C-05-01 | PASSWORD_RESET_TOKEN | `reset_token_id` | `reset_token_id` | 14 | OK |
| C-05-02 | PASSWORD_RESET_TOKEN | `user_id` | `user_id` | 7 | OK |
| C-05-03 | PASSWORD_RESET_TOKEN | `token_hash` | `token_hash` | 10 | OK |
| C-05-04 | PASSWORD_RESET_TOKEN | `created_at` | `created_at` | 10 | OK |
| C-05-05 | PASSWORD_RESET_TOKEN | `expires_at` | `expires_at` | 10 | OK |
| C-06-01 | INSTRUMENT | `instrument_id` | `instrument_id` | 13 | OK |
| C-06-02 | INSTRUMENT | `code` | `code` | 4 | OK |
| C-06-03 | INSTRUMENT | `name` | `name` | 4 | OK · NR |
| C-06-04 | INSTRUMENT | `description` | `description` | 11 | OK |
| C-06-05 | INSTRUMENT | `purpose` | `purpose` | 7 | OK |
| C-07-01 | INSTRUMENT_VERSION | `instrument_version_id` | `instrument_version_id` | 21 | OK |
| C-07-02 | INSTRUMENT_VERSION | `instrument_id` | `instrument_id` | 13 | OK |
| C-07-03 | INSTRUMENT_VERSION | `version` | `version` | 7 | OK · NR |
| C-07-04 | INSTRUMENT_VERSION | `status` | `status` | 6 | OK |
| C-07-05 | INSTRUMENT_VERSION | `source_description` | `source_description` | 18 | OK |
| C-07-06 | INSTRUMENT_VERSION | `population` | `population` | 10 | OK |
| C-07-07 | INSTRUMENT_VERSION | `administration_conditions` | `administration_conditions` | 25 | OK |
| C-07-08 | INSTRUMENT_VERSION | `license_information` | `license_information` | 19 | OK |
| C-07-09 | INSTRUMENT_VERSION | `limitations` | `limitations` | 11 | OK |
| C-08-01 | INSTRUMENT_VERSION_REFERENCE | `instrument_version_id` | `instrument_version_id` | 21 | OK |
| C-08-02 | INSTRUMENT_VERSION_REFERENCE | `reference_order` | `reference_order` | 15 | OK |
| C-08-03 | INSTRUMENT_VERSION_REFERENCE | `citation` | `citation` | 8 | OK |
| C-08-04 | INSTRUMENT_VERSION_REFERENCE | `url` | `url` | 3 | OK |
| C-09-01 | QUESTION | `question_id` | `question_id` | 11 | OK |
| C-09-02 | QUESTION | `instrument_version_id` | `instrument_version_id` | 21 | OK |
| C-09-03 | QUESTION | `position` | `position` | 8 | OK · NR |
| C-09-04 | QUESTION | `prompt` | `prompt` | 6 | OK |
| C-09-05 | QUESTION | `required` | `required` | 8 | OK |
| C-10-01 | ANSWER_OPTION | `option_id` | `option_id` | 9 | OK |
| C-10-02 | ANSWER_OPTION | `question_id` | `question_id` | 11 | OK |
| C-10-03 | ANSWER_OPTION | `position` | `position` | 8 | OK · NR |
| C-10-04 | ANSWER_OPTION | `label` | `label` | 5 | OK · NR |
| C-11-01 | SCORING_DEFINITION | `instrument_version_id` | `instrument_version_id` | 21 | OK |
| C-11-02 | SCORING_DEFINITION | `method` | `method` | 6 | OK · NR |
| C-12-01 | SCORING_CONTRIBUTION | `option_id` | `option_id` | 9 | OK |
| C-12-02 | SCORING_CONTRIBUTION | `question_id` | `question_id` | 11 | OK |
| C-12-03 | SCORING_CONTRIBUTION | `instrument_version_id` | `instrument_version_id` | 21 | OK |
| C-12-04 | SCORING_CONTRIBUTION | `contribution` | `contribution` | 12 | OK |
| C-13-01 | ASSESSMENT_ATTEMPT | `attempt_id` | `attempt_id` | 10 | OK |
| C-13-02 | ASSESSMENT_ATTEMPT | `user_id` | `user_id` | 7 | OK |
| C-13-03 | ASSESSMENT_ATTEMPT | `instrument_id` | `instrument_id` | 13 | OK |
| C-13-04 | ASSESSMENT_ATTEMPT | `instrument_version_id` | `instrument_version_id` | 21 | OK |
| C-13-05 | ASSESSMENT_ATTEMPT | `status` | `status` | 6 | OK |
| C-13-06 | ASSESSMENT_ATTEMPT | `started_at` | `started_at` | 10 | OK |
| C-13-07 | ASSESSMENT_ATTEMPT | `expires_at` | `expires_at` | 10 | OK |
| C-13-08 | ASSESSMENT_ATTEMPT | `ended_at` | `ended_at` | 8 | OK |
| C-14-01 | ANSWER | `attempt_id` | `attempt_id` | 10 | OK |
| C-14-02 | ANSWER | `instrument_version_id` | `instrument_version_id` | 21 | OK |
| C-14-03 | ANSWER | `question_id` | `question_id` | 11 | OK |
| C-14-04 | ANSWER | `option_id` | `option_id` | 9 | OK |
| C-15-01 | INTERPRETATION | `interpretation_id` | `interpretation_id` | 17 | OK |
| C-15-02 | INTERPRETATION | `instrument_version_id` | `instrument_version_id` | 21 | OK |
| C-15-03 | INTERPRETATION | `label` | `label` | 5 | OK · NR |
| C-15-04 | INTERPRETATION | `description` | `description` | 11 | OK |
| C-15-05 | INTERPRETATION | `limitations` | `limitations` | 11 | OK |
| C-15-06 | INTERPRETATION | `lower_bound` | `lower_bound` | 11 | OK |
| C-15-07 | INTERPRETATION | `upper_bound` | `upper_bound` | 11 | OK |
| C-16-01 | ASSESSMENT_RESULT | `attempt_id` | `attempt_id` | 10 | OK |
| C-16-02 | ASSESSMENT_RESULT | `instrument_version_id` | `instrument_version_id` | 21 | OK |
| C-16-03 | ASSESSMENT_RESULT | `score` | `score` | 5 | OK |
| C-16-04 | ASSESSMENT_RESULT | `interpretation_id` | `interpretation_id` | 17 | OK |
| C-16-05 | ASSESSMENT_RESULT | `calculated_at` | `calculated_at` | 13 | OK |
| C-17-01 | DIMENSION | `dimension_id` | `dimension_id` | 12 | OK |
| C-17-02 | DIMENSION | `code` | `code` | 4 | OK |
| C-17-03 | DIMENSION | `name` | `name` | 4 | OK · NR |
| C-17-04 | DIMENSION | `description` | `description` | 11 | OK |
| C-18-01 | DIMENSION_VERSION | `dimension_version_id` | `dimension_version_id` | 20 | OK |
| C-18-02 | DIMENSION_VERSION | `dimension_id` | `dimension_id` | 12 | OK |
| C-18-03 | DIMENSION_VERSION | `version` | `version` | 7 | OK · NR |
| C-18-04 | DIMENSION_VERSION | `definition` | `definition` | 10 | OK |
| C-18-05 | DIMENSION_VERSION | `min_value` | `min_value` | 9 | OK |
| C-18-06 | DIMENSION_VERSION | `max_value` | `max_value` | 9 | OK |
| C-18-07 | DIMENSION_VERSION | `step` | `step` | 4 | OK |
| C-18-08 | DIMENSION_VERSION | `status` | `status` | 6 | OK |
| C-19-01 | DIMENSION_ANCHOR | `dimension_version_id` | `dimension_version_id` | 20 | OK |
| C-19-02 | DIMENSION_ANCHOR | `value` | `value` | 5 | OK · NR |
| C-19-03 | DIMENSION_ANCHOR | `label` | `label` | 5 | OK · NR |
| C-20-01 | CHECK_IN | `check_in_id` | `check_in_id` | 11 | OK |
| C-20-02 | CHECK_IN | `user_id` | `user_id` | 7 | OK |
| C-20-03 | CHECK_IN | `recorded_at` | `recorded_at` | 11 | OK |
| C-20-04 | CHECK_IN | `created_at` | `created_at` | 10 | OK |
| C-20-05 | CHECK_IN | `updated_at` | `updated_at` | 10 | OK |
| C-20-06 | CHECK_IN | `revision` | `revision` | 8 | OK |
| C-20-07 | CHECK_IN | `note` | `note` | 4 | OK |
| C-21-01 | MEASUREMENT | `check_in_id` | `check_in_id` | 11 | OK |
| C-21-02 | MEASUREMENT | `dimension_id` | `dimension_id` | 12 | OK |
| C-21-03 | MEASUREMENT | `dimension_version_id` | `dimension_version_id` | 20 | OK |
| C-21-04 | MEASUREMENT | `value` | `value` | 5 | OK · NR |
| C-22-01 | CONTEXT_TAG | `context_tag_id` | `context_tag_id` | 14 | OK |
| C-22-02 | CONTEXT_TAG | `code` | `code` | 4 | OK |
| C-22-03 | CONTEXT_TAG | `name` | `name` | 4 | OK · NR |
| C-22-04 | CONTEXT_TAG | `description` | `description` | 11 | OK |
| C-22-05 | CONTEXT_TAG | `status` | `status` | 6 | OK |
| C-23-01 | CHECK_IN_CONTEXT_TAG | `check_in_id` | `check_in_id` | 11 | OK |
| C-23-02 | CHECK_IN_CONTEXT_TAG | `context_tag_id` | `context_tag_id` | 14 | OK |
| C-24-01 | INSTRUMENT_VERSION_COMPATIBILITY | `version_a_id` | `version_a_id` | 12 | OK |
| C-24-02 | INSTRUMENT_VERSION_COMPATIBILITY | `version_b_id` | `version_b_id` | 12 | OK |
| C-24-03 | INSTRUMENT_VERSION_COMPATIBILITY | `instrument_id` | `instrument_id` | 13 | OK |
| C-24-04 | INSTRUMENT_VERSION_COMPATIBILITY | `rationale` | `rationale` | 9 | OK |
| C-24-05 | INSTRUMENT_VERSION_COMPATIBILITY | `reference` | `reference` | 9 | OK |
| C-25-01 | DIMENSION_VERSION_COMPATIBILITY | `version_a_id` | `version_a_id` | 12 | OK |
| C-25-02 | DIMENSION_VERSION_COMPATIBILITY | `version_b_id` | `version_b_id` | 12 | OK |
| C-25-03 | DIMENSION_VERSION_COMPATIBILITY | `dimension_id` | `dimension_id` | 12 | OK |
| C-25-04 | DIMENSION_VERSION_COMPATIBILITY | `rationale` | `rationale` | 9 | OK |
| C-25-05 | DIMENSION_VERSION_COMPATIBILITY | `reference` | `reference` | 9 | OK |
| C-26-01 | TOPIC | `topic_id` | `topic_id` | 8 | OK |
| C-26-02 | TOPIC | `code` | `code` | 4 | OK |
| C-26-03 | TOPIC | `name` | `name` | 4 | OK · NR |
| C-26-04 | TOPIC | `description` | `description` | 11 | OK |
| C-27-01 | RESOURCE | `resource_id` | `resource_id` | 11 | OK |
| C-27-02 | RESOURCE | `type` | `type` | 4 | OK · NR |
| C-27-03 | RESOURCE | `status` | `status` | 6 | OK |
| C-27-04 | RESOURCE | `title` | `title` | 5 | OK |
| C-27-05 | RESOURCE | `summary` | `summary` | 7 | OK |
| C-27-06 | RESOURCE | `body` | `body` | 4 | OK |
| C-27-07 | RESOURCE | `external_url` | `external_url` | 12 | OK |
| C-28-01 | RESOURCE_TOPIC | `resource_id` | `resource_id` | 11 | OK |
| C-28-02 | RESOURCE_TOPIC | `topic_id` | `topic_id` | 8 | OK |
| C-29-01 | INSTRUMENT_TOPIC | `instrument_id` | `instrument_id` | 13 | OK |
| C-29-02 | INSTRUMENT_TOPIC | `topic_id` | `topic_id` | 8 | OK |
| C-30-01 | INTERPRETATION_TOPIC | `interpretation_id` | `interpretation_id` | 17 | OK |
| C-30-02 | INTERPRETATION_TOPIC | `topic_id` | `topic_id` | 8 | OK |
| C-31-01 | DIMENSION_TOPIC | `dimension_id` | `dimension_id` | 12 | OK |
| C-31-02 | DIMENSION_TOPIC | `topic_id` | `topic_id` | 8 | OK |
| C-32-01 | AUDIT_EVENT | `audit_event_id` | `audit_event_id` | 14 | OK |
| C-32-02 | AUDIT_EVENT | `actor_user_id` | `actor_user_id` | 13 | OK |
| C-32-03 | AUDIT_EVENT | `actor_kind` | `actor_kind` | 10 | OK |
| C-32-04 | AUDIT_EVENT | `action` | `action` | 6 | OK · NR |
| C-32-05 | AUDIT_EVENT | `target_type` | `target_type` | 11 | OK |
| C-32-06 | AUDIT_EVENT | `target_identifier` | `target_identifier` | 17 | OK |
| C-32-07 | AUDIT_EVENT | `occurred_at` | `occurred_at` | 11 | OK |
| C-32-08 | AUDIT_EVENT | `metadata` | `metadata` | 8 | OK |


## Matriz de 32 PK

Fuente: inventario de claves de integridad, contrastado con especificación/diccionarios. Componentes en orden aprobado; nombres de columnas físicos idénticos. No se sustituyen PK compartidas o compuestas por nuevos IDs.

| ID | Relación / tabla B | Componentes | Propósito | Nombre objetivo aprobado | Bytes |
| --- | --- | --- | --- | --- | --- |
| PK-001 | USER / `yusay.app_user` | `user_id` | Identificar fila por clave aprobada | `pk_app_user` | 11 |
| PK-002 | USER_CREDENTIAL / `yusay.user_credential` | `user_id` | Identificar fila por clave aprobada | `pk_user_credential` | 18 |
| PK-003 | ADMINISTRATOR / `yusay.administrator` | `user_id` | Identificar fila por clave aprobada | `pk_administrator` | 16 |
| PK-004 | EMAIL_VERIFICATION_TOKEN / `yusay.email_verification_token` | `verification_token_id` | Identificar fila por clave aprobada | `pk_email_verification_token` | 27 |
| PK-005 | PASSWORD_RESET_TOKEN / `yusay.password_reset_token` | `reset_token_id` | Identificar fila por clave aprobada | `pk_password_reset_token` | 23 |
| PK-006 | INSTRUMENT / `yusay.instrument` | `instrument_id` | Identificar fila por clave aprobada | `pk_instrument` | 13 |
| PK-007 | INSTRUMENT_VERSION / `yusay.instrument_version` | `instrument_version_id` | Identificar fila por clave aprobada | `pk_instrument_version` | 21 |
| PK-008 | INSTRUMENT_VERSION_REFERENCE / `yusay.instrument_version_reference` | `instrument_version_id, reference_order` | Identificar combinación aprobada | `pk_instrument_version_reference` | 31 |
| PK-009 | QUESTION / `yusay.question` | `question_id` | Identificar fila por clave aprobada | `pk_question` | 11 |
| PK-010 | ANSWER_OPTION / `yusay.answer_option` | `option_id` | Identificar fila por clave aprobada | `pk_answer_option` | 16 |
| PK-011 | SCORING_DEFINITION / `yusay.scoring_definition` | `instrument_version_id` | Identificar fila por clave aprobada | `pk_scoring_definition` | 21 |
| PK-012 | SCORING_CONTRIBUTION / `yusay.scoring_contribution` | `option_id` | Identificar fila por clave aprobada | `pk_scoring_contribution` | 23 |
| PK-013 | ASSESSMENT_ATTEMPT / `yusay.assessment_attempt` | `attempt_id` | Identificar fila por clave aprobada | `pk_assessment_attempt` | 21 |
| PK-014 | ANSWER / `yusay.answer` | `attempt_id, question_id` | Identificar combinación aprobada | `pk_answer` | 9 |
| PK-015 | INTERPRETATION / `yusay.interpretation` | `interpretation_id` | Identificar fila por clave aprobada | `pk_interpretation` | 17 |
| PK-016 | ASSESSMENT_RESULT / `yusay.assessment_result` | `attempt_id` | Identificar fila por clave aprobada | `pk_assessment_result` | 20 |
| PK-017 | DIMENSION / `yusay.dimension` | `dimension_id` | Identificar fila por clave aprobada | `pk_dimension` | 12 |
| PK-018 | DIMENSION_VERSION / `yusay.dimension_version` | `dimension_version_id` | Identificar fila por clave aprobada | `pk_dimension_version` | 20 |
| PK-019 | DIMENSION_ANCHOR / `yusay.dimension_anchor` | `dimension_version_id, value` | Identificar combinación aprobada | `pk_dimension_anchor` | 19 |
| PK-020 | CHECK_IN / `yusay.check_in` | `check_in_id` | Identificar fila por clave aprobada | `pk_check_in` | 11 |
| PK-021 | MEASUREMENT / `yusay.measurement` | `check_in_id, dimension_id` | Identificar combinación aprobada | `pk_measurement` | 14 |
| PK-022 | CONTEXT_TAG / `yusay.context_tag` | `context_tag_id` | Identificar fila por clave aprobada | `pk_context_tag` | 14 |
| PK-023 | CHECK_IN_CONTEXT_TAG / `yusay.check_in_context_tag` | `check_in_id, context_tag_id` | Identificar combinación aprobada | `pk_check_in_context_tag` | 23 |
| PK-024 | INSTRUMENT_VERSION_COMPATIBILITY / `yusay.instrument_version_compatibility` | `version_a_id, version_b_id` | Identificar combinación aprobada | `pk_instrument_version_compatibility` | 35 |
| PK-025 | DIMENSION_VERSION_COMPATIBILITY / `yusay.dimension_version_compatibility` | `version_a_id, version_b_id` | Identificar combinación aprobada | `pk_dimension_version_compatibility` | 34 |
| PK-026 | TOPIC / `yusay.topic` | `topic_id` | Identificar fila por clave aprobada | `pk_topic` | 8 |
| PK-027 | RESOURCE / `yusay.resource` | `resource_id` | Identificar fila por clave aprobada | `pk_resource` | 11 |
| PK-028 | RESOURCE_TOPIC / `yusay.resource_topic` | `resource_id, topic_id` | Identificar combinación aprobada | `pk_resource_topic` | 17 |
| PK-029 | INSTRUMENT_TOPIC / `yusay.instrument_topic` | `instrument_id, topic_id` | Identificar combinación aprobada | `pk_instrument_topic` | 19 |
| PK-030 | INTERPRETATION_TOPIC / `yusay.interpretation_topic` | `interpretation_id, topic_id` | Identificar combinación aprobada | `pk_interpretation_topic` | 23 |
| PK-031 | DIMENSION_TOPIC / `yusay.dimension_topic` | `dimension_id, topic_id` | Identificar combinación aprobada | `pk_dimension_topic` | 18 |
| PK-032 | AUDIT_EVENT / `yusay.audit_event` | `audit_event_id` | Identificar fila por clave aprobada | `pk_audit_event` | 14 |


## Matriz de 9 AK

Fuente: mismo inventario aprobado. `uq_` no decide realización física. Email conserva igualdad sin distinción de mayúsculas; OQ-PHYS-004 resuelve text/canonicalización y MP-PHYS-002 mantiene pendiente su materialización exacta; códigos conservan comparación sensible a mayúsculas.

| ID | Relación | Componentes | Propósito | Nombre objetivo aprobado B | Bytes |
| --- | --- | --- | --- | --- | --- |
| AK-001 | USER | `email` | Correo canónico único sin distinción de mayúsculas | `uq_app_user_email` | 17 |
| AK-002 | INSTRUMENT | `code` | Código único en su catálogo | `uq_instrument_code` | 18 |
| AK-003 | INSTRUMENT_VERSION | `instrument_id, version` | Número de versión único en su padre | `uq_instrument_version_instrument_id_version` | 43 |
| AK-004 | QUESTION | `instrument_version_id, position` | Posición única en su padre | `uq_question_instrument_version_id_position` | 42 |
| AK-005 | ANSWER_OPTION | `question_id, position` | Posición única en su padre | `uq_answer_option_question_id_position` | 37 |
| AK-006 | DIMENSION | `code` | Código único en su catálogo | `uq_dimension_code` | 17 |
| AK-007 | DIMENSION_VERSION | `dimension_id, version` | Número de versión único en su padre | `uq_dimension_version_dimension_id_version` | 41 |
| AK-008 | CONTEXT_TAG | `code` | Código único en su catálogo | `uq_context_tag_code` | 19 |
| AK-009 | TOPIC | `code` | Código único en su catálogo | `uq_topic_code` | 13 |


## Matriz de 6 URA

Fuente: mismo inventario aprobado. URA permite referencias compuestas y no se reclasifica como AK mínima. `ref_` expresa propósito, sin crear una categoría de restricción del motor.

| ID | Relación | Componentes | Propósito | Nombre objetivo aprobado | Bytes |
| --- | --- | --- | --- | --- | --- |
| URA-001 | INSTRUMENT_VERSION | `instrument_id, instrument_version_id` | Permitir referencia compuesta y preservar pertenencia | `uq_instrument_version_ref_instrument_id_instrument_version_id` | 61 |
| URA-002 | QUESTION | `instrument_version_id, question_id` | Permitir referencia compuesta y preservar pertenencia | `uq_question_ref_instrument_version_id_question_id` | 49 |
| URA-003 | ANSWER_OPTION | `question_id, option_id` | Permitir referencia compuesta y preservar pertenencia | `uq_answer_option_ref_question_id_option_id` | 42 |
| URA-004 | ASSESSMENT_ATTEMPT | `attempt_id, instrument_version_id` | Permitir referencia compuesta y preservar pertenencia | `uq_assessment_attempt_ref_attempt_id_instrument_version_id` | 58 |
| URA-005 | INTERPRETATION | `instrument_version_id, interpretation_id` | Permitir referencia compuesta y preservar pertenencia | `uq_interpretation_ref_instrument_version_id_interpretation_id` | 61 |
| URA-006 | DIMENSION_VERSION | `dimension_id, dimension_version_id` | Permitir referencia compuesta y preservar pertenencia | `uq_dimension_version_ref_dimension_id_dimension_version_id` | 58 |


Las seis URA conservan exactamente sus nombres aprobados. `uq_instrument_version_ref_instrument_id_instrument_version_id` y `uq_interpretation_ref_instrument_version_id_interpretation_id` tienen 61 bytes y dejan dos de margen. No abreviarlos ni añadir prefijos/sufijos; cualquier cambio futuro exige documentación y aprobación expresa.

## Matriz de 41 FK, incluidas 13 compuestas

Fuente: inventario completo de referencias. Componentes y orden se preservan exactamente. Destinos físicos según B; relaciones lógicas intactas. Todas obligatorias salvo AUDIT_EVENT.actor_user_id. Simples hacia PK; las 13 compuestas hacia URA aprobadas. Metadata y target_identifier no se convierten en FKs.

| ID | Origen lógico | Componentes origen | Destino B | Componentes destino | Clase / propósito | Nombre objetivo aprobado | Bytes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| FK-001 | USER_CREDENTIAL | `user_id` | `yusay.app_user` | `user_id` | SIMPLE · existencia en USER | `fk_user_credential_app_user` | 27 |
| FK-002 | ADMINISTRATOR | `user_id` | `yusay.app_user` | `user_id` | SIMPLE · existencia en USER | `fk_administrator_app_user` | 25 |
| FK-003 | EMAIL_VERIFICATION_TOKEN | `user_id` | `yusay.app_user` | `user_id` | SIMPLE · existencia en USER | `fk_email_verification_token_app_user` | 36 |
| FK-004 | PASSWORD_RESET_TOKEN | `user_id` | `yusay.app_user` | `user_id` | SIMPLE · existencia en USER | `fk_password_reset_token_app_user` | 32 |
| FK-005 | INSTRUMENT_VERSION | `instrument_id` | `yusay.instrument` | `instrument_id` | SIMPLE · existencia en INSTRUMENT | `fk_instrument_version_instrument` | 32 |
| FK-006 | INSTRUMENT_VERSION_REFERENCE | `instrument_version_id` | `yusay.instrument_version` | `instrument_version_id` | SIMPLE · existencia en INSTRUMENT_VERSION | `fk_instrument_version_reference_instrument_version` | 50 |
| FK-007 | QUESTION | `instrument_version_id` | `yusay.instrument_version` | `instrument_version_id` | SIMPLE · existencia en INSTRUMENT_VERSION | `fk_question_instrument_version` | 30 |
| FK-008 | ANSWER_OPTION | `question_id` | `yusay.question` | `question_id` | SIMPLE · existencia en QUESTION | `fk_answer_option_question` | 25 |
| FK-009 | SCORING_DEFINITION | `instrument_version_id` | `yusay.instrument_version` | `instrument_version_id` | SIMPLE · existencia en INSTRUMENT_VERSION | `fk_scoring_definition_instrument_version` | 40 |
| FK-010 | SCORING_CONTRIBUTION | `instrument_version_id` | `yusay.scoring_definition` | `instrument_version_id` | SIMPLE · existencia en SCORING_DEFINITION | `fk_scoring_contribution_scoring_definition` | 42 |
| FK-011 | SCORING_CONTRIBUTION | `instrument_version_id, question_id` | `yusay.question` | `instrument_version_id, question_id` | COMPUESTA · pertenencia histórica | `fk_scoring_contribution_question_version` | 40 |
| FK-012 | SCORING_CONTRIBUTION | `question_id, option_id` | `yusay.answer_option` | `question_id, option_id` | COMPUESTA · pertenencia histórica | `fk_scoring_contribution_option_question` | 39 |
| FK-013 | ASSESSMENT_ATTEMPT | `user_id` | `yusay.app_user` | `user_id` | SIMPLE · existencia en USER | `fk_assessment_attempt_app_user` | 30 |
| FK-014 | ASSESSMENT_ATTEMPT | `instrument_id, instrument_version_id` | `yusay.instrument_version` | `instrument_id, instrument_version_id` | COMPUESTA · pertenencia histórica | `fk_assessment_attempt_instrument_version` | 40 |
| FK-015 | ANSWER | `attempt_id, instrument_version_id` | `yusay.assessment_attempt` | `attempt_id, instrument_version_id` | COMPUESTA · pertenencia histórica | `fk_answer_attempt_version` | 25 |
| FK-016 | ANSWER | `instrument_version_id, question_id` | `yusay.question` | `instrument_version_id, question_id` | COMPUESTA · pertenencia histórica | `fk_answer_question_version` | 26 |
| FK-017 | ANSWER | `question_id, option_id` | `yusay.answer_option` | `question_id, option_id` | COMPUESTA · pertenencia histórica | `fk_answer_selected_option` | 25 |
| FK-018 | INTERPRETATION | `instrument_version_id` | `yusay.instrument_version` | `instrument_version_id` | SIMPLE · existencia en INSTRUMENT_VERSION | `fk_interpretation_instrument_version` | 36 |
| FK-019 | ASSESSMENT_RESULT | `attempt_id` | `yusay.assessment_attempt` | `attempt_id` | SIMPLE · existencia en ASSESSMENT_ATTEMPT | `fk_assessment_result_attempt` | 28 |
| FK-020 | ASSESSMENT_RESULT | `attempt_id, instrument_version_id` | `yusay.assessment_attempt` | `attempt_id, instrument_version_id` | COMPUESTA · pertenencia histórica | `fk_assessment_result_attempt_version` | 36 |
| FK-021 | ASSESSMENT_RESULT | `instrument_version_id, interpretation_id` | `yusay.interpretation` | `instrument_version_id, interpretation_id` | COMPUESTA · pertenencia histórica | `fk_assessment_result_interpretation_version` | 43 |
| FK-022 | DIMENSION_VERSION | `dimension_id` | `yusay.dimension` | `dimension_id` | SIMPLE · existencia en DIMENSION | `fk_dimension_version_dimension` | 30 |
| FK-023 | DIMENSION_ANCHOR | `dimension_version_id` | `yusay.dimension_version` | `dimension_version_id` | SIMPLE · existencia en DIMENSION_VERSION | `fk_dimension_anchor_dimension_version` | 37 |
| FK-024 | CHECK_IN | `user_id` | `yusay.app_user` | `user_id` | SIMPLE · existencia en USER | `fk_check_in_app_user` | 20 |
| FK-025 | MEASUREMENT | `check_in_id` | `yusay.check_in` | `check_in_id` | SIMPLE · existencia en CHECK_IN | `fk_measurement_check_in` | 23 |
| FK-026 | MEASUREMENT | `dimension_id, dimension_version_id` | `yusay.dimension_version` | `dimension_id, dimension_version_id` | COMPUESTA · pertenencia histórica | `fk_measurement_dimension_version` | 32 |
| FK-027 | CHECK_IN_CONTEXT_TAG | `check_in_id` | `yusay.check_in` | `check_in_id` | SIMPLE · existencia en CHECK_IN | `fk_check_in_context_tag_check_in` | 32 |
| FK-028 | CHECK_IN_CONTEXT_TAG | `context_tag_id` | `yusay.context_tag` | `context_tag_id` | SIMPLE · existencia en CONTEXT_TAG | `fk_check_in_context_tag_context_tag` | 35 |
| FK-029 | INSTRUMENT_VERSION_COMPATIBILITY | `instrument_id, version_a_id` | `yusay.instrument_version` | `instrument_id, instrument_version_id` | COMPUESTA · pertenencia histórica · A | `fk_instrument_version_compatibility_version_a` | 45 |
| FK-030 | INSTRUMENT_VERSION_COMPATIBILITY | `instrument_id, version_b_id` | `yusay.instrument_version` | `instrument_id, instrument_version_id` | COMPUESTA · pertenencia histórica · B | `fk_instrument_version_compatibility_version_b` | 45 |
| FK-031 | DIMENSION_VERSION_COMPATIBILITY | `dimension_id, version_a_id` | `yusay.dimension_version` | `dimension_id, dimension_version_id` | COMPUESTA · pertenencia histórica · A | `fk_dimension_version_compatibility_version_a` | 44 |
| FK-032 | DIMENSION_VERSION_COMPATIBILITY | `dimension_id, version_b_id` | `yusay.dimension_version` | `dimension_id, dimension_version_id` | COMPUESTA · pertenencia histórica · B | `fk_dimension_version_compatibility_version_b` | 44 |
| FK-033 | RESOURCE_TOPIC | `resource_id` | `yusay.resource` | `resource_id` | SIMPLE · existencia en RESOURCE | `fk_resource_topic_resource` | 26 |
| FK-034 | RESOURCE_TOPIC | `topic_id` | `yusay.topic` | `topic_id` | SIMPLE · existencia en TOPIC | `fk_resource_topic_topic` | 23 |
| FK-035 | INSTRUMENT_TOPIC | `instrument_id` | `yusay.instrument` | `instrument_id` | SIMPLE · existencia en INSTRUMENT | `fk_instrument_topic_instrument` | 30 |
| FK-036 | INSTRUMENT_TOPIC | `topic_id` | `yusay.topic` | `topic_id` | SIMPLE · existencia en TOPIC | `fk_instrument_topic_topic` | 25 |
| FK-037 | INTERPRETATION_TOPIC | `interpretation_id` | `yusay.interpretation` | `interpretation_id` | SIMPLE · existencia en INTERPRETATION | `fk_interpretation_topic_interpretation` | 38 |
| FK-038 | INTERPRETATION_TOPIC | `topic_id` | `yusay.topic` | `topic_id` | SIMPLE · existencia en TOPIC | `fk_interpretation_topic_topic` | 29 |
| FK-039 | DIMENSION_TOPIC | `dimension_id` | `yusay.dimension` | `dimension_id` | SIMPLE · existencia en DIMENSION | `fk_dimension_topic_dimension` | 28 |
| FK-040 | DIMENSION_TOPIC | `topic_id` | `yusay.topic` | `topic_id` | SIMPLE · existencia en TOPIC | `fk_dimension_topic_topic` | 24 |
| FK-041 | AUDIT_EVENT | `actor_user_id` | `yusay.app_user` | `user_id` | SIMPLE · actor opcional desvinculable | `fk_audit_event_app_user` | 23 |


Se conservan las dos referencias de ASSESSMENT_RESULT a Attempt: `attempt` simple y `attempt_version` compuesta. No se elimina ninguna por aparente redundancia. En Compatibilidad, A/B distinguen FKs al mismo destino y conservan el padre común; no implican cronología. Answer y ScoringContribution preservan simultáneamente versión, pregunta y opción mediante sus referencias independientes.

## CHECK e índices: etiquetas sin selección física

Las fuentes aprueban reglas, no un inventario físico CHECK. Las siguientes son etiquetas posibles **si posteriormente se aprueba esa realización**; no afirman que deban crearse ni que protejan toda la política. Descomposición, comparadores y mecanismos siguen pendientes.

| Relación | Regla existente | Etiqueta candidata | Bytes | Fuente |
| --- | --- | --- | --- | --- |
| INSTRUMENT_VERSION | version > 0 | `ck_instrument_version_version_positive` | 38 | [Fuente](../logical-model-v1/13-dominios-logicos.md) |
| DIMENSION_VERSION | version > 0 | `ck_dimension_version_version_positive` | 37 | [Fuente](../logical-model-v1/13-dominios-logicos.md) |
| INSTRUMENT_VERSION_REFERENCE | reference_order > 0, no consecutividad | `ck_instrument_version_reference_reference_order_positive` | 56 | [Fuente](../logical-model-v1/13-dominios-logicos.md) |
| QUESTION | position > 0, no consecutividad | `ck_question_position_positive` | 29 | [Fuente](../logical-model-v1/13-dominios-logicos.md) |
| ANSWER_OPTION | position > 0, no consecutividad | `ck_answer_option_position_positive` | 34 | [Fuente](../logical-model-v1/13-dominios-logicos.md) |
| CHECK_IN | revision > 0, no prueba incremento | `ck_check_in_revision_positive` | 29 | [Fuente](../logical-model-v1/13-dominios-logicos.md) |
| INTERPRETATION | lower_bound ≤ upper_bound; inclusivos | `ck_interpretation_bounds` | 24 | [Fuente](../logical-model-v1/13-dominios-logicos.md) |
| DIMENSION_VERSION | min_value < max_value | `ck_dimension_version_bounds` | 27 | [Fuente](../logical-model-v1/13-dominios-logicos.md) |
| DIMENSION_VERSION | step > 0 | `ck_dimension_version_step_positive` | 34 | [Fuente](../logical-model-v1/13-dominios-logicos.md) |
| DIMENSION_VERSION | Extremo superior alcanzable mediante step | `ck_dimension_version_scale_endpoint` | 35 | [Fuente](../logical-model-v1/13-dominios-logicos.md) |
| CHECK_IN | Intervalo inclusivo original de 168 horas | `ck_check_in_recorded_window` | 27 | [Fuente](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) |
| EMAIL_VERIFICATION_TOKEN | 24 horas desde created_at; created_at < expires_at | `ck_email_verification_token_expiry` | 34 | [Fuente](../logical-model-v1/04-diccionario-datos/01-identidad.md) |
| PASSWORD_RESET_TOKEN | 30 minutos desde created_at; created_at < expires_at | `ck_password_reset_token_expiry` | 30 | [Fuente](../logical-model-v1/04-diccionario-datos/01-identidad.md) |
| ASSESSMENT_ATTEMPT | 720 horas desde started_at | `ck_assessment_attempt_expiry` | 28 | [Fuente](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) |
| INSTRUMENT_VERSION_COMPATIBILITY | version_a_id < version_b_id, comparación física pendiente | `ck_instrument_version_compatibility_canonical_pair` | 50 | [Fuente](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) |
| DIMENSION_VERSION_COMPATIBILITY | version_a_id < version_b_id, comparación física pendiente | `ck_dimension_version_compatibility_canonical_pair` | 49 | [Fuente](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) |
| RESOURCE | title obligatorio y no vacío desde DRAFT | `ck_resource_title_nonempty` | 26 | [Fuente](../logical-model-v1/04-diccionario-datos/05-contenido.md) |


No se fuerza una etiqueta única para contenido publicado, enumeraciones, nulabilidad o perfiles de metadata: granularidad física y nombres concretos **NO VERIFICADO**, con políticas verificadas en D05/D06 y dominios. No se inventan CHECK para sumas entre Answers, cobertura de interpretaciones, mínimos de hijos, autorización, retención o incremento transaccional: su asignación pertenece a OQ-PHYS-007.

AK y URA son conceptos lógicos; UNIQUE sería una realización, sin borrar su clasificación. Un índice único parcial cubre un subconjunto y no sustituye la unicidad completa que permite referencias históricas. Para FK PostgreSQL exige claves únicas/primarias admisibles o un índice único **no parcial**. [Condiciones referenciales](https://www.postgresql.org/docs/18/sql-createtable.html).

Se evaluaron `uxp_` (único y parcial), `uidx_` (único sin distinguir parcialidad) y `uq_` (confundible con restricción). `uxp_` queda aprobado para índices únicos parciales y `idx_` para índices independientes generales; `uidx_` y el uso ambiguo de `uq_` para parciales no fueron seleccionados. Índices que respalden PK/UNIQUE conservarían el nombre de la restricción, evitando duplicarlos mediante otro `idx_`. Las máximas por estado siguen siendo invariantes, no índices seleccionados. [Índices parciales](https://www.postgresql.org/docs/18/indexes-partial.html).

## Colisiones y ambigüedades

**B no presenta colisiones por tabla, minúsculas o truncamiento en sus 32 tablas, 147 columnas y 88 nombres de claves/referencias.** Las 17 etiquetas CHECK tampoco chocan con ese registro. No se afirma ausencia de objetos externos en una instalación no inspeccionada. Las colisiones siguientes pertenecen a alternativas de construcción de nombres, no a objetos implementados.

| ID | Clasificación / hallazgo | Evidencia | Tratamiento y estado actual |
| --- | --- | --- | --- |
| N-001 | Riesgo real de contexto sintáctico | `user` reservado; `yusay.user` admite nombre de relación calificado, `user` aislado no | B aprobado: `app_user`; alternativas con `user` no seleccionadas. |
| N-002 | Colisión confirmada en FK solo por destino | Dos `fk_assessment_result_assessment_attempt` en una tabla | Distinguir `attempt` / `attempt_version`. |
| N-003 | Colisión confirmada en FK solo por destino | Dos FKs por tabla de Compatibilidad hacia la misma versión | Distinguir `version_a` / `version_b`. |
| N-004 | Colisión confirmada tras truncamiento | Alternativa extensa pierde A/B en ambas tablas de Compatibilidad | Propósito corto; componentes completos en matriz. |
| N-005 | Riesgo real de longitud futura | Dos URA de 61 bytes | F resuelto: nombres exactos, sin abreviación ni prefijos/sufijos. |
| N-006 | Ambigüedad de estilo entre tablas | `fk_answer_option_question` puede nombrar dos referencias distintas | B usa `fk_answer_selected_option` para ANSWER; duplicación entre tablas sería válida, pero confusa. |
| N-007 | Organización resuelta; límite documental | Seis módulos dentro de un esquema no son seis espacios físicos | Conservar módulos en correspondencias documentales. |
| N-008 | Riesgo real condicionado a entorno | search_path depende de orden y permisos de creación | Calificación aprobada; entorno/privilegios siguen en OQ-PHYS-010. |
| N-009 | NO VERIFICADO; sin conflicto comprobado | Objetos preexistentes, herramientas y configuración futura | Revalidar cuando existan. |


### Longitud en la alternativa extensa

Se evaluó `fk_<tabla>_<destino>_<columnas_origen>` con nombres fuente. **11 de 41** exceden 63 bytes; dos grupos A/B colisionan al truncarse. No se propone usar estos nombres ni se ejecuta DDL.

| Origen / componentes | Alternativa extensa | Bytes | Efecto |
| --- | --- | --- | --- |
| INSTRUMENT_VERSION_REFERENCE / `instrument_version_id` | `fk_instrument_version_reference_instrument_version_instrument_version_id` | 72 | Truncamiento; sin colisión confirmada |
| SCORING_CONTRIBUTION / `instrument_version_id` | `fk_scoring_contribution_scoring_definition_instrument_version_id` | 64 | Truncamiento; sin colisión confirmada |
| SCORING_CONTRIBUTION / `instrument_version_id, question_id` | `fk_scoring_contribution_question_instrument_version_id_question_id` | 66 | Truncamiento; sin colisión confirmada |
| ASSESSMENT_ATTEMPT / `instrument_id, instrument_version_id` | `fk_assessment_attempt_instrument_version_instrument_id_instrument_version_id` | 76 | Truncamiento; sin colisión confirmada |
| ASSESSMENT_RESULT / `attempt_id, instrument_version_id` | `fk_assessment_result_assessment_attempt_attempt_id_instrument_version_id` | 72 | Truncamiento; sin colisión confirmada |
| ASSESSMENT_RESULT / `instrument_version_id, interpretation_id` | `fk_assessment_result_interpretation_instrument_version_id_interpretation_id` | 75 | Truncamiento; sin colisión confirmada |
| MEASUREMENT / `dimension_id, dimension_version_id` | `fk_measurement_dimension_version_dimension_id_dimension_version_id` | 66 | Truncamiento; sin colisión confirmada |
| INSTRUMENT_VERSION_COMPATIBILITY / `instrument_id, version_a_id` | `fk_instrument_version_compatibility_instrument_version_instrument_id_version_a_id` | 81 | Truncamiento y colisión A/B |
| INSTRUMENT_VERSION_COMPATIBILITY / `instrument_id, version_b_id` | `fk_instrument_version_compatibility_instrument_version_instrument_id_version_b_id` | 81 | Truncamiento y colisión A/B |
| DIMENSION_VERSION_COMPATIBILITY / `dimension_id, version_a_id` | `fk_dimension_version_compatibility_dimension_version_dimension_id_version_a_id` | 78 | Truncamiento y colisión A/B |
| DIMENSION_VERSION_COMPATIBILITY / `dimension_id, version_b_id` | `fk_dimension_version_compatibility_dimension_version_dimension_id_version_b_id` | 78 | Truncamiento y colisión A/B |


Prefijos de 63 bytes idénticos comprobados en los pares:

- Instrument: `fk_instrument_version_compatibility_instrument_version_instrume`.
- Dimension: `fk_dimension_version_compatibility_dimension_version_dimension_`.

El esquema se conserva como contexto, no concatenado al nombre de restricción. Los nombres actuales completos B tienen máximo 61 bytes; tablas máximo 32 y columnas máximo 25. Ninguno cambia al normalizarse a minúsculas o truncarse a 63.

## Evaluación especial de yusay.user

Sería incorrecto declarar que `yusay.user` siempre provoca error: aunque USER es reservado, la gramática de relación calificada admite tras el punto una etiqueta que incluye palabras reservadas. Esta conclusión se basa en qualified_name, indirection, attr_name y ColLabel del [parser oficial](https://raw.githubusercontent.com/postgres/postgres/REL_18_STABLE/src/backend/parser/gram.y), no en una prueba sobre servidor.

El riesgo está en `user` sin calificar en contextos de nombre de relación, o en herramientas que eliminan calificación/generan contextos diferentes. search_path no cambia la categoría reservada. La tabla de aplicación no es un rol del motor ni la expresión de identidad de sesión.

| Alternativa | Correspondencia | Ventaja | Coste / riesgo |
| --- | --- | --- | --- |
| A: calificado literal | USER → `yusay.user` | Correspondencia literal viable en contexto de relación calificada | Dependencia de sintaxis contextual; nombre aislado requiere comillas; herramientas NO VERIFICADO. |
| B: excepción física | USER → `yusay.app_user` | Nombre ordinario con o sin calificación | Excepción explícita; conserva `user_id` y la identidad lógica. |
| C: comillas para la excepción | USER → `yusay."user"` | Literalidad mediante identificador delimitado | Disciplina de quoting; excepción a nombres sin comillas. |


**B aprobado.** `yusay.app_user` evita depender de contextos sintácticos específicos o comillas sin cambiar USER ni `user_id`. A/C permanecen como alternativas históricas no seleccionadas.

## Esquema único, calificación y search_path

Un esquema `yusay` reúne las 32 relaciones y permite referencias entre módulos. Los seis módulos siguen como agrupación documental del dominio; no generan separación física de permisos, propiedad, aislamiento o despliegue. Esto no concede a ADMINISTRATOR acceso privado ni modifica auditoría/desvinculación.

Se evaluaron esquema único, múltiples esquemas por módulo y esquema único con prefijos de módulo. Los últimos separan nominalmente, pero cambian nombres, alargan identificadores y agregan organización no exigida por las fuentes. No se comprobó conflicto que obligue a adoptarlos. Se aprueba **esquema único sin prefijos de módulo**. Esquemas separados y prefijos de módulo quedan como alternativas históricas no seleccionadas.

Se aprueban referencias explícitas `yusay.<tabla>` en documentación y referencias físicas futuras para identificar destinos y reducir dependencia del search_path de tablas. No configura el entorno ni elimina resolución de funciones, tipos u operadores. No se elige search_path ni matriz de permisos. [Esquemas y resolución](https://www.postgresql.org/docs/18/ddl-schemas.html).

Ese manual advierte que search_path confía en quienes pueden crear objetos en sus esquemas: homónimos pueden alterar resolución. Es riesgo condicionado a configuración/privilegios futuros, no fallo actual probado; coordinación con OQ-PHYS-010. Las restricciones se identifican por **tabla calificada + nombre local**, no como si `yusay.fk_x` bastara. Los índices se ubican en el esquema de su tabla; cada operación futura tiene reglas propias de calificación.

## Resoluciones aprobadas y autoridad

- **ID:** OQ-PHYS-002.
- **Status:** RESOLVED.
- **Fecha de aprobación:** 2026-10-07; fecha de la decisión, no de creación del archivo.
- **Autoridad:** responsable del proyecto.
- **Alcance:** convenciones de nombres y organización de objetos del diseño físico v1.0.
- **Historial:** revisión previa y seis propuestas A..F evaluadas; aprobación expresa conjunta del escenario B y políticas siguientes. Las alternativas no seleccionadas se conservan en sus secciones.

### OQ-PHYS-002-A

- **ID:** OQ-PHYS-002-A.
- **Tema:** Organización física.
- **Status:** RESOLVED.
- **Fecha de aprobación:** 2026-10-07.
- **Autoridad:** responsable del proyecto.
- **Decisión aprobada:** Un único esquema de aplicación `yusay` para las 32 tablas; sin esquemas separados ni prefijos de módulo. Se mantienen los seis módulos como organización lógica/documental.
- **Impacto y límite:** 32 relaciones y 02/03; roles, propietarios, permisos y despliegue no seleccionados.

### OQ-PHYS-002-B

- **ID:** OQ-PHYS-002-B.
- **Tema:** Nombre físico de USER.
- **Status:** RESOLVED.
- **Fecha de aprobación:** 2026-10-07.
- **Autoridad:** responsable del proyecto.
- **Decisión aprobada:** USER → `yusay.app_user`; `user_id` intacto. Excepción de nomenclatura física para evitar la dependencia contextual de la palabra reservada user.
- **Impacto y límite:** R-001, PK/AK de USER y FKs hacia USER; las demás columnas y relaciones lógicas no cambian.

### OQ-PHYS-002-C

- **ID:** OQ-PHYS-002-C.
- **Tema:** Convenciones generales.
- **Status:** RESOLVED.
- **Fecha de aprobación:** 2026-10-07.
- **Autoridad:** responsable del proyecto.
- **Decisión aprobada:** Singular, minúsculas, snake_case y sin comillas; 147 columnas literales. Patrones `pk_`, `uq_`, `fk_`, `ck_`, `idx_` según la tabla de convenciones. Las seis URA usan `ref_`. Se conservan componentes y orden. Los 32 PK, 9 AK, 6 URA y 41 FK del escenario B son nombres objetivo aprobados.
- **Impacto y límite:** 88 nombres exactos; no autoriza implementación ni aprueba los CHECK ilustrativos como inventario físico.

### OQ-PHYS-002-D

- **ID:** OQ-PHYS-002-D.
- **Tema:** Índices únicos parciales.
- **Status:** RESOLVED.
- **Fecha de aprobación:** 2026-10-07.
- **Autoridad:** responsable del proyecto.
- **Decisión aprobada:** Patrón `uxp_<tabla>_<proposito>` aprobado, diferenciando parcialidad de UNIQUE convencional e índices generales.
- **Impacto y límite:** Solo nomenclatura; existencia, columnas y predicados de índices permanecen pendientes.

### OQ-PHYS-002-E

- **ID:** OQ-PHYS-002-E.
- **Tema:** Referencias calificadas.
- **Status:** RESOLVED.
- **Fecha de aprobación:** 2026-10-07.
- **Autoridad:** responsable del proyecto.
- **Decisión aprobada:** Tablas calificadas con `yusay.` en documentación y referencias físicas futuras. Restricciones identificadas por tabla calificada y nombre local, respetando su espacio de nombres.
- **Impacto y límite:** Reduce dependencia de search_path para tablas; configuración, roles y privilegios siguen en OQ-PHYS-010.

### OQ-PHYS-002-F

- **ID:** OQ-PHYS-002-F.
- **Tema:** URA largas.
- **Status:** RESOLVED.
- **Fecha de aprobación:** 2026-10-07.
- **Autoridad:** responsable del proyecto.
- **Decisión aprobada:** Conservar exactamente las seis URA del escenario B, incluidas las dos de 61 bytes. Sin abreviaciones, prefijos o sufijos; no depender del truncamiento.
- **Impacto y límite:** Cualquier modificación futura exige documentarse y aprobarse expresamente.

## Límites y continuidad

No se encontraron contradicciones estructurales entre las fuentes contrastadas. Las menciones históricas a PostgreSQL PROVISIONALLY ACCEPTED en la capa lógica no se reescriben: ADR-001 ACCEPTED y OQ-PHYS-001 RESOLVED son decisiones posteriores registradas en la capa física.

**Alcance histórico de OQ-PHYS-002:** su resolución aprobó organización y nombres, sin aprobar por sí sola tipos o mecanismos. La consolidación posterior de OQ-PHYS-003..010, registrada a continuación, resuelve las políticas arquitectónicas; las expresiones y mecanismos residuales quedan en MP-PHYS. Objetos preexistentes y compatibilidad de herramientas siguen NO VERIFICADOS. No se añaden relaciones ni tecnologías de aplicación.

**OQ-PHYS-002 y OQ-PHYS-002-A..F están RESOLVED; diseño físico IN PROGRESS.** Modelo lógico APPROVED / FROZEN, conceptual y ADR-001 permanecen intactos. No se ejecuta implementación ni se crean commits.

[Índice físico](00-indice.md) · [OQ-PHYS-002](01-contexto-y-alcance.md#oq-phys-002).


## Corrección documental y verificación de formalización

Se corrigieron 26 etiquetas visibles D05 para que coincidan con los archivos enlazados: Identidad D01, Evaluaciones D02, Seguimiento D03, Compatibilidad D04 y Auditoría D06. Las seis etiquetas D05 de Contenido permanecen correctas. Rutas, anchors e IDs R-001..R-032 se conservan; no se editaron diccionarios.

Verificación documental: 32 tablas objetivo, 147 columnas literales, 32 PK/9 AK/6 URA/41 FK (13 compuestas), 88 nombres exactamente iguales a B previo; sin colisiones por espacio ni nombres superiores a 63 bytes. Las seis URA se conservan. No son pruebas ejecutadas de un esquema. Riesgos residuales: margen de dos bytes en URA largas y entorno/objetos/herramientas todavía no verificados; no justifican renombrar silenciosamente objetivos aprobados.

## OQ-PHYS-003

**Status: RESOLVED en alcance arquitectónico. Diseño físico: IN PROGRESS.** Consolidación aprobada por el responsable del proyecto: 2026-10-07. Se adopta uuid nativo, UUID v4 y generación centralizada en PostgreSQL para las 16 raíces; las cinco identidades compartidas reutilizan valores y las once PK compuestas conservan componentes. Orden canónico: los 16 bytes sin signo. Las alternativas previas se conservan expresamente como antecedentes no seleccionados; no son decisiones vigentes. OQ-PHYS-001/002 y los 88 nombres permanecen intactos.

### Alcance y fuentes de la auditoría de identificadores

Se amplía este entregable 02, previsto para decisiones/mapeo, evitando otro documento paralelo. Las matrices de nombres anteriores siguen siendo el registro aprobado; esta sección añade clasificación y propagación, no repite las 147 columnas ni redefine claves.

Fuentes inspeccionadas: [inventario](../logical-model-v1/02-inventario-relaciones.md), [modelo lógico](../logical-model-v1/03-modelo-logico.md), [especificación maestra](../logical-model-v1/especificacion-maestra-v1.0.md), los seis diccionarios D01..D06 enlazados en la matriz R, [integridad](../logical-model-v1/05-integridad-referencial.md), [transacciones](../logical-model-v1/07-transacciones-y-concurrencia.md), [privacidad](../logical-model-v1/08-privacidad-eliminacion-retencion.md), [dominios](../logical-model-v1/13-dominios-logicos.md), [índice físico](00-indice.md), [contexto](01-contexto-y-alcance.md) y [ADR-001](../../06-decisions/ADR-001-database-engine.md). Se conservan 32 relaciones, 147 atributos, 32 PK, 9 AK, 6 URA y 41 FK (13 compuestas). Nombres y orden de atributos coinciden entre fuente y diccionarios; todos los destinos referenciales poseen clave documentada.

Identificadores opacos, estables, sin significado de negocio y con unicidad según cada clave son **obligaciones lógicas**. Opacidad semántica no significa necesariamente impredecibilidad criptográfica: BIGINT secuencial no es por sí mismo contradicción estructural, aunque revele orden de asignación. Ninguna alternativa cambia la autorización.

### Clasificación comprobada de las 32 PK

Resultado: **16 identificadores propios, 5 claves compartidas y 11 compuestas**. Las primeras son las únicas raíces que generan identidad nueva. Compartidas reutilizan el identificador de otro registro; compuestas combinan referencias y, en dos casos, un valor de dominio. No se agrega columna id ni se convierte un componente FK en identidad nueva. Los FK-nnn remiten al inventario aprobado anterior.

| ID existente | Relación lógica | Tabla aprobada | PK en orden | Clase | Valores nuevos | Origen de componentes | Dependencias FK de la fila | Alternativas físicas | Observaciones |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PK-001 | USER | `yusay.app_user` | `user_id` | PROPIO | Sí: identidad nueva | user_id → IDF-01 (raíz) | Sin FK | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-002 | USER_CREDENTIAL | `yusay.user_credential` | `user_id` | COMPARTIDA | No identidad nueva | user_id → IDF-01 / USER.user_id | FK-001 | Tipo de familia origen; sin generador propio | Copiar valor exacto; no regenerar |
| PK-003 | ADMINISTRATOR | `yusay.administrator` | `user_id` | COMPARTIDA | No identidad nueva | user_id → IDF-01 / USER.user_id | FK-002 | Tipo de familia origen; sin generador propio | Copiar valor exacto; no regenerar |
| PK-004 | EMAIL_VERIFICATION_TOKEN | `yusay.email_verification_token` | `verification_token_id` | PROPIO | Sí: identidad nueva | verification_token_id → IDF-02 (raíz) | FK-003 | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-005 | PASSWORD_RESET_TOKEN | `yusay.password_reset_token` | `reset_token_id` | PROPIO | Sí: identidad nueva | reset_token_id → IDF-03 (raíz) | FK-004 | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-006 | INSTRUMENT | `yusay.instrument` | `instrument_id` | PROPIO | Sí: identidad nueva | instrument_id → IDF-04 (raíz) | Sin FK | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-007 | INSTRUMENT_VERSION | `yusay.instrument_version` | `instrument_version_id` | PROPIO | Sí: identidad nueva | instrument_version_id → IDF-05 (raíz) | FK-005 | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-008 | INSTRUMENT_VERSION_REFERENCE | `yusay.instrument_version_reference` | `instrument_version_id, reference_order` | COMPUESTA | No identidad nueva; asignar reference_order del dominio | instrument_version_id → IDF-05 / INSTRUMENT_VERSION.instrument_version_id; reference_order → valor de dominio | FK-006 | Tipo de familia origen; sin generador propio; valor entero en OQ-PHYS-005 | Conservar orden; no surrogate; reference_order positivo, no necesariamente consecutivo |
| PK-009 | QUESTION | `yusay.question` | `question_id` | PROPIO | Sí: identidad nueva | question_id → IDF-06 (raíz) | FK-007 | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-010 | ANSWER_OPTION | `yusay.answer_option` | `option_id` | PROPIO | Sí: identidad nueva | option_id → IDF-07 (raíz) | FK-008 | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-011 | SCORING_DEFINITION | `yusay.scoring_definition` | `instrument_version_id` | COMPARTIDA | No identidad nueva | instrument_version_id → IDF-05 / INSTRUMENT_VERSION.instrument_version_id | FK-009 | Tipo de familia origen; sin generador propio | Copiar valor exacto; no regenerar |
| PK-012 | SCORING_CONTRIBUTION | `yusay.scoring_contribution` | `option_id` | COMPARTIDA | No identidad nueva | option_id → IDF-07 / ANSWER_OPTION.option_id | FK-010, FK-011, FK-012 | Tipo de familia origen; sin generador propio | Copiar valor exacto; no regenerar; Question/versión y opción/pregunta simultáneas |
| PK-013 | ASSESSMENT_ATTEMPT | `yusay.assessment_attempt` | `attempt_id` | PROPIO | Sí: identidad nueva | attempt_id → IDF-08 (raíz) | FK-013, FK-014 | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-014 | ANSWER | `yusay.answer` | `attempt_id, question_id` | COMPUESTA | No identidad nueva | attempt_id → IDF-08 / ASSESSMENT_ATTEMPT.attempt_id; question_id → IDF-06 / QUESTION.question_id | FK-015, FK-016, FK-017 | Tipo de familia origen; sin generador propio | Conservar orden; no surrogate |
| PK-015 | INTERPRETATION | `yusay.interpretation` | `interpretation_id` | PROPIO | Sí: identidad nueva | interpretation_id → IDF-09 (raíz) | FK-018 | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-016 | ASSESSMENT_RESULT | `yusay.assessment_result` | `attempt_id` | COMPARTIDA | No identidad nueva | attempt_id → IDF-08 / ASSESSMENT_ATTEMPT.attempt_id | FK-019, FK-020, FK-021 | Tipo de familia origen; sin generador propio | Copiar valor exacto; no regenerar; referencia simple y compuesta de Attempt |
| PK-017 | DIMENSION | `yusay.dimension` | `dimension_id` | PROPIO | Sí: identidad nueva | dimension_id → IDF-10 (raíz) | Sin FK | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-018 | DIMENSION_VERSION | `yusay.dimension_version` | `dimension_version_id` | PROPIO | Sí: identidad nueva | dimension_version_id → IDF-11 (raíz) | FK-022 | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-019 | DIMENSION_ANCHOR | `yusay.dimension_anchor` | `dimension_version_id, value` | COMPUESTA | No identidad nueva; asignar value del dominio | dimension_version_id → IDF-11 / DIMENSION_VERSION.dimension_version_id; value → valor de dominio | FK-023 | Tipo de familia origen; sin generador propio; valor entero en OQ-PHYS-005 | Conservar orden; no surrogate; value es valor de escala |
| PK-020 | CHECK_IN | `yusay.check_in` | `check_in_id` | PROPIO | Sí: identidad nueva | check_in_id → IDF-12 (raíz) | FK-024 | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-021 | MEASUREMENT | `yusay.measurement` | `check_in_id, dimension_id` | COMPUESTA | No identidad nueva | check_in_id → IDF-12 / CHECK_IN.check_in_id; dimension_id → IDF-10 / DIMENSION.dimension_id | FK-025, FK-026 | Tipo de familia origen; sin generador propio | Conservar orden; no surrogate |
| PK-022 | CONTEXT_TAG | `yusay.context_tag` | `context_tag_id` | PROPIO | Sí: identidad nueva | context_tag_id → IDF-13 (raíz) | Sin FK | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-023 | CHECK_IN_CONTEXT_TAG | `yusay.check_in_context_tag` | `check_in_id, context_tag_id` | COMPUESTA | No identidad nueva | check_in_id → IDF-12 / CHECK_IN.check_in_id; context_tag_id → IDF-13 / CONTEXT_TAG.context_tag_id | FK-027, FK-028 | Tipo de familia origen; sin generador propio | Conservar orden; no surrogate |
| PK-024 | INSTRUMENT_VERSION_COMPATIBILITY | `yusay.instrument_version_compatibility` | `version_a_id, version_b_id` | COMPUESTA | No identidad nueva | version_a_id → IDF-05 / INSTRUMENT_VERSION.instrument_version_id; version_b_id → IDF-05 / INSTRUMENT_VERSION.instrument_version_id | FK-029, FK-030 | Tipo de familia origen; sin generador propio | Conservar orden; no surrogate; mismo padre, par canónico |
| PK-025 | DIMENSION_VERSION_COMPATIBILITY | `yusay.dimension_version_compatibility` | `version_a_id, version_b_id` | COMPUESTA | No identidad nueva | version_a_id → IDF-11 / DIMENSION_VERSION.dimension_version_id; version_b_id → IDF-11 / DIMENSION_VERSION.dimension_version_id | FK-031, FK-032 | Tipo de familia origen; sin generador propio | Conservar orden; no surrogate; mismo padre, par canónico |
| PK-026 | TOPIC | `yusay.topic` | `topic_id` | PROPIO | Sí: identidad nueva | topic_id → IDF-14 (raíz) | Sin FK | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-027 | RESOURCE | `yusay.resource` | `resource_id` | PROPIO | Sí: identidad nueva | resource_id → IDF-15 (raíz) | Sin FK | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |
| PK-028 | RESOURCE_TOPIC | `yusay.resource_topic` | `resource_id, topic_id` | COMPUESTA | No identidad nueva | resource_id → IDF-15 / RESOURCE.resource_id; topic_id → IDF-14 / TOPIC.topic_id | FK-033, FK-034 | Tipo de familia origen; sin generador propio | Conservar orden; no surrogate |
| PK-029 | INSTRUMENT_TOPIC | `yusay.instrument_topic` | `instrument_id, topic_id` | COMPUESTA | No identidad nueva | instrument_id → IDF-04 / INSTRUMENT.instrument_id; topic_id → IDF-14 / TOPIC.topic_id | FK-035, FK-036 | Tipo de familia origen; sin generador propio | Conservar orden; no surrogate |
| PK-030 | INTERPRETATION_TOPIC | `yusay.interpretation_topic` | `interpretation_id, topic_id` | COMPUESTA | No identidad nueva | interpretation_id → IDF-09 / INTERPRETATION.interpretation_id; topic_id → IDF-14 / TOPIC.topic_id | FK-037, FK-038 | Tipo de familia origen; sin generador propio | Conservar orden; no surrogate |
| PK-031 | DIMENSION_TOPIC | `yusay.dimension_topic` | `dimension_id, topic_id` | COMPUESTA | No identidad nueva | dimension_id → IDF-10 / DIMENSION.dimension_id; topic_id → IDF-14 / TOPIC.topic_id | FK-039, FK-040 | Tipo de familia origen; sin generador propio | Conservar orden; no surrogate |
| PK-032 | AUDIT_EVENT | `yusay.audit_event` | `audit_event_id` | PROPIO | Sí: identidad nueva | audit_event_id → IDF-16 (raíz) | FK-041 | uuid / v4; ver familia | Asignación no demuestra persistencia ni autorización |


En claves propias, las FKs de la fila no determinan el ID nuevo: por ejemplo Question necesita su versión, pero question_id es identidad propia. En claves compartidas sí determinan el valor reutilizado. Las 11 compuestas no necesitan generador opaco; reference_order y DIMENSION_ANCHOR.value son los **dos componentes de PK que no son IDs opacos**. El primero es entero positivo sin consecutividad; el segundo es entero de escala, cuyo dominio se mantiene y no se fija aquí su tipo físico. Ni position, version ni revision se confunden con un generador de identidad.

### Familias de representación referencial

Se identificaron **16 familias y 62 ocurrencias de columnas de identidad**. IDF son etiquetas analíticas, no nuevos dominios/atributos. El agrupamiento deriva de las referencias aprobadas, incluida actor_user_id y los extremos A/B de Compatibilidad; ninguna columna terminada en _id queda sin familia. Cada familia debe conservar exactamente la política física elegida en todos sus usos. Familias diferentes pueden elegir tipos distintos, si se aprueba y justifica, sin crear unicidad global entre tablas. Una política uniforme no mezcla las identidades de dos familias.

| Familia | Raíz que genera | Ocurrencias | Propagación obligatoria | Alternativas evaluadas |
| --- | --- | --- | --- | --- |
| IDF-01 | `yusay.app_user.user_id` | 8 | `yusay.app_user.user_id`; `yusay.user_credential.user_id`; `yusay.administrator.user_id`; `yusay.email_verification_token.user_id`; `yusay.password_reset_token.user_id`; `yusay.assessment_attempt.user_id`; `yusay.check_in.user_id`; `yusay.audit_event.actor_user_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-02 | `yusay.email_verification_token.verification_token_id` | 1 | `yusay.email_verification_token.verification_token_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-03 | `yusay.password_reset_token.reset_token_id` | 1 | `yusay.password_reset_token.reset_token_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-04 | `yusay.instrument.instrument_id` | 5 | `yusay.instrument.instrument_id`; `yusay.instrument_version.instrument_id`; `yusay.assessment_attempt.instrument_id`; `yusay.instrument_version_compatibility.instrument_id`; `yusay.instrument_topic.instrument_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-05 | `yusay.instrument_version.instrument_version_id` | 11 | `yusay.instrument_version.instrument_version_id`; `yusay.instrument_version_reference.instrument_version_id`; `yusay.question.instrument_version_id`; `yusay.scoring_definition.instrument_version_id`; `yusay.scoring_contribution.instrument_version_id`; `yusay.assessment_attempt.instrument_version_id`; `yusay.answer.instrument_version_id`; `yusay.interpretation.instrument_version_id`; `yusay.assessment_result.instrument_version_id`; `yusay.instrument_version_compatibility.version_a_id`; `yusay.instrument_version_compatibility.version_b_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-06 | `yusay.question.question_id` | 4 | `yusay.question.question_id`; `yusay.answer_option.question_id`; `yusay.scoring_contribution.question_id`; `yusay.answer.question_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-07 | `yusay.answer_option.option_id` | 3 | `yusay.answer_option.option_id`; `yusay.scoring_contribution.option_id`; `yusay.answer.option_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-08 | `yusay.assessment_attempt.attempt_id` | 3 | `yusay.assessment_attempt.attempt_id`; `yusay.answer.attempt_id`; `yusay.assessment_result.attempt_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-09 | `yusay.interpretation.interpretation_id` | 3 | `yusay.interpretation.interpretation_id`; `yusay.assessment_result.interpretation_id`; `yusay.interpretation_topic.interpretation_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-10 | `yusay.dimension.dimension_id` | 5 | `yusay.dimension.dimension_id`; `yusay.dimension_version.dimension_id`; `yusay.measurement.dimension_id`; `yusay.dimension_version_compatibility.dimension_id`; `yusay.dimension_topic.dimension_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-11 | `yusay.dimension_version.dimension_version_id` | 5 | `yusay.dimension_version.dimension_version_id`; `yusay.dimension_anchor.dimension_version_id`; `yusay.measurement.dimension_version_id`; `yusay.dimension_version_compatibility.version_a_id`; `yusay.dimension_version_compatibility.version_b_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-12 | `yusay.check_in.check_in_id` | 3 | `yusay.check_in.check_in_id`; `yusay.measurement.check_in_id`; `yusay.check_in_context_tag.check_in_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-13 | `yusay.context_tag.context_tag_id` | 2 | `yusay.context_tag.context_tag_id`; `yusay.check_in_context_tag.context_tag_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-14 | `yusay.topic.topic_id` | 5 | `yusay.topic.topic_id`; `yusay.resource_topic.topic_id`; `yusay.instrument_topic.topic_id`; `yusay.interpretation_topic.topic_id`; `yusay.dimension_topic.topic_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-15 | `yusay.resource.resource_id` | 2 | `yusay.resource.resource_id`; `yusay.resource_topic.resource_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |
| IDF-16 | `yusay.audit_event.audit_event_id` | 1 | `yusay.audit_event.audit_event_id` | uuid aprobado; raíz v4 centralizada; usos heredan valor |


UUID v4 y v7 son **algoritmos/versiones de valores del mismo tipo uuid**, no dos tipos SQL diferentes. Cambiar de generador dentro de una familia exigiría una política explícita y revisión; no se deduce que el tipo uuid obligue automáticamente a v4 o v7. Las cinco compartidas heredan el valor de su raíz, no generan otro de la misma versión.

### Propagación de las 41 FK

Esta matriz referencia los FK-nnn ya documentados con componentes, destinos y nombres aprobados. Añade solo las familias por posición y la clave de destino: **28 simples, 13 compuestas, 54 correspondencias de componentes**. La elección aprobada de uuid para una raíz propaga su representación a todos estos usos; no se admiten conversiones con pérdida, cambio de signo, mezcla de formatos o valores regenerados.

| FK existente | Tabla origen | Clase | Familias de componentes en orden | Tabla destino | Clave destino | Condición conservada |
| --- | --- | --- | --- | --- | --- | --- |
| FK-001 | `yusay.user_credential` | SIMPLE | IDF-01 (mismo orden) | `yusay.app_user` | PK-001 | Componentes obligatorios; valores existentes |
| FK-002 | `yusay.administrator` | SIMPLE | IDF-01 (mismo orden) | `yusay.app_user` | PK-001 | Componentes obligatorios; valores existentes |
| FK-003 | `yusay.email_verification_token` | SIMPLE | IDF-01 (mismo orden) | `yusay.app_user` | PK-001 | Componentes obligatorios; valores existentes |
| FK-004 | `yusay.password_reset_token` | SIMPLE | IDF-01 (mismo orden) | `yusay.app_user` | PK-001 | Componentes obligatorios; valores existentes |
| FK-005 | `yusay.instrument_version` | SIMPLE | IDF-04 (mismo orden) | `yusay.instrument` | PK-006 | Componentes obligatorios; valores existentes |
| FK-006 | `yusay.instrument_version_reference` | SIMPLE | IDF-05 (mismo orden) | `yusay.instrument_version` | PK-007 | Componentes obligatorios; valores existentes |
| FK-007 | `yusay.question` | SIMPLE | IDF-05 (mismo orden) | `yusay.instrument_version` | PK-007 | Componentes obligatorios; valores existentes |
| FK-008 | `yusay.answer_option` | SIMPLE | IDF-06 (mismo orden) | `yusay.question` | PK-009 | Componentes obligatorios; valores existentes |
| FK-009 | `yusay.scoring_definition` | SIMPLE | IDF-05 (mismo orden) | `yusay.instrument_version` | PK-007 | Componentes obligatorios; valores existentes |
| FK-010 | `yusay.scoring_contribution` | SIMPLE | IDF-05 (mismo orden) | `yusay.scoring_definition` | PK-011 | Componentes obligatorios; valores existentes |
| FK-011 | `yusay.scoring_contribution` | COMPUESTA | IDF-05 → posición; IDF-06 (mismo orden) | `yusay.question` | URA-002 | Componentes obligatorios; valores existentes |
| FK-012 | `yusay.scoring_contribution` | COMPUESTA | IDF-06 → posición; IDF-07 (mismo orden) | `yusay.answer_option` | URA-003 | Componentes obligatorios; valores existentes |
| FK-013 | `yusay.assessment_attempt` | SIMPLE | IDF-01 (mismo orden) | `yusay.app_user` | PK-001 | Componentes obligatorios; valores existentes |
| FK-014 | `yusay.assessment_attempt` | COMPUESTA | IDF-04 → posición; IDF-05 (mismo orden) | `yusay.instrument_version` | URA-001 | Componentes obligatorios; valores existentes |
| FK-015 | `yusay.answer` | COMPUESTA | IDF-08 → posición; IDF-05 (mismo orden) | `yusay.assessment_attempt` | URA-004 | Componentes obligatorios; valores existentes |
| FK-016 | `yusay.answer` | COMPUESTA | IDF-05 → posición; IDF-06 (mismo orden) | `yusay.question` | URA-002 | Componentes obligatorios; valores existentes |
| FK-017 | `yusay.answer` | COMPUESTA | IDF-06 → posición; IDF-07 (mismo orden) | `yusay.answer_option` | URA-003 | Componentes obligatorios; valores existentes |
| FK-018 | `yusay.interpretation` | SIMPLE | IDF-05 (mismo orden) | `yusay.instrument_version` | PK-007 | Componentes obligatorios; valores existentes |
| FK-019 | `yusay.assessment_result` | SIMPLE | IDF-08 (mismo orden) | `yusay.assessment_attempt` | PK-013 | Componentes obligatorios; valores existentes |
| FK-020 | `yusay.assessment_result` | COMPUESTA | IDF-08 → posición; IDF-05 (mismo orden) | `yusay.assessment_attempt` | URA-004 | Componentes obligatorios; valores existentes |
| FK-021 | `yusay.assessment_result` | COMPUESTA | IDF-05 → posición; IDF-09 (mismo orden) | `yusay.interpretation` | URA-005 | Componentes obligatorios; valores existentes |
| FK-022 | `yusay.dimension_version` | SIMPLE | IDF-10 (mismo orden) | `yusay.dimension` | PK-017 | Componentes obligatorios; valores existentes |
| FK-023 | `yusay.dimension_anchor` | SIMPLE | IDF-11 (mismo orden) | `yusay.dimension_version` | PK-018 | Componentes obligatorios; valores existentes |
| FK-024 | `yusay.check_in` | SIMPLE | IDF-01 (mismo orden) | `yusay.app_user` | PK-001 | Componentes obligatorios; valores existentes |
| FK-025 | `yusay.measurement` | SIMPLE | IDF-12 (mismo orden) | `yusay.check_in` | PK-020 | Componentes obligatorios; valores existentes |
| FK-026 | `yusay.measurement` | COMPUESTA | IDF-10 → posición; IDF-11 (mismo orden) | `yusay.dimension_version` | URA-006 | Componentes obligatorios; valores existentes |
| FK-027 | `yusay.check_in_context_tag` | SIMPLE | IDF-12 (mismo orden) | `yusay.check_in` | PK-020 | Componentes obligatorios; valores existentes |
| FK-028 | `yusay.check_in_context_tag` | SIMPLE | IDF-13 (mismo orden) | `yusay.context_tag` | PK-022 | Componentes obligatorios; valores existentes |
| FK-029 | `yusay.instrument_version_compatibility` | COMPUESTA | IDF-04 → posición; IDF-05 (mismo orden) | `yusay.instrument_version` | URA-001 | Componentes obligatorios; valores existentes |
| FK-030 | `yusay.instrument_version_compatibility` | COMPUESTA | IDF-04 → posición; IDF-05 (mismo orden) | `yusay.instrument_version` | URA-001 | Componentes obligatorios; valores existentes |
| FK-031 | `yusay.dimension_version_compatibility` | COMPUESTA | IDF-10 → posición; IDF-11 (mismo orden) | `yusay.dimension_version` | URA-006 | Componentes obligatorios; valores existentes |
| FK-032 | `yusay.dimension_version_compatibility` | COMPUESTA | IDF-10 → posición; IDF-11 (mismo orden) | `yusay.dimension_version` | URA-006 | Componentes obligatorios; valores existentes |
| FK-033 | `yusay.resource_topic` | SIMPLE | IDF-15 (mismo orden) | `yusay.resource` | PK-027 | Componentes obligatorios; valores existentes |
| FK-034 | `yusay.resource_topic` | SIMPLE | IDF-14 (mismo orden) | `yusay.topic` | PK-026 | Componentes obligatorios; valores existentes |
| FK-035 | `yusay.instrument_topic` | SIMPLE | IDF-04 (mismo orden) | `yusay.instrument` | PK-006 | Componentes obligatorios; valores existentes |
| FK-036 | `yusay.instrument_topic` | SIMPLE | IDF-14 (mismo orden) | `yusay.topic` | PK-026 | Componentes obligatorios; valores existentes |
| FK-037 | `yusay.interpretation_topic` | SIMPLE | IDF-09 (mismo orden) | `yusay.interpretation` | PK-015 | Componentes obligatorios; valores existentes |
| FK-038 | `yusay.interpretation_topic` | SIMPLE | IDF-14 (mismo orden) | `yusay.topic` | PK-026 | Componentes obligatorios; valores existentes |
| FK-039 | `yusay.dimension_topic` | SIMPLE | IDF-10 (mismo orden) | `yusay.dimension` | PK-017 | Componentes obligatorios; valores existentes |
| FK-040 | `yusay.dimension_topic` | SIMPLE | IDF-14 (mismo orden) | `yusay.topic` | PK-026 | Componentes obligatorios; valores existentes |
| FK-041 | `yusay.audit_event` | SIMPLE | IDF-01 (mismo orden) | `yusay.app_user` | PK-001 | Referencia opcional desvinculable |


Las 13 compuestas no son conjuntos intercambiables de IDs. Attempt conserva instrumento y versión; Answer conserva Attempt/versión, pregunta/versión y opción/pregunta a la vez. ScoringContribution conserva su definición y las pertenencias de pregunta/opción. Result conserva Attempt y versión de Interpretation; no se elimina su FK simple adicional. Measurement conserva dimensión/versión. Cada tabla de Compatibilidad mantiene dos FKs compuestas con padre común; no basta comparar extremos sin verificar su catálogo.

### Compatibilidad de las seis URA

Cada componente hereda la familia de su PK o referencia. Se conservan las seis combinaciones y sus nombres exactos ya aprobados; no se enumeran otra vez los nombres largos ni se abrevia ninguno. Elegir tipos por familia no elimina estas URA, aunque un componente por sí solo ya sea PK.

| URA existente | Relación / componentes aprobados | Familias en orden | FK compuestas dependientes |
| --- | --- | --- | --- |
| URA-001 | `yusay.instrument_version` / `instrument_id, instrument_version_id` | IDF-04, IDF-05 | FK-014, FK-029, FK-030 |
| URA-002 | `yusay.question` / `instrument_version_id, question_id` | IDF-05, IDF-06 | FK-011, FK-016 |
| URA-003 | `yusay.answer_option` / `question_id, option_id` | IDF-06, IDF-07 | FK-012, FK-017 |
| URA-004 | `yusay.assessment_attempt` / `attempt_id, instrument_version_id` | IDF-08, IDF-05 | FK-015, FK-020 |
| URA-005 | `yusay.interpretation` / `instrument_version_id, interpretation_id` | IDF-05, IDF-09 | FK-021 |
| URA-006 | `yusay.dimension_version` / `dimension_id, dimension_version_id` | IDF-10, IDF-11 | FK-026, FK-031, FK-032 |


Las AK de códigos y correo no son familias de identificadores opacos generados. Las AK que combinan un ID con version/position heredan el ID de su familia, pero mantienen el otro componente en su dominio. Elegir UUID no convierte números de versión, posiciones o medidas en UUID. Email/colación permanece OQ-PHYS-004; valores numéricos no opacos, OQ-PHYS-005.

### Comparación técnica A / B / C

**Hechos verificados:** BIGINT almacena enteros con signo en 8 bytes, rango −9223372036854775808..9223372036854775807. UUID es un valor de 128 bits, equivalente a 16 bytes de valor; ambos algoritmos pueden usar el tipo nativo. Esto no mide tamaño total de fila/índice, que incluye sobrecarga. [Numéricos PostgreSQL 18](https://www.postgresql.org/docs/18/datatype-numeric.html), [tipo UUID](https://www.postgresql.org/docs/18/datatype-uuid.html).

PostgreSQL 18 ofrece generación v4 mediante gen_random_uuid/uuidv4 y v7 mediante uuidv7; esta última combina tiempo de milisegundos, fracción submilisegundo y aleatoriedad. La extracción del tiempo de v7 no garantiza el instante exacto de generación. Son capacidades verificadas, no funciones configuradas en Yusay. [Funciones UUID de la rama 18](https://www.postgresql.org/docs/18/functions-uuid.html).

RFC 9562 define v4 aleatorio y v7 con timestamp Unix de 48 bits en milisegundos y otros bits para aleatoriedad/monotonicidad. El orden binario de campos es big-endian; advierte sobre representaciones GUID de COM con orden distinto. Estas propiedades no prueban orden de confirmación entre procesos ni convierten UUID en secreto. [RFC 9562, §§4, 5.4 y 5.7](https://www.rfc-editor.org/rfc/rfc9562.html#section-5.7).

| Aspecto | A — BIGINT | B — UUID v4 | C — UUID v7 |
| --- | --- | --- | --- |
| Valor almacenado | 8 bytes; entero exacto con signo | 16 bytes; tipo uuid | 16 bytes; mismo tipo uuid |
| Generación aplicable | Identidad/secuencia central; backend necesitaría asignación coordinada | PostgreSQL o generador backend conforme al estándar | Soporte nativo real en PostgreSQL 18 o generador backend conforme |
| Predictibilidad / privacidad | Secuencia hace previsible asignación; no contiene fecha obligatoria | Sin campo temporal; aleatoriedad no equivale a autorización | Puede revelar tiempo aproximado; la ventaja de localidad tiene coste de exposición |
| Distribución esperada | Asignación creciente concentra inserciones en región final | Dispersión de valores respecto de un orden de claves | Concentración aproximada por tiempo; no orden de commit |
| Índices y joins — hipótesis | Menor anchura de clave puede reducir memoria y coste; posible contención en zona final | Mayor anchura/dispersión puede aumentar páginas y presión de caché | Podría mejorar localidad respecto de v4; no reduce anchura ni garantiza mejor join |
| Portabilidad | Número portable; estado de asignación central necesita operación/restauración | Generación independiente de motor posible; requiere contrato común | También independiente si hay generador compatible; reloj y variante importan |
| Concurrencia | Secuencia coordina valores; no resuelve reglas del negocio | Generadores concurrentes posibles; mantener PK ante colisión | Posible generación concurrente; no presumir monotonicidad global |
| URLs / APIs | Puede enumerarse; evitar pérdida de precisión al transportar enteros grandes | Formato de intercambio debe definirse; IDs conocidos siguen sensibles | Mismo problema de autorización, más información temporal |
| Aplicabilidad MVP | Opción simple y compacta si se acepta predictibilidad | Opción razonable para minimizar información inferible y unificar | Opción válida si se acepta exposición temporal y se justifica beneficio medido |
| Estado vigente | Antecedente no seleccionado | uuid / v4 APROBADO para las 16 raíces | Antecedente no seleccionado |


**No hay benchmarks de Yusay.** Tamaño menor de valor no demuestra porcentajes de ahorro o latencia; dispersión/localidad son hipótesis cualitativas según generador y accesos. Rendimiento real de índices y joins, fragmentación, tasas de inserción y contención quedan **NO VERIFICADOS**, dependientes de OQ-PHYS-009. UUID v7 no es superior por definición: no ofrece ventaja demostrada en joins por igualdad, ni compensa necesariamente su información temporal.

### Generación: autoridad y operación

La identidad PostgreSQL usa una secuencia implícita; identidad no reemplaza PK/UNIQUE ni garantiza por sí sola unicidad. No se elige modalidad de identidad ni secuencia concreta. [Columnas identity](https://www.postgresql.org/docs/18/ddl-identity-columns.html).

La asignación de secuencia es atómica entre sesiones; abortar una transacción no recupera el número. Puede haber huecos y se debe coordinar estado al importar/restaurar; el ID asignado no acredita que la fila se haya confirmado. [Funciones de secuencia](https://www.postgresql.org/docs/18/functions-sequence.html). Esto no afecta al dominio de reference_order, que no es secuencia de identidades.

| Criterio | Centralizada PostgreSQL | Backend | Híbrida controlada |
| --- | --- | --- | --- |
| Consistencia | Una autoridad configurada por familia raíz; hijos copian | Contrato único de tipo, versión y codificación por familia | Misma política por familia con orígenes de generación explícitos; no mezcla incidental |
| Atomicidad | ID disponible en la transacción; completar operación/hijos antes de confirmar | Conocer ID antes no confirma persistencia; misma atomicidad de filas | Necesita distinguir asignación, escritura y confirmación |
| Conocer antes de persistir | UUID puede obtenerse del motor antes de escritura; necesita comunicación. BIGINT requiere reserva/asignación | UUID local puede preasignarse; BIGINT necesita coordinador/reserva | Puede cubrir preasignación, a costa de reglas sobre valores suministrados |
| Dependencia | Motor ya aceptado; integración debe recuperar el valor correcto | Lenguaje/librería y contrato aún no elegidos | Dos rutas a operar/probar sin requisito actual que las justifique |
| Concurrencia / colisiones | PK sigue siendo barrera; secuencia coordina, UUID probabilístico | UUID requiere fuente aleatoria/generador fiable; BIGINT sin coordinador puede colisionar | Coordinar ambas rutas; no creer que formato común garantice generadores correctos |
| Operación y pruebas | Restauración/importación y obtención de ID de esta fila; sin publicación prematura | Disponibilidad/calidad del generador, concurrencia y serialización | Además matriz de orígenes permitidos y coherencia de reintentos |
| MVP | Reduce contratos de generación externa | Útil si se necesita ID local antes de persistencia; necesidad NO VERIFICADA | Complejidad adicional; justificación funcional NO VERIFICADA |


No se inventa un generador distinto para cada tabla hija ni para cada FK. BIGINT generado en backend mediante lectura del máximo más uno es una alternativa descartada por carreras; un esquema distribuido de rangos exigiría coordinación no definida, sin añadir entidades aquí. Para UUID, colisión improbable no significa imposible: futuras pruebas deben cubrir rechazo de duplicados y tratamiento seguro del fallo. No se fija reintento automático ni idempotencia nueva (OQ-PHYS-007).

La consistencia histórica no permite regenerar IDs por editar, publicar, retirar, cambiar estado, reintentar o restaurar un registro existente. Nueva identidad y repetición de una operación son conceptos diferentes; la política de confirmación/resultado incierto conserva VF/DP-TRANS. Los UUID de tokens identifican filas, no sustituyen el secreto ni token_hash de verificación/recuperación.

### Orden canónico de Compatibilidad

Se mantienen `yusay.instrument_version_compatibility` y `yusay.dimension_version_compatibility`, PK `(version_a_id, version_b_id)` y sus dos FKs compuestas con padre común. Simetría semántica no implica duplicar la fila; no hay autopares ni transitividad inferida. La regla lógica `version_a_id < version_b_id` es obligatoria; el orden binario está aprobado; su validación entre PostgreSQL y backend y la protección física concreta siguen pendientes.

| Alternativa | Orden total propuesto para evaluación | Contrato con backend | Lo que no representa |
| --- | --- | --- | --- |
| BIGINT | Comparación numérica exacta de enteros con signo | Misma precisión y signo; nunca comparación lexicográfica de decimales o conversión con pérdida | Tiempo, versión ni confirmación; asignación creciente no prueba cronología |
| UUID v4 | Orden lexicográfico de los 16 octetos, cada uno sin signo, en orden de representación binaria del motor | Decodificar UUID a bytes canónicos y usar ese orden, no comparador textual/locale o GUID sin validar | Orden aleatorio de identidades, sin cronología |
| UUID v7 | Exactamente el mismo comparador binario de uuid que v4 | Mismo contrato; versión del generador no modifica comparador | Puede correlacionar con tiempo, pero no con commit, versión o creación efectiva |


El código oficial PostgreSQL 18 compara UUID mediante memcmp de sus 16 octetos; operadores y comparador de claves emplean esa comparación. Contrato aprobado para UUID: secuencia binaria canónica, octetos 0..255, primer octeto diferente decide; todos iguales significa igualdad. Backend debe reproducirlo mediante una representación decodificada verificada, sin comparar dos mitades con signo, sin bytes GUID de orden mixto y sin colación textual. [Implementación oficial uuid.c](https://raw.githubusercontent.com/postgres/postgres/REL_18_STABLE/src/backend/utils/adt/uuid.c). El comparador de un backend específico está **NO VERIFICADO**: no existe tecnología seleccionada.

Normalización conceptual, independiente del tipo: verificar dos versiones existentes del mismo padre; si A = B, rechazar autopar; si A < B, conservar `(A,B)`; en caso contrario conservar `(B,A)`. Aplicar esa misma orientación al consultar el inverso. La PK compuesta evita duplicación del par normalizado y las FKs conservan pertenencia; no se selecciona cómo implementar esas protecciones. Un resultado de comparación no declara compatibilidad ni permite deducir A↔C a partir de A↔B y B↔C.

No se usa fecha, número de versión, llegada de solicitud ni identificador de padre para desempatar. Los valores son estables; una vez confirmado, el par no se reorienta ni renumera porque cambie un reloj. Las restricciones aprobadas de inmutabilidad/revisión de errores siguen vigentes.

Vectores documentales propuestos para una futura prueba del contrato (no UUID generados ni pruebas ejecutadas):

| Entrada lógica | Resultado esperado | Error que detecta |
| --- | --- | --- |
| BIGINT 2 y 10 | 2 < 10 | Comparación como texto |
| BIGINT −1 y 1 | −1 < 1 | Conversión a entero sin signo; no implica aprobar IDs negativos |
| UUID v4 7fffffff-0000-4000-8000-000000000001 y 80000000-0000-4000-8000-000000000001 | Primero < segundo | Mitades interpretadas con signo |
| UUID v7 01900000-0000-7000-8000-000000000001 y 01900000-0000-7000-8000-000000000002 | Primero < segundo | Mismo prefijo temporal; debe comparar todos los bytes |
| Mismo UUID representado con diferente capitalización de hex | Igualdad del valor decodificado; autopar rechazado | Confundir forma textual con identidad |
| (A,B) y (B,A) de mismo padre | Mismo par canónico | Dependencia del orden de entrada |


### Seguridad, privacidad y supresión

IDs no son autorizaciones: incluso UUID v4 conocido permite apuntar a un objeto y exige controlar dueño/acceso; ADMINISTRATOR no concede acceso al bienestar privado. BIGINT secuencial facilita enumeración; v7 puede aportar información temporal; v4 reduce ambas inferencias por formato pero no hace anónimo un registro vinculado a una persona. Ninguna política usa correo, fecha personal o datos de bienestar para derivar IDs.

No existe contrato aprobado de URLs/APIs. La exposición directa es decisión pendiente: si se requiere otro identificador público persistente, no se añade columna ni relación para resolverlo silenciosamente. Una representación reversible del mismo ID tampoco es una nueva garantía de autorización. No se fijan formatos de payload ni longitudes de URL.

La elección no cambia supresión individual/cuenta, dependencias personales, desvinculación de actor_user_id/target_identifier y metadata, ni la excepción DP-TRANS-001. AUDIT_EVENT.audit_event_id identifica el evento y no preserva una identidad personal suprimida; las referencias a usuario siguen desvinculables. USER_DELETED no conserva IDs personales; UUID no habilita copias/hash identificables ni auditoría de Attempts/CheckIns/Results prohibida.

Los IDs admitidos en metadata T/P pertenecen a Topic y versiones de catálogos, con sus mismas familias y orientación canónica, sin nuevas FKs. target_identifier es opcional/polimórfico conforme al catálogo aprobado; uuid para raíces no aprueba representación de ese campo ni serialización interna de IDs; OQ-PHYS-006 aprueba jsonb para metadata y MP-PHYS-003 conserva validación/serialización pendientes. No se inventa identificador independiente para el par de compatibilidad.

Backups conservan sus 30 días; auditoría sus 180; Attempts terminales sin Result sus 30. Generadores/IDs no sustituyen la obligación de reaplicar supresiones antes de habilitar una restauración. Tipo de ID y estrategia de generación no justifican recuperar datos privados para reconstruir un evento ausente. La realización operativa permanece OQ-PHYS-008.

### Resolución aprobada y alternativas históricas

La recomendación previa queda aprobada: uuid nativo, UUID v4 centralizado en PostgreSQL para las 16 raíces; cinco identidades compartidas sin generador propio y once PK compuestas sin sustitutos. Las 16 familias conservan sus 62 apariciones, las 41 FK y seis URA. Los componentes reference_order y DIMENSION_ANCHOR.value son enteros de dominio, conforme a OQ-PHYS-005.

BIGINT, UUID v7, una política diferenciada y la generación backend/híbrida quedan como alternativas evaluadas y no seleccionadas. No se autorizan excepciones por familia, generación independiente de claves compartidas ni nuevas identidades públicas. No se seleccionan funciones SQL, DEFAULT, extensiones ni librerías.

El orden canónico compara los 16 octetos sin signo, sin significado temporal. La correspondencia con un comparador/serializador backend concreto deberá verificarse cuando exista tecnología elegida. Generar identidad no demuestra persistencia ni autorización. La generación criptográfica del secreto de tokens desde backend es distinta de la identidad UUID de su fila.

### Preguntas para decisión de OQ-PHYS-003

Son desgloses históricos con su resolución o mecanismo residual explícitos. El cierre arquitectónico de OQ-PHYS-003 no cierra automáticamente detalles de integración. No reabren la reutilización lógica de claves compartidas ni las PK/FK/URA congeladas.

### OQ-PHYS-003-A

- **ID:** OQ-PHYS-003-A.
- **Pregunta:** ¿Se adopta uuid/v4 uniforme para las 16 familias o BIGINT, uuid/v7 o una distribución explícita por familia?
- **Motivo:** Cerrar tipo y versión de generación sin mezclar identidades.
- **Impacto:** 32 PK, 62 columnas, 41 FK, seis URA; OQ-PHYS-005.
- **Status:** RESOLVED en alcance arquitectónico.
- **Resolución:** uuid nativo y UUID v4 uniformes para las 16 raíces, con propagación por familia.

### OQ-PHYS-003-B

- **ID:** OQ-PHYS-003-B.
- **Pregunta:** ¿Será PostgreSQL la autoridad de generación o existe necesidad aprobable de preasignación local que justifique backend/híbrida?
- **Motivo:** Definir una política por familia y sus excepciones justificadas.
- **Impacto:** Raíces, integración y operación; OQ-PHYS-007/010.
- **Status:** RESOLVED en alcance arquitectónico.
- **Resolución:** Generación centralizada en PostgreSQL para las raíces; sin generación propia para claves compartidas.

### OQ-PHYS-003-C

- **ID:** OQ-PHYS-003-C.
- **Tema:** Contrato de codificación, transporte y binding del backend para UUID y claves compartidas.
- **Status:** RESOLVED documentalmente (MP-PHYS-016).
- **Especificación física:** Intercambio textual canónico según RFC 9562 (36 caracteres en minúsculas `xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx`). En el driver de base de datos se realiza binding nativo al tipo `uuid` de PostgreSQL (16 bytes sin signo). En las 5 claves compartidas (`user_credential`, `administrator`, `scoring_definition`, `scoring_contribution`, `assessment_result`), el backend copia el valor exacto de la entidad padre sin regenerar identificadores.

### OQ-PHYS-003-D

- **ID:** OQ-PHYS-003-D.
- **Pregunta:** ¿Se aprueba el orden numérico exacto para BIGINT o el orden binario de 16 octetos sin signo para uuid, según el tipo finalmente elegido?
- **Motivo:** Materializar version_a_id < version_b_id sin cronología ni orden textual incidental.
- **Impacto:** Dos relaciones de Compatibilidad y metadata P; OQ-PHYS-006/007.
- **Status:** RESOLVED en alcance arquitectónico.
- **Resolución:** Orden total estable de los 16 bytes sin signo; no temporal ni textual.

### OQ-PHYS-003-E

- **ID:** OQ-PHYS-003-E.
- **Pregunta:** ¿Se adopta el contrato de equivalencia PostgreSQL/backend y su validación con límites de signo, byte order, igualdad y orientación inversa?
- **Motivo:** Evitar que lectores/escritores normalicen distinto el mismo par.
- **Impacto:** Comparadores/serialización de versiones; plan de verificación 06.
- **Status:** RESOLVED en alcance arquitectónico.
- **Resolución:** Equivalencia obligatoria con ese orden aprobado; implementación y prueba del comparador backend especificadas en MP-PHYS-016.

### OQ-PHYS-003-F

- **ID:** OQ-PHYS-003-F.
- **Tema:** Exposición en APIs y URLs.
- **Status:** RESOLVED documentalmente (MP-PHYS-016).
- **Especificación física:** Los UUIDs v4 de recursos públicos o del usuario autenticado pueden exponerse en rutas canónicas REST (`/instruments/{instrument_id}`, `/check-ins/{check_in_id}`). Los UUIDs enviados por clientes **nunca constituyen autorización por sí mismos**: cada operación valida la sesión y autorizaciones del usuario (`user_id = current_user_id`).

### OQ-PHYS-003-G

- **ID:** OQ-PHYS-003-G.
- **Tema:** Asignación, importación y duplicados.
- **Status:** RESOLVED documentalmente (MP-PHYS-016).
- **Especificación física:** La autoridad de generación de UUID v4 para las 16 raíces reside de forma centralizada en PostgreSQL (`gen_random_uuid()`) o es pre-asignada por backend bajo el mismo estándar CSPRNG. En importaciones o restauraciones se preservan los UUIDs originales sin regeneración. Ante colisión (probabilidad negligible), la restricción PK rechaza la inserción con `23505`.

### Criterios de validación futura y riesgos residuales

1. Mapeo aprobado conserva 32 PK, clasificación 16/5/11, 147 atributos, nueve AK, seis URA y 41 FK; nombres exactos OQ-PHYS-002 intactos. Las 54 posiciones FK y los 62 usos de ID concuerdan con su raíz sin mezclar familias.
2. Generador solo en 16 raíces; cinco compartidas sin generador propio; compuestas sin surrogate; los dos componentes de dominio no reciben generación UUID. Probar creación de padre/hijos y rechazo referencial de inexistencias/pertenencias incorrectas.
3. Comprobar UUID v4, representación, intercambio sin pérdida y valor correcto recuperado tras escribir; validar generación centralizada, colisiones/fallos e importación/restauración sin regenerar identidades. BIGINT/secuencias son antecedentes no seleccionados.
4. Validar comparator PostgreSQL/backend con igualdad, signo, diferencias en todos los octetos y serializadores elegidos; pruebas de A/B inversos, autopares, padres distintos y no transitividad. Lectura/escritura históricas no regeneran IDs.
5. Ensayar concurrencia, rollback y resultados inciertos conforme a VF/DP-TRANS: ID asignado no equivale a éxito; máximos IN_PROGRESS/ACTIVE/PUBLISHED y envío atómico conservan diseño pendiente en OQ-PHYS-007. IDs no resuelven esas reglas.
6. Comprobar autorización en accesos por ID, exposición temporal/previsible aceptada y supresión/desvinculación en todas las dependencias; restauración aplica supresiones antes de reabrir servicio. Sin almacenar identidad personal para reconstruir auditoría.
7. Medir índice/joins/inserción con carga aprobada en OQ-PHYS-009 antes de afirmar superioridad. No hay cifras o pruebas productivas actuales.

Riesgos residuales **NO VERIFICADOS**: carga/rendimiento real, necesidad de IDs antes de persistencia, generadores/serializadores/comparadores del backend y operación de restauración. Hechos documentales verificados: cobertura estructural, clasificación, familias, soporte v4/v7 en PostgreSQL 18, tamaños de valor y comparador del motor. No se detectó discrepancia estructural entre fuentes y nombres aprobados que requiera alterar la línea base.

**OQ-PHYS-003..010: RESOLVED en alcance arquitectónico; diseño físico IN PROGRESS.** OQ-PHYS-003-C/F/G y los mecanismos residuales siguen OPEN. La auditoría previa es antecedente de la resolución, no prueba de implementación ni dictamen físico. Conceptual, lógico y ADR-001 se preservan.

[Contexto y OQ-PHYS-003](01-contexto-y-alcance.md#oq-phys-003) · [Índice físico](00-indice.md).


## OQ-PHYS-004

**Status: RESOLVED.** Autoridad: responsable del proyecto; registro y ratificación: 2026-10-07. USER.email se representa como `text` nativo y conserva la AK y su nombre de restricción/índice objetivo `uq_app_user_email`, sin nuevos atributos ni claves.

**C-PHYS-001 RESOLVED — Inmutabilidad total en el MVP:**
Se ratifica formalmente la inmutabilidad estricta del correo electrónico durante todo el MVP. No existe funcionalidad, servicio, endpoint ni interfaz de cambio de correo. El correo asignado en el registro (`USER_REGISTERED`) permanece invariable durante toda la vida de la cuenta. No se agregan tablas de tokens de cambio, estados temporales ni columnas accesorias en la base de datos. La unicidad y canonicalización operan exclusivamente durante la creación de la cuenta y en la autenticación/recuperación.

### Especificación técnica física del correo electrónico (MP-PHYS-002)

1. **Almacenamiento físico:** Tipo nativo `text` en `yusay.app_user.email`. Se aplican eliminación estricta de espacios en blanco iniciales y finales (`trim`) y rechazo de espacios intermedios o caracteres de control ASCII (0x00–0x1F, 0x7F).
2. **Estructura y sintaxis de la parte local:**
   - Codificación ASCII estricta, sin entrecomillado (*quoted-string* no admitido en el MVP).
   - Caracteres permitidos: letras ASCII `[a-zA-Z]`, dígitos `[0-9]`, punto `.`, guion `-`, guion bajo `_` y signo más `+`.
   - Caracteres terminantemente prohibidos: `%` (signo de porcentaje rechazado expresamente), espacios, caracteres Unicode en parte local, comillas, barras y cualquier carácter de puntuación no listado.
   - Preservación de representación: Se preserva fielmente la representación literal de mayúsculas y minúsculas introducida por el usuario al persistir en la columna `email`.
   - Reglas de puntuación en parte local: se prohíbe punto inicial (`.usuario`), punto final (`usuario.`) y puntos consecutivos adyacentes (`..`).
   - Sin normalizaciones de proveedor: no se descartan los sufijos `+alias` ni se omiten los puntos (no se asume normalización estilo Gmail u otros proveedores).
   - Longitud técnica máxima: conforme a RFC 5321, máximo 64 octetos para la parte local.
3. **Estructura y canonicalización del dominio:**
   - Canonicalización obligatoria en backend previa a persistencia: Conversión a minúsculas y normalización IDNA/Punycode conforme a RFC 5890 / UTS #46.
   - Estructura de etiquetas: Etiquetas de dominio separadas por puntos (`.`), cada una de 1 a 63 octetos, comenzando y terminando por carácter alfanumérico ASCII (sin guion inicial o final en cada etiqueta).
   - Terminación de dominio: No se exige una terminación puramente alfabética para no descartar TLDs internacionalizados codificados en Punycode (e.g. `xn--...` que contiene dígitos y guiones).
   - Longitud máxima del dominio: 255 octetos (RFC 5321). Longitud máxima total del correo canónico: 254 caracteres (RFC 5321 errata 1690).
4. **Unicidad e índice funcional candidato:**
   - La unicidad lógica se define de manera insensible a mayúsculas y minúsculas (*case-insensitive*).
   - Para respaldar la AK aprobada `uq_app_user_email`, se especifica el índice funcional único:
     `CREATE UNIQUE INDEX uq_app_user_email ON yusay.app_user (lower(email) COLLATE "C");`
   - La colación explícita `"C"` garantiza una comparación byte a byte determinista y libre de variaciones por locale del sistema operativo, glibc o ICU, previniendo corrupción de índices y divergencias de unicidad.
5. **Validación complementaria en capas (Backend y PostgreSQL):**
   - *Backend:* Ejecuta `trim`, valida sintaxis de parte local (alfanumérico + `._-+`, sin `%`, sin puntos iniciales/finales/dobles), ejecuta canonicalización IDNA Punycode del dominio a minúsculas y valida longitudes (local ≤ 64, total ≤ 254).
   - *PostgreSQL (CHECK complementario):* Restricción declarativa que refuerza la sintaxis permitida, rechaza `%`, espacios y caracteres no permitidos, y valida la estructura general sin imponer restricciones indebidas al TLD:
     `CONSTRAINT ck_app_user_email_format CHECK (email ~ '^[a-zA-Z0-9._+-]+@[a-zA-Z0-9.-]+$' AND email NOT LIKE '.%' AND email NOT LIKE '%.' AND email NOT LIKE '%..%' AND email NOT LIKE '%@%@%' AND length(email) <= 254)`
6. **Inmutabilidad (`C-PHYS-001 RESOLVED`):**
   - El correo electrónico permanece inmutable durante todo el MVP. No existen endpoints ni transacciones de actualización de email; las restricciones operan exclusivamente en inserción (`USER_REGISTERED`) y en búsquedas de autenticación/recuperación.

## OQ-PHYS-005

**Status: RESOLVED.** Política autorizada de tipos y capacidad física (`MP-PHYS-001` y `MP-PHYS-005`):
- **Textuales:** `text` nativo para atributos de texto generales y códigos; no se imponen límites artificiales `varchar(n)`.
- **Enteros y análisis formal de capacidad numérica (MP-PHYS-001):**
  * Se analizan los 15 atributos enteros del modelo lógico normativo:
    1. `INSTRUMENT_VERSION_REFERENCE.reference_order` (orden positivo > 0).
    2. `QUESTION.position` (posición positiva > 0).
    3. `ANSWER_OPTION.position` (posición positiva > 0).
    4. `SCORING_CONTRIBUTION.contribution` (entero con signo en `[-2147483648, 2147483647]`).
    5. `INTERPRETATION.lower_bound` (entero con signo inclusivo).
    6. `INTERPRETATION.upper_bound` (entero con signo inclusivo).
    7. `ASSESSMENT_RESULT.score` (entero con signo, resultado de la acumulación SUM).
    8. `INSTRUMENT_VERSION.version` (versión positiva > 0).
    9. `DIMENSION_VERSION.version` (versión positiva > 0).
    10. `DIMENSION_VERSION.min_value` (extremo inferior de escala).
    11. `DIMENSION_VERSION.max_value` (extremo superior de escala).
    12. `DIMENSION_VERSION.step` (incremento positivo > 0).
    13. `DIMENSION_ANCHOR.value` (valor de escala en `[min_value, max_value]`).
    14. `CHECK_IN.revision` (revisión optimista positiva ≥ 1).
    15. `MEASUREMENT.value` (valor en escala `[min_value, max_value]`).
  * **Cálculo de extremos alcanzables por versión de instrumento:**
    Para cualquier `INSTRUMENT_VERSION` en estado `DRAFT` o `READY`:
    Dado que cada pregunta $q \in Q$ exige responder obligatoriamente una opción $o \in Options(q)$ (REV-LOG-004), los límites teóricos absolutos de la puntuación alcanzable se calculan deterministamente a partir de sus contribuciones aprobadas:
    $$Score_{min} = \sum_{q \in Q} \min_{o \in Options(q)} (contribution(o))$$
    $$Score_{max} = \sum_{q \in Q} \max_{o \in Options(q)} (contribution(o))$$
  * **Comportamiento de agregación en PostgreSQL, límites de `bigint` y desbordamiento:**
    - Al ejecutar la agregación `SELECT SUM(sc.contribution) ...`, PostgreSQL promociona internamente el tipo del acumulador: `SUM(integer)` retorna un tipo `bigint` (entero con signo de 64 bits: $[-9.223.372.036.854.775.808, +9.223.372.036.854.775.807]$).
    - **Aclaración crítica de capacidad:** Un tipo `bigint` no es infinito y **también puede desbordarse** (`ERROR: bigint out of range`) ante sumas extremas no acotadas. Además, que el cálculo intermedio retorne `bigint` no garantiza que el resultado final quepa en la columna de destino `ASSESSMENT_RESULT.score`, cuyo tipo normativo es `integer` (32 bits con signo: $[-2.147.483.648, +2.147.483.647]$). Un valor superior provocaría un fallo transaccional en la inserción (`ERROR: integer out of range`).
    - Por lo tanto, el sistema **no confía pasivamente** en la ampliación a `bigint` durante la consulta de cálculo.
  * **Validación segura de cotas antes de publicar instrumentos (Guarda editorial preventiva):**
    - Se establece como invariante formal de publicación que ninguna `INSTRUMENT_VERSION` podrá transicionar a `READY` ni a `PUBLISHED` sin superar una validación exhaustiva de cotas extremas en la capa editorial:
      1. Se calculan deterministamente las cotas extremas alcanzables:
         $$Score_{min} = \sum_{q \in Q} \min_{o \in Options(q)} (contribution(o))$$
         $$Score_{max} = \sum_{q \in Q} \max_{o \in Options(q)} (contribution(o))$$
      2. Se valida formalmente que:
         $$-2.147.483.648 \le Score_{min} \le Score_{max} \le +2.147.483.647$$
         Esta condición garantiza de forma estricta que la agregación acumulativa nunca desborda `bigint` durante el cálculo intermedio ni desborda `integer` al persistir el resultado final en `ASSESSMENT_RESULT.score`.
      3. Se verifica que todos los límites de los rangos de interpretación en `INTERPRETATION` (`lower_bound` y `upper_bound`) pertenezcan estrictamente al intervalo representable de `integer` y que cubran continuamente el espectro $[Score_{min}, Score_{max}]$ sin solapamientos ni brechas.
    - Si cualquier cota supera el rango de 32 bits con signo o presenta discontinuidades, la transición de estado es rechazada inmediatamente en la capa de catálogo, impidiendo la publicación del instrumento y blindando la base de datos contra desbordamientos en tiempo de ejecución.
  * **Escalas de Seguimiento (`DIMENSION_VERSION` y `MEASUREMENT`):**
    - Se verifica que $min\_value < max\_value$ y $step > 0$, con $(max\_value - min\_value) \pmod{step} = 0$. Todos los valores pertenecen a la escala finita de la dimensión, garantizándose su perfecta representabilidad en `integer`.
- **Instantes y duraciones:**
  * Tipo nativo `timestamptz` (microsegundos UTC).
  * PostgreSQL almacena internamente en UTC y no persiste la zona horaria del cliente. La presentación en la zona horaria local se gestiona en la capa de interfaz/backend.
  * Duraciones interpretadas estrictamente como horas transcurridas: tokens de verificación 24 horas, restablecimiento 30 minutos, edición de CheckIn 168 horas, vigencia de intentos 720 horas, retenciones de 30 y 180 períodos de 24 horas.
- **Booleanos y valores controlados:**
  * `boolean` nativo (`QUESTION.required`).
  * Valores controlados modelados mediante `text` y restricciones `CHECK` con catálogos cerrados exactos de los diccionarios, sin `ENUM` nativo ni tablas maestras auxiliares.
- **Identificador de auditoría polimórfico (`target_identifier`):**
  * Resuelto físicamente como `text` con serialización canónica según el tipo de destino (UUIDv4 canónico en minúsculas para entidades con ID simple; NULL para entidades sin identificador o relaciones compuestas).

### Correspondencia de los 147 atributos

Cada fila toma nombre, orden, dominio y obligatoriedad del diccionario indicado. El ID C conserva la correspondencia de nombres anterior. La tabla añade tipos sin sustituir los diccionarios. La capacidad de los 15 atributos enteros queda resuelta y validada con `integer` estándar de 32 bits respaldada por la guarda de publicación de instrumentos y promociones seguras en agregaciones. `target_identifier` se modela físicamente como `text` nativo sin nueva FK.

| ID | Atributo lógico | Correspondencia física | Dominio lógico de fuente | Tipo / decisión | Obligatorio | Fuente / validación residual |
| --- | --- | --- | --- | --- | --- | --- |
| C-01-01 | USER.user_id | `yusay.app_user.user_id` | Identificador de User; identificador opaco y estable | uuid | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / IDF-01; contrato de intercambio MP-PHYS-016 |
| C-01-02 | USER.email | `yusay.app_user.email` | Correo canónico; unicidad sin distinción de mayúsculas | text | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / OQ-PHYS-004; MP-PHYS-002; C-PHYS-001 |
| C-01-03 | USER.email_verified_at | `yusay.app_user.email_verified_at` | Instante de verificación del correo | timestamptz | No | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / Precisión/reloj MP-PHYS-008 |
| C-01-04 | USER.created_at | `yusay.app_user.created_at` | Instante de creación de la cuenta | timestamptz | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / Precisión/reloj MP-PHYS-008 |
| C-01-05 | USER.adult_confirmed_at | `yusay.app_user.adult_confirmed_at` | Instante de confirmación de mayoría de edad por el usuario | timestamptz | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / Precisión/reloj MP-PHYS-008 |
| C-01-06 | USER.status | `yusay.app_user.status` | ACTIVE o BLOCKED | text | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / Valores cerrados; CHECK pendiente |
| C-02-01 | USER_CREDENTIAL.user_id | `yusay.user_credential.user_id` | Identificador de la cuenta | uuid | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / IDF-01; contrato de intercambio MP-PHYS-016 |
| C-02-02 | USER_CREDENTIAL.password_hash | `yusay.user_credential.password_hash` | Hash de contraseña; formato y algoritmo no seleccionados | text | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / Sin longitud/default nuevos; formato según dominio |
| C-02-03 | USER_CREDENTIAL.password_changed_at | `yusay.user_credential.password_changed_at` | Instante de cambio de contraseña registrado | timestamptz | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / Precisión/reloj MP-PHYS-008 |
| C-03-01 | ADMINISTRATOR.user_id | `yusay.administrator.user_id` | Identificador de User habilitado como administrador | uuid | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / IDF-01; contrato de intercambio MP-PHYS-016 |
| C-04-01 | EMAIL_VERIFICATION_TOKEN.verification_token_id | `yusay.email_verification_token.verification_token_id` | Identificador del token; identificador opaco y estable | uuid | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / IDF-02; contrato de intercambio MP-PHYS-016 |
| C-04-02 | EMAIL_VERIFICATION_TOKEN.user_id | `yusay.email_verification_token.user_id` | Identificador de la cuenta destinataria | uuid | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / IDF-01; contrato de intercambio MP-PHYS-016 |
| C-04-03 | EMAIL_VERIFICATION_TOKEN.token_hash | `yusay.email_verification_token.token_hash` | Hash del token; formato y algoritmo abiertos | text | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / Sin longitud/default nuevos; formato según dominio |
| C-04-04 | EMAIL_VERIFICATION_TOKEN.created_at | `yusay.email_verification_token.created_at` | Instante de emisión | timestamptz | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / Precisión/reloj MP-PHYS-008 |
| C-04-05 | EMAIL_VERIFICATION_TOKEN.expires_at | `yusay.email_verification_token.expires_at` | Instante de vencimiento; vigencia de 24 horas | timestamptz | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / Precisión/reloj MP-PHYS-008 |
| C-05-01 | PASSWORD_RESET_TOKEN.reset_token_id | `yusay.password_reset_token.reset_token_id` | Identificador del token; identificador opaco y estable | uuid | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / IDF-03; contrato de intercambio MP-PHYS-016 |
| C-05-02 | PASSWORD_RESET_TOKEN.user_id | `yusay.password_reset_token.user_id` | Identificador de la cuenta destinataria | uuid | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / IDF-01; contrato de intercambio MP-PHYS-016 |
| C-05-03 | PASSWORD_RESET_TOKEN.token_hash | `yusay.password_reset_token.token_hash` | Hash del token; formato y algoritmo abiertos | text | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / Sin longitud/default nuevos; formato según dominio |
| C-05-04 | PASSWORD_RESET_TOKEN.created_at | `yusay.password_reset_token.created_at` | Instante de emisión | timestamptz | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / Precisión/reloj MP-PHYS-008 |
| C-05-05 | PASSWORD_RESET_TOKEN.expires_at | `yusay.password_reset_token.expires_at` | Instante de vencimiento; vigencia de 30 minutos | timestamptz | Sí | [D01](../logical-model-v1/04-diccionario-datos/01-identidad.md) / Precisión/reloj MP-PHYS-008 |
| C-06-01 | INSTRUMENT.instrument_id | `yusay.instrument.instrument_id` | Identificador de instrumento | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-04; contrato de intercambio MP-PHYS-016 |
| C-06-02 | INSTRUMENT.code | `yusay.instrument.code` | Código del instrumento | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-06-03 | INSTRUMENT.name | `yusay.instrument.name` | Nombre | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-06-04 | INSTRUMENT.description | `yusay.instrument.description` | Descripción | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-06-05 | INSTRUMENT.purpose | `yusay.instrument.purpose` | Propósito | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-07-01 | INSTRUMENT_VERSION.instrument_version_id | `yusay.instrument_version.instrument_version_id` | Identificador de versión | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-05; contrato de intercambio MP-PHYS-016 |
| C-07-02 | INSTRUMENT_VERSION.instrument_id | `yusay.instrument_version.instrument_id` | Identificador de instrumento | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-04; contrato de intercambio MP-PHYS-016 |
| C-07-03 | INSTRUMENT_VERSION.version | `yusay.instrument_version.version` | Entero positivo | integer base / capacidad pendiente | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / MP-PHYS-001; validar rango y cálculos |
| C-07-04 | INSTRUMENT_VERSION.status | `yusay.instrument_version.status` | DRAFT / READY / PUBLISHED / RETIRED | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Valores cerrados; CHECK pendiente |
| C-07-05 | INSTRUMENT_VERSION.source_description | `yusay.instrument_version.source_description` | Descripción de la fuente | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-07-06 | INSTRUMENT_VERSION.population | `yusay.instrument_version.population` | Población de referencia | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-07-07 | INSTRUMENT_VERSION.administration_conditions | `yusay.instrument_version.administration_conditions` | Condiciones de administración | text | No | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-07-08 | INSTRUMENT_VERSION.license_information | `yusay.instrument_version.license_information` | Información de licencia | text | No | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-07-09 | INSTRUMENT_VERSION.limitations | `yusay.instrument_version.limitations` | Limitaciones | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-08-01 | INSTRUMENT_VERSION_REFERENCE.instrument_version_id | `yusay.instrument_version_reference.instrument_version_id` | Identificador de versión | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-05; contrato de intercambio MP-PHYS-016 |
| C-08-02 | INSTRUMENT_VERSION_REFERENCE.reference_order | `yusay.instrument_version_reference.reference_order` | Orden de referencia; entero positivo, sin consecutividad obligatoria | integer base / capacidad pendiente | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / MP-PHYS-001; validar rango y cálculos |
| C-08-03 | INSTRUMENT_VERSION_REFERENCE.citation | `yusay.instrument_version_reference.citation` | Cita de la fuente | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-08-04 | INSTRUMENT_VERSION_REFERENCE.url | `yusay.instrument_version_reference.url` | URL de referencia; detalles de validación no especificados | text | No | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-09-01 | QUESTION.question_id | `yusay.question.question_id` | Identificador de pregunta | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-06; contrato de intercambio MP-PHYS-016 |
| C-09-02 | QUESTION.instrument_version_id | `yusay.question.instrument_version_id` | Identificador de versión | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-05; contrato de intercambio MP-PHYS-016 |
| C-09-03 | QUESTION.position | `yusay.question.position` | Posición en la versión; entero positivo, sin consecutividad obligatoria | integer base / capacidad pendiente | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / MP-PHYS-001; validar rango y cálculos |
| C-09-04 | QUESTION.prompt | `yusay.question.prompt` | Enunciado | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-09-05 | QUESTION.required | `yusay.question.required` | Indicador lógico de respuesta requerida | boolean | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / True al publicar; sin default |
| C-10-01 | ANSWER_OPTION.option_id | `yusay.answer_option.option_id` | Identificador de opción | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-07; contrato de intercambio MP-PHYS-016 |
| C-10-02 | ANSWER_OPTION.question_id | `yusay.answer_option.question_id` | Identificador de pregunta | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-06; contrato de intercambio MP-PHYS-016 |
| C-10-03 | ANSWER_OPTION.position | `yusay.answer_option.position` | Posición dentro de la pregunta; entero positivo, sin consecutividad obligatoria | integer base / capacidad pendiente | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / MP-PHYS-001; validar rango y cálculos |
| C-10-04 | ANSWER_OPTION.label | `yusay.answer_option.label` | Texto de la opción | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-11-01 | SCORING_DEFINITION.instrument_version_id | `yusay.scoring_definition.instrument_version_id` | Identificador de versión | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-05; contrato de intercambio MP-PHYS-016 |
| C-11-02 | SCORING_DEFINITION.method | `yusay.scoring_definition.method` | SUM | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Valores cerrados; CHECK pendiente |
| C-12-01 | SCORING_CONTRIBUTION.option_id | `yusay.scoring_contribution.option_id` | Identificador de opción | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-07; contrato de intercambio MP-PHYS-016 |
| C-12-02 | SCORING_CONTRIBUTION.question_id | `yusay.scoring_contribution.question_id` | Identificador de pregunta | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-06; contrato de intercambio MP-PHYS-016 |
| C-12-03 | SCORING_CONTRIBUTION.instrument_version_id | `yusay.scoring_contribution.instrument_version_id` | Identificador de versión | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-05; contrato de intercambio MP-PHYS-016 |
| C-12-04 | SCORING_CONTRIBUTION.contribution | `yusay.scoring_contribution.contribution` | Entero con signo | integer base / capacidad pendiente | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / MP-PHYS-001; validar rango y cálculos |
| C-13-01 | ASSESSMENT_ATTEMPT.attempt_id | `yusay.assessment_attempt.attempt_id` | Identificador de intento | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-08; contrato de intercambio MP-PHYS-016 |
| C-13-02 | ASSESSMENT_ATTEMPT.user_id | `yusay.assessment_attempt.user_id` | Identificador de propietario | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-01; contrato de intercambio MP-PHYS-016 |
| C-13-03 | ASSESSMENT_ATTEMPT.instrument_id | `yusay.assessment_attempt.instrument_id` | Identificador de instrumento | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-04; contrato de intercambio MP-PHYS-016 |
| C-13-04 | ASSESSMENT_ATTEMPT.instrument_version_id | `yusay.assessment_attempt.instrument_version_id` | Identificador de versión histórica | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-05; contrato de intercambio MP-PHYS-016 |
| C-13-05 | ASSESSMENT_ATTEMPT.status | `yusay.assessment_attempt.status` | IN_PROGRESS / SUBMITTED / EXPIRED / CANCELLED | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Valores cerrados; CHECK pendiente |
| C-13-06 | ASSESSMENT_ATTEMPT.started_at | `yusay.assessment_attempt.started_at` | Instante de inicio | timestamptz | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Precisión/reloj MP-PHYS-008 |
| C-13-07 | ASSESSMENT_ATTEMPT.expires_at | `yusay.assessment_attempt.expires_at` | Instante de expiración | timestamptz | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Precisión/reloj MP-PHYS-008 |
| C-13-08 | ASSESSMENT_ATTEMPT.ended_at | `yusay.assessment_attempt.ended_at` | Instante de terminación; condicional según estado | timestamptz | No | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Precisión/reloj MP-PHYS-008 |
| C-14-01 | ANSWER.attempt_id | `yusay.answer.attempt_id` | Identificador de intento | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-08; contrato de intercambio MP-PHYS-016 |
| C-14-02 | ANSWER.instrument_version_id | `yusay.answer.instrument_version_id` | Identificador de versión histórica | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-05; contrato de intercambio MP-PHYS-016 |
| C-14-03 | ANSWER.question_id | `yusay.answer.question_id` | Identificador de pregunta | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-06; contrato de intercambio MP-PHYS-016 |
| C-14-04 | ANSWER.option_id | `yusay.answer.option_id` | Identificador de opción seleccionada | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-07; contrato de intercambio MP-PHYS-016 |
| C-15-01 | INTERPRETATION.interpretation_id | `yusay.interpretation.interpretation_id` | Identificador de interpretación | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-09; contrato de intercambio MP-PHYS-016 |
| C-15-02 | INTERPRETATION.instrument_version_id | `yusay.interpretation.instrument_version_id` | Identificador de versión | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-05; contrato de intercambio MP-PHYS-016 |
| C-15-03 | INTERPRETATION.label | `yusay.interpretation.label` | Etiqueta oficial | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-15-04 | INTERPRETATION.description | `yusay.interpretation.description` | Descripción respaldada | text | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-15-05 | INTERPRETATION.limitations | `yusay.interpretation.limitations` | Limitaciones de interpretación | text | No | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Sin longitud/default nuevos; formato según dominio |
| C-15-06 | INTERPRETATION.lower_bound | `yusay.interpretation.lower_bound` | Entero con signo; límite inclusivo | integer base / capacidad pendiente | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / MP-PHYS-001; validar rango y cálculos |
| C-15-07 | INTERPRETATION.upper_bound | `yusay.interpretation.upper_bound` | Entero con signo; límite inclusivo | integer base / capacidad pendiente | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / MP-PHYS-001; validar rango y cálculos |
| C-16-01 | ASSESSMENT_RESULT.attempt_id | `yusay.assessment_result.attempt_id` | Identificador de intento | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-08; contrato de intercambio MP-PHYS-016 |
| C-16-02 | ASSESSMENT_RESULT.instrument_version_id | `yusay.assessment_result.instrument_version_id` | Identificador de versión histórica | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-05; contrato de intercambio MP-PHYS-016 |
| C-16-03 | ASSESSMENT_RESULT.score | `yusay.assessment_result.score` | Entero con signo | integer base / capacidad pendiente | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / MP-PHYS-001; validar rango y cálculos |
| C-16-04 | ASSESSMENT_RESULT.interpretation_id | `yusay.assessment_result.interpretation_id` | Identificador de interpretación oficial | uuid | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / IDF-09; contrato de intercambio MP-PHYS-016 |
| C-16-05 | ASSESSMENT_RESULT.calculated_at | `yusay.assessment_result.calculated_at` | Instante de cálculo | timestamptz | Sí | [D02](../logical-model-v1/04-diccionario-datos/02-evaluaciones.md) / Precisión/reloj MP-PHYS-008 |
| C-17-01 | DIMENSION.dimension_id | `yusay.dimension.dimension_id` | Identificador de dimensión | uuid | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / IDF-10; contrato de intercambio MP-PHYS-016 |
| C-17-02 | DIMENSION.code | `yusay.dimension.code` | Código de dimensión | text | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Sin longitud/default nuevos; formato según dominio |
| C-17-03 | DIMENSION.name | `yusay.dimension.name` | Nombre | text | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Sin longitud/default nuevos; formato según dominio |
| C-17-04 | DIMENSION.description | `yusay.dimension.description` | Descripción | text | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Sin longitud/default nuevos; formato según dominio |
| C-18-01 | DIMENSION_VERSION.dimension_version_id | `yusay.dimension_version.dimension_version_id` | Identificador de versión | uuid | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / IDF-11; contrato de intercambio MP-PHYS-016 |
| C-18-02 | DIMENSION_VERSION.dimension_id | `yusay.dimension_version.dimension_id` | Identificador de dimensión | uuid | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / IDF-10; contrato de intercambio MP-PHYS-016 |
| C-18-03 | DIMENSION_VERSION.version | `yusay.dimension_version.version` | Entero positivo | integer base / capacidad pendiente | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / MP-PHYS-001; validar rango y cálculos |
| C-18-04 | DIMENSION_VERSION.definition | `yusay.dimension_version.definition` | Definición de la dimensión en esta versión | text | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Sin longitud/default nuevos; formato según dominio |
| C-18-05 | DIMENSION_VERSION.min_value | `yusay.dimension_version.min_value` | Entero; extremo inferior | integer base / capacidad pendiente | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / MP-PHYS-001; validar rango y cálculos |
| C-18-06 | DIMENSION_VERSION.max_value | `yusay.dimension_version.max_value` | Entero; extremo superior | integer base / capacidad pendiente | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / MP-PHYS-001; validar rango y cálculos |
| C-18-07 | DIMENSION_VERSION.step | `yusay.dimension_version.step` | Entero positivo; incremento de escala | integer base / capacidad pendiente | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / MP-PHYS-001; validar rango y cálculos |
| C-18-08 | DIMENSION_VERSION.status | `yusay.dimension_version.status` | DRAFT / ACTIVE / RETIRED | text | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Valores cerrados; CHECK pendiente |
| C-19-01 | DIMENSION_ANCHOR.dimension_version_id | `yusay.dimension_anchor.dimension_version_id` | Identificador de versión | uuid | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / IDF-11; contrato de intercambio MP-PHYS-016 |
| C-19-02 | DIMENSION_ANCHOR.value | `yusay.dimension_anchor.value` | Entero perteneciente a la escala | integer base / capacidad pendiente | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / MP-PHYS-001; validar rango y cálculos |
| C-19-03 | DIMENSION_ANCHOR.label | `yusay.dimension_anchor.label` | Etiqueta descriptiva | text | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Sin longitud/default nuevos; formato según dominio |
| C-20-01 | CHECK_IN.check_in_id | `yusay.check_in.check_in_id` | Identificador de CheckIn | uuid | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / IDF-12; contrato de intercambio MP-PHYS-016 |
| C-20-02 | CHECK_IN.user_id | `yusay.check_in.user_id` | Identificador del propietario | uuid | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / IDF-01; contrato de intercambio MP-PHYS-016 |
| C-20-03 | CHECK_IN.recorded_at | `yusay.check_in.recorded_at` | Instante al que corresponde el registro | timestamptz | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Precisión/reloj MP-PHYS-008 |
| C-20-04 | CHECK_IN.created_at | `yusay.check_in.created_at` | Instante original de creación | timestamptz | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Precisión/reloj MP-PHYS-008 |
| C-20-05 | CHECK_IN.updated_at | `yusay.check_in.updated_at` | Ausente hasta la primera edición confirmada; después, instante de la última modificación | timestamptz | No | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Precisión/reloj MP-PHYS-008 |
| C-20-06 | CHECK_IN.revision | `yusay.check_in.revision` | Entero positivo; versión de edición | integer base / capacidad pendiente | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / MP-PHYS-001; validar rango y cálculos |
| C-20-07 | CHECK_IN.note | `yusay.check_in.note` | Nota personal opcional | text | No | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Sin longitud/default nuevos; formato según dominio |
| C-21-01 | MEASUREMENT.check_in_id | `yusay.measurement.check_in_id` | Identificador de CheckIn | uuid | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / IDF-12; contrato de intercambio MP-PHYS-016 |
| C-21-02 | MEASUREMENT.dimension_id | `yusay.measurement.dimension_id` | Identificador de dimensión | uuid | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / IDF-10; contrato de intercambio MP-PHYS-016 |
| C-21-03 | MEASUREMENT.dimension_version_id | `yusay.measurement.dimension_version_id` | Identificador de versión histórica | uuid | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / IDF-11; contrato de intercambio MP-PHYS-016 |
| C-21-04 | MEASUREMENT.value | `yusay.measurement.value` | Entero perteneciente a la escala original | integer base / capacidad pendiente | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / MP-PHYS-001; validar rango y cálculos |
| C-22-01 | CONTEXT_TAG.context_tag_id | `yusay.context_tag.context_tag_id` | Identificador de etiqueta | uuid | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / IDF-13; contrato de intercambio MP-PHYS-016 |
| C-22-02 | CONTEXT_TAG.code | `yusay.context_tag.code` | Código de etiqueta | text | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Sin longitud/default nuevos; formato según dominio |
| C-22-03 | CONTEXT_TAG.name | `yusay.context_tag.name` | Nombre | text | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Sin longitud/default nuevos; formato según dominio |
| C-22-04 | CONTEXT_TAG.description | `yusay.context_tag.description` | Descripción | text | No | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Sin longitud/default nuevos; formato según dominio |
| C-22-05 | CONTEXT_TAG.status | `yusay.context_tag.status` | ACTIVE / RETIRED | text | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / Valores cerrados; CHECK pendiente |
| C-23-01 | CHECK_IN_CONTEXT_TAG.check_in_id | `yusay.check_in_context_tag.check_in_id` | Identificador de CheckIn | uuid | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / IDF-12; contrato de intercambio MP-PHYS-016 |
| C-23-02 | CHECK_IN_CONTEXT_TAG.context_tag_id | `yusay.check_in_context_tag.context_tag_id` | Identificador de ContextTag | uuid | Sí | [D03](../logical-model-v1/04-diccionario-datos/03-seguimiento.md) / IDF-13; contrato de intercambio MP-PHYS-016 |
| C-24-01 | INSTRUMENT_VERSION_COMPATIBILITY.version_a_id | `yusay.instrument_version_compatibility.version_a_id` | Identificador de la versión situada primero según el orden canónico total y estable | uuid | Sí | [D04](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) / IDF-05; contrato de intercambio MP-PHYS-016 |
| C-24-02 | INSTRUMENT_VERSION_COMPATIBILITY.version_b_id | `yusay.instrument_version_compatibility.version_b_id` | Identificador de la otra versión según ese mismo orden | uuid | Sí | [D04](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) / IDF-05; contrato de intercambio MP-PHYS-016 |
| C-24-03 | INSTRUMENT_VERSION_COMPATIBILITY.instrument_id | `yusay.instrument_version_compatibility.instrument_id` | Identificador del Instrument común | uuid | Sí | [D04](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) / IDF-04; contrato de intercambio MP-PHYS-016 |
| C-24-04 | INSTRUMENT_VERSION_COMPATIBILITY.rationale | `yusay.instrument_version_compatibility.rationale` | Justificación documentada de compatibilidad | text | Sí | [D04](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) / Sin longitud/default nuevos; formato según dominio |
| C-24-05 | INSTRUMENT_VERSION_COMPATIBILITY.reference | `yusay.instrument_version_compatibility.reference` | Referencia de respaldo | text | No | [D04](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) / Sin longitud/default nuevos; formato según dominio |
| C-25-01 | DIMENSION_VERSION_COMPATIBILITY.version_a_id | `yusay.dimension_version_compatibility.version_a_id` | Identificador de la versión situada primero según el orden canónico total y estable | uuid | Sí | [D04](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) / IDF-11; contrato de intercambio MP-PHYS-016 |
| C-25-02 | DIMENSION_VERSION_COMPATIBILITY.version_b_id | `yusay.dimension_version_compatibility.version_b_id` | Identificador de la otra versión según ese mismo orden | uuid | Sí | [D04](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) / IDF-11; contrato de intercambio MP-PHYS-016 |
| C-25-03 | DIMENSION_VERSION_COMPATIBILITY.dimension_id | `yusay.dimension_version_compatibility.dimension_id` | Identificador del Dimension común | uuid | Sí | [D04](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) / IDF-10; contrato de intercambio MP-PHYS-016 |
| C-25-04 | DIMENSION_VERSION_COMPATIBILITY.rationale | `yusay.dimension_version_compatibility.rationale` | Justificación documentada de compatibilidad | text | Sí | [D04](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) / Sin longitud/default nuevos; formato según dominio |
| C-25-05 | DIMENSION_VERSION_COMPATIBILITY.reference | `yusay.dimension_version_compatibility.reference` | Referencia de respaldo | text | No | [D04](../logical-model-v1/04-diccionario-datos/04-compatibilidad.md) / Sin longitud/default nuevos; formato según dominio |
| C-26-01 | TOPIC.topic_id | `yusay.topic.topic_id` | Identificador de Topic | uuid | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / IDF-14; contrato de intercambio MP-PHYS-016 |
| C-26-02 | TOPIC.code | `yusay.topic.code` | Código de Topic | text | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / Sin longitud/default nuevos; formato según dominio |
| C-26-03 | TOPIC.name | `yusay.topic.name` | Nombre | text | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / Sin longitud/default nuevos; formato según dominio |
| C-26-04 | TOPIC.description | `yusay.topic.description` | Descripción | text | No | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / Sin longitud/default nuevos; formato según dominio |
| C-27-01 | RESOURCE.resource_id | `yusay.resource.resource_id` | Identificador de Resource | uuid | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / IDF-15; contrato de intercambio MP-PHYS-016 |
| C-27-02 | RESOURCE.type | `yusay.resource.type` | ARTICLE / EXTERNAL_LINK | text | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / Valores cerrados; CHECK pendiente |
| C-27-03 | RESOURCE.status | `yusay.resource.status` | DRAFT / PUBLISHED / RETIRED | text | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / Valores cerrados; CHECK pendiente |
| C-27-04 | RESOURCE.title | `yusay.resource.title` | Título obligatorio y no vacío desde DRAFT | text | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / Sin longitud/default nuevos; formato según dominio |
| C-27-05 | RESOURCE.summary | `yusay.resource.summary` | Resumen opcional | text | No | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / Sin longitud/default nuevos; formato según dominio |
| C-27-06 | RESOURCE.body | `yusay.resource.body` | Contenido de artículo; condicional al publicar | text | No | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / Sin longitud/default nuevos; formato según dominio |
| C-27-07 | RESOURCE.external_url | `yusay.resource.external_url` | URL de enlace externo; condicional al publicar | text | No | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / Sin longitud/default nuevos; formato según dominio |
| C-28-01 | RESOURCE_TOPIC.resource_id | `yusay.resource_topic.resource_id` | Identificador de RESOURCE | uuid | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / IDF-15; contrato de intercambio MP-PHYS-016 |
| C-28-02 | RESOURCE_TOPIC.topic_id | `yusay.resource_topic.topic_id` | Identificador de Topic | uuid | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / IDF-14; contrato de intercambio MP-PHYS-016 |
| C-29-01 | INSTRUMENT_TOPIC.instrument_id | `yusay.instrument_topic.instrument_id` | Identificador de INSTRUMENT | uuid | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / IDF-04; contrato de intercambio MP-PHYS-016 |
| C-29-02 | INSTRUMENT_TOPIC.topic_id | `yusay.instrument_topic.topic_id` | Identificador de Topic | uuid | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / IDF-14; contrato de intercambio MP-PHYS-016 |
| C-30-01 | INTERPRETATION_TOPIC.interpretation_id | `yusay.interpretation_topic.interpretation_id` | Identificador de INTERPRETATION | uuid | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / IDF-09; contrato de intercambio MP-PHYS-016 |
| C-30-02 | INTERPRETATION_TOPIC.topic_id | `yusay.interpretation_topic.topic_id` | Identificador de Topic | uuid | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / IDF-14; contrato de intercambio MP-PHYS-016 |
| C-31-01 | DIMENSION_TOPIC.dimension_id | `yusay.dimension_topic.dimension_id` | Identificador de DIMENSION | uuid | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / IDF-10; contrato de intercambio MP-PHYS-016 |
| C-31-02 | DIMENSION_TOPIC.topic_id | `yusay.dimension_topic.topic_id` | Identificador de Topic | uuid | Sí | [D05](../logical-model-v1/04-diccionario-datos/05-contenido.md) / IDF-14; contrato de intercambio MP-PHYS-016 |
| C-32-01 | AUDIT_EVENT.audit_event_id | `yusay.audit_event.audit_event_id` | Identificador de evento; opaco, estable, sin significado de negocio | uuid | Sí | [D06](../logical-model-v1/04-diccionario-datos/06-auditoria.md) / IDF-16; contrato de intercambio MP-PHYS-016 |
| C-32-02 | AUDIT_EVENT.actor_user_id | `yusay.audit_event.actor_user_id` | Identificador de User actor, desvinculable | uuid | No | [D06](../logical-model-v1/04-diccionario-datos/06-auditoria.md) / IDF-01; contrato de intercambio MP-PHYS-016 |
| C-32-03 | AUDIT_EVENT.actor_kind | `yusay.audit_event.actor_kind` | Clasificación del actor; catálogo cerrado aprobado | text | Sí | [D06](../logical-model-v1/04-diccionario-datos/06-auditoria.md) / Valores cerrados; CHECK pendiente |
| C-32-04 | AUDIT_EVENT.action | `yusay.audit_event.action` | Acción administrativa/de seguridad; catálogo cerrado aprobado | text | Sí | [D06](../logical-model-v1/04-diccionario-datos/06-auditoria.md) / Valores cerrados; CHECK pendiente |
| C-32-05 | AUDIT_EVENT.target_type | `yusay.audit_event.target_type` | Tipo de destino; catálogo cerrado aprobado | text | Sí | [D06](../logical-model-v1/04-diccionario-datos/06-auditoria.md) / Valores cerrados; CHECK pendiente |
| C-32-06 | AUDIT_EVENT.target_identifier | `yusay.audit_event.target_identifier` | Identificador del destino, sujeto a supresión personal | text (serialización canónica estricta) | No | [D06](../logical-model-v1/04-diccionario-datos/06-auditoria.md) / MP-PHYS-001/003; text nativo, no nueva FK |
| C-32-07 | AUDIT_EVENT.occurred_at | `yusay.audit_event.occurred_at` | Instante de ocurrencia del evento | timestamptz | Sí | [D06](../logical-model-v1/04-diccionario-datos/06-auditoria.md) / Precisión/reloj MP-PHYS-008 |
| C-32-08 | AUDIT_EVENT.metadata | `yusay.audit_event.metadata` | Información permitida por acción, sin datos privados | jsonb | No | [D06](../logical-model-v1/04-diccionario-datos/06-auditoria.md) / Perfiles N/F/D/C/E/T/P; MP-PHYS-003 |

**Cobertura comprobada:** 32 relaciones / 147 atributos; 62 uuid, 16 timestamptz, 15 integer de base analizados (capacidad suficiente con `integer` estándar de 32 bits, evitando desbordamientos), un boolean, un jsonb, 52 text (incluyendo `target_identifier` resuelto físicamente como `text`). No se modifica nulabilidad ni orden de claves. Se preservan estrictamente los 147 atributos normativos.

## OQ-PHYS-006

**Status: RESOLVED en alcance arquitectónico.** AUDIT_EVENT.metadata usa jsonb; no se agrega relación ni atributo. Los catálogos y la matriz exacta de combinaciones son los aprobados en el [diccionario de Auditoría](../logical-model-v1/04-diccionario-datos/06-auditoria.md): tres actor_kind (USER, ADMINISTRATOR, ANONYMOUS), 28 action y doce target_type. SYSTEM no pertenece al catálogo MVP. CATALOG_UPDATED usa C para catálogos generales y E para RESOURCE.

### Correspondencia de perfiles aprobados

| Perfil | Claves permitidas / obligatoriedad | Dominio y aplicación de fuente |
| --- | --- | --- |
| N | Sin metadata | Ausencia; no payload libre ni objeto vacío como sustituto impuesto |
| F | reason_code opcional | CREDENTIALS_NOT_ACCEPTED; sin enumerar cuentas |
| D | reason_code opcional | AUTHENTICATION_REQUIRED, ACCOUNT_NOT_ACTIVE, EMAIL_NOT_VERIFIED, OPERATION_NOT_AUTHORIZED |
| C | changed_fields opcional | Solo nombres/categorías permitidos por target_type en el diccionario; sin valores anteriores/nuevos. Categorías de definición versionada solo en DRAFT |
| E | changed_fields, correction_kind opcionales conforme a fuente | Campos editoriales aprobados y SPELLING, PUNCTUATION, FORMAT, SURFACE_CLARITY, SAME_CONTENT_LINK_REPAIR; no convierte cambios sustanciales en correcciones |
| T | topic_id obligatorio para las dos acciones de asociación | EDITORIAL_ASSOCIATION_ADDED / EDITORIAL_ASSOCIATION_REMOVED; identidad Topic existente, sin nueva FK |
| P | version_a_id y version_b_id obligatorios | COMPATIBILITY_DECLARED; familia de versiones del catálogo correspondiente y par canónico |

El diccionario conserva el listado exacto de changed_fields por destino, las 28 acciones y todas las combinaciones actor/action/target_type; no se amplían por este documento. PASSWORD_CHANGED es cambio autenticado y PASSWORD_RESET_COMPLETED recuperación. CONTEXT_TAG_ACTIVATED es reactivación. USER_REGISTERED no exige sesión previa. USER_DELETED carece de identificadores personales persistentes.

jsonb aporta representación, **no** validación automática de perfiles. PostgreSQL protegerá restricciones estructurales apropiadas y el backend construirá metadata autorizada según la operación. Se debe validar combinación, claves permitidas, dominios y presencia T/P. Nulabilidad del atributo no elimina exigencias por acción. La representación de UUID dentro de metadata y de target_identifier queda por documentar, sin inventar atributos o tipos lógicos.

Se prohíben metadata arbitraria, datos privados de bienestar, contraseñas, hashes, tokens, correos, IP, user-agent, identificadores de sesión y payloads libres. changed_fields contiene solo nombres/categorías autorizados, sin valores antes/después. No se auditan operaciones privadas de Attempts, CheckIns o Results. La desvinculación conserva actor_kind histórico y elimina referencias personales conforme a 05; retención máxima de 180 períodos de 24 horas desde occurred_at.

### Especificación técnica física de auditoría y perfiles (MP-PHYS-003)

1. **Almacenamiento y semántica de `metadata`:**
   - Tipo de datos nativo: `jsonb`.
   - Distinción semántica estricta:
     * *Ausencia de metadata:* La columna `metadata` es `NULL` a nivel SQL.
     * *JSON null:* Prohibido. Se rechaza `metadata = 'null'::jsonb`.
     * *Objeto vacío:* `metadata = '{}'::jsonb`. Válido únicamente donde un perfil permita ausencia de campos opcionales dentro de un objeto ya instanciado, pero para perfil N la exigencia es `metadata IS NULL`.
   - Prohibición de claves adicionales: Toda clave en `metadata` debe pertenecer al catálogo cerrado de su perfil correspondiente. Se rechaza cualquier clave no autorizada.
2. **Validación estructural por perfil en PostgreSQL y Backend:**
   - **Perfil N (Sin metadata):**
     * Acciones: `USER_REGISTERED`, `EMAIL_VERIFICATION_TOKEN_ISSUED`, `EMAIL_VERIFIED`, `PASSWORD_RESET_TOKEN_ISSUED`, `PASSWORD_RESET_COMPLETED`, `PASSWORD_CHANGED`, `SIGN_IN_SUCCEEDED`, `SIGN_OUT`, `USER_BLOCKED`, `USER_UNBLOCKED`, `USER_DELETED`, `CATALOG_CREATED`, `INSTRUMENT_VERSION_READY`, `INSTRUMENT_VERSION_RETURNED_TO_DRAFT`, `INSTRUMENT_VERSION_PUBLISHED`, `INSTRUMENT_VERSION_RETIRED`, `DIMENSION_VERSION_ACTIVATED`, `DIMENSION_VERSION_RETIRED`, `CONTEXT_TAG_ACTIVATED`, `CONTEXT_TAG_RETIRED`, `RESOURCE_PUBLISHED`, `RESOURCE_RETIRED`.
     * Guarda física: `metadata IS NULL`.
   - **Perfil F (Fallo de acceso):**
     * Acción: `SIGN_IN_FAILED`.
     * Claves: `reason_code` opcional. Valor permitido exclusivo: `'CREDENTIALS_NOT_ACCEPTED'`.
     * Guarda: `metadata IS NULL OR (jsonb_typeof(metadata) = 'object' AND (metadata ? 'reason_code' AND metadata->>'reason_code' = 'CREDENTIALS_NOT_ACCEPTED' AND jsonb_object_keys(metadata) = 1))`.
   - **Perfil D (Denegación de autorización):**
     * Acción: `AUTHORIZATION_DENIED`.
     * Claves: `reason_code` opcional. Valores permitidos: `'AUTHENTICATION_REQUIRED'`, `'ACCOUNT_NOT_ACTIVE'`, `'EMAIL_NOT_VERIFIED'`, `'OPERATION_NOT_AUTHORIZED'`.
     * Guarda: `metadata IS NULL OR (jsonb_typeof(metadata) = 'object' AND (metadata->>'reason_code' IN ('AUTHENTICATION_REQUIRED', 'ACCOUNT_NOT_ACTIVE', 'EMAIL_NOT_VERIFIED', 'OPERATION_NOT_AUTHORIZED') AND (jsonb_object_keys(metadata) = 1)))`.
   - **Perfil C (Edición de catálogo general):**
     * Acción: `CATALOG_UPDATED` sobre destinos que no son `RESOURCE`.
     * Claves: `changed_fields` opcional (array jsonb de strings sin valores antes/después).
     * Solo nombres/categorías permitidos por target_type en el diccionario:
       - `INSTRUMENT`: `['name', 'description', 'purpose']`.
       - `INSTRUMENT_VERSION`: `['version', 'source_description', 'population', 'administration_conditions', 'license_information', 'limitations', 'REFERENCES', 'QUESTIONS', 'ANSWER_OPTIONS', 'SCORING', 'INTERPRETATIONS']`.
       - `DIMENSION`: `['name', 'description']`.
       - `DIMENSION_VERSION`: `['version', 'definition', 'min_value', 'max_value', 'step']`.
       - `CONTEXT_TAG`: `['name', 'description']`.
       - `TOPIC`: `['name', 'description']`.
   - **Perfil E (Edición editorial de Resource):**
     * Acción: `CATALOG_UPDATED` sobre target_type `RESOURCE`.
     * Claves: `changed_fields` (array con valores entre `'title'`, `'summary'`, `'body'`, `'external_url'`) y `correction_kind` opcional (`'SPELLING'`, `'PUNCTUATION'`, `'FORMAT'`, `'SURFACE_CLARITY'`, `'SAME_CONTENT_LINK_REPAIR'`).
   - **Perfil T (Asociación editorial):**
     * Acciones: `EDITORIAL_ASSOCIATION_ADDED`, `EDITORIAL_ASSOCIATION_REMOVED`.
     * Claves obligatorias: `topic_id` obligatorio con formato UUID v4 canónico (`'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'`).
     * Guarda: `metadata IS NOT NULL AND metadata ? 'topic_id' AND metadata->>'topic_id' ~ '^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'`.
   - **Perfil P (Declaración de compatibilidad):**
     * Acción: `COMPATIBILITY_DECLARED`.
     * Claves obligatorias: `version_a_id` y `version_b_id` ambos con formato UUID v4 canónico y orden canónico de bytes `version_a_id < version_b_id`.
     * Guarda: `metadata IS NOT NULL AND metadata ? 'version_a_id' AND metadata ? 'version_b_id'`.
3. **Serialización canónica de `target_identifier`:**
   - Para destinos que poseen un identificador simple de tipo UUID (`USER`, `INSTRUMENT`, `INSTRUMENT_VERSION`, `DIMENSION`, `DIMENSION_VERSION`, `CONTEXT_TAG`, `TOPIC`, `RESOURCE`, `INTERPRETATION`): Se serializa en minúsculas en formato canónico de 36 caracteres: `xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx`.
   - Para destinos sin identificador (`AUTHENTICATION`): `target_identifier IS NULL`.
   - Para relaciones de compatibilidad (`INSTRUMENT_VERSION_COMPATIBILITY`, `DIMENSION_VERSION_COMPATIBILITY`): `target_identifier IS NULL`, identificándose el par obligatoriamente mediante metadata P.
4. **Validación de actor, acción y destino:**
   - La tupla `(actor_kind, action, target_type)` se valida contra la matriz exacta de 28 filas aprobadas en el diccionario D06 mediante restricción CHECK compuesta en `AUDIT_EVENT`.
   - Comprobación de existencia del destino: Antes de emitir un evento con `target_identifier`, la capa de servicio backend verifica la existencia del registro en la tabla correspondiente, evitando eventos huérfanos con identificadores inexistentes.
   - Desvinculación de referencias personales: Ante la eliminación de una cuenta (`app_user`), una rutina transaccional actualiza `AUDIT_EVENT` estableciendo `actor_user_id = NULL` donde la cuenta fue actor, y `target_identifier = NULL` donde `target_type = 'USER'` coincida con el `user_id` suprimido. El evento `USER_DELETED` se inserta sin identificador de usuario ni copia de email.

## Trazabilidad de la consolidación

OQ-PHYS-007 se desarrolla en [03](03-integridad-e-indices.md) y [04](04-transacciones-y-concurrencia.md); OQ-PHYS-008/010 en [05](05-privacidad-eliminacion-y-operacion.md); OQ-PHYS-009 en 03. El [contexto](01-contexto-y-alcance.md) registra decisiones, mecanismos, conflicto y correspondencia TF-PHYS. La aprobación arquitectónica no constituye implementación ni cierre del diseño físico.

