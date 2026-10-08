# Architecture

**Status: IN PROGRESS (Stack consolidado en ADR-002)**

La arquitectura tecnológica de Yusay se formaliza en [ADR-001](../06-decisions/ADR-001-database-engine.md) (PostgreSQL 18) y [ADR-002](../06-decisions/ADR-002-technology-stack.md) (Next.js en frontend, ASP.NET Core .NET 10 LTS monolito modular en backend, Dapper + Npgsql en persistencia, Flyway en migraciones, xUnit + Testcontainers en testing y Docker Compose en desarrollo local). Esta selección preserva intacta la línea base lógica v1.0 y el diseño físico v1.0. Los [bounded contexts](../03-domain/bounded-contexts.md) se estructuran como módulos desacoplados dentro del monolito modular.

## Principios técnicos

### Persistent Integrity

> The database protects persistent structural integrity.

La integridad estructural expresable mediante mecanismos relacionales debe protegerse en la base de datos. La validación de aplicación complementa esa responsabilidad, no la sustituye.

### Application Responsibility

> The application implements business behavior, authorization and use-case orchestration.

La aplicación coordina casos de uso, comportamiento y autorización server-side. No toda regla de negocio se expresa como una restricción relacional.

### Historical Reproducibility

> Historical records must remain interpretable according to the exact definition under which they were generated.

Las versiones exactas deben permitir entender registros históricos sin depender de la publicación actual.

### No Accidental Complexity

> New infrastructure or technologies are introduced only when they solve a concrete requirement.

No se incorporan componentes para completar un diagrama sin una necesidad demostrada.

### Privacy by Design

> Yusay should collect and expose only the information necessary for its declared purpose.

La privacidad condiciona datos, permisos y observabilidad desde el diseño.

### No Premature Implementation

> Domain and data decisions precede framework-specific entity modeling.

No se crean entidades de framework, endpoints ni infraestructura durante esta etapa.

## Open Questions

### OQ-ARCH-001

- **ID:** OQ-ARCH-001.
- **Pregunta:** ¿Qué criterios del dominio y requisitos no funcionales deben guiar la arquitectura física?
- **Motivo:** Justificar alternativas sin convertir bounded contexts en microservicios.
- **Impacto:** [Bounded contexts](../03-domain/bounded-contexts.md), [RNF](../02-product/non-functional-requirements.md) y decisiones pendientes.
- **Status:** OPEN.

### Preguntas relacionadas

- [OQ-ARCH-007](../06-decisions/README.md#oq-arch-007): ¿Qué criterios se usarán para evaluar Backend technology?
- [OQ-ARCH-008](../06-decisions/README.md#oq-arch-008): ¿Qué criterios se usarán para evaluar Frontend technology?
- [OQ-ARCH-009](../06-decisions/README.md#oq-arch-009): ¿Qué Persistence strategy preservará integridad y consistencia?
- [OQ-ARCH-010](../06-decisions/README.md#oq-arch-010): ¿Qué Authentication strategy cumplirá las necesidades de identidad y seguridad?
- [OQ-DOM-026](../03-domain/aggregate-candidates.md#oq-dom-026): ¿Qué operaciones requieren consistencia atómica y cuáles admiten información derivada?
