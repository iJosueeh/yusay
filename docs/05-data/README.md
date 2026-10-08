# Data

**Estado vigente: modelo lógico v1.0 FAVORABLE / APPROVED / FROZEN; diseño físico v1.0 IN PROGRESS.**

El [modelo conceptual v0.1](../03-domain/conceptual-model.md) permanece CLOSED. El [modelo lógico v1.0](logical-model-v1/12-dictamen-modelo-logico-v1.md) está formalmente aprobado y congelado; la [fase de diseño físico v1.0](physical-model-v1/00-indice.md) está IN PROGRESS: preparación documental iniciada, sin esquema implementado ni aprobación del diseño físico.

[ADR-001](../06-decisions/ADR-001-database-engine.md) está ACCEPTED: PostgreSQL es el motor principal del MVP por aprobación del responsable del proyecto el 2026-10-07. No define esquema físico ni estrategia de acceso a datos. Las menciones provisionales de la carpeta lógica congelada reflejan el estado al aprobar esa línea base.

## Base para el trabajo posterior

- [Lenguaje ubicuo](../03-domain/ubiquitous-language.md): conceptos y distinciones.
- [Visión del dominio](../03-domain/domain-overview.md): definición, ejecución y vínculo histórico.
- [Aggregate candidates](../03-domain/aggregate-candidates.md): clasificaciones del baseline, con candidates explícitos; no entidades de persistencia.
- [Reglas de negocio](../02-product/business-rules.md): invariantes y preguntas abiertas.
- [Requisitos no funcionales](../02-product/non-functional-requirements.md): integridad, transacciones y privacidad.

## Criterios que orientarán el modelado

Separar Instrument de InstrumentVersion, Dimension de DimensionVersion y AssessmentAttempt de CheckIn. Mantener registros vinculados a sus definiciones exactas. No convertir automáticamente Timeline y Trend en entidades persistentes independientes. Justificar cada relación y restricción a partir del dominio.

## Open Questions

**Antecedente de la etapa previa:** se conservan los registros siguientes con sus estados históricos; no reabren decisiones de la línea base lógica ya congelada. Su correspondencia posterior se documenta en la [matriz](logical-model-v1/10-matriz-trazabilidad.md) y las [resoluciones](logical-model-v1/11-pendientes-y-riesgos.md). El inicio físico organiza trabajo posterior y no resuelve nuevamente preguntas de dominio.

### OQ-DATA-001

- **ID:** OQ-DATA-001.
- **Pregunta:** ¿Qué identidades y cardinalidades de los conceptos deben explicitarse al iniciar el modelo conceptual?
- **Motivo:** Distinguir invariantes conocidas de asociaciones todavía candidatas.
- **Impacto:** [Visión del dominio](../03-domain/domain-overview.md), [aggregate candidates](../03-domain/aggregate-candidates.md) y futuro modelo conceptual.
- **Status:** RESOLVED.
- **Resolución:** Las identidades conceptuales y cardinalidades del baseline v0.1 están documentadas; esto no define identificadores de persistencia. Véanse [DR-DOM-001 a DR-DOM-004 y relaciones](../03-domain/conceptual-model.md#relaciones-y-cardinalidades).

### OQ-DATA-002

- **ID:** OQ-DATA-002.
- **Pregunta:** ¿Qué semántica temporal requieren los registros y el contexto?
- **Motivo:** Precisar periodos, fechas y orden temporal antes de modelar datos; no seleccionar tipos SQL.
- **Impacto:** [Tracking](../02-product/product-definition.md#tracking), Timeline, Trend y futuro modelo conceptual.
- **Status:** OPEN.

### OQ-DATA-003

- **ID:** OQ-DATA-003.
- **Pregunta:** ¿Cómo se representará relacionalmente la compatibilidad entre InstrumentVersions y DimensionVersions?
- **Motivo:** DR-DOM-003 fija la política, no su representación persistente.
- **Etapa:** Logical Data Model.
- **Documentos afectados:** [Modelo conceptual](../03-domain/conceptual-model.md), [reglas de negocio](../02-product/business-rules.md); DR-DOM-003, RN-023 y este documento.
- **Status:** OPEN.

### OQ-DATA-004

- **ID:** OQ-DATA-004.
- **Pregunta:** ¿Cómo garantizará el modelo relacional que una Answer solo referencie una Question de la InstrumentVersion exacta de AssessmentAttempt?
- **Motivo:** La invariante de DR-DOM-001 debe protegerse sin anticipar su diseño relacional.
- **Etapa:** Logical Data Model.
- **Documentos afectados:** [Modelo conceptual](../03-domain/conceptual-model.md), [reglas de negocio](../02-product/business-rules.md); DR-DOM-001, RN-008, RN-012 y este documento.
- **Status:** OPEN.

### OQ-DATA-005

- **ID:** OQ-DATA-005.
- **Pregunta:** ¿Cómo garantizará el modelo relacional que la AnswerOption seleccionada pertenezca a la Question respondida?
- **Motivo:** La pertenencia está aceptada, pero su representación relacional sigue pendiente.
- **Etapa:** Logical Data Model.
- **Documentos afectados:** [Modelo conceptual](../03-domain/conceptual-model.md), [reglas de negocio](../02-product/business-rules.md); DR-DOM-001, RN-013 y este documento.
- **Status:** OPEN.

### OQ-DATA-006

- **ID:** OQ-DATA-006.
- **Pregunta:** ¿Cómo representará el modelo relacional DimensionVersion y una sola Measurement por Dimension dentro de un CheckIn?
- **Motivo:** DR-DOM-002 preserva la definición exacta y el baseline prohíbe repetir Dimension; no se ha diseñado su representación.
- **Etapa:** Logical Data Model.
- **Documentos afectados:** [Modelo conceptual](../03-domain/conceptual-model.md), [reglas de negocio](../02-product/business-rules.md); DR-DOM-002, RN-020, RN-030, RN-031 y este documento.
- **Status:** OPEN.

### OQ-DATA-007

- **ID:** OQ-DATA-007.
- **Pregunta:** ¿Cómo se representarán relacionalmente ScoringDefinition histórica y sus contribuciones SUM normales/invertidas por Question + AnswerOption?
- **Motivo:** DR-DOM-005 define scoring y reproducibilidad conceptual, sin elegir su representación ni confirmar ScoringRule o ScoringMapping como Entities.
- **Etapa:** Logical Data Model.
- **Documentos afectados:** [DR-DOM-005](../03-domain/conceptual-model.md#dr-dom-005), [reglas de negocio](../02-product/business-rules.md) y este documento.
- **Status:** OPEN.

### Preguntas relacionadas

- [OQ-DOM-021](../03-domain/aggregate-candidates.md#oq-dom-021): ¿Dónde pertenece AssessmentResult y qué invariantes determinan su ciclo de vida?
- [OQ-DOM-026](../03-domain/aggregate-candidates.md#oq-dom-026): ¿Qué operaciones requieren consistencia atómica y cuáles admiten información derivada?
- [OQ-DOM-010](../02-product/business-rules.md#oq-dom-010): ¿Cómo se conserva la definición histórica de una Dimension cuando cambian sus reglas?
- [OQ-DOM-011](../02-product/business-rules.md#oq-dom-011): ¿Qué criterios definen compatibilidad entre mediciones, instrumentos y versiones?
- [OQ-DOM-012](../02-product/business-rules.md#oq-dom-012): ¿Qué política concilia retención histórica y eliminación de datos personales?
