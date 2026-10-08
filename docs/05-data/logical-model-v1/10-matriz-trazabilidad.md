# Matriz de correspondencia y trazabilidad

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md).

## Fuentes y lectura

- [Especificación maestra lógica v1.0](especificacion-maestra-v1.0.md): fuente aprobada de atributos, PK, AK, FK y URA, conservada sin modificaciones.
- [Conceptual v0.1 CLOSED](../../03-domain/conceptual-model.md) y [clasificación de conceptos](../../03-domain/aggregate-candidates.md): antecedente conceptual preservado.
- AJ-01..04, VF-01..05 y reglas adicionales del prompt maestro; aclaración posterior del usuario sobre bloqueo de publicación sin interpretaciones respaldadas.
- [RF](../../02-product/functional-requirements.md), [RN](../../02-product/business-rules.md) y [RNF](../../02-product/non-functional-requirements.md): IDs existentes; las referencias por intervalo incluyen únicamente IDs ya presentes.

**Directa** identifica un concepto previamente documentado. **Descomposición lógica** o **asociación lógica** identifica su representación relacional aprobada; no crea un nuevo Aggregate Root ni resuelve una clasificación candidate. La matriz compara capas y no reconstruye atributos desde el conceptual.

Las referencias a OQ-DATA señalan trazabilidad histórica: el hecho de recibir una representación aprobada no modifica silenciosamente el estado de las preguntas en documentos anteriores.

## Correspondencia de las 32 relaciones

### Identidad — 5

| N.º / relación aprobada | Antecedente conceptual | Trazabilidad | Resultado del contraste |
| --- | --- | --- | --- |
| [1. USER](especificacion-maestra-v1.0.md#1-user) | User — Identity | RF-001, RF-002, RF-004; AJ-04; VF-02 | Directa. AJ-04 precisa espacios exteriores, dominio normalizado, unicidad sin distinción de mayúsculas, sin reglas de proveedores ni cambio de correo; confirmación adulta, estados y verificación. |
| [2. USER_CREDENTIAL](especificacion-maestra-v1.0.md#2-user_credential) | Acceso de User; sin objeto separado confirmado | RF-002, RF-003; AJ-04; VF-01 | Descomposición lógica de credenciales; PK/FK user_id aprobada, sin nueva raíz conceptual. |
| [3. ADMINISTRATOR](especificacion-maestra-v1.0.md#3-administrator) | Administrator — actor, no raíz adicional | RN-026; AJ-04; VF-01 | Representa habilitación administrativa mediante PK/FK a USER; no concede acceso privado. |
| [4. EMAIL_VERIFICATION_TOKEN](especificacion-maestra-v1.0.md#4-email_verification_token) | Verificación de correo — capacidad Identity | RF-001; AJ-04; VF-04, VF-05 | Relación técnica lógica aprobada; hash, expiración y eliminación al consumir, sin consumed_at. |
| [5. PASSWORD_RESET_TOKEN](especificacion-maestra-v1.0.md#5-password_reset_token) | Recuperación — capacidad Identity | RF-003; AJ-04; VF-04, VF-05 | Relación técnica lógica aprobada; sin consumed_at; recuperación no verifica ni desbloquea. |

### Evaluaciones — 11

| N.º / relación aprobada | Antecedente conceptual | Trazabilidad | Resultado del contraste |
| --- | --- | --- | --- |
| [6. INSTRUMENT](especificacion-maestra-v1.0.md#6-instrument) | Instrument — Aggregate Root | DR-DOM-001; RN-001, RN-002; RF-005, RF-019 | Directa. Identidad del instrumento distinta de la versión. |
| [7. INSTRUMENT_VERSION](especificacion-maestra-v1.0.md#7-instrument_version) | InstrumentVersion — Aggregate Root | DR-DOM-006; RN-003..007, RN-039, RN-040; VF-02 | Directa. AK por instrumento/version y URA aprobadas; READY congelada es precisión nueva. |
| [8. INSTRUMENT_VERSION_REFERENCE](especificacion-maestra-v1.0.md#8-instrument_version_reference) | Fuente/referencias de la definición histórica | DR-DOM-006; RN-002; RF-005, RF-019; VF-01 | Descomposición lógica de referencias; PK por versión y reference_order aprobada. |
| [9. QUESTION](especificacion-maestra-v1.0.md#9-question) | Question — Entity de InstrumentVersion | DR-DOM-001; RN-012, RN-014, RN-029; VF-01 | Directa. required y position concretan requiredness/orden; REV-LOG-004 exige required = true al publicar para MVP, sin alterar configuración histórica. |
| [10. ANSWER_OPTION](especificacion-maestra-v1.0.md#10-answer_option) | AnswerOption — Entity interna | DR-DOM-001; RN-013, RN-029; VF-01 | Directa. No contiene score; contribuciones permanecen separadas. |
| [11. SCORING_DEFINITION](especificacion-maestra-v1.0.md#11-scoring_definition) | ScoringDefinition — Definition Object dependiente | DR-DOM-005; RN-015, RN-036; AJ-01; VF-01, VF-05 | Directa como representación lógica; PK/FK de versión y method SUM no la convierten en raíz. |
| [12. SCORING_CONTRIBUTION](especificacion-maestra-v1.0.md#12-scoring_contribution) | Contribuciones de ScoringDefinition | DR-DOM-005; RN-015, RN-036; AJ-01; VF-01, VF-05 | Descomposición lógica aprobada; option_id PK y FKs compuestas. No confirma una Entity conceptual. |
| [13. ASSESSMENT_ATTEMPT](especificacion-maestra-v1.0.md#13-assessment_attempt) | AssessmentAttempt — Aggregate Root | RN-008..011, RN-016; RF-006..008; VF-01..04 | Directa. Vigencia de 720 horas y un IN_PROGRESS por usuario/instrumento. Tras RETIRED puede continuar/enviarse si se inició válidamente PUBLISHED, sigue IN_PROGRESS y no expiró; conserva versión exacta. REV-LOG-006 resuelta. |
| [14. ANSWER](especificacion-maestra-v1.0.md#14-answer) | Answer — Entity interna de AssessmentAttempt | DR-DOM-001; RN-012..014; VF-01, VF-04 | Directa. PK por intento/pregunta; FKs compuestas conservan versión y opción correctas. REV-LOG-004 exige una Answer válida por cada Question para SUBMITTED, validada transaccionalmente. |
| [15. INTERPRETATION](especificacion-maestra-v1.0.md#15-interpretation) | Interpretation — definición histórica dependiente | DR-DOM-004, DR-DOM-005; RN-038; AJ-01; VF-01 | Cambio de alcance C-LOG-001: cobertura obligatoria de todo score alcanzable mediante respuestas completas válidas en el MVP (REV-LOG-004 resuelta). |
| [16. ASSESSMENT_RESULT](especificacion-maestra-v1.0.md#16-assessment_result) | AssessmentResult — dependiente del límite de Attempt | DR-DOM-005; RN-016..018, RN-037, RN-038; AJ-01; VF-01, VF-03 | Directa con cambio C-LOG-001: interpretation_id obligatorio; PK/FKs preservan intento y versión. |

### Seguimiento — 7

| N.º / relación aprobada | Antecedente conceptual | Trazabilidad | Resultado del contraste |
| --- | --- | --- | --- |
| [17. DIMENSION](especificacion-maestra-v1.0.md#17-dimension) | Dimension — Aggregate Root de Tracking | DR-DOM-002; RN-020, RN-021; RF-021; AJ-03 | Directa. Identidad estable distinta de DimensionVersion. |
| [18. DIMENSION_VERSION](especificacion-maestra-v1.0.md#18-dimension_version) | DimensionVersion — Entity interna de Dimension | DR-DOM-002; RN-020, RN-031; AJ-03; VF-01, VF-02 | Directa. Escala entera y lifecycle DRAFT → ACTIVE → RETIRED precisan la definición histórica; reemplazo coordina retiro y activación atómicos. |
| [19. DIMENSION_ANCHOR](especificacion-maestra-v1.0.md#19-dimension_anchor) | Labels/anchors de DimensionVersion | DR-DOM-002; AJ-03; VF-01 | Descomposición lógica de anchors; valor pertenece a escala, regla no garantizada solo por FK. |
| [20. CHECK_IN](especificacion-maestra-v1.0.md#20-check_in) | CheckIn — Aggregate Root; Note — Value 0..1 | DR-DOM-007; RN-019, RN-022, RN-030, RN-041; AJ-03; VF-03, VF-04 | Directa. note? representa Note. recorded_at inicial y editable permanece en [created_at - 168 horas, created_at], inclusivo; edición antes de created_at + 168 horas incrementa revision una vez por operación transaccional confirmada; updated_at ausente hasta primera edición y luego última modificación. REV-LOG-005 resuelta. |
| [21. MEASUREMENT](especificacion-maestra-v1.0.md#21-measurement) | Measurement — Value Object candidate de CheckIn | DR-DOM-002; RN-020, RN-030, RN-031; AJ-03; VF-01 | Representación lógica aprobada sin cambiar candidate. PK por CheckIn/Dimension y FK compuesta de versión. |
| [22. CONTEXT_TAG](especificacion-maestra-v1.0.md#22-context_tag) | ContextTag — Entity/catalog concept de Tracking | DR-DOM-004; RF-015; VF-02 | Directa. ACTIVE ↔ RETIRED no altera su significado; distinto de Topic. |
| [23. CHECK_IN_CONTEXT_TAG](especificacion-maestra-v1.0.md#23-check_in_context_tag) | Asociación CheckIn ↔ ContextTag | Relaciones conceptuales de Tracking; RF-015; VF-01 | Materializa asociación aprobada; solo se agregan tags ACTIVE y se preservan vínculos históricos. |

### Compatibilidad — 2

| N.º / relación aprobada | Antecedente conceptual | Trazabilidad | Resultado del contraste |
| --- | --- | --- | --- |
| [24. INSTRUMENT_VERSION_COMPATIBILITY](especificacion-maestra-v1.0.md#24-instrument_version_compatibility) | ComparabilityPolicy — pares del mismo Instrument | DR-DOM-003; RN-023; OQ-DATA-003; VF-01 | Representación de política mediante relación lógica aprobada; pares simétricos no transitivos. |
| [25. DIMENSION_VERSION_COMPATIBILITY](especificacion-maestra-v1.0.md#25-dimension_version_compatibility) | ComparabilityPolicy — pares de la misma Dimension | DR-DOM-003; RN-023; OQ-DATA-003; VF-01 | Representación de política mediante relación lógica aprobada; versiones de la misma Dimension. |

### Contenido — 6

| N.º / relación aprobada | Antecedente conceptual | Trazabilidad | Resultado del contraste |
| --- | --- | --- | --- |
| [26. TOPIC](especificacion-maestra-v1.0.md#26-topic) | Topic — Entity/catalog concept de Content | DR-DOM-004; RN-034; VF-01 | Directa. Vocabulario de contenido, no ContextTag. |
| [27. RESOURCE](especificacion-maestra-v1.0.md#27-resource) | Resource — Aggregate Root de Content | DR-DOM-004; RN-033; RF-022, RF-023; AJ-02; VF-02, VF-05 | Directa. Tipos, validación por tipo, publicación y retiro precisan el ciclo editorial. |
| [28. RESOURCE_TOPIC](especificacion-maestra-v1.0.md#28-resource_topic) | Resource ↔ Topic | DR-DOM-004; RN-034; RF-022; AJ-02; VF-01 | Asociación lógica aprobada; al menos un Topic para publicar Resource exige validación transaccional. |
| [29. INSTRUMENT_TOPIC](especificacion-maestra-v1.0.md#29-instrument_topic) | Instrument ↔ Topic | DR-DOM-004; RN-034; VF-01 | Asociación lógica aprobada con Instrument, no InstrumentVersion. |
| [30. INTERPRETATION_TOPIC](especificacion-maestra-v1.0.md#30-interpretation_topic) | Interpretation ↔ Topic | DR-DOM-004; RN-034; RF-022; VF-01 | Asociación lógica aprobada de interpretación histórica y vocabulario de contenido. |
| [31. DIMENSION_TOPIC](especificacion-maestra-v1.0.md#31-dimension_topic) | Dimension ↔ Topic | DR-DOM-004; RN-034; RF-022; VF-01 | Asociación lógica aprobada con Dimension, no DimensionVersion. |

### Auditoría — 1

| N.º / relación aprobada | Antecedente conceptual | Trazabilidad | Resultado del contraste |
| --- | --- | --- | --- |
| [32. AUDIT_EVENT](especificacion-maestra-v1.0.md#32-audit_event) | Auditoría — capacidad Platform | RN-027; RNF-007; VF-04, VF-05 | Relación lógica de soporte aprobada; desvinculación personal y catálogos exactos aprobados en Auditoría (REV-LOG-001 RESOLVED). |

## Correspondencias sin relación persistente adicional

- Note se representa mediante CHECK_IN.note opcional, compatible con 0..1 Value.
- Timeline y Trend siguen siendo read models/información derivada, sin relación independiente.
- Guidance sigue siendo capacidad compuesta de Interpretation, documentación, Topic y Resource.
- ComparabilityPolicy sigue siendo política conceptual; las dos relaciones de compatibilidad no la convierten en Aggregate Root.
- ScoringDefinition sigue siendo Definition Object; SCORING_CONTRIBUTION no confirma ScoringRule/ScoringMapping como Entity conceptual.
- El inventario no introduce Topics vinculados directamente con InstrumentVersion o DimensionVersion ni relaciones directas Resource con instrumentos o dimensiones.

## Comprobación de referencias compuestas

La fuente declara URA en INSTRUMENT_VERSION, QUESTION, ANSWER_OPTION, ASSESSMENT_ATTEMPT, INTERPRETATION y DIMENSION_VERSION. Las combinaciones referenciadas están documentadas:

- ASSESSMENT_ATTEMPT → INSTRUMENT_VERSION por (instrument_id, instrument_version_id).
- ANSWER → ASSESSMENT_ATTEMPT por (attempt_id, instrument_version_id); → QUESTION por (instrument_version_id, question_id); → ANSWER_OPTION por (question_id, option_id).
- SCORING_CONTRIBUTION → SCORING_DEFINITION por instrument_version_id; → QUESTION por (instrument_version_id, question_id); → ANSWER_OPTION por (question_id, option_id).
- ASSESSMENT_RESULT → ASSESSMENT_ATTEMPT por attempt_id y por (attempt_id, instrument_version_id); → INTERPRETATION por (instrument_version_id, interpretation_id). Se conserva la referencia simple redundante indicada por la fuente, sin eliminarla por conveniencia.
- MEASUREMENT → DIMENSION_VERSION por (dimension_id, dimension_version_id).
- Cada compatibilidad refiere dos versiones mediante la identidad común de su Instrument o Dimension.

No se identificó una referencia compuesta hacia componentes ausentes en la clave aprobada de destino. URA se conserva como unicidad adicional para referencias compuestas, sin reclasificarla automáticamente como AK mínima.

Las FKs no garantizan por sí solas SUBMITTED con exactamente un Result, READY completa, cobertura de scores alcanzables, una Measurement mínima, estado ACTIVE al registrar ni un Topic mínimo al publicar. Esas invariantes se trazan a validación/transacciones; sus mecanismos no se eligen aquí.

## Conflicto comprobado y precisiones

**C-LOG-001 — obligatoriedad de Interpretation.** DR-DOM-005 y DR-DOM-006, RN-038 y RF-008 conservan interpretación condicional según la fuente. AJ-01 y la especificación exigen interpretation_id obligatorio y una Interpretation por cada score alcanzable. Es una diferencia de alcance real entre capas. La aclaración autorizada bloquea publicación si la fuente no aporta interpretaciones respaldadas; no autoriza inventarlas. El baseline original no se reescribe ni se presenta como actualizado.

**READY congelada** es una precisión de VF-02: el conceptual no autorizaba expresamente editar READY, pero tampoco declaraba su congelación. No se clasifica como contradicción comprobada.

**Eliminación e historia** no se contradicen por sí mismas: se preservan definiciones históricas, mientras VF-04 permite suprimir datos personales. La implementación tendrá que preservar esa distinción, sin inferir cascadas físicas.

Escalas enteras, lifecycle de DimensionVersion, revision, vencimientos, Resources y retención desarrollan asuntos previamente abiertos. No se clasifican como conflictos solo por no estar definidos en la etapa conceptual.

## Pendientes reales y precisiones resueltas

Se registran en [pendientes y riesgos](11-pendientes-y-riesgos.md#pendientes-de-revisión), con IDs de revisión; no se convierten en atributos, claves ni nuevas reglas:

- REV-LOG-001: RESOLVED para MVP; tres actores, 28 acciones, doce destinos y siete perfiles; T/P obligatorios por acción y desvinculación personal.
- REV-LOG-002: RESOLVED para MVP lógico; dominios transversales, orden total estable, posiciones positivas, límites enteros inclusivos, UTC y días de 24 horas. Diseño físico diferido.
- REV-LOG-003: RESOLVED en reglas lógicas de email; los mecanismos técnicos siguen abiertos.
- REV-LOG-004: RESOLVED para MVP; preguntas requeridas y exactamente una Answer válida por cada Question antes de SUBMITTED. SUM completo, cobertura de scores de respuestas completas y fuente compatible; alternativas 1 y 3 futuras no aprobadas.
- REV-LOG-005: RESOLVED; intervalo inclusivo y recorded_at editable dentro del intervalo original.
- REV-LOG-006: RESOLVED; continuidad y envío sobre versión histórica retirada bajo las condiciones aprobadas.
- REV-LOG-007: RESOLVED para MVP; correcciones superficiales y enlaces con igual contenido; propósito, significado, recomendaciones, alcance o tipo nuevos requieren Resource nuevo.
- REV-LOG-008: RESOLVED para MVP lógico; orden total, estable y documentado. Tipo físico y comparación concreta quedan en diseño físico.

- REV-LOG-009: RESOLVED para MVP; declaraciones vigentes sin edición/eliminación ordinaria. Error exige revisión de diseño; revocación trazable no representada.
- REV-LOG-010: RESOLVED para MVP; no eliminar ordinariamente instrumentos, dimensiones o versiones referenciados históricamente; retiro cuando corresponda y supresión personal independiente.

- REV-LOG-011: RESOLVED para MVP; body informativo no vacío y revisión editorial; URL absoluta HTTPS sintácticamente válida y revisada, sin solicitudes automáticas.
- REV-LOG-012: RESOLVED para MVP; code/significado estables, correcciones de textos y asociaciones autorizadas sin dejar PUBLISHED sin Topics ni alterar historia.
- REV-LOG-013: RESOLVED para MVP; sin eliminación física ordinaria de Topics/Resources; retiro terminal de Resources publicados y conservación histórica cuando corresponda.

Las políticas MVP de REV-LOG-008/009/010 no contradicen claves, compatibilidad por pares ni supresión personal. Se registra como limitación la ausencia de revocación trazable, sin mecanismos nuevos.

## Contraste de las últimas aprobaciones

No se detectan contradicciones con AJ-01..04/VF-01..05 ni supresión personal: T/P obligatorios condicionalmente no cambian metadata?, actor_kind histórico no conserva identidad, USER_DELETED carece de IDs persistentes, dominios precisan validaciones y no modifican PK/AK/URA/FK. El diseño físico no está seleccionado.

## Hallazgos de la revisión transversal

Se contrastaron los seis diccionarios y 13-dominios-logicos.md, 05-integridad-referencial.md y 06-estados-y-transiciones.md. IDs RT/DP identifican hallazgos, no RF/RN/RNF ni nuevas políticas.

**Conforme** indica coherencia de la regla contrastada. **Precisión documental necesaria** identifica detalle aprobado que faltaba en los transversales y se incorporó. **Contradicción comprobada** exige afirmaciones incompatibles; no se infiere de una omisión. **Decisión pendiente** identifica comportamiento o diseño no aprobado, sin seleccionar una solución.

| Hallazgo | Clasificación | Evidencia y precisión incorporada | Aplicación |
| --- | --- | --- | --- |
| RT-001 — Envío, score e interpretación | Precisión documental necesaria | Evaluaciones, AJ-01/REV-LOG-004 y Estados exigen Answers completas, SUM, resolución de Interpretation existente, Result y SUBMITTED atómicos. No se crea interpretación al enviar. | [07](07-transacciones-y-concurrencia.md#envío-y-resultado-oficial) |
| RT-002 — Attempt único en curso | Precisión documental necesaria | Evaluaciones e Integridad fijan máximo un IN_PROGRESS por usuario/instrumento, independiente de versión y coordinado ante inicios concurrentes. | [07](07-transacciones-y-concurrencia.md#publicación-retiro-e-inicio-de-evaluaciones) |
| RT-003 — Expiración y respuestas | Precisión documental necesaria | En expires_at no se envía; cierre EXPIRED/CANCELLED elimina Answers, sin Result. Detección tardía usa ended_at = expires_at. | [07](07-transacciones-y-concurrencia.md#expiración-cancelación-y-respuestas-parciales), [08](08-privacidad-eliminacion-retencion.md#expiración-cancelación-y-tokens) |
| RT-004 — Versiones de catálogos | Precisión documental necesaria | Evaluaciones/Seguimiento y Estados conservan máximo PUBLISHED/ACTIVE, retiro histórico, continuidad válida y reemplazo atómico de ACTIVE. | [07](07-transacciones-y-concurrencia.md#activación-y-retiro-de-dimensiones) |
| RT-005 — Edición de CheckIn | Precisión documental necesaria | Seguimiento, REV-LOG-005 y Dominios fijan ventana estricta de 168 horas, intervalo inclusivo original, revision + 1 por operación y protección de conjunto/versiones de Measurements y vínculos ContextTag. | [07](07-transacciones-y-concurrencia.md#checkin-revisión-y-dependencias-editables) |
| RT-006 — Resource y Topic mínimo | Precisión documental necesaria | Contenido/AJ-02 exigen publicación válida por tipo y mínimo de un Topic preservado ante cambios de asociaciones concurrentes. | [07](07-transacciones-y-concurrencia.md#resources-topics-y-compatibilidad) |
| RT-007 — Supresión y auditoría | Precisión documental necesaria | VF-04, diccionarios e Integridad distinguen dependencias privadas de catálogos; cuenta requiere auditoría desvinculada como actor/destino. USER_DELETED sin IDs personales. | [08](08-privacidad-eliminacion-retencion.md#eliminación-de-cuenta-y-dependencias) |
| RT-008 — Retenciones | Precisión documental necesaria | Attempts terminales sin Result: 30 días desde ended_at; auditoría: máximo 180 desde occurred_at; backups cifrados: 30. Días de 24 horas; sin reinicio por limpieza/desvinculación/restauración. | [08](08-privacidad-eliminacion-retencion.md#retenciones-y-referencias-temporales) |
| RT-009 — Restauración | Precisión documental necesaria | VF-04 y diccionarios exigen reaplicar supresiones antes de habilitar servicio; la copia antigua no autoriza reintroducir datos o referencias eliminados. | [08](08-privacidad-eliminacion-retencion.md#backups-y-restauración) |
| RT-010 — REV-LOG-001/002 | Conforme | Auditoría/Dominios: catálogos aprobados, perfiles C/E y T/P, metadata restringida, clasificación histórica sin identidad y tiempo inequívoco. Opcionalidad estructural compatible con obligaciones por acción. | [09](09-decisiones-arquitectonicas.md#decisiones-consolidadas-y-su-aplicación-transversal) |
| RT-011 — Postgres y límites | Conforme | 09 mantiene ADR-001 PROVISIONALLY ACCEPTED, aplicaciones/tipos físicos abiertos y ausencia de congelación; ahora diferencia guardas aprobadas de implementación. | [09](09-decisiones-arquitectonicas.md#decisiones-físicas-y-operativas-diferidas) |
| RT-012 — Estado/trazabilidad de 09/10 | Precisión documental necesaria | Actualizar contraste inicial y conformidad provisional de 05/06; enlazar políticas transversales y separar decisiones pendientes sin reabrir REV-LOG. | 09 y esta matriz |
| DP-TRANS-001 — Fallo de auditoría | Conforme | RESOLVED MVP: alternativa B aprobada solo para supresión de cuenta; USER/dependencias/desvinculación atómicos. Supresión confirmada válida sin USER_DELETED, sin evento ficticio o datos para reconstrucción; resultado incierto se verifica. Otras operaciones auditables mantienen registro garantizado. | [07](07-transacciones-y-concurrencia.md#dp-trans-001), [09](09-decisiones-arquitectonicas.md#dp-trans-001) |
| DP-TRANS-002 — Cancelación/expiración | Conforme | RESOLVED MVP: confirmación válida antes de expires_at → CANCELLED/ended_at efectivo; al vencer → EXPIRED/ended_at = expires_at. Terminales irreversibles, limpieza de Answers y orden de confirmación coherente; sin entrega fuera de vigencia. | [07](07-transacciones-y-concurrencia.md#dp-trans-002), [09](09-decisiones-arquitectonicas.md#dp-trans-002) |
| RT-013 — Realización técnica de restauración | Decisión pendiente | Resultado de reaplicar supresiones está aprobado; fuente actualizada y coordinación técnica se difieren sin añadir relaciones ni mecanismos. | [08](08-privacidad-eliminacion-retencion.md#backups-y-restauración) |

Las fuentes de RT-001..009 son **Conformes** entre sí; su clasificación identifica la insuficiencia previa de 07/08. Las precisiones están incorporadas y no cambian políticas.

**Contradicción comprobada nueva:** ninguna entre los documentos contrastados. C-LOG-001 sigue como diferencia entre versiones documentales ya registrada; no se modifica ni se cierra silenciosamente el conceptual.

**Decisiones pendientes:** DP-TRANS-001/002 están RESOLVED para el MVP mediante resoluciones expresas del usuario. RT-013 pertenece al diseño técnico diferido. Detalles físicos de tipos, coordinación, limpieza y respaldo no se seleccionan.

## Límite de esta entrega

La matriz conserva la secuencia de revisión; la aprobación formal y congelación de la línea base lógica se registran en el dictamen definitivo. El usuario aprobó provisionalmente Identidad, con las precisiones incorporadas, y autorizó Evaluaciones, cuyo diccionario está documentado para revisión. Seguimiento está aprobado provisionalmente con precisiones incorporadas; Compatibilidad incorpora REV-LOG-008/009/010, contrastadas sin incompatibilidades comprobables y consolidadas para MVP. Contenido está aprobado provisionalmente con políticas editoriales consolidadas sin incompatibilidades comprobables. Auditoría tiene catálogos/combinaciones aprobados para MVP mediante REV-LOG-001. REV-LOG-002 aprueba [dominios](13-dominios-logicos.md); [integridad](05-integridad-referencial.md) y [estados](06-estados-y-transiciones.md) son conformes provisionalmente según el usuario; 07/08/09/10 incorporan revisión transversal y precisiones aprobadas. DP-TRANS-001/002 están RESOLVED para el MVP, sin cambiar los cierres REV-LOG. El dictamen lógico v1.0 está APPROVED y su línea base FROZEN por autorización formal del responsable del proyecto el 2026-10-07. Las conformidades provisionales anteriores se conservan como antecedentes. Las cuestiones físicas PostgreSQL y los mecanismos de aplicación no se seleccionan. [ADR-001](../../06-decisions/ADR-001-database-engine.md) permanece PROVISIONALLY ACCEPTED.

[Índice y fuentes](00-indice.md).
