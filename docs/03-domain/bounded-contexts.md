# Bounded Contexts candidatos

**Estado: baseline conceptual; límites de contextos sujetos a refinamiento.** No se decide su arquitectura física.

- **Identity:** identidad, acceso, perfil y controles de privacidad.
- **Assessment:** definición histórica de InstrumentVersion (incluida Interpretation) y ejecución SINGLE_CHOICE, con AssessmentResult dependiente del límite de AssessmentAttempt.
- **Tracking:** CheckIn, Dimension como Aggregate Root, DimensionVersion interna, Measurement candidate, ContextTag y Note; Timeline y Trend como modelos derivados.
- **Content:** Resource con ciclo independiente y Topic como vocabulario controlado. Topic relaciona contenido con Instrument, Interpretation y Dimension.
- **Platform:** capacidades transversales de seguridad, auditoría y operación. Su condición de contexto de dominio o soporte transversal requiere análisis.

Assessment puede analizarse internamente como:

```text
Assessment
├── Definition
└── Execution
```

Esta distinción expresa responsabilidades conceptuales; no decide separar despliegues ni bases de datos. Guidance es capacidad compuesta que conecta Interpretation y Content mediante Topic, no un contexto o entidad nuevo. ContextTag y Topic mantienen responsabilidades distintas.

> Bounded Context no implica Microservice.

No se ha decidido la arquitectura física. Un Modular Monolith puede estudiarse posteriormente, sin considerarlo una decisión adoptada o irreversible.

## Open Questions

### OQ-DOM-017

- **ID:** OQ-DOM-017.
- **Pregunta:** ¿Qué conceptos y reglas pertenecen a cada contexto candidato?
- **Motivo:** Validar límites semánticos antes de asumir separación técnica.
- **Impacto:** Este documento y [dominio](domain-overview.md).
- **Status:** OPEN.

### OQ-DOM-018

- **ID:** OQ-DOM-018.
- **Pregunta:** ¿Qué contratos se necesitan entre Definition, Execution, Tracking y Content?
- **Motivo:** Precisar dependencias sin decidir servicios físicos.
- **Impacto:** Este documento y [Architecture](../04-architecture/README.md).
- **Status:** OPEN.

### OQ-DOM-019

- **ID:** OQ-DOM-019.
- **Pregunta:** ¿Qué responsabilidades de Context quedan por delimitar además de ContextTag y Note de Tracking?
- **Pregunta original:** ¿Dónde se ubican las responsabilidades de Guidance y Context?
- **Motivo:** DR-DOM-004 resuelve Guidance como capacidad compuesta; ContextTag y Note pertenecen a Tracking. La representación temporal más amplia de Context continúa abierta.
- **Impacto:** [Producto](../02-product/product-definition.md) y contextos candidatos.
- **Status:** OPEN.

### OQ-DOM-020

- **ID:** OQ-DOM-020.
- **Pregunta:** ¿Platform es un bounded context o un conjunto de capacidades transversales?
- **Motivo:** La lista inicial de candidatos no resuelve su naturaleza.
- **Impacto:** Este documento y RNF-003, RNF-007 y RNF-012.
- **Status:** OPEN.
