# Requisitos no funcionales

Baseline documental: todos los registros tienen **Priority: MUST** y **Status: PROPOSED**. `ACCEPTED` y `DEFERRED` se reservan para aprobación o aplazamiento explícitos. Los criterios pendientes no deben interpretarse como garantías operativas.

## RNF-001 — Integridad persistente

**Description:** La base de datos deberá impedir, siempre que sea razonablemente expresable mediante mecanismos relacionales, la persistencia de estados estructuralmente inválidos.

La validación realizada por la aplicación no sustituye las restricciones de integridad de la base de datos. Ambas capas cumplen responsabilidades diferentes. Las restricciones concretas se diseñarán después del modelo conceptual y lógico.

## RNF-002 — Consistencia transaccional

**Description:** Las operaciones que deban preservar invariantes conjuntamente deben mantener consistencia transaccional, incluso ante fallos o concurrencia. Scoring, resolución de Interpretation cuando corresponda, creación de AssessmentResult y transición a SUBMITTED forman una operación conceptual atómica según [DR-DOM-005](../03-domain/conceptual-model.md#dr-dom-005). SUBMITTED implica exactamente un resultado oficial; los otros estados no lo tienen. Los mecanismos y demás límites deben analizarse sin diseñar aún restricciones relacionales.

## RNF-003 — Seguridad

**Description:** Proteger credenciales, comunicaciones y datos privados frente a acceso o modificación indebidos. Los mecanismos y criterios de verificación se definirán con la estrategia de seguridad; no se selecciona un protocolo de autenticación.

## RNF-004 — Autorización server-side

**Description:** Comprobar permisos en el servidor para cada operación y acceso a datos privados. La interfaz no constituye una barrera de autorización; Administrator no dispone de acceso privado implícito.

## RNF-005 — Privacidad

**Description:** Limitar el acceso y la exposición de respuestas, resultados, check-ins y contexto a su finalidad y permisos explícitos. Definir controles y políticas de retención y eliminación antes de implementar.

## RNF-006 — Data minimization

**Description:** Recoger y exponer solo los datos necesarios para el propósito declarado. Los campos personales, de contexto y de auditoría deben justificar su necesidad; no presuponer información clínica.

## RNF-007 — Trazabilidad

**Description:** Mantener AssessmentAttempt ligado a InstrumentVersion exacta y Measurement a DimensionVersion exacta; preservar significado histórico e Interpretation frente a evolución de Resources. Answers, InstrumentVersion exacta y ScoringDefinition histórica deben permitir reproducir/auditar el resultado; consultar no lo recalcula. Hacer auditables operaciones administrativas relevantes. La auditoría debe permitir atribuir la operación sin exponer innecesariamente contenido privado.

## RNF-008 — Migraciones versionadas

**Description:** Cuando exista un esquema físico, gestionar su evolución mediante migraciones versionadas y revisables, preservando integridad e historia. Este requisito no autoriza crear migraciones en la etapa actual.

## RNF-009 — Rendimiento

**Description:** Favorecer interacción fluida en catálogo, guardado de progreso, envío, historial y seguimiento. Los objetivos actuales son iniciales y no un SLA contractual.

Los presupuestos numéricos de latencia, percentiles, carga, volumen y entorno de medición están pendientes en [OQ-NFR-001](#oq-nfr-001). El requisito mantiene Status: PROPOSED; la pregunta mantiene Status: OPEN. No se inventan umbrales sin una base de uso.

## RNF-010 — Accesibilidad

**Description:** Permitir comprender y operar formularios, resultados y visualizaciones con alternativas accesibles, navegación por teclado e información que no dependa únicamente del color. El estándar, nivel de conformidad y criterios de aceptación están abiertos.

## RNF-011 — Responsive design

**Description:** Ofrecer el MVP web en distintos tamaños de pantalla, manteniendo legibilidad y acceso a las funciones principales. Los dispositivos y navegadores de referencia están abiertos.

## RNF-012 — Observabilidad

**Description:** Permitir observar fallos y comportamiento operativo sin registrar innecesariamente respuestas, resultados o contexto privado. Métricas, registros, alertas y stack están pendientes.

## Open Questions

### OQ-NFR-001

- **ID:** OQ-NFR-001.
- **Pregunta:** ¿Qué latencias, percentiles, carga, volumen y entorno se usarán para evaluar rendimiento?
- **Motivo:** Hacer medible el objetivo inicial sin inventar un SLA.
- **Impacto:** RNF-009 y [ADR-001](../06-decisions/ADR-001-database-engine.md).
- **Status:** OPEN.

### OQ-NFR-002

- **ID:** OQ-NFR-002.
- **Pregunta:** ¿Qué estándar y nivel de accesibilidad se adoptarán?
- **Motivo:** Definir criterios verificables sin presumir una norma seleccionada.
- **Impacto:** RNF-010 y frontend pendiente.
- **Status:** OPEN.

### OQ-NFR-003

- **ID:** OQ-NFR-003.
- **Pregunta:** ¿Qué dispositivos y navegadores compondrán la matriz responsive?
- **Motivo:** Verificar Web First con un alcance concreto.
- **Impacto:** RNF-011 y [LIM-006](limitations.md#lim-006--web-first).
- **Status:** OPEN.

### OQ-NFR-004

- **ID:** OQ-NFR-004.
- **Pregunta:** ¿Qué eventos administrativos deben auditarse y durante cuánto tiempo?
- **Motivo:** Concretar trazabilidad con minimización de datos.
- **Impacto:** RNF-007, RN-027 y [Platform](../03-domain/bounded-contexts.md).
- **Status:** OPEN.

### OQ-NFR-005

- **ID:** OQ-NFR-005.
- **Pregunta:** ¿Qué señales operativas se observarán sin exponer información privada?
- **Motivo:** Separar necesidades de observabilidad de la selección de stack.
- **Impacto:** RNF-012 y [Observability stack](../06-decisions/README.md#pending-architectural-decisions).
- **Status:** OPEN.

### Preguntas relacionadas

- [OQ-DOM-012](business-rules.md#oq-dom-012): ¿Qué política concilia retención histórica y eliminación de datos personales?
- [OQ-DOM-026](../03-domain/aggregate-candidates.md#oq-dom-026): ¿Qué operaciones requieren consistencia atómica y cuáles admiten información derivada?
