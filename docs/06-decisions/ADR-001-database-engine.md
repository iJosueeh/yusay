# ADR-001: Database Engine

## Status

**ACCEPTED**

- **Identificador:** ADR-001.
- **Estado anterior:** PROVISIONALLY ACCEPTED.
- **Decisión aprobada:** PostgreSQL como motor principal de base de datos relacional.
- **Fecha de aprobación:** 2026-10-07; identifica la aprobación formal, no la creación ni modificación del archivo.
- **Autoridad:** responsable del proyecto.
- **Alcance:** persistencia relacional principal del MVP de Yusay.

La selección provisional anterior se formaliza mediante autorización expresa del responsable del proyecto. No selecciona versión del motor, proveedor, acceso a datos ni diseño físico. La decisión es posterior a la congelación lógica: no reescribe el estado histórico de PostgreSQL en esa línea base.

## Context

Yusay requiere relacionar personas, instrumentos, versiones, preguntas, opciones, intentos, respuestas, resultados y registros de seguimiento. Se identifican numerosas invariantes de pertenencia, unicidad e historia, además de consultas temporales y agregaciones.

El [baseline conceptual v0.1](../03-domain/conceptual-model.md) permanece cerrado. El [modelo lógico v1.0](../05-data/logical-model-v1/12-dictamen-modelo-logico-v1.md) tiene dictamen **FAVORABLE** y estado **APPROVED / FROZEN**, con aprobación del responsable del proyecto el 2026-10-07. Su inventario verificado consta de **32 relaciones, 147 atributos, 32 PK, 9 AK, 6 unicidades adicionales y 41 FK, incluidas 13 compuestas**. Estos valores describen relaciones lógicas, no tablas físicas implementadas.

La estimación de carga verificada y el resto de la arquitectura tecnológica siguen pendientes. La evaluación es cualitativa y expresa valoraciones arquitectónicas; no se han realizado benchmarks comparativos de las alternativas en este repositorio.

La selección debe preservar pertenencia histórica entre definiciones versionadas, unicidad condicionada por estado, transacciones atómicas, concurrencia de evaluaciones/publicaciones/CheckIns, supresión personal y desvinculación de auditoría, retenciones y restauración sin reintroducir información suprimida. Definiciones históricas, datos privados y contenido editorial conservan su separación. Este ADR se adapta a esas políticas, sin modificarlas.

## Decision Drivers

- Dominio predominantemente relacional e invariantes fuertes.
- Integridad referencial, constraints y consistencia transaccional.
- Preservación de definiciones y registros históricos.
- Consultas temporales y agregaciones para seguimiento.
- Posibilidad de expresar integridad también en la base de datos.
- Ecosistema amplio y complejidad operativa proporcionada al producto.
- Ausencia actual de requisitos que justifiquen complejidad o licenciamiento enterprise.

## Options Considered

Las alternativas siguientes se comparan cualitativamente, sin puntuaciones o benchmarks empíricos. Costes totales incluyen operación, soporte, infraestructura y condiciones de licencia/edición; no se fijan importes ni proveedor.

- **PostgreSQL — seleccionado:** alta correspondencia con el modelo relacional, PK/FK compuestas/UNIQUE/CHECK, índices únicos parciales, transacciones y concurrencia. Admite metadata semiestructurada mediante capacidades como JSONB sin abandonar integridad relacional. Motor de código abierto bajo [PostgreSQL License](https://www.postgresql.org/about/licence/); esto no elimina costes de operación. Ofrece el ajuste identificado para las reglas aprobadas sin justificar licenciamiento enterprise adicional.
- **MySQL con InnoDB — alternativa viable, no seleccionada:** conserva su condición de principal alternativa relacional evaluada. InnoDB ofrece [transacciones ACID y claves foráneas](https://dev.mysql.com/doc/refman/8.4/en/innodb-introduction.html); no se descarta por carecer de ellas. La representación de metadata y el cumplimiento de unicidades condicionadas requieren evaluar mecanismos concretos; este ADR no diseña emulaciones ni elige tipos. PostgreSQL se prefiere por la correspondencia directa entre sus índices únicos parciales y los máximos condicionados documentados. Licencia, edición, soporte y operación deben evaluarse según distribución/servicio.
- **Microsoft SQL Server — alternativa relacional viable, no seleccionada:** ofrece integridad declarativa, transacciones y concurrencia, además de índices únicos filtrados y capacidades para JSON. Sus [índices filtrados](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-filtered-indexes) son relevantes para la unicidad condicionada, por lo que esta capacidad no se presenta como exclusiva de PostgreSQL. No se identifica una ventaja del dominio que justifique preferirlo; edición, licenciamiento y operación requieren evaluación específica, sin asumir costes iguales para todas las ediciones.
- **MongoDB — no seleccionado para persistencia principal:** conserva el argumento previo: relaciones y pertenencia histórica fuertes favorecen el modelo relacional aprobado. Soporta [transacciones multidocumento](https://www.mongodb.com/docs/manual/core/transactions/), validación de documentos y almacenamiento semiestructurado; no se descarta por falta de atomicidad general. La validación de documentos no equivale a las FKs simples/compuestas del modelo relacional; reproducir sus invariantes requeriría evaluar diseño y coordinación adicionales. Costes, licencia, despliegue y soporte dependen de la alternativa operativa concreta.
- **Oracle Database — alternativa histórica preservada, no seleccionada:** técnicamente capaz, pero no presenta ventajas identificadas suficientes para justificar complejidad y posibles costes adicionales. Se conserva el argumento previo; la comparación solicitada de los cuatro motores anteriores no borra esta alternativa del historial.

## Decision

Seleccionar **PostgreSQL** como motor principal de base de datos relacional para la persistencia principal del MVP de Yusay. **ADR-001: ACCEPTED**, por aprobación formal del responsable del proyecto el 2026-10-07.

> PostgreSQL no se selecciona porque sea universalmente superior a las demás alternativas, sino porque presenta actualmente el mejor ajuste identificado para las características del dominio de Yusay.

## Rationale

El dominio favorece integridad referencial, transacciones y constraints para impedir estados estructuralmente inválidos. PostgreSQL ofrece un ajuste adecuado para estos requisitos y para el versionado histórico, las consultas temporales y las agregaciones previstas.

- **Integridad relacional:** [PK, FK simples/compuestas, UNIQUE y CHECK](https://www.postgresql.org/docs/current/ddl-constraints.html) permiten materializar gran parte de las restricciones aprobadas. No sustituyen autorización, completitud entre filas o validación metodológica.
- **Unicidad condicional:** [índices únicos parciales](https://www.postgresql.org/docs/current/indexes-partial.html) permiten expresar reglas como máximo un Attempt IN_PROGRESS por usuario/instrumento o una versión PUBLISHED por instrumento. Son capacidades justificables por reglas existentes, no índices físicos ya seleccionados.
- **Concurrencia:** transacciones ACID, [aislamiento y control de concurrencia](https://www.postgresql.org/docs/current/transaction-iso.html) hacen viable coordinar operaciones compuestas. Nivel de aislamiento, bloqueos y tratamiento técnico de conflictos siguen diferidos.
- **Versionado:** claves y referencias pueden preservar la definición histórica exacta; el motor no congela por sí mismo versiones ni comprueba automáticamente sus políticas de publicación.
- **Auditoría:** metadata estructurada puede representarse con mecanismos como [JSONB](https://www.postgresql.org/docs/current/datatype-json.html), bajo catálogos, perfiles y restricciones de privacidad aprobados. JSONB sigue siendo una posibilidad; no se selecciona como tipo físico de AUDIT_EVENT.metadata.
- **Operación:** motor relacional de código abierto con ecosistema maduro y alternativas de despliegue, sin comprometer proveedor. Selección del motor no equivale a dimensionamiento, seguridad o recuperación ya validados.

Partial indexes, range types y exclusion constraints son capacidades potencialmente útiles para restricciones condicionadas o temporales, si el modelo posterior justifica su uso. JSONB ofrece flexibilidad para información que lo requiera; no implica almacenar como documentos todo el dominio ni evitar relaciones estructuradas.

Full-text search se considera potencialmente suficiente inicialmente, sin cerrar la Search strategy ni incorporar un motor de búsqueda adicional. El ecosistema amplio y la ausencia actual de necesidades enterprise favorecen esta selección definitiva del motor para el MVP.

No se decide usar todas estas capacidades. Su necesidad concreta se evaluará durante una fase independiente de diseño físico; el interés en una característica no sustituye una regla de dominio. Las capacidades del motor hacen viable ese diseño, pero no garantizan automáticamente todas las invariantes ni sustituyen diseño transaccional, validación de aplicación y pruebas técnicas.

## Consequences

### Favorables

- Permite analizar restricciones persistentes junto con validación de aplicación.
- Favorece consultas relacionales, temporales y de agregación sobre registros coherentes.
- Proporciona un motor seleccionado con alta correspondencia con el modelo lógico congelado, integridad relacional e índices condicionados.
- Permite evaluar opciones de despliegue sin comprometer todavía un proveedor.
- Dispone de mecanismos de concurrencia y recuperación que requieren diseño y validación.

### Negativas y responsabilidades

- Dependencia potencial de características PostgreSQL-specific y menor portabilidad.
- Necesidad de conocimientos SQL/PostgreSQL.
- No depender exclusivamente del ORM: las restricciones y transacciones deben comprenderse y verificarse en la base de datos.
- Una migración futura a otro motor podría ser costosa, especialmente si se adoptan capacidades específicas.
- Deben definirse operación, mantenimiento y migraciones sin asumir que la elección del motor resuelve esos aspectos.
- Diseñar cuidadosamente índices, restricciones y transacciones; evitar trasladar toda la lógica de negocio a triggers o procedimientos almacenados.
- Verificar invariantes que no puedan expresarse con restricciones declarativas simples, como completitud de respuestas, cobertura de interpretaciones y mínimos de hijos.
- Diseñar supresión, retención y restauración con las políticas ya aprobadas; conservar la excepción exclusiva de supresión de cuenta de DP-TRANS-001.
- Validar concurrencia real y rendimiento sobre consultas/cargas representativas; definir seguridad, backups y recuperación en etapas posteriores.

## Approval Limits

Quedan diferidos: versión concreta de PostgreSQL, hosting/servicio administrado, tipos físicos (UUID, BIGINT, VARCHAR, TEXT, TIMESTAMPTZ, JSONB, etc.), longitudes/colaciones/email canónico físico, índices específicos/planes, restricciones físicas y enforcement, aislamiento/bloqueos, migraciones, ORM/acceso a datos, backend/frontend, infraestructura/despliegue/observabilidad, hashing/sesiones y mecanismos de limpieza/retención/backups/restauración. Ejemplos técnicos no son selecciones definitivas.

La línea base lógica v1.0 permanece **FAVORABLE / APPROVED / FROZEN**, con aprobación 2026-10-07. No se modifica relación, atributo, dominio, clave, cardinalidad, restricción, estado, transición, concurrencia, privacidad, auditoría ni resolución AJ/VF/REV-LOG/DP-TRANS. Sus menciones de PostgreSQL provisional describen correctamente el estado anterior a esta decisión posterior.

Este ADR no inicia automáticamente diseño físico o implementación ni genera SQL, migraciones, APIs o entidades de framework.

## Open Questions

### OQ-ARCH-002

- **ID:** OQ-ARCH-002.
- **Pregunta:** ¿El modelo conceptual y lógico confirma el ajuste relacional previsto?
- **Motivo:** Contrastar el ajuste relacional previsto con el modelo consolidado.
- **Impacto:** Este ADR y [Data](../05-data/README.md).
- **Status:** RESOLVED.
- **Resolución:** el modelo lógico v1.0 aprobado y congelado confirma el ajuste relacional documentado; el responsable del proyecto aprueba PostgreSQL. No constituye prueba de rendimiento o validación de implementación.

### OQ-ARCH-003

- **ID:** OQ-ARCH-003.
- **Pregunta:** ¿Qué constraints e índices se justifican por reglas concretas?
- **Motivo:** No convertir capacidades del motor en diseño físico prematuro.
- **Impacto:** Este ADR, [RN](../02-product/business-rules.md) y futuro modelo físico.
- **Status:** OPEN.

### OQ-ARCH-004

- **ID:** OQ-ARCH-004.
- **Pregunta:** ¿Qué casos requieren range types, exclusion constraints o JSONB?
- **Motivo:** Evaluar necesidad y coste de portabilidad antes de adoptar capacidades específicas.
- **Impacto:** Este ADR y futura Persistence strategy.
- **Status:** OPEN.

### OQ-ARCH-005

- **ID:** OQ-ARCH-005.
- **Pregunta:** ¿Qué evidencia justificaría revisar PostgreSQL frente a MySQL u otra alternativa?
- **Motivo:** Mantener trazabilidad si apareciera evidencia que justificase una propuesta futura de cambio de motor; ACCEPTED no significa que la decisión siga provisional.
- **Alcance:** una revisión futura debe documentar evidencia e impacto y obtener aprobación expresa; no autoriza cambiar esta selección ni la línea base congelada.
- **Impacto:** Este ADR y registro de decisiones.
- **Status:** OPEN.

### OQ-ARCH-006

- **ID:** OQ-ARCH-006.
- **Pregunta:** ¿Qué versión de PostgreSQL se evaluará cuando corresponda concretar su adopción?
- **Motivo:** La selección del motor no fija versión ni ciclo operativo.
- **Impacto:** Este ADR y Hosting/deployment.
- **Status:** OPEN.

### Preguntas relacionadas

- [OQ-NFR-001](../02-product/non-functional-requirements.md#oq-nfr-001): ¿Qué latencias, percentiles, carga, volumen y entorno se usarán para evaluar rendimiento?
- [OQ-ARCH-011](README.md#oq-arch-011): ¿Qué Hosting/deployment responde a la carga y restricciones operativas?
- [OQ-ARCH-009](README.md#oq-arch-009): ¿Qué Persistence strategy preservará integridad y consistencia?
- [OQ-ARCH-013](README.md#oq-arch-013): ¿Qué alcance de búsqueda debe cubrir Search strategy?
