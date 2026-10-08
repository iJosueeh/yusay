# Conceptual Domain Model — v0.1

**Estado: Conceptual Domain Model v0.1 CLOSED.** Este documento consolida únicamente las decisiones de dominio proporcionadas y aceptadas para el MVP. Describe conceptos, pertenencias, relaciones y reglas; no define su persistencia. El Relational Logical Model queda NEXT, sin iniciarse en esta tarea. El Physical Data Model y la Application Architecture permanecen pendientes.

Yusay permite seguimiento personal estructurado del bienestar; no es diagnóstico ni sustituye profesionales. Los [principios técnicos](../04-architecture/README.md) y la [privacidad](../02-product/business-rules.md#privacy) siguen vigentes.

## Decisiones de dominio

### DR-DOM-001

**Assessment Question Model**  
**Status: ACCEPTED FOR MVP**

El MVP soporta preguntas **SINGLE_CHOICE**. Cada Question pertenece exactamente a una InstrumentVersion y define sus propias AnswerOptions; las opciones no se comparten automáticamente entre Questions.

Cada Answer pertenece a un AssessmentAttempt, responde exactamente una Question y selecciona exactamente una AnswerOption de esa Question. La Question debe pertenecer a la misma InstrumentVersion del intento. Existe como máximo una Answer por combinación AssessmentAttempt + Question.

Durante DRAFT pueden existir definiciones incompletas. Solo se publica una versión que cumple las reglas requeridas para ser ejecutable; las reglas de readiness y publicación están en [DR-DOM-006](#dr-dom-006), que resuelve OQ-DOM-028.

No se incorporan TEXT, NUMBER, MULTIPLE_CHOICE, un form builder genérico ni otros tipos sin un instrumento soportado que los requiera. Radio buttons, cards o escalas visuales son presentaciones posibles de SINGLE_CHOICE, no tipos nuevos de dominio ni una selección de UI.

No se presupone un campo `score` en AnswerOption. La definición conceptual de ScoringDefinition está en [DR-DOM-005](#dr-dom-005), que resuelve OQ-DOM-027; su representación relacional continúa pendiente.

### DR-DOM-002

**Historical Dimension Definition**  
**Status: ACCEPTED FOR MVP**

Dimension representa la identidad conceptual estable de aquello que se mide, por ejemplo Stress, Mood, Energy o Sleep. DimensionVersion contiene la definición concreta de medición: escala, rango permitido, step cuando corresponda, labels/anchors y semántica necesaria para interpretar el valor.

Cada Measurement conserva referencia conceptual a la **DimensionVersion exacta** utilizada al registrar el CheckIn; no basta referenciar la Dimension genérica. Los valores concretos de escalas y unidades siguen abiertos.

Cambios de escala, semántica, interpretación o significado histórico requieren una nueva DimensionVersion. Una definición histórica utilizada no puede modificarse de forma que cambie retroactivamente el significado de Measurements existentes. Una versión anterior puede retirarse para nuevas Measurements sin invalidar el histórico.

Dimension es Aggregate Root de Tracking; DimensionVersion es Entity interna de ese agregado. Measurement permanece Value Object candidate dentro de CheckIn. No se copia automáticamente el lifecycle de InstrumentVersion ni se decide cómo persistir DimensionVersion.

### DR-DOM-003

**Longitudinal Comparability**  
**Status: ACCEPTED FOR MVP**

ComparabilityPolicy es una política/regla del dominio, no una entidad persistente confirmada.

- Dos AssessmentResults de la misma InstrumentVersion son comparables.
- Dos Measurements de la misma DimensionVersion son comparables.
- Versiones diferentes del mismo Instrument requieren una declaración explícita y documentada de compatibilidad.
- Versiones diferentes de la misma Dimension requieren una declaración explícita y documentada de compatibilidad.
- No se comparan automáticamente mediciones de diferentes Instruments ni de diferentes Dimensions en el MVP.
- UNKNOWN se trata conservadoramente como NOT COMPARABLE: ausencia de información de compatibilidad significa no comparable.

Pertenecer al mismo Instrument o Dimension, compartir rango numérico, tener nombres similares o permitir normalización matemática a una escala común no demuestra comparabilidad entre versiones.

No se confirman entidades Comparison, ComparisonGroup, ComparisonSeries ni Compatibility. La representación relacional de compatibilidad queda pendiente en [OQ-DATA-003](../05-data/README.md#oq-data-003).

### DR-DOM-004

**Guidance and Content Relationships**  
**Status: ACCEPTED FOR MVP**

Guidance es una capacidad compuesta a partir de Interpretation, documentación/limitaciones, Topic y Resource; no una entidad persistente independiente.

Interpretation pertenece a la definición histórica de InstrumentVersion, explica el resultado según el instrumento y preserva su significado histórico. Resource no la reemplaza.

Resource es contenido educativo/informativo con ciclo independiente: puede evolucionar, publicarse o retirarse sin modificar el significado de AssessmentResults históricos.

Topic es vocabulario controlado de Content. Para el MVP permite asociaciones conceptuales con Instrument, Interpretation, Dimension y Resource. No se crean asociaciones directas Resource → Instrument, Resource → Interpretation o Resource → Dimension si la relación se resuelve mediante Topic. No se asocian directamente InstrumentVersion o DimensionVersion con Topic sin un caso de dominio posterior que lo justifique.

Los recorridos conceptuales son:

```text
AssessmentResult → Interpretation → Topic → Resource
Dimension → Topic → Resource
```

ContextTag pertenece al contexto personal de Tracking, por ejemplo University, Work, Family o Relationships. Topic clasifica contenido y conceptos, por ejemplo Stress, Sleep, Self-care o Seeking support. Una coincidencia textual no los convierte en el mismo concepto.

No se incorporan Recommendation entity, RecommendationEngine, AI recommendation, UserResourceProfile ni scoring de recomendaciones para el MVP. Guidance no implica diagnóstico, tratamiento ni recomendación clínica.

### DR-DOM-005

**Assessment Scoring Model**  
**Status: ACCEPTED FOR MVP**

ScoringDefinition determina cómo las Answers válidas de una InstrumentVersion se transforman en AssessmentResult oficial. Se separan Answer (qué respondió el usuario), ScoringDefinition (cómo se transforma en resultado), AssessmentResult (resultado oficial producido) e Interpretation (significado documentado cuando corresponda).

El método inicialmente soportado es **SUM**, aditivo. ScoringDefinition define la contribución de cada combinación relevante Question + AnswerOption y permite scoring normal e invertido. La puntuación no es propiedad universal ni intrínseca de AnswerOption; no se presupone un campo score en ella.

El MVP no soporta subscales, múltiples scores, fórmulas arbitrarias, scripting, motores universales ni transformaciones no requeridas por instrumentos soportados. Una versión cuyo scoring no pueda representarse con una estrategia soportada no puede publicarse como ejecutable hasta incorporar explícitamente ese soporte.

Submit exige las respuestas requeridas antes del cálculo. El scoring es determinista. Scoring, resolución de Interpretation cuando corresponda, creación de AssessmentResult y transición a SUBMITTED pertenecen a **una misma operación conceptual atómica**; no se diseña su implementación.

AssessmentResult conserva el resultado oficial; consultarlo no implica recalcularlo. Answers, InstrumentVersion exacta y ScoringDefinition histórica permiten reproducir/auditar cómo se obtuvo. Una versión publicada conserva inmutable su ScoringDefinition.

ScoringDefinition es **Definition Object dependiente de InstrumentVersion**, no Aggregate Root. ScoringRule y ScoringMapping no se confirman como Entities del modelo conceptual.

Si la fuente define Interpretations por score/rango, cada resultado oficial producible que requiera interpretación debe resolver una Interpretation inequívoca. No se inventan Interpretations para instrumentos cuya fuente no las define.

### DR-DOM-006

**InstrumentVersion Publication Readiness**  
**Status: ACCEPTED FOR MVP**

El lifecycle conceptual es:

```text
DRAFT ⇄ READY → PUBLISHED → RETIRED
```

- **DRAFT:** definición editable que puede estar incompleta; no admite nuevos AssessmentAttempts.
- **READY:** definición completa, ejecutable y validada conceptualmente; aún no admite nuevos AssessmentAttempts. No es una etiqueta administrativa arbitraria.
- **PUBLISHED:** definición validada y ejecutable, disponible para nuevos AssessmentAttempts.
- **RETIRED:** no admite nuevos AssessmentAttempts y preserva completamente el histórico.

Para alcanzar READY, como mínimo:

- Pertenencia inequívoca a un Instrument y al menos una Question.
- Questions ejecutables; SINGLE_CHOICE con AnswerOptions válidas.
- Orden requerido y requiredness definidos.
- ScoringDefinition completa y con estrategia soportada cuando el instrumento produce score.
- Contribución inequívoca para todas las respuestas relevantes para scoring y resultado determinista.
- Interpretations completas y no ambiguas cuando la fuente las define.
- Documentación obligatoria aplicable de Instrument / InstrumentVersion completa.
- Fuente/referencias, población, propósito y condiciones de uso aplicables documentados.
- Licencia/condiciones de uso documentadas cuando corresponda y limitaciones documentadas.

No se inventan requisitos metodológicos no respaldados por la fuente.

Como máximo una InstrumentVersion por Instrument puede estar PUBLISHED para iniciar nuevos intentos. Cada AssessmentAttempt nuevo usa la versión PUBLISHED correspondiente. Los intentos históricos conservan su versión exacta aunque posteriormente quede RETIRED.

Publicar congela la definición estructural y semánticamente; cambios posteriores que afecten significado requieren otra versión. PUBLISHED y RETIRED no regresan a estados editables. READY puede volver a DRAFT antes de publicación.

RETIRED no elimina Questions, AnswerOptions, ScoringDefinition, Interpretations, Attempts ni Results históricos. La continuidad de intentos en progreso tras un retiro permanece en [OQ-DOM-007](../02-product/business-rules.md#oq-dom-007); no se deduce una política no proporcionada. Tampoco se diseñan mecanismos de transición ni restricciones relacionales.

### DR-DOM-007

**CheckIn Note**  
**Status: ACCEPTED FOR MVP**

Un CheckIn contiene **0..1 Note**, texto libre opcional proporcionado por el usuario. Note pertenece exclusivamente a ese CheckIn; no tiene identidad ni lifecycle independiente y se clasifica como **Value**.

Note no es Measurement, no modifica sus valores, no participa en scoring y no establece causalidad entre ContextTags y Measurements. No se utiliza automáticamente para producir interpretaciones clínicas.

No se define tipo de almacenamiento, longitud física exacta, índice ni estrategia de persistencia.

## Pertenencias conceptuales

La clasificación está en [aggregate-candidates](aggregate-candidates.md). El baseline establece User, Instrument, InstrumentVersion, AssessmentAttempt, CheckIn, Dimension y Resource como Aggregate Roots. Question y AnswerOption pertenecen a InstrumentVersion; Answer pertenece a AssessmentAttempt; DimensionVersion pertenece a Dimension.

AssessmentResult es un objeto dependiente del límite de consistencia de AssessmentAttempt, sin Aggregate Root ni CRUD independiente. ScoringDefinition es Definition Object dependiente de InstrumentVersion; Interpretation y Measurement conservan sus clasificaciones candidate. Note es un Value perteneciente a CheckIn, no una Entity independiente. ContextTag y Topic son Entity/catalog concepts de Tracking y Content respectivamente.

Esta clasificación no convierte cada concepto en entidad persistente ni determina estructuras relacionales.

## Relaciones y cardinalidades

La notación `A 1 → 0..N B` expresa que cada A puede relacionarse con cero o muchos B; cada B tiene exactamente un A en estas relaciones de pertenencia. Las asociaciones N:M no implican estructuras de persistencia.

### Assessment Definition

```text
Instrument        1 → 0..N InstrumentVersion
InstrumentVersion 1 → 0..N Question
Question          1 → 0..N AnswerOption
InstrumentVersion 1 → 0..1 ScoringDefinition
InstrumentVersion 1 → 0..N Interpretation
```

Las cardinalidades de definición permiten construcción incompleta durante DRAFT. No son una autorización para publicar definiciones incompletas. Una Question SINGLE_CHOICE ejecutable/publicable requiere opciones válidas según las reglas de publicación. La readiness de una versión requiere al menos una Question y las condiciones de [DR-DOM-006](#dr-dom-006); las cardinalidades de construcción no equivalen a readiness.

### Assessment Execution

```text
User              1 → 0..N AssessmentAttempt
InstrumentVersion 1 → 0..N AssessmentAttempt
AssessmentAttempt 1 → 0..N Answer
Question          1 → 0..N Answer
AssessmentAttempt 1 → 0..1 AssessmentResult
```

Cada Answer selecciona exactamente una AnswerOption de su Question. La multiplicidad de Answers de una Question corresponde a distintos intentos; no permite más de una Answer por Question dentro del mismo intento SINGLE_CHOICE.

La relación opcional con AssessmentResult está condicionada por el estado: SUBMITTED tiene exactamente un resultado oficial; IN_PROGRESS, CANCELLED y EXPIRED no tienen resultado oficial.

### Tracking

```text
User             1 → 0..N CheckIn
CheckIn          1 → 1..N Measurement
Dimension        1 → 0..N DimensionVersion
DimensionVersion 1 → 0..N Measurement
CheckIn       0..N ↔ 0..N ContextTag
CheckIn          1 → 0..1 Note
```

Un CheckIn válido contiene al menos una Measurement. Cada Measurement pertenece a un CheckIn y conserva la DimensionVersion exacta; dentro del mismo CheckIn no puede existir más de una Measurement de la misma Dimension, aunque se tratara de versiones distintas.

Note es texto libre opcional y Value exclusivo de CheckIn según [DR-DOM-007](#dr-dom-007); no es Measurement ni participa en scoring.

### Content

```text
Resource       0..N ↔ 0..N Topic
Instrument     0..N ↔ 0..N Topic
Interpretation 0..N ↔ 0..N Topic
Dimension      0..N ↔ 0..N Topic
```

Estas son asociaciones semánticas. Su representación posterior permanece abierta; no se definen asociaciones directas de Topic con InstrumentVersion o DimensionVersion.

## Invariantes del baseline

Las [reglas de negocio](../02-product/business-rules.md) mantienen sus IDs y expresan las invariantes:

- AssessmentAttempt referencia exactamente un User y una InstrumentVersion; sus Answers respetan Question y AnswerOption de esa definición.
- SUBMITTED, EXPIRED y CANCELLED son terminales para el MVP. Un nuevo Assessment requiere un nuevo AssessmentAttempt.
- El submit genera exactamente un AssessmentResult oficial como consecuencia del intento SUBMITTED; no se administra por CRUD independiente. Los otros estados no tienen resultado oficial.
- Las respuestas enviadas son inmutables y los resultados históricos no se recalculan automáticamente.
- Un CheckIn válido contiene al menos una Measurement y no repite una Dimension.
- Las definiciones históricas de InstrumentVersion y DimensionVersion preservan el significado de sus registros.
- La comparabilidad entre versiones distintas requiere declaración explícita; UNKNOWN no permite comparación.
- Administrator no obtiene acceso automático a información privada por administrar definiciones o contenido.

Readiness y publicación se definen en DR-DOM-006. Plazos, comportamiento operativo ante fallos, edición/eliminación de CheckIn y políticas de privacidad continúan abiertos donde no fueron decididos. No se inventan mecanismos técnicos para estas invariantes.

## Timeline, Trend y Guidance

**Timeline** es read model/projection que deriva principalmente de AssessmentResults y CheckIns. Ordena eventos históricos y responde «¿Qué ocurrió y cuándo?». No requiere que las mediciones mostradas sean comparables.

**Trend** es read model/cálculo derivado de AssessmentResults o Measurements compatibles. Responde «¿Cómo evolucionó una medición comparable a través del tiempo?». No es una entidad persistente confirmada.

**Guidance** combina interpretación documentada, limitaciones y contenido educativo relacionado mediante Topic. Los Resources actuales pueden evolucionar sin alterar el significado histórico de AssessmentResult. No es una entidad independiente ni recomendación clínica.

Timeline, Trend, Guidance y ComparabilityPolicy no se convierten en entidades por aparecer en el producto. Contexto temporal no implica causalidad.

## Open Questions

### OQ-DOM-027

- **ID:** OQ-DOM-027.
- **Pregunta:** ¿Cuál será la estructura concreta de ScoringDefinition?
- **Motivo:** SINGLE_CHOICE no determina cómo se representan las reglas ni supone score en AnswerOption.
- **Etapa:** Refinamiento del dominio, previo a su representación en Logical Data Model.
- **Documentos afectados:** Este documento, [reglas de negocio](../02-product/business-rules.md) y [aggregate candidates](aggregate-candidates.md).
- **Status:** RESOLVED.
- **Resolución:** ScoringDefinition queda definido conceptualmente como Definition Object dependiente con estrategia SUM, contribuciones y atomicidad del submit. La representación relacional sigue abierta en OQ-DATA-007. Véase [DR-DOM-005](#dr-dom-005).

### OQ-DOM-028

- **ID:** OQ-DOM-028.
- **Pregunta:** ¿Qué reglas concretas de completitud hacen publicable y ejecutable una InstrumentVersion SINGLE_CHOICE?
- **Motivo:** Las cardinalidades durante DRAFT permiten construcción incompleta, pero no definen todas las condiciones de publicación.
- **Etapa:** Refinamiento del dominio antes de cerrar las reglas de publicación en Logical Data Model.
- **Documentos afectados:** Este documento, RN-029 y [RF-020](../02-product/functional-requirements.md#rf-020).
- **Status:** RESOLVED.
- **Resolución:** Se definieron lifecycle, condiciones mínimas de READY, publicación única por Instrument y preservación histórica. Véase [DR-DOM-006](#dr-dom-006).

### OQ-DOM-029

- **ID:** OQ-DOM-029.
- **Pregunta:** ¿Qué multiplicidad y condiciones de uso tendrá Note dentro de CheckIn?
- **Motivo:** Solo se ha definido su pertenencia como Value; no sus límites ni cardinalidad.
- **Etapa:** Refinamiento del dominio antes de representar Note en Logical Data Model.
- **Documentos afectados:** Este documento, [aggregate candidates](aggregate-candidates.md) y [Tracking](../02-product/product-definition.md#tracking).
- **Status:** RESOLVED.
- **Resolución:** Note queda definido como texto libre opcional 0..1 y Value exclusivo de CheckIn, sin identidad, lifecycle ni scoring propios. Véase [DR-DOM-007](#dr-dom-007).

La representación relacional de compatibilidad, Answers, opciones y DimensionVersion está pendiente en [OQ-DATA-003 a OQ-DATA-006](../05-data/README.md#oq-data-003).

## Límite de esta versión

**CLOSED** significa suficiente definición conceptual para iniciar el modelo lógico; no significa dominio inmutable ni impide refinamientos ante nuevos requisitos. **Relational Logical Model: NEXT**, en una tarea posterior independiente.

La revisión de las preguntas restantes no identificó un bloqueo para comenzar ese análisis: las reglas estructurales están definidas; los valores de instrumentos/Dimensions, procedimientos operativos, privacidad, semántica temporal adicional y capacidades opcionales siguen pendientes y deben resolverse al concretar los casos afectados. No se dan por resueltos ni se consideran autorización para implementar. Véanse [preguntas de producto](../02-product/product-definition.md#open-questions), [reglas](../02-product/business-rules.md#open-questions), [dominio](domain-overview.md#open-questions) y [datos](../05-data/README.md#open-questions). No contiene un modelo lógico, diseño físico ni selección tecnológica. PostgreSQL sigue PROVISIONALLY ACCEPTED; backend, frontend, persistence/ORM, authentication, hosting e infraestructura siguen OPEN.
