# Ubiquitous Language — baseline

Los términos técnicos se mantienen en inglés. Los nombres con espacios y formas como InstrumentVersion o CheckIn identifican el mismo concepto, sin presuponer clases.

- **Instrument:** identidad estable y propósito de un instrumento, distinto de su definición versionada.
- **Instrument Version (InstrumentVersion):** definición exacta con Questions, AnswerOptions, ScoringDefinition e Interpretations. Su lifecycle es DRAFT ⇄ READY → PUBLISHED → RETIRED. READY es completa, validada y ejecutable; solo PUBLISHED permite nuevos intentos, con máximo una por Instrument. Publicada, conserva inmutables estructura, semántica y scoring; RETIRED preserva historia.
- **Question:** Entity de una InstrumentVersion, SINGLE_CHOICE para el MVP, con sus propias opciones.
- **Answer Option (AnswerOption):** opción de una Question específica; no presupone un campo score.
- **Assessment:** capacidad de autoevaluación estructurada; una ejecución concreta es AssessmentAttempt.
- **Assessment Attempt (AssessmentAttempt):** ejecución por un User de una InstrumentVersion exacta. SUBMITTED, EXPIRED y CANCELLED son terminales.
- **Answer:** Entity interna del intento que responde exactamente una Question de su versión y selecciona exactamente una opción válida; máximo una por intento y Question.
- **Assessment Result (AssessmentResult):** objeto dependiente del límite de consistencia del intento, generado por submit. SUBMITTED tiene exactamente uno oficial; los demás estados no tienen resultado oficial. No posee CRUD independiente.
- **ScoringDefinition:** Definition Object dependiente de InstrumentVersion que transforma Answers válidas en resultado determinista mediante SUM. Define contribuciones normales/invertidas de Question + AnswerOption; no es Aggregate Root ni un score intrínseco de la opción.
- **Interpretation:** objeto histórico dependiente de InstrumentVersion / Value-Definition candidate que explica el resultado según el instrumento, sin diagnóstico de Yusay.
- **Check-in (CheckIn):** registro personal breve, diferente de Assessment, con al menos una Measurement y sin repetir Dimension.
- **Dimension:** identidad conceptual estable de aquello que se mide; Aggregate Root de Tracking. Mood, Stress, Energy y Sleep son ejemplos iniciales.
- **DimensionVersion:** Entity interna de Dimension con definición de medición, escala, rango, step cuando corresponda, labels/anchors y semántica.
- **Measurement:** Value Object candidate de CheckIn con valor y referencia conceptual a la DimensionVersion exacta utilizada.
- **Context:** información personal que sitúa registros en periodos o circunstancias; no prueba causalidad. Su semántica temporal completa permanece abierta.
- **ContextTag:** Entity/catalog concept de Tracking para contexto personal, como Work, Family o University; no clasifica contenido educativo.
- **Note:** texto libre opcional, 0..1 Value exclusivo de CheckIn, sin identidad/lifecycle independiente; no modifica Measurements, no participa en scoring ni implica causalidad o interpretación clínica automática.
- **Timeline:** Read Model / projection de eventos principalmente de AssessmentResults y CheckIns; responde qué ocurrió y cuándo, sin exigir comparabilidad.
- **Trend:** derived Read Model / domain calculation sobre AssessmentResults o Measurements compatibles; responde cómo evolucionó una medición comparable.
- **ComparabilityPolicy:** política/regla de comparabilidad según versión exacta o declaración explícita entre versiones del mismo concepto. UNKNOWN se trata como NOT COMPARABLE; no es entidad persistente confirmada.
- **Guidance:** capacidad compuesta de Interpretation, documentación/limitaciones, Topic y Resource, sin entidad independiente ni recomendación clínica.
- **Resource:** Aggregate Root de contenido educativo/informativo con publicación, retiro y evolución independientes; no reemplaza Interpretation histórica.
- **Topic:** Entity/catalog concept de Content y vocabulario controlado asociado a Instrument, Interpretation, Dimension y Resource.
- **User:** actor propietario de sus registros privados y Aggregate Root.
- **Administrator:** actor que administra definiciones y contenido sin acceso automático a datos privados.
- **Visitor:** actor sin autenticación, con permisos pendientes.

## Distinciones obligatorias

```text
Assessment != CheckIn
Instrument != InstrumentVersion
Dimension != DimensionVersion
Interpretation != Resource
ContextTag != Topic
Timeline != Trend
AssessmentResult != Guidance
Historical definition != current content
Timeline != persisted domain entity
Trend != persisted domain entity
```

Timeline, Trend y Guidance no son entidades confirmadas. Véanse [agregados](aggregate-candidates.md) y [modelo conceptual](conceptual-model.md); las clasificaciones no implican tablas. DimensionVersion no hereda automáticamente el lifecycle de InstrumentVersion.
