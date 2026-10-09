# Architecture Decision Records

Los ADR documentan contexto, alternativas, decisión, motivos y consecuencias. Una decisión provisional puede revisarse cuando aparezca evidencia nueva; una pregunta abierta no constituye una selección implícita.

## Registro

- [ADR-001: Database Engine](ADR-001-database-engine.md) — **ACCEPTED**: PostgreSQL como motor principal del MVP; aprobación formal 2026-10-07 por el responsable del proyecto. Estado anterior: PROVISIONALLY ACCEPTED.
- [ADR-002: Technology Stack](ADR-002-technology-stack.md) — **ACCEPTED**: Stack tecnológico consolidado para el MVP (Next.js + React en frontend, ASP.NET Core .NET 10 LTS monolito modular en backend, Dapper + Npgsql en persistencia sobre PostgreSQL 18, Flyway para migraciones, xUnit + Testcontainers para testing y Docker Compose para desarrollo local); aprobación formal 2026-10-08 por el responsable del proyecto.

La línea base lógica v1.0 permanece FAVORABLE / APPROVED / FROZEN y el diseño físico v1.0 APROBADO CON CONDICIONES DE IMPLEMENTACIÓN. Estas decisiones tecnológicas se adaptan a dichos modelos sin modificarlos.

## Decisiones de dominio

[DR-DOM-001 a DR-DOM-007](../03-domain/conceptual-model.md#decisiones-de-dominio) están ACCEPTED FOR MVP. Son decisiones conceptuales, no selecciones de tecnología.

## Pending Architectural Decisions

Decisiones resueltas por ADR-001 y ADR-002:
- **Database Engine:** PostgreSQL 18 ([ADR-001](ADR-001-database-engine.md)).
- **Backend technology:** ASP.NET Core (.NET 10 LTS), monolito modular ([ADR-002](ADR-002-technology-stack.md)).
- **Frontend technology:** Next.js (React 19, TypeScript) ([ADR-002](ADR-002-technology-stack.md)).
- **Persistence strategy:** Dapper + Npgsql ([ADR-002](ADR-002-technology-stack.md)).
- **Migrations & Testing:** Flyway + xUnit + Testcontainers ([ADR-002](ADR-002-technology-stack.md)).
- **Authentication strategy:** JWT + Argon2id consolidado en backend ([ADR-002](ADR-002-technology-stack.md); [OQ-ARCH-010](#oq-arch-010)). OAuth externo permanece como capacidad COULD abierta (OQ-PROD-018).

Decisiones que mantienen **Status: OPEN**:
- **Authorization & permissions:** propiedad de datos privados, habilitación ADMINISTRATOR y respuestas 401/403/503 ([OQ-ARCH-017](#oq-arch-017)).
- **Hosting/deployment:** definir entorno de producción y proveedor cloud según restricciones operativas.
- **Caching strategy:** justificar solo si responde a necesidades concretas de rendimiento (salvo denylist de revocación selectiva de sesión en backend).
- **Search strategy:** definir necesidades de búsqueda antes de decidir mecanismos o infraestructura.
- **Notification infrastructure:** evaluar conforme se concreten recordatorios y canales.
- **Observability stack:** definir señales necesarias y protección de datos antes de elegir herramientas.

## Open Questions

### OQ-ARCH-007

- **ID:** OQ-ARCH-007.
- **Pregunta:** ¿Qué criterios se usarán para evaluar Backend technology?
- **Status:** RESOLVED via [ADR-002](ADR-002-technology-stack.md) (ASP.NET Core .NET 10 LTS, monolito modular).

### OQ-ARCH-008

- **ID:** OQ-ARCH-008.
- **Pregunta:** ¿Qué criterios se usarán para evaluar Frontend technology?
- **Status:** RESOLVED via [ADR-002](ADR-002-technology-stack.md) (Next.js con SSR/SSG para contenido público y SPA reactiva para aplicación privada).

### OQ-ARCH-009

- **ID:** OQ-ARCH-009.
- **Pregunta:** ¿Qué Persistence strategy preservará integridad y consistencia?
- **Status:** RESOLVED via [ADR-002](ADR-002-technology-stack.md) (Dapper + Npgsql sobre PostgreSQL 18, respetando el DDL normativo sin ORM invasivo).

### OQ-ARCH-010

- **ID:** OQ-ARCH-010.
- **Pregunta:** ¿Qué Authentication strategy cumplirá las necesidades de identidad y seguridad?
- **Motivo:** Precisar sesión, verificación y recuperación sin asumir JWT u OAuth principal.
- **Impacto:** RF-001 a RF-003 y RNF-003. La autorización server-side (RNF-004) queda asignada a OQ-ARCH-017.
- **Status:** RESOLVED via [ADR-002](ADR-002-technology-stack.md) (JWT + Argon2id, secretos CSPRNG y revocación MP-PHYS-015 con denylist Redis), con la estrategia implementada y verificada en Yusay.Api. OAuth externo permanece como capacidad COULD abierta en OQ-PROD-018. El alcance de autorización pendiente (RNF-004) se traslada a OQ-ARCH-017.

### OQ-ARCH-011

- **ID:** OQ-ARCH-011.
- **Pregunta:** ¿Qué Hosting/deployment responde a la carga y restricciones operativas?
- **Motivo:** Seleccionar entorno solo cuando existan necesidades verificables.
- **Impacto:** RNF-009, ADR-001 y Architecture.
- **Status:** OPEN.

### OQ-ARCH-012

- **ID:** OQ-ARCH-012.
- **Pregunta:** ¿Existe un requisito concreto que justifique Caching strategy?
- **Motivo:** Evitar infraestructura añadida sin una necesidad de rendimiento.
- **Impacto:** RNF-009 y Architecture.
- **Status:** OPEN.

### OQ-ARCH-013

- **ID:** OQ-ARCH-013.
- **Pregunta:** ¿Qué alcance de búsqueda debe cubrir Search strategy?
- **Motivo:** Comprobar suficiencia inicial de full-text search sin darla por decidida.
- **Impacto:** RF-005, RF-022 y ADR-001.
- **Status:** OPEN.

### OQ-ARCH-014

- **ID:** OQ-ARCH-014.
- **Pregunta:** ¿Qué Notification infrastructure requieren los recordatorios definidos?
- **Motivo:** Evaluar canales y operación una vez acotada la capacidad SHOULD.
- **Impacto:** RF-026 y Architecture.
- **Status:** OPEN.

### OQ-ARCH-015

- **ID:** OQ-ARCH-015.
- **Pregunta:** ¿Qué Observability stack permitirá observar las señales necesarias?
- **Motivo:** Elegir herramientas después de identificar señales y restricciones de privacidad.
- **Impacto:** RNF-012 y Architecture.
- **Status:** OPEN.

### OQ-ARCH-016

- **ID:** OQ-ARCH-016.
- **Pregunta:** ¿Qué capacidades del equipo y restricciones operativas condicionan las alternativas?
- **Motivo:** Evaluar viabilidad y costes sin presumir un entorno o experiencia técnica.
- **Impacto:** Todas las decisiones pendientes y ADR-001.
- **Status:** OPEN.

### OQ-ARCH-017

- **ID:** OQ-ARCH-017.
- **Pregunta:** ¿Qué Authorization strategy aplicará el backend a los recursos de Yusay?
- **Motivo:** Las reglas sustantivas están aprobadas (propiedad de datos privados, ACTIVE y correo verificado, alcance limitado de la habilitación ADMINISTRATOR), pero faltan por decidir el mecanismo de aplicación server-side y las respuestas de denegación.
- **Impacto:** RNF-004, RNF-005, RN-025, RN-026 y RF-019 a RF-023; depende de OQ-PROD-002, OQ-PROD-008 y OQ-NFR-004.
- **Status:** OPEN.
