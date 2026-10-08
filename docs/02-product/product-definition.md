# Definición del producto — baseline

Yusay permite seguimiento personal estructurado del bienestar. Sus tres pilares conectan medición, registro, contexto y comprensión, dentro de las [limitaciones](limitations.md) y el [alcance](scope.md) del MVP.

## Assessment

Permite utilizar instrumentos estructurados y adecuadamente documentados. Un Instrument puede incluir nombre, código, propósito, descripción, fuente, población objetivo, licencia o condiciones de uso, versiones, preguntas, opciones, scoring, interpretaciones, limitaciones y referencias.

Crear un cuestionario en el sistema no lo convierte automáticamente en un instrumento psicológico validado. La procedencia, las condiciones de uso y el fundamento de su interpretación deben quedar documentados.

Existe una separación conceptual:

```text
Instrument → InstrumentVersion → Question → AnswerOption
```

Una versión publicada representa una definición histórica específica del instrumento. El intento y su resultado se vinculan a esa definición exacta, incluidas las reglas de scoring e interpretación. La versión actualmente publicada no reemplaza la definición de registros anteriores. El MVP utiliza SINGLE_CHOICE según [DR-DOM-001](../03-domain/conceptual-model.md#dr-dom-001); no asume score en AnswerOption ni un form builder genérico. [DR-DOM-005](../03-domain/conceptual-model.md#dr-dom-005) limita inicialmente scoring a SUM determinista, con contribuciones normales/invertidas definidas por ScoringDefinition. [DR-DOM-006](../03-domain/conceptual-model.md#dr-dom-006) establece readiness validada y lifecycle DRAFT ⇄ READY → PUBLISHED → RETIRED; solo PUBLISHED permite nuevos intentos.

## Tracking

Comprende Check-ins, Timeline, Context y Trends. Un `CheckIn` no es un `Assessment`: representa un registro personal breve y periódico, sin asumir las propiedades de un instrumento estructurado.

Las dimensiones iniciales candidatas son `Mood`, `Stress`, `Energy` y `Sleep`. Yusay administrará sus definiciones y reglas de medición, configurables sin modificar código. Los usuarios no podrán crear dimensiones arbitrarias durante el MVP. Dimension identifica aquello que se mide; DimensionVersion define la medición exacta. Cada Measurement conserva su DimensionVersion; cambios semánticos requieren otra versión sin alterar historia. Las escalas y unidades concretas siguen abiertas. Véase [DR-DOM-002](../03-domain/conceptual-model.md#dr-dom-002).

`Timeline` y `Trend` son read models/información derivada, no entidades confirmadas. Timeline ordena eventos principalmente de AssessmentResults y CheckIns sin exigir comparabilidad. Trend deriva solo de AssessmentResults o Measurements compatibles según [DR-DOM-003](../03-domain/conceptual-model.md#dr-dom-003). Su materialización no está decidida.

El contexto personal forma parte de la propuesta; el mecanismo mínimo para registrarlo está pendiente. ContextTag y Note pertenecen conceptualmente a Tracking: ContextTag expresa contexto personal y Note es texto libre opcional, 0..1 Value exclusivo del CheckIn, sin identidad ni lifecycle independiente, sin modificar Measurements ni participar en scoring; no implica causalidad o interpretación clínica automática ([DR-DOM-007](../03-domain/conceptual-model.md#dr-dom-007)). Los context tags siguen siendo SHOULD; la clasificación conceptual no introduce una nueva prioridad de producto.

## Guidance

Comprende explicación contextual de resultados, limitaciones, recursos educativos, información relacionada y posibles siguientes pasos informativos. Las interpretaciones documentadas del instrumento deben presentarse con su propósito y límites.

Guidance es capacidad compuesta, no entidad independiente. Interpretation histórica y Resource educativo son distintos: el contenido actual puede evolucionar sin alterar AssessmentResults históricos. Topic relaciona Instrument, Interpretation, Dimension y Resource según [DR-DOM-004](../03-domain/conceptual-model.md#dr-dom-004). ContextTag no es Topic.

No comprende diagnóstico, prescripción, tratamiento, recomendación clínica ni interpretación clínica generada automáticamente.

## Open Questions

### OQ-PROD-004

- **ID:** OQ-PROD-004.
- **Pregunta:** ¿Qué instrumentos se incorporarán y con qué documentación y condiciones de uso?
- **Motivo:** Aplicar LIM-003 sin presumir validación ni permisos.
- **Impacto:** Assessment, [catálogo](functional-requirements.md#rf-005) y RN-002.
- **Status:** OPEN.

### OQ-PROD-005

- **ID:** OQ-PROD-005.
- **Pregunta:** ¿Cómo se capturará el contexto personal mínimo?
- **Motivo:** La propuesta incluye contexto, pero context tags es SHOULD y no define el mecanismo mínimo.
- **Impacto:** Tracking, [RF-015](functional-requirements.md#rf-015) y [Context](../03-domain/ubiquitous-language.md).
- **Status:** OPEN.

### OQ-PROD-006

- **ID:** OQ-PROD-006.
- **Pregunta:** ¿Qué escalas, unidades y restricciones concretas tendrá cada DimensionVersion?
- **Pregunta original:** ¿Qué escalas, unidades y restricciones tendrá cada Dimension?
- **Motivo:** DR-DOM-002 define DimensionVersion como soporte histórico, pero no elige escalas o unidades concretas.
- **Impacto:** Tracking, [RN-020](business-rules.md#checkin) y [dominio](../03-domain/domain-overview.md).
- **Status:** OPEN.

### OQ-PROD-007

- **ID:** OQ-PROD-007.
- **Pregunta:** ¿Qué criterios editoriales y procedimientos concretos regirán la selección y mantenimiento de explicaciones y recursos?
- **Pregunta original:** ¿Cómo se seleccionarán y mantendrán las explicaciones y los recursos?
- **Motivo:** DR-DOM-004 ya separa Interpretation histórica, Resource independiente y Topic; faltan criterios editoriales y procedimientos.
- **Impacto:** [RF-018](functional-requirements.md#rf-018), RF-022 y RF-023.
- **Status:** OPEN.
