# Reglas de negocio

Baseline de reglas del modelo conceptual v0.1, sin definir tablas ni clases. Las decisiones aceptadas están en [conceptual-model](../03-domain/conceptual-model.md); los detalles no decididos permanecen abiertos. Véanse [lenguaje ubicuo](../03-domain/ubiquitous-language.md) y [aggregate candidates](../03-domain/aggregate-candidates.md).

## Instrument

- **RN-001:** Un Instrument puede tener múltiples InstrumentVersions; identidad del instrumento y definición versionada son conceptos distintos.
- **RN-002:** Solo utilizar instrumentos con propósito, procedencia, scoring, interpretación y condiciones de uso adecuadamente documentados. Incorporar un cuestionario no equivale a validarlo psicológicamente.

## InstrumentVersion

- **RN-003:** Una InstrumentVersion pertenece exactamente a un Instrument.
- **RN-004:** El número de versión es único dentro del Instrument.
- **RN-005:** Publicar congela estructural y semánticamente InstrumentVersion, incluida la ScoringDefinition utilizada. PUBLISHED y RETIRED no vuelven a estados editables.
- **RN-006:** Cambios posteriores de estructura, scoring o significado requieren una nueva versión; no sobrescriben la definición histórica publicada.
- **RN-007:** Retirar una versión no elimina su definición ni la información histórica vinculada.

## AssessmentAttempt

- **RN-008:** Un AssessmentAttempt pertenece exactamente a un User y a una InstrumentVersion.
- **RN-009:** Los estados del MVP son `IN_PROGRESS`, `SUBMITTED`, `EXPIRED` y `CANCELLED`. SUBMITTED, EXPIRED y CANCELLED son terminales. Las condiciones y plazos de expiración/cancelación permanecen abiertos.
- **RN-010:** Un intento terminal no vuelve a IN_PROGRESS. Un nuevo Assessment requiere un nuevo AssessmentAttempt.
- **RN-011:** Las respuestas pueden modificarse antes del envío, según el estado del intento; una vez enviadas son inmutables.

## Answer

- **RN-012:** Cada Answer pertenece a un AssessmentAttempt y responde exactamente una Question de la InstrumentVersion exacta de ese intento.
- **RN-013:** Cada Answer selecciona exactamente una AnswerOption perteneciente a la Question respondida.
- **RN-014:** El MVP soporta SINGLE_CHOICE: máximo una Answer por AssessmentAttempt + Question. No incorpora TEXT, NUMBER, MULTIPLE_CHOICE ni un form builder genérico. La representación visual no define otro tipo de dominio.

## AssessmentResult

- **RN-015:** ScoringDefinition de la InstrumentVersion exacta transforma Answers válidas en resultado determinista mediante SUM para el MVP, con contribuciones normales/invertidas por Question + AnswerOption, sin score intrínseco en AnswerOption.
- **RN-016:** Un AssessmentAttempt SUBMITTED tiene exactamente un AssessmentResult oficial, generado como consecuencia del submit y dependiente de su límite de consistencia, sin CRUD ni Aggregate Root independiente. IN_PROGRESS, CANCELLED y EXPIRED no tienen resultado oficial. La política ante fallos de cálculo permanece abierta sin relajar esta invariante.
- **RN-017:** Los resultados históricos no se recalculan automáticamente al publicar nuevas versiones ni al consultarlos. Answers, versión exacta y ScoringDefinition histórica permiten reproducir/auditar el resultado oficial.
- **RN-018:** Los registros históricos permanecen interpretables según la definición exacta que los generó; no dependen de la versión actualmente publicada.

## CheckIn

- **RN-019:** Un CheckIn pertenece exactamente a un User y no es un AssessmentAttempt.
- **RN-020:** Cada Measurement conserva la DimensionVersion exacta utilizada y respeta su definición de medición. Dimension es la identidad estable; escalas, unidades y rangos concretos permanecen abiertos.
- **RN-021:** Yusay administra las Dimensions y permite configurarlas sin modificar código. El User no crea dimensiones arbitrarias durante el MVP.
- **RN-022:** Modificar y eliminar un CheckIn requiere respetar reglas explícitas. Ventanas, condiciones, efectos sobre historia y auditoría permanecen **Status: OPEN**.

## Comparison

- **RN-023:** Resultados de la misma InstrumentVersion y Measurements de la misma DimensionVersion son comparables. Versiones distintas del mismo Instrument o Dimension requieren declaración explícita y documentada de compatibilidad. UNKNOWN se trata como NOT COMPARABLE. No se comparan automáticamente diferentes Instruments ni diferentes Dimensions; igualdad de rango, nombre o normalización matemática no basta. ComparabilityPolicy es política de dominio, no entidad confirmada.
- **RN-024:** La asociación temporal entre contexto y medición no implica causalidad; Timeline y Trend no deben presentarla como explicación causal.

## Privacy

- **RN-025:** Un usuario no puede acceder a los datos privados de otro. La capacidad opcional de compartir reportes exige autorización explícita y limitada; no habilita acceso general.
- **RN-026:** Administrator no obtiene automáticamente acceso a respuestas, resultados o check-ins privados.

## Administration

- **RN-027:** Las operaciones administrativas relevantes deben ser auditables. El catálogo de operaciones y la retención de auditoría están pendientes.
- **RN-028:** La publicación y el retiro deben respetar integridad e historia. Solo PUBLISHED permite iniciar nuevos intentos; RETIRED lo impide y preserva historia. Continuar intentos ya iniciados tras el retiro permanece abierto en OQ-DOM-007.

## Invariantes incorporadas en v0.1

- **RN-029:** Cada Question pertenece exactamente a una InstrumentVersion y define sus propias AnswerOptions, sin compartirlas automáticamente. DRAFT admite construcción incompleta; READY exige validación y completitud ejecutable según [DR-DOM-006](../03-domain/conceptual-model.md#dr-dom-006); solo PUBLISHED permite nuevos intentos. No se asume score en AnswerOption.
- **RN-030:** Un CheckIn válido contiene al menos una Measurement y no puede contener más de una de la misma Dimension, incluso entre versiones diferentes.
- **RN-031:** Los cambios de escala, semántica, interpretación o significado de Measurement requieren una nueva DimensionVersion. Una definición utilizada no cambia retroactivamente el significado histórico; retirarla para nuevas mediciones no invalida historia. No se copia automáticamente el lifecycle de InstrumentVersion ([DR-DOM-002](../03-domain/conceptual-model.md#dr-dom-002)).
- **RN-032:** Timeline ordena eventos principalmente de AssessmentResults y CheckIns sin exigir comparabilidad; Trend deriva solo de AssessmentResults o Measurements compatibles. Ambos son read models, no entidades confirmadas ([DR-DOM-003](../03-domain/conceptual-model.md#dr-dom-003)).
- **RN-033:** Guidance es capacidad compuesta, no entidad persistente; Interpretation histórica no es Resource. Resource puede evolucionar, publicarse o retirarse sin modificar resultados históricos ([DR-DOM-004](../03-domain/conceptual-model.md#dr-dom-004)).
- **RN-034:** Topic es vocabulario controlado de Content asociado conceptualmente a Instrument, Interpretation, Dimension y Resource. No se crean vínculos directos Resource con esos conceptos si se resuelven por Topic, ni Topic con InstrumentVersion o DimensionVersion sin un caso posterior. ContextTag es contexto personal de Tracking, distinto de Topic.
- **RN-035:** Guidance no produce diagnóstico, tratamiento ni recomendación clínica. El MVP no incorpora Recommendation entity, RecommendationEngine, AI recommendation, UserResourceProfile ni scoring de recomendaciones.

- **RN-036:** ScoringDefinition es Definition Object dependiente de InstrumentVersion, no Aggregate Root. El MVP no admite subscales, múltiples scores, fórmulas arbitrarias, scripting ni motores universales; scoring no soportado impide publicar la versión como ejecutable ([DR-DOM-005](../03-domain/conceptual-model.md#dr-dom-005)).
- **RN-037:** Submit exige respuestas requeridas antes del cálculo. Scoring, resolución de Interpretation cuando corresponda, creación de resultado y transición a SUBMITTED forman una operación conceptual atómica.
- **RN-038:** Si la fuente define Interpretations por score/rango, todo resultado oficial producible que requiera interpretación resuelve una inequívoca. No se inventan interpretaciones ausentes en la fuente.
- **RN-039:** InstrumentVersion sigue DRAFT ⇄ READY → PUBLISHED → RETIRED. READY representa validación superada según DR-DOM-006 y puede volver a DRAFT antes de publicación; DRAFT y READY no admiten nuevos intentos.
- **RN-040:** Como máximo una InstrumentVersion por Instrument puede estar PUBLISHED para nuevos intentos. Cada intento nuevo usa esa versión; los históricos conservan su definición exacta aunque quede RETIRED.
- **RN-041:** CheckIn contiene 0..1 Note, texto libre opcional y Value exclusivo sin identidad/lifecycle independiente. No modifica Measurements ni participa en scoring, no establece causalidad entre ContextTags y Measurements ni se usa automáticamente para interpretaciones clínicas ([DR-DOM-007](../03-domain/conceptual-model.md#dr-dom-007)).

## Open Questions

### OQ-DOM-001

- **ID:** OQ-DOM-001.
- **Pregunta:** ¿Qué transiciones de AssessmentAttempt son válidas y bajo qué condiciones?
- **Motivo:** Los estados terminales están definidos; faltan condiciones concretas de expiración/cancelación.
- **Impacto:** RN-009, RN-010 y [RF-007](functional-requirements.md#rf-007).
- **Status:** OPEN.
- **Resolución parcial:** Se fijaron estados terminales; las condiciones de las transiciones no especificadas siguen pendientes en OQ-DOM-002.

### OQ-DOM-002

- **ID:** OQ-DOM-002.
- **Pregunta:** ¿Qué condiciones y plazos hacen expirar o cancelar un intento?
- **Motivo:** Definir vigencia sin inventar ventanas temporales.
- **Impacto:** RN-009 y RF-007.
- **Status:** OPEN.

### OQ-DOM-003

- **ID:** OQ-DOM-003.
- **Pregunta:** ¿Qué respuestas son obligatorias para enviar un intento?
- **Motivo:** La requiredness de cada Question debe estar definida para READY y submit exige las respuestas requeridas; su configuración concreta depende del instrumento documentado.
- **Impacto:** RN-014 y RF-008.
- **Status:** OPEN.
- **Resolución parcial:** DR-DOM-001 fija SINGLE_CHOICE y DR-DOM-006 exige requiredness definida; DR-DOM-005 exige respuestas requeridas antes de calcular. La configuración concreta según fuente sigue abierta; OQ-DOM-028 está resuelta. Véase [DR-DOM-001](../03-domain/conceptual-model.md#dr-dom-001).

### OQ-DOM-004

- **ID:** OQ-DOM-004.
- **Pregunta:** ¿Cómo se resolverán concurrencia y reintentos de envío?
- **Motivo:** Preservar envío irreversible y unicidad oficial ante solicitudes repetidas.
- **Impacto:** RN-010, RN-016 y [RNF-002](non-functional-requirements.md#rnf-002--consistencia-transaccional).
- **Status:** OPEN.

### OQ-DOM-005

- **ID:** OQ-DOM-005.
- **Pregunta:** ¿Qué comportamiento corresponde a un fallo de cálculo del resultado?
- **Motivo:** Evitar pérdida de consistencia o duplicación de resultados oficiales.
- **Impacto:** RN-015, RN-016 y RF-008.
- **Status:** OPEN.
- **Resolución parcial:** Scoring, Interpretation aplicable, resultado y transición a SUBMITTED son conceptualmente atómicos según [DR-DOM-005](../03-domain/conceptual-model.md#dr-dom-005); quedan pendientes la recuperación y comunicación operativa de fallos, sin permitir estados que violen RN-016.

### OQ-DOM-006

- **ID:** OQ-DOM-006.
- **Pregunta:** ¿Qué correcciones de interpretaciones o metadatos publicados preservan su comprensión histórica?
- **Motivo:** Precisar los límites de corrección sin relajar la inmutabilidad estructural ni del scoring.
- **Impacto:** RN-005, RN-006, RN-018 y [Definition](../03-domain/domain-overview.md#assessment-definition).
- **Status:** OPEN.
- **Resolución parcial:** [DR-DOM-006](../03-domain/conceptual-model.md#dr-dom-006) congela estructura y semántica al publicar y exige nueva versión ante cambios de significado. Solo quedan abiertos procedimientos de corrección que respeten esa inmutabilidad.

### OQ-DOM-007

- **ID:** OQ-DOM-007.
- **Pregunta:** ¿Puede continuarse un intento ya iniciado cuando su InstrumentVersion queda RETIRED?
- **Pregunta original:** ¿Puede iniciarse o continuarse un intento sobre una versión retirada?
- **Motivo:** DR-DOM-006 prohíbe iniciar nuevos intentos sobre RETIRED; no define continuidad de los intentos ya iniciados.
- **Impacto:** RN-007, RN-028, RF-006 y RF-007.
- **Status:** OPEN.
- **Resolución parcial:** No se permiten nuevos intentos en RETIRED según [DR-DOM-006](../03-domain/conceptual-model.md#dr-dom-006); la continuidad no fue decidida.

### OQ-DOM-008

- **ID:** OQ-DOM-008.
- **Pregunta:** ¿Qué condiciones permiten modificar un CheckIn?
- **Motivo:** Concretar una función MUST sin fijar una ventana arbitraria.
- **Impacto:** RN-022, RF-011 y tendencias derivadas.
- **Status:** OPEN.

### OQ-DOM-009

- **ID:** OQ-DOM-009.
- **Pregunta:** ¿Qué condiciones y efectos tiene eliminar un CheckIn?
- **Motivo:** Delimitar eliminación y sus consecuencias sobre historia y auditoría.
- **Impacto:** RN-022, RF-011 y RNF-007.
- **Status:** OPEN.

### OQ-DOM-010

- **ID:** OQ-DOM-010.
- **Pregunta:** ¿Cómo se conserva la definición histórica de una Dimension cuando cambian sus reglas?
- **Motivo:** Mantener interpretabilidad de Measurements configurables.
- **Impacto:** RN-020, RN-021 y [Tracking](product-definition.md#tracking).
- **Status:** RESOLVED.
- **Resolución:** Measurement conserva DimensionVersion exacta; cambios semánticos requieren otra versión y no alteran historia. Véase [DR-DOM-002](../03-domain/conceptual-model.md#dr-dom-002).

### OQ-DOM-011

- **ID:** OQ-DOM-011.
- **Pregunta:** ¿Qué criterios definen compatibilidad entre mediciones, instrumentos y versiones?
- **Motivo:** Impedir comparaciones que el dominio no justifique.
- **Impacto:** RN-023, LIM-009, RF-016 y RF-017.
- **Status:** RESOLVED.
- **Resolución:** Se definieron comparabilidad por versión exacta, declaración entre versiones y tratamiento conservador de UNKNOWN. Su representación relacional queda en OQ-DATA-003. Véase [DR-DOM-003](../03-domain/conceptual-model.md#dr-dom-003).

### OQ-DOM-012

- **ID:** OQ-DOM-012.
- **Pregunta:** ¿Qué política concilia retención histórica y eliminación de datos personales?
- **Motivo:** No confundir preservación de definiciones con conservación ilimitada de registros privados.
- **Impacto:** RN-007, RN-018, RNF-005 y [Data](../05-data/README.md).
- **Status:** OPEN.

### OQ-DOM-013

- **ID:** OQ-DOM-013.
- **Pregunta:** ¿Qué datos podrá incluir un reporte compartido opcional?
- **Motivo:** Delimitar la exposición autorizada sin habilitar acceso general.
- **Impacto:** RN-025 y RF-025.
- **Status:** OPEN.

### OQ-DOM-014

- **ID:** OQ-DOM-014.
- **Pregunta:** ¿Qué vigencia y revocación tendrá un enlace temporal compartido?
- **Motivo:** Precisar el control de acceso limitado de la capacidad COULD.
- **Impacto:** RN-025, RF-025 y estrategia de autorización.
- **Status:** OPEN.
