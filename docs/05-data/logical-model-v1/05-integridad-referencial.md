# Integridad referencial

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md).

## Fuentes y significado

La [especificación maestra](especificacion-maestra-v1.0.md) aporta las claves; los seis diccionarios describen propósito, atributos y cardinalidades. [Dominios aprobados](13-dominios-logicos.md) y resoluciones posteriores precisan validez y nulabilidad sin cambiar relaciones o claves.

PK identifica filas; AK conserva unicidad candidata aprobada; URA conserva unicidad adicional para referencias compuestas, sin reclasificarla automáticamente como AK mínima. Una FK exige existencia y pertenencia al destino; no prueba estado, autorización, cálculo ni completitud.

Se registran **32 PK, 9 AK, 6 URA y 41 FK: 28 simples y 13 compuestas**. La referencia simple adicional de ASSESSMENT_RESULT a Attempt se conserva aunque también exista la compuesta.

## Inventario de claves

| Relación | PK | AK | URA |
| --- | --- | --- | --- |
| USER | user_id | email | — |
| USER_CREDENTIAL | user_id | — | — |
| ADMINISTRATOR | user_id | — | — |
| EMAIL_VERIFICATION_TOKEN | verification_token_id | — | — |
| PASSWORD_RESET_TOKEN | reset_token_id | — | — |
| INSTRUMENT | instrument_id | code | — |
| INSTRUMENT_VERSION | instrument_version_id | (instrument_id, version) | (instrument_id, instrument_version_id) |
| INSTRUMENT_VERSION_REFERENCE | (instrument_version_id, reference_order) | — | — |
| QUESTION | question_id | (instrument_version_id, position) | (instrument_version_id, question_id) |
| ANSWER_OPTION | option_id | (question_id, position) | (question_id, option_id) |
| SCORING_DEFINITION | instrument_version_id | — | — |
| SCORING_CONTRIBUTION | option_id | — | — |
| ASSESSMENT_ATTEMPT | attempt_id | — | (attempt_id, instrument_version_id) |
| ANSWER | (attempt_id, question_id) | — | — |
| INTERPRETATION | interpretation_id | — | (instrument_version_id, interpretation_id) |
| ASSESSMENT_RESULT | attempt_id | — | — |
| DIMENSION | dimension_id | code | — |
| DIMENSION_VERSION | dimension_version_id | (dimension_id, version) | (dimension_id, dimension_version_id) |
| DIMENSION_ANCHOR | (dimension_version_id, value) | — | — |
| CHECK_IN | check_in_id | — | — |
| MEASUREMENT | (check_in_id, dimension_id) | — | — |
| CONTEXT_TAG | context_tag_id | code | — |
| CHECK_IN_CONTEXT_TAG | (check_in_id, context_tag_id) | — | — |
| INSTRUMENT_VERSION_COMPATIBILITY | (version_a_id, version_b_id) | — | — |
| DIMENSION_VERSION_COMPATIBILITY | (version_a_id, version_b_id) | — | — |
| TOPIC | topic_id | code | — |
| RESOURCE | resource_id | — | — |
| RESOURCE_TOPIC | (resource_id, topic_id) | — | — |
| INSTRUMENT_TOPIC | (instrument_id, topic_id) | — | — |
| INTERPRETATION_TOPIC | (interpretation_id, topic_id) | — | — |
| DIMENSION_TOPIC | (dimension_id, topic_id) | — | — |
| AUDIT_EVENT | audit_event_id | — | — |

No se agregan claves de token_hash, user_id en tokens, nombres, títulos, URLs ni metadata. Máximo una versión publicada/activa y un intento en progreso son invariantes condicionados por estado, no AK nuevas.

## Inventario completo de referencias

Cada fila corresponde a una FK aprobada, con sus componentes en el orden documentado. **Todas son obligatorias salvo AUDIT_EVENT.actor_user_id**. Los dominios de referencias coinciden con los de sus destinos.

| Origen | Componentes FK | Destino | Componentes referenciados |
| --- | --- | --- | --- |
| USER_CREDENTIAL | user_id | USER | user_id |
| ADMINISTRATOR | user_id | USER | user_id |
| EMAIL_VERIFICATION_TOKEN | user_id | USER | user_id |
| PASSWORD_RESET_TOKEN | user_id | USER | user_id |
| INSTRUMENT_VERSION | instrument_id | INSTRUMENT | instrument_id |
| INSTRUMENT_VERSION_REFERENCE | instrument_version_id | INSTRUMENT_VERSION | instrument_version_id |
| QUESTION | instrument_version_id | INSTRUMENT_VERSION | instrument_version_id |
| ANSWER_OPTION | question_id | QUESTION | question_id |
| SCORING_DEFINITION | instrument_version_id | INSTRUMENT_VERSION | instrument_version_id |
| SCORING_CONTRIBUTION | instrument_version_id | SCORING_DEFINITION | instrument_version_id |
| SCORING_CONTRIBUTION | instrument_version_id, question_id | QUESTION | instrument_version_id, question_id |
| SCORING_CONTRIBUTION | question_id, option_id | ANSWER_OPTION | question_id, option_id |
| ASSESSMENT_ATTEMPT | user_id | USER | user_id |
| ASSESSMENT_ATTEMPT | instrument_id, instrument_version_id | INSTRUMENT_VERSION | instrument_id, instrument_version_id |
| ANSWER | attempt_id, instrument_version_id | ASSESSMENT_ATTEMPT | attempt_id, instrument_version_id |
| ANSWER | instrument_version_id, question_id | QUESTION | instrument_version_id, question_id |
| ANSWER | question_id, option_id | ANSWER_OPTION | question_id, option_id |
| INTERPRETATION | instrument_version_id | INSTRUMENT_VERSION | instrument_version_id |
| ASSESSMENT_RESULT | attempt_id | ASSESSMENT_ATTEMPT | attempt_id |
| ASSESSMENT_RESULT | attempt_id, instrument_version_id | ASSESSMENT_ATTEMPT | attempt_id, instrument_version_id |
| ASSESSMENT_RESULT | instrument_version_id, interpretation_id | INTERPRETATION | instrument_version_id, interpretation_id |
| DIMENSION_VERSION | dimension_id | DIMENSION | dimension_id |
| DIMENSION_ANCHOR | dimension_version_id | DIMENSION_VERSION | dimension_version_id |
| CHECK_IN | user_id | USER | user_id |
| MEASUREMENT | check_in_id | CHECK_IN | check_in_id |
| MEASUREMENT | dimension_id, dimension_version_id | DIMENSION_VERSION | dimension_id, dimension_version_id |
| CHECK_IN_CONTEXT_TAG | check_in_id | CHECK_IN | check_in_id |
| CHECK_IN_CONTEXT_TAG | context_tag_id | CONTEXT_TAG | context_tag_id |
| INSTRUMENT_VERSION_COMPATIBILITY | instrument_id, version_a_id | INSTRUMENT_VERSION | instrument_id, instrument_version_id |
| INSTRUMENT_VERSION_COMPATIBILITY | instrument_id, version_b_id | INSTRUMENT_VERSION | instrument_id, instrument_version_id |
| DIMENSION_VERSION_COMPATIBILITY | dimension_id, version_a_id | DIMENSION_VERSION | dimension_id, dimension_version_id |
| DIMENSION_VERSION_COMPATIBILITY | dimension_id, version_b_id | DIMENSION_VERSION | dimension_id, dimension_version_id |
| RESOURCE_TOPIC | resource_id | RESOURCE | resource_id |
| RESOURCE_TOPIC | topic_id | TOPIC | topic_id |
| INSTRUMENT_TOPIC | instrument_id | INSTRUMENT | instrument_id |
| INSTRUMENT_TOPIC | topic_id | TOPIC | topic_id |
| INTERPRETATION_TOPIC | interpretation_id | INTERPRETATION | interpretation_id |
| INTERPRETATION_TOPIC | topic_id | TOPIC | topic_id |
| DIMENSION_TOPIC | dimension_id | DIMENSION | dimension_id |
| DIMENSION_TOPIC | topic_id | TOPIC | topic_id |
| AUDIT_EVENT | actor_user_id | USER | user_id |

Las referencias simples apuntan a PK; las compuestas históricas a las URA correspondientes. Los componentes de una FK compuesta no admiten ausencia parcial. Las cuatro asociaciones de Contenido tienen PK compuesta y dos FKs simples; no se confunden ambos conceptos.

## Cadenas de pertenencia histórica

- **Attempt:** (instrument_id, instrument_version_id) exige que la versión pertenezca al instrumento registrado.
- **Answer:** (attempt_id, instrument_version_id) conserva versión del Attempt; (instrument_version_id, question_id) comprueba pregunta de esa versión; (question_id, option_id) comprueba opción de esa pregunta. Las tres se cumplen simultáneamente.
- **Contribución SUM:** instrument_version_id exige ScoringDefinition de la versión; las FKs de pregunta y opción impiden aportes de otra definición. option_id PK limita una contribución por opción, pero no obliga a cubrir todas las opciones.
- **Result:** las FKs conservan versión del Attempt y de Interpretation. No prueban SUM ni pertenencia del score al rango seleccionado.
- **Measurement:** (dimension_id, dimension_version_id) exige escala perteneciente a esa Dimension; la PK (check_in_id, dimension_id) evita dimensiones repetidas aun con versiones diferentes.
- **Compatibilidad:** cada par usa dos FKs con un instrument_id o dimension_id común. No pueden relacionar versiones de padres distintos. Orden canónico impide duplicado inverso y autorreferencia; la simetría es semántica y no implica almacenar el inverso.
- **Contenido:** InstrumentTopic y DimensionTopic refieren las identidades estables; InterpretationTopic refiere la definición histórica. No hay FK directa a versiones desde Topic ni Resource → Instrument/Interpretation/Dimension.
- **Auditoría:** actor_user_id opcional refiere User; target_identifier y IDs de metadata no se convierten en FKs nuevas.

## Cardinalidades derivadas y mínimos condicionados

- USER admite 0..1 USER_CREDENTIAL y 0..1 ADMINISTRATOR por PK/FK de hijos; sus claves no prueban cobertura total de credenciales. Tokens: 0..N según claves, con política de último válido coordinada.
- Instrument y Dimension admiten 0..N versiones. Versiones admiten 0..N componentes durante construcción; ScoringDefinition tiene 0..1 por PK/FK.
- Attempt admite 0..N Answers y 0..1 Result por claves. SUBMITTED requiere exactamente un Result y una Answer válida por cada Question.
- CheckIn admite estructuralmente 0..N Measurements, pero su creación válida exige 1..N; máximo una por Dimension. Vínculos ContextTag: 0..N.
- Las cuatro asociaciones de Topics son N:M; Resource PUBLISHED exige al menos un Topic.
- Cada declaración de compatibilidad relaciona exactamente dos versiones distintas; cada versión puede participar en 0..N pares.
- USER puede ser actor de 0..N eventos; cada evento tiene 0..1 actor User. Su ausencia tras desvinculación conserva actor_kind histórico.

## Validez de atributos y reglas entre filas

[Dominios](13-dominios-logicos.md) consolida IDs, códigos, textos, enteros, UTC, email, URLs, símbolos y metadata. Posiciones positivas no tienen que ser consecutivas; límites de Interpretation son enteros inclusivos con lower_bound ≤ upper_bound.

Requieren validación/coordinación además de claves:

1. Máximo una InstrumentVersion PUBLISHED o DimensionVersion ACTIVE por padre; reemplazo de ACTIVE coordina retiro/activación atómicos.
2. Máximo un Attempt IN_PROGRESS por usuario/instrumento, sin depender de la versión.
3. Inicio en PUBLISHED; continuación válida tras RETIRED conserva versión original, estado y vigencia.
4. Publicación MVP con todas las preguntas required = true, contribuciones SUM completas y metodología que permita respuestas completas.
5. SUBMITTED con todas las Answers válidas, score oficial SUM y exactamente un Result; score cubierto una vez por interpretación oficial.
6. CheckIn con Measurement mínima, versión ACTIVE al crear y valor perteneciente a escala; correcciones en versión original y ventana aprobada.
7. Asociaciones ContextTag nuevas solo ACTIVE; vínculos históricos retirados permitidos.
8. Resource publicado con contenido válido de su tipo y Topics mínimos; retirar asociación no puede violar el mínimo.
9. Tokens de último uso válido, consumo único y plazos estrictos; recuperación invalida otros tokens de recuperación y sesiones.
10. Combinaciones de auditoría aprobadas; T exige topic_id y P exige el par canónico en metadata. La opcionalidad estructural no exime esos casos.

Una implementación futura debe preservar estos invariantes; aquí no se seleccionan índices físicos, triggers, cascadas, aislamiento ni bloqueos.

## Eliminación, autorización e historia

Eliminación individual autorizada de Assessment/CheckIn y de cuenta coordina sus dependencias privadas, sin destruir catálogos ni compatibilidad compartida. Answers se eliminan al expirar/cancelar Attempt; Result no queda huérfano por supresión personal.

No se permite eliminación ordinaria de instrumentos, dimensiones o versiones referenciados por historia. RETIRED se aplica donde el modelo lo prevé. Topics/Resources no admiten eliminación física ordinaria; declaraciones de compatibilidad no se editan ni borran ordinariamente y errores se escalan a revisión de diseño.

Eliminar User exige desvincular actor_user_id y referencias personales de auditoría; USER_DELETED no conserva IDs personales. DP-TRANS-001 exige confirmación atómica de USER, sus dependencias personales y la desvinculación existente. La indisponibilidad del registro de USER_DELETED no bloquea ni invalida esa supresión confirmada; es una excepción aprobada únicamente para supresión de cuenta. No se introduce una cascada que destruya historia ni una FK que impida cumplir supresión.

Integridad no equivale a autorización: ADMINISTRATOR no concede acceso automático a bienestar privado. Dominios/catálogos de auditoría tampoco crean matriz de permisos.

## Contraste y límite

No se identifican destinos ausentes ni FKs hacia combinaciones sin clave aprobada. Metadata T/P obligatoria es condicional, sin cambiar atributos. C-LOG-001 sigue como cambio documentado entre capas; el conceptual original y la fuente permanecen intactos.

[Estados](06-estados-y-transiciones.md) consolida transiciones y guardas. [Transacciones](07-transacciones-y-concurrencia.md), [privacidad](08-privacidad-eliminacion-retencion.md) y [dictamen definitivo](12-dictamen-modelo-logico-v1.md) están documentados; línea base lógica APPROVED / FROZEN. PostgreSQL permanece [PROVISIONALLY ACCEPTED](../../06-decisions/ADR-001-database-engine.md); tecnologías de aplicación abiertas.

[Índice](00-indice.md) · [Matriz](10-matriz-trazabilidad.md).
