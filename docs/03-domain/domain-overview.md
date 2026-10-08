# Visión del dominio — baseline

El descubrimiento queda consolidado como baseline del [modelo conceptual v0.1](conceptual-model.md). El conceptual queda v0.1 CLOSED; el modelo lógico es NEXT y el físico sigue pendiente.

## Assessment Definition

Instrument, InstrumentVersion, Question, AnswerOption, ScoringDefinition e Interpretation describen la definición histórica. El MVP admite SINGLE_CHOICE y scoring SUM definido por ScoringDefinition, separado de Answer y AnswerOption. DRAFT puede estar incompleto; READY valida completitud y ejecutabilidad; solo PUBLISHED admite nuevos intentos, con máximo una versión por Instrument. Véanse [DR-DOM-005](conceptual-model.md#dr-dom-005) y [DR-DOM-006](conceptual-model.md#dr-dom-006).

## Assessment Execution

AssessmentAttempt, Answer y AssessmentResult describen la ejecución por un User. AssessmentAttempt conserva la InstrumentVersion exacta. El submit exige respuestas requeridas y comprende scoring determinista, Interpretation cuando corresponda, creación del resultado y transición a SUBMITTED en una operación conceptual atómica. AssessmentResult pertenece al límite del intento, sin raíz ni CRUD independiente; consultarlo no recalcula su valor.

## Principio histórico

> Los registros históricos deben permanecer vinculados a la definición exacta bajo la cual fueron generados.

```text
AssessmentAttempt → InstrumentVersion
                     ├── Questions y AnswerOptions
                     ├── ScoringDefinition
                     └── Interpretations

CheckIn → Measurement → DimensionVersion → Dimension
```

Las versiones actuales no reemplazan definiciones históricas. Retirar versiones no invalida registros previos ni produce recálculo automático.

## Tracking y contenido

Dimension es identidad estable; DimensionVersion define exactamente la Measurement. Un CheckIn válido reúne al menos una Measurement, sin repetir Dimension. ContextTag representa contexto personal y Note es texto libre opcional, 0..1 Value exclusivo de CheckIn sin scoring ni identidad independiente; la semántica temporal más amplia de Context sigue pendiente.

Topic es vocabulario controlado de Content, distinto de ContextTag. Relaciona Instrument, Interpretation, Dimension y Resource. Interpretation conserva significado histórico; Resource educativo evoluciona independientemente.

## Modelos derivados y capacidades

Timeline muestra principalmente eventos de AssessmentResults y CheckIns sin exigir comparabilidad. Trend calcula evolución con AssessmentResults o Measurements compatibles según DR-DOM-003. Guidance compone Interpretation, documentación/limitaciones, Topic y Resource según DR-DOM-004. Ninguno es entidad persistente confirmada; ComparabilityPolicy es política de dominio.

Contexto temporal no implica causalidad. Guidance no incluye diagnóstico ni recomendación clínica.

## Open Questions

### OQ-DOM-015

- **ID:** OQ-DOM-015.
- **Pregunta:** ¿Cómo se representa Context y su relación temporal con los registros?
- **Motivo:** Definir el concepto sin atribuir causalidad ni presumir etiquetas obligatorias.
- **Impacto:** [Producto](../02-product/product-definition.md#tracking), RN-024 y futuro modelo conceptual.
- **Status:** OPEN.

### OQ-DOM-016

- **ID:** OQ-DOM-016.
- **Pregunta:** ¿Qué relaciones y cardinalidades corresponden a los conceptos del dominio?
- **Motivo:** Pasar del descubrimiento a un modelo conceptual justificado.
- **Impacto:** Este documento, [lenguaje ubicuo](ubiquitous-language.md) y [Data](../05-data/README.md).
- **Status:** RESOLVED.
- **Resolución:** Las relaciones y cardinalidades del baseline v0.1 están documentadas, incluidas asociaciones de Topic y definiciones durante construcción. Véase [DR-DOM-001 a DR-DOM-004 y relaciones conceptuales](conceptual-model.md#relaciones-y-cardinalidades).

### Preguntas relacionadas

- [OQ-DOM-010](../02-product/business-rules.md#oq-dom-010): ¿Cómo se conserva la definición histórica de una Dimension cuando cambian sus reglas?
- [OQ-DOM-021](aggregate-candidates.md#oq-dom-021): ¿Dónde pertenece AssessmentResult y qué invariantes determinan su ciclo de vida?
