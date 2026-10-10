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

Las dimensiones iniciales candidatas son `Mood`, `Stress`, `Energy` y `Sleep`. Yusay administrará sus definiciones y reglas de medición, configurables sin modificar código. Los usuarios no podrán crear dimensiones arbitrarias durante el MVP. Dimension identifica aquello que se mide; DimensionVersion define la medición exacta. Cada Measurement conserva su DimensionVersion; cambios semánticos requieren otra versión sin alterar historia. Las escalas y unidades concretas de las dimensiones iniciales quedan fijadas en [OQ-PROD-006](#oq-prod-006). Véase [DR-DOM-002](../03-domain/conceptual-model.md#dr-dom-002).

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
- **Motivo:** DR-DOM-002 define DimensionVersion como soporte histórico, pero no elegía escalas o unidades concretas para las dimensiones iniciales candidatas.
- **Impacto:** Tracking, [RN-020](business-rules.md#checkin), RN-021, RN-023, RN-030, RN-031, RF-010, RF-016, RF-021 y [dominio](../03-domain/domain-overview.md).
- **Resolución:** Las dimensiones iniciales del MVP quedan definidas con sus códigos y descripciones: `mood` — estado de ánimo percibido; `stress` — estrés percibido; `energy` — energía percibida; `sleep` — calidad del último periodo de sueño percibida, no duración. Cada una tendrá exactamente una versión inicial con `version = 1`, `min_value = 1`, `max_value = 5` y `step = 1`, conforme a todas las restricciones aprobadas de DIMENSION_VERSION. Cada versión inicial lleva cinco anclas descriptivas, una por valor: Mood — «Muy negativo», «Negativo», «Ni negativo ni positivo», «Positivo», «Muy positivo»; Stress — «Nada de estrés», «Poco estrés», «Estrés moderado», «Mucho estrés», «Estrés muy intenso»; Energy — «Muy baja», «Baja», «Moderada», «Alta», «Muy alta»; Sleep — «Muy mala», «Mala», «Regular», «Buena», «Muy buena». Las escalas son ordinales, subjetivas y no clínicas. Toda DimensionVersion inicial describirá explícitamente en `definition` la percepción medida, la referencia temporal, el carácter ordinal y subjetivo de la escala 1–5, la orientación de los valores y el alcance no clínico, sin añadir columnas a la línea base lógica congelada y sin inferir automáticamente la orientación a partir del texto. Un CheckIn válido contiene al menos una Measurement y las dimensiones restantes son opcionales, sin repetir ninguna Dimensión (RN-030, RF-010); la edición existente conserva el conjunto de dimensiones registrado. Cada Measurement conserva su DimensionVersion exacta y no se presume compatibilidad entre versiones distintas (RN-020, RN-023). Las cinco anclas iniciales son una decisión editorial de estas versiones: no modifican la cardinalidad aprobada 0..N ni crean un mínimo general de anclas. Todo se representa con los campos existentes, sin migraciones estructurales; la creación de estos datos iniciales y su administración corresponde a RF-021. La validación de pertenencia de anclas (G1), la validación en la activación (G2) y la inmutabilidad de `definition` en versiones ACTIVE/RETIRED (G3) permanecen como pendientes obligatorios de RF-021 y no se describen como implementados; en particular, G3 es la garantía física de la orientación aquí documentada. Esta resolución es exclusivamente normativa: no crea datos iniciales ni migraciones. OQ-DATA-003 y OQ-DATA-006 permanecen OPEN de forma independiente y no se resuelven por esta decisión.
- **Status:** RESOLVED mediante aprobación explícita del responsable del proyecto.

### OQ-PROD-007

- **ID:** OQ-PROD-007.
- **Pregunta:** ¿Qué criterios editoriales y procedimientos concretos regirán la selección y mantenimiento de explicaciones y recursos?
- **Pregunta original:** ¿Cómo se seleccionarán y mantendrán las explicaciones y los recursos?
- **Motivo:** DR-DOM-004 ya separa Interpretation histórica, Resource independiente y Topic; faltan criterios editoriales y procedimientos.
- **Impacto:** [RF-018](functional-requirements.md#rf-018), RF-022 y RF-023.
- **Status:** OPEN.
