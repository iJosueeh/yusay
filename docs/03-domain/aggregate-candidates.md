# Agregados y conceptos — baseline v0.1

La clasificación consolida las decisiones aceptadas del [modelo conceptual](conceptual-model.md). No determina entidades ORM ni estructuras de persistencia. Las clasificaciones candidate conservan su carácter abierto.

## Aggregate Roots

User, Instrument, InstrumentVersion, AssessmentAttempt, CheckIn, Dimension y Resource son Aggregate Roots del baseline. Dimension pertenece a Tracking. Instrument e InstrumentVersion son raíces distintas.

## Conceptos internos y dependientes

- **Question:** Entity interna de InstrumentVersion.
- **AnswerOption:** Entity interna de Question / InstrumentVersion.
- **ScoringDefinition:** Definition Object dependiente de InstrumentVersion, con método SUM para el MVP; no Aggregate Root. Su representación relacional permanece pendiente. ScoringRule y ScoringMapping no son Entities confirmadas.
- **Interpretation:** objeto dependiente de InstrumentVersion / Value-Definition candidate; no Aggregate Root.
- **Answer:** Entity interna de AssessmentAttempt.
- **AssessmentResult:** objeto dependiente del límite de consistencia de AssessmentAttempt; no Aggregate Root independiente ni CRUD independiente.
- **Measurement:** Value Object candidate perteneciente a CheckIn.
- **DimensionVersion:** Entity interna del agregado Dimension.
- **ContextTag:** Entity/catalog concept de Tracking.
- **Note:** Value de texto libre opcional, 0..1 por CheckIn, sin identidad ni lifecycle independiente; no Measurement ni scoring.
- **Topic:** Entity/catalog concept de Content.

```text
InstrumentVersion
 ├── Question
 │    └── AnswerOption
 ├── ScoringDefinition (Definition Object)
 └── Interpretation (candidate)

AssessmentAttempt
 ├── Answer
 └── AssessmentResult (dependiente del límite de consistencia)

CheckIn
 ├── Measurement (Value Object candidate)
 └── Note (Value)

Dimension
 └── DimensionVersion
```

## Capacidades y modelos derivados

- **Timeline:** Read Model / projection; no Entity confirmada.
- **Trend:** derived Read Model / domain calculation; no Entity confirmada.
- **Guidance:** composed product/domain capability; no Entity persistente confirmada.
- **ComparabilityPolicy:** domain policy/rule; no Entity persistente confirmada.

Las cardinalidades y DR-DOM-001 a DR-DOM-007 se documentan en [conceptual-model](conceptual-model.md).

## Open Questions

### OQ-DOM-021

- **ID:** OQ-DOM-021.
- **Pregunta:** ¿Dónde pertenece AssessmentResult y qué invariantes determinan su ciclo de vida?
- **Motivo:** Analizar generación, unicidad e interpretación histórica sin resolver arbitrariamente su ubicación.
- **Impacto:** [RN-015 a RN-018](../02-product/business-rules.md#assessmentresult), AssessmentAttempt y futuro modelo conceptual.
- **Status:** RESOLVED.
- **Resolución:** AssessmentResult depende del límite de AssessmentAttempt; el submit genera un resultado oficial para SUBMITTED, sin raíz ni CRUD independiente. Véase [baseline e invariantes conceptuales](conceptual-model.md#invariantes-del-baseline).

### OQ-DOM-022

- **ID:** OQ-DOM-022.
- **Pregunta:** ¿Qué límites de consistencia justifican separar Instrument e InstrumentVersion?
- **Motivo:** Validar raíces candidatas y coordinación del ciclo de vida.
- **Impacto:** Este documento y [RN-001 a RN-007](../02-product/business-rules.md#instrument).
- **Status:** RESOLVED.
- **Resolución:** El baseline fija Instrument e InstrumentVersion como Aggregate Roots separados. Los límites operativos de coordinación restantes se mantienen en OQ-DOM-026. Véase [pertenencias conceptuales](conceptual-model.md#pertenencias-conceptuales).

### OQ-DOM-023

- **ID:** OQ-DOM-023.
- **Pregunta:** ¿Qué tamaños y patrones de concurrencia presentan definiciones, respuestas y Measurements?
- **Motivo:** Comprobar si las agrupaciones candidatas son manejables.
- **Impacto:** Este documento, RNF-002 y RNF-009.
- **Status:** OPEN.

### OQ-DOM-024

- **ID:** OQ-DOM-024.
- **Pregunta:** ¿Qué ciclo de vida tienen Dimension, Context, Topic y Resource?
- **Motivo:** Las pertenencias de Dimension, DimensionVersion, ContextTag, Note, Topic y Resource ya están fijadas; faltan detalles de ciclo de vida sin copiar el de InstrumentVersion.
- **Impacto:** Este documento y [lenguaje ubicuo](ubiquitous-language.md).
- **Status:** OPEN.

### OQ-DOM-025

- **ID:** OQ-DOM-025.
- **Pregunta:** ¿Qué referencias entre agregados se necesitan para preservar invariantes?
- **Motivo:** Examinar pertenencia y consistencia antes de definir asociaciones físicas.
- **Impacto:** Este documento, [reglas de negocio](../02-product/business-rules.md) y [Data](../05-data/README.md).
- **Status:** RESOLVED.
- **Resolución:** Las referencias conceptuales del baseline están fijadas: Attempt/Question/opción de la versión exacta, Measurement/DimensionVersion y asociaciones por Topic. No define su garantía relacional; esa etapa mantiene OQ-DATA-003 a OQ-DATA-006 abiertas. Véanse [DR-DOM-001](conceptual-model.md#dr-dom-001), [DR-DOM-002](conceptual-model.md#dr-dom-002) y [DR-DOM-004](conceptual-model.md#dr-dom-004).

### OQ-DOM-026

- **ID:** OQ-DOM-026.
- **Pregunta:** ¿Qué otros límites operativos de consistencia requieren las operaciones del dominio además de submit y generación de AssessmentResult?
- **Pregunta original:** ¿Qué operaciones requieren consistencia atómica y cuáles admiten información derivada?
- **Motivo:** Scoring, Interpretation aplicable, resultado y transición a SUBMITTED forman una operación conceptual atómica en AssessmentAttempt según DR-DOM-005; deben acotarse las otras operaciones sin elegir persistencia.
- **Impacto:** [RNF-002](../02-product/non-functional-requirements.md#rnf-002--consistencia-transaccional), envío y resultado, contextos candidatos y Data.
- **Status:** OPEN.
