# Requisitos funcionales

Baseline documental derivado del [alcance](scope.md) y la [definición del producto](product-definition.md). Cada registro indica ID, Name, Description, Priority y Status. `PROPOSED` indica propuesta pendiente de aprobación; `ACCEPTED`, aprobación explícita; `DEFERRED`, aplazamiento explícito. Se conservan los estados individuales `PROPOSED`: las decisiones conceptuales aceptadas refinan su descripción, sin aprobar criterios todavía abiertos. Véase [modelo conceptual v0.1](../03-domain/conceptual-model.md).

## Identity

### RF-001
- **Name:** Registro y verificación de correo.
- **Description:** Permitir crear una cuenta y verificar el correo asociado. Los efectos de no verificarlo están abiertos.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-002
- **Name:** Autenticación y logout.
- **Description:** Permitir autenticarse y cerrar la sesión mediante una estrategia por definir.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-003
- **Name:** Recuperación de contraseña.
- **Description:** Permitir recuperar acceso mediante recuperación de contraseña, con controles de seguridad por definir.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-004
- **Name:** Perfil y privacidad.
- **Description:** Permitir consultar y actualizar el perfil y utilizar controles básicos de privacidad; sus campos y acciones quedan abiertos.
- **Priority:** MUST.
- **Status:** PROPOSED.

## Assessment Catalog

### RF-005
- **Name:** Catálogo y detalle.
- **Description:** Mostrar instrumentos disponibles y su documentación, propósito, versión, condiciones de uso y limitaciones.
- **Priority:** MUST.
- **Status:** PROPOSED.

## Assessment Execution

### RF-006
- **Name:** Iniciar y responder un Assessment.
- **Description:** Crear un AssessmentAttempt asociado al User y a la InstrumentVersion PUBLISHED correspondiente, única como máximo por Instrument para nuevos intentos; permitir responder preguntas SINGLE_CHOICE seleccionando exactamente una AnswerOption válida por Answer y modificar respuestas antes del envío. Máximo una Answer por intento y Question.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-007
- **Name:** Guardar y continuar progreso.
- **Description:** Conservar progreso y permitir continuar un intento conforme a su estado y a las condiciones de vigencia pendientes.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-008
- **Name:** Enviar y obtener resultado.
- **Description:** El submit exige respuestas requeridas y realiza scoring SUM determinista, resolución de Interpretation cuando la fuente la define, creación del resultado oficial y transición a SUBMITTED como operación conceptual atómica usando la versión exacta. El resultado pertenece al límite de consistencia del intento, sin CRUD independiente; los otros estados no tienen resultado oficial.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-009
- **Name:** Historial de evaluaciones.
- **Description:** Permitir al User consultar sus evaluaciones y resultados oficiales sin recalcularlos, conservando Answers, InstrumentVersion exacta y ScoringDefinition histórica para reproducir/auditar el cálculo.
- **Priority:** MUST.
- **Status:** PROPOSED.

## Check-ins

### RF-010
- **Name:** Crear y consultar CheckIn.
- **Description:** Permitir al User registrar y consultar check-ins personales con al menos una Measurement, sin repetir Dimension, y conservar la DimensionVersion exacta usada por cada Measurement. Un CheckIn puede incluir 0..1 Note de texto libre opcional, sin alterar Measurements ni participar en scoring.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-011
- **Name:** Modificar y eliminar CheckIn.
- **Description:** Permitir modificar (PUT) y eliminar (DELETE) check-ins propios conforme a las reglas resueltas en [OQ-DOM-008](business-rules.md#oq-dom-008) y [OQ-DOM-009](business-rules.md#oq-dom-009): propiedad exclusiva del autor autenticado, concurrencia optimista mediante `revision`, ventana absoluta de 168 horas solo para la edición (la eliminación está permitida en cualquier momento), borrado físico en cascada con exclusión de los registros eliminados en Timeline y Trend, 200 con representación actualizada en edición, 204 en eliminación y respuestas uniformes 404/409.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-012
- **Name:** Personalización adicional de check-ins.
- **Description:** Permitir personalización adicional dentro de las Dimensions administradas por Yusay; no habilita dimensiones arbitrarias creadas por usuarios.
- **Priority:** COULD.
- **Status:** PROPOSED.

## Timeline

### RF-013
- **Name:** Timeline y filtros básicos.
- **Description:** Presentar eventos históricos principalmente de AssessmentResults y CheckIns en una Timeline derivada, aunque sus mediciones no sean comparables; permitir filtros básicos pendientes de concreción.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-014
- **Name:** Filtros avanzados.
- **Description:** Ampliar las posibilidades de filtrar el seguimiento; los criterios adicionales deben definirse.
- **Priority:** SHOULD.
- **Status:** PROPOSED.

### RF-015
- **Name:** Context tags.
- **Description:** Asociar etiquetas de contexto personal a registros, con vocabulario y reglas de asociación pendientes.
- **Priority:** SHOULD.
- **Status:** PROPOSED.

## Trends

### RF-016
- **Name:** Tendencias básicas.
- **Description:** Derivar tendencias de AssessmentResults o Measurements compatibles según DR-DOM-003: misma versión exacta, o compatibilidad explícita entre versiones del mismo concepto; UNKNOWN no es comparable. No atribuir causalidad al contexto.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-017
- **Name:** Comparación temporal.
- **Description:** Permitir comparación temporal adicional según reglas explícitas de compatibilidad; su presentación y alcance están abiertos.
- **Priority:** SHOULD.
- **Status:** PROPOSED.

## Guidance

### RF-018
- **Name:** Explicaciones y limitaciones.
- **Description:** Ofrecer Guidance como capacidad compuesta de Interpretation histórica, documentación/limitaciones, Topic y Resource, sin entidad independiente ni diagnóstico, tratamiento o recomendación clínica.
- **Priority:** MUST.
- **Status:** PROPOSED.

## Administration

### RF-019
- **Name:** Administrar definiciones de instrumentos.
- **Description:** Administrar Instrument, InstrumentVersion, Question, AnswerOption, scoring e interpretaciones, preservando la inmutabilidad de versiones publicadas.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-020
- **Name:** Publicar y retirar versiones.
- **Description:** Gestionar DRAFT ⇄ READY → PUBLISHED → RETIRED según DR-DOM-006: READY requiere validación de completitud y ejecutabilidad; máximo una PUBLISHED por Instrument para nuevos intentos. Publicar congela estructura, semántica y scoring; retirar impide nuevos intentos y conserva historia. Registrar operaciones relevantes para auditoría.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-021
- **Name:** Administrar Dimensions.
- **Description:** Permitir a Yusay configurar Dimensions y DimensionVersions sin modificar código, preservando la definición exacta usada por Measurements históricas; los permisos administrativos concretos están abiertos.
- **Priority:** MUST.
- **Status:** PROPOSED.

## Resources

### RF-022
- **Name:** Consultar recursos relacionados.
- **Description:** Mostrar contenido educativo relacionado mediante Topic: AssessmentResult → Interpretation → Topic → Resource, o Dimension → Topic → Resource. No sustituir Interpretation histórica por Resource actual.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-023
- **Name:** Administrar recursos.
- **Description:** Permitir administrar, publicar y retirar Resources de forma independiente sin alterar AssessmentResults históricos. Los detalles del ciclo editorial están pendientes.
- **Priority:** MUST.
- **Status:** PROPOSED.

## Export

### RF-024
- **Name:** Exportar historial.
- **Description:** Permitir exportar el historial propio; formato y contenido exportable quedan abiertos.
- **Priority:** SHOULD.
- **Status:** PROPOSED.

### RF-025
- **Name:** Compartir reportes temporalmente.
- **Description:** Permitir enlaces temporales para compartir reportes mediante acceso explícito y limitado; vigencia, revocación y datos incluidos quedan abiertos.
- **Priority:** COULD.
- **Status:** PROPOSED.

## Capacidades transversales del alcance

### RF-026
- **Name:** Recordatorios.
- **Description:** Permitir recordatorios para el seguimiento personal; canales, frecuencia y configuración quedan abiertos.
- **Priority:** SHOULD.
- **Status:** PROPOSED.

### RF-027
- **Name:** OAuth externo.
- **Description:** Permitir acceso mediante OAuth externo si se incorpora esta capacidad opcional; no selecciona la estrategia principal de autenticación.
- **Priority:** COULD.
- **Status:** PROPOSED.

### RF-028
- **Name:** Preferencias avanzadas.
- **Description:** Permitir preferencias adicionales del User; su contenido requiere definición.
- **Priority:** COULD.
- **Status:** PROPOSED.

## Open Questions

### OQ-PROD-008

- **ID:** OQ-PROD-008.
- **Pregunta:** ¿Qué funciones requieren correo verificado?
- **Motivo:** El alcance exige verificación, pero no establece sus efectos.
- **Impacto:** RF-001 y [Identity](../03-domain/bounded-contexts.md).
- **Status:** OPEN.

### OQ-PROD-009

- **ID:** OQ-PROD-009.
- **Pregunta:** ¿Qué campos y operaciones comprende el perfil?
- **Motivo:** Delimitar RF-004 aplicando minimización de datos.
- **Impacto:** RF-004 y [RNF-006](non-functional-requirements.md#rnf-006--data-minimization).
- **Status:** OPEN.

### OQ-PROD-010

- **ID:** OQ-PROD-010.
- **Pregunta:** ¿Qué acciones concretas ofrecerán los controles básicos de privacidad?
- **Motivo:** Convertir RF-004 en un requisito verificable sin asumir mecanismos.
- **Impacto:** RF-004, RNF-005 y [Privacy](business-rules.md#privacy).
- **Status:** OPEN.

### OQ-PROD-011

- **ID:** OQ-PROD-011.
- **Pregunta:** ¿Cuáles son los filtros básicos y cuáles los avanzados?
- **Motivo:** Distinguir MUST de SHOULD en el seguimiento.
- **Impacto:** RF-013 y RF-014.
- **Status:** OPEN.

### OQ-PROD-012

- **ID:** OQ-PROD-012.
- **Pregunta:** ¿Qué vocabulario y asociaciones admiten los context tags?
- **Motivo:** Concretar la capacidad SHOULD sin decidir la representación de Context.
- **Impacto:** RF-015 y [producto](product-definition.md#tracking).
- **Status:** OPEN.

### OQ-PROD-013

- **ID:** OQ-PROD-013.
- **Pregunta:** ¿Qué interacción adicional comprende la comparación temporal SHOULD?
- **Motivo:** Distinguirla de tendencias básicas MUST; la compatibilidad sigue siendo obligatoria.
- **Impacto:** RF-016, RF-017 y [RN-023](business-rules.md#comparison).
- **Status:** OPEN.

### OQ-PROD-014

- **ID:** OQ-PROD-014.
- **Pregunta:** ¿Qué canales, frecuencia y configuración tendrán los recordatorios?
- **Motivo:** Precisar RF-026 antes de evaluar infraestructura de notificaciones.
- **Impacto:** RF-026 y [decisiones pendientes](../06-decisions/README.md#pending-architectural-decisions).
- **Status:** OPEN.

### OQ-PROD-015

- **ID:** OQ-PROD-015.
- **Pregunta:** ¿Qué datos y formatos incluirá la exportación del historial?
- **Motivo:** Delimitar la capacidad SHOULD y su exposición de información privada.
- **Impacto:** RF-024 y RNF-005.
- **Status:** OPEN.

### OQ-PROD-016

- **ID:** OQ-PROD-016.
- **Pregunta:** ¿Qué personalización adicional de check-ins se ofrecería?
- **Motivo:** Acotar COULD sin habilitar Dimensions arbitrarias.
- **Impacto:** RF-012 y RN-021.
- **Status:** OPEN.

### OQ-PROD-017

- **ID:** OQ-PROD-017.
- **Pregunta:** ¿Qué preferencias avanzadas se ofrecerían?
- **Motivo:** La capacidad COULD está definida solo a nivel de alcance.
- **Impacto:** RF-028 y RF-004.
- **Status:** OPEN.

### OQ-PROD-018

- **ID:** OQ-PROD-018.
- **Pregunta:** ¿Qué proveedores y recorrido de acceso comprendería OAuth externo si se incorpora?
- **Motivo:** Precisar COULD sin seleccionar la estrategia principal de autenticación.
- **Impacto:** RF-027 y [Authentication strategy](../06-decisions/README.md#pending-architectural-decisions).
- **Status:** OPEN.

### Preguntas relacionadas

- [OQ-PROD-002](../01-discovery/target-users.md#oq-prod-002): ¿Qué información podrá consultar un Visitor?
- [OQ-PROD-005](product-definition.md#oq-prod-005): ¿Cómo se capturará el contexto personal mínimo?
- [OQ-DOM-003](business-rules.md#oq-dom-003): ¿Qué respuestas son obligatorias para enviar un intento?
- [OQ-DOM-013](business-rules.md#oq-dom-013): ¿Qué datos podrá incluir un reporte compartido opcional?
- [OQ-DOM-014](business-rules.md#oq-dom-014): ¿Qué vigencia y revocación tendrá un enlace temporal compartido?
