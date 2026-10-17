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
- **Authorization & permissions:** política `Administrator` evaluada por petición en backend ([OQ-ARCH-017](#oq-arch-017)); gobernanza de habilitación y revocación fuera de banda mediante el rol técnico `yusay_admin_ops` y la extensión física operacional `yusay.administrative_provisioning_event` bajo la alternativa R1 ([OQ-ARCH-018](#oq-arch-018); [MP-PHYS-014](../05-data/physical-model-v1/05-privacidad-eliminacion-y-operacion.md#mp-phys-014); [MP-PHYS-017](../05-data/physical-model-v1/03-integridad-e-indices.md#mp-phys-017)).

Decisiones que mantienen **Status: OPEN**:
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
- **Motivo:** Las reglas sustantivas están aprobadas (propiedad de datos privados, ACTIVE y correo verificado, alcance limitado de la habilitación ADMINISTRATOR); faltaban el mecanismo de aplicación server-side y las respuestas de denegación.
- **Impacto:** RNF-004, RNF-005, RN-025, RN-026 y RF-019 a RF-023.
- **Resolución:** El backend aplicará una política `Administrator` evaluada por petición: un requirement de autorización consultará `yusay.administrator` por el `user_id` del principal ya validado, con una consulta a PostgreSQL por cada solicitud administrativa. La habilitación administrativa no se emite en el JWT ni en ningún claim. Se mantienen sin cambios la comprobación de identidad ACTIVE y correo verificado y la ausencia de `FallbackPolicy`. Denegaciones: 401 ante autenticación ausente o inválida; 403 con ProblemDetails y `traceId` ante identidad válida sin habilitación administrativa; 503 fail-closed si la comprobación de autorización no puede ejecutarse. RN-025, RN-026 y RNF-004 permanecen inalteradas: la política no concede acceso a respuestas, resultados ni check-ins ajenos, y la autorización de propiedad continúa resuelta en los casos de uso. Revocación efectiva desde el commit del `DELETE` para toda comprobación administrativa posterior, sin ventana ligada a la vigencia del token; solicitudes administrativas previamente autorizadas pueden completarse. Estado de implementación al cierre: existen los 401/503 en ProblemDetails, la comprobación de identidad por petición y la denylist de tokens; la política, su requirement, el repositorio de habilitación y la vía de 403 de autorización quedan decididos pero no implementados. La gobernanza de la habilitación y revocación de administradores (bootstrap, rol operativo, trazabilidad y retención) queda fuera de esta resolución y se tramita en OQ-ARCH-018, que permanece OPEN. OQ-PROD-002, OQ-PROD-008 y OQ-NFR-004 no se resuelven por esta decisión.
- **Status:** RESOLVED mediante aprobación explícita del responsable del proyecto.

### OQ-ARCH-018

- **ID:** OQ-ARCH-018.
- **Pregunta:** ¿Cómo se habilitarán y revocarán los administradores, y cómo se trazará esa operación durante el MVP?
- **Motivo:** OQ-ARCH-017 definió la comprobación de autorización en tiempo de ejecución, pero la mutación sobre `yusay.administrator` no cuenta con endpoints en la API del MVP, no pertenece al catálogo cerrado de `audit_event` y carecía de soporte formal de bitácora y asignación de rol operativo.
- **Impacto:** RN-027, RNF-006, RNF-007, MP-PHYS-014, MP-PHYS-017, RF-021 y la habilitación de los actores de RF-019 a RF-023.
- **Resolución:** Aprobada la Alternativa R1 para el aprovisionamiento y revocación de administradores fuera de banda mediante enmienda normativa B8b-2:
  1. **Identidad Operativa:** Se ratifica el rol técnico dedicado `yusay_admin_ops` (introducido en B8b-1 / V019) como el único autorizado para ejecutar altas y bajas fuera de banda mediante un runner CLI o script operacional auditado.
  2. **Soporte de Bitácora:** Se aprueba la creación de la tabla relacional `yusay.administrative_provisioning_event` en PostgreSQL como una **extensión física operacional versionada de infraestructura** bajo [MP-PHYS-014](../05-data/physical-model-v1/05-privacidad-eliminacion-y-operacion.md#mp-phys-014) y [MP-PHYS-017](../05-data/physical-model-v1/03-integridad-e-indices.md#mp-phys-017). No forma parte del modelo lógico normativo congelado v1.0, el cual se preserva estrictamente intacto en sus 32 relaciones (R-001..R-032) y 147 columnas, sin catalogarse como R-033.
  3. **Atomicidad e Idempotencia:** Toda alta o baja se ejecuta en un bloque transaccional atómico junto con la inserción en la bitácora, exigiendo `RowsAffected == 1` en `yusay.administrator`; una baja con cero filas afectadas aborta con `ROLLBACK` y no emite evento. La clave primaria `event_id uuid` (generada por el runner antes del DML) actúa como token único de idempotencia. Ante dudas en el `COMMIT` o conflictos relacionales (en `pk_administrator` o `pk_administrative_provisioning_event`), rige el protocolo de revalidación determinista comparando los cuatro campos (`target_user_id`, `action`, `operator_identifier`, `ticket_ref` con seguridad ante NULL). Si el resultado de la operación o de la revalidación no puede comprobarse (corte de red, caída del motor o timeout), la operación permanece duraderamente en estado `PENDIENTE_VERIFICACION`.
  4. **Mínimo Privilegio y Segregación de Identidad:** Se neutralizan los privilegios por defecto de V001 revocando todo acceso a `yusay_app` y `yusay_worker`. Se exige que `yusay_admin_ops` **carezca de privilegios `INSERT` globales a nivel de tabla** (directos o heredados); posee exclusivamente `SELECT` global e `INSERT` restringido por columna (`event_id`, `target_user_id`, `action`, `operator_identifier`, `ticket_ref`). Las columnas técnicas (`db_user`, `client_addr`, `occurred_at`) son forzadas por el motor con sus valores predeterminados. `CURRENT_USER` representa únicamente el rol de conexión efectivo en PostgreSQL y **no una identidad humana verificada**; la atribución personal descansa exclusivamente en `operator_identifier` autodeclarado y validado en el runner.
  5. **Limitaciones Declaradas Expresamente:**
     - *DML directo:* La obligatoriedad de la bitácora es una garantía procedural del runner; el motor relacional no impide que una sesión interactiva directa con `yusay_admin_ops` ejecute DML sobre `yusay.administrator` omitiendo la bitácora.
     - *Restauraciones:* La bitácora co-ubicada se revierte con las copias de seguridad de PostgreSQL; carece de independencia probatoria frente a restauraciones completas.
     - *Supresión de cuentas (VF-04):* `target_user_id` sin clave foránea evita bloquear la eliminación de cuentas en `user_account`, pero no garantiza la supresión ni desvinculación de la identidad; la tensión entre inmutabilidad de la bitácora y derecho de supresión permanece formalmente abierta.
     - *Identificación del operador:* La atribución humana es declarativa en `operator_identifier`.
  6. **Condiciones para Futura Implementación:**
     - Persistencia previa y recuperable del `event_id` generado por el runner antes de abrir la transacción DML.
     - Verificación automatizada de ausencia de privilegios `INSERT` globales directos o heredados sobre la tabla en los entornos de despliegue.
- **Status:** RESOLVED mediante aprobación explícita del responsable del proyecto (2026-10-17). OQ-PROD-002, OQ-PROD-008, OQ-NFR-004, VF-04 y hosting/deployment permanecen OPEN.
