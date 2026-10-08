# Yusay — Diseño físico de datos v1.0

**Estado de la fase: APROBADO CON CONDICIONES DE IMPLEMENTACIÓN.** Inicio documental: 2026-10-07. Cierre documental: 2026-10-07. Esquema físico especificado; implementación/DDL diferida a fase técnica autorizada.

## Propósito y línea base

La fase materializa el [modelo lógico FAVORABLE / APPROVED / FROZEN](../logical-model-v1/12-dictamen-modelo-logico-v1.md) en PostgreSQL conforme a [ADR-001 ACCEPTED](../../06-decisions/ADR-001-database-engine.md). Ambos y el conceptual cerrado permanecen intactos. Se conservan seis módulos, 32 relaciones, 147 atributos, 32 PK, nueve AK, seis URA y 41 FK (28 simples y 13 compuestas).

## Decisiones vigentes

OQ-PHYS-001/002 continúan RESOLVED: PostgreSQL 18, referencia inicial 18.6; esquema yusay, USER → app_user, las otras 31 tablas singulares snake_case, columnas originales y 88 nombres objetivo aprobados. Se conserva la política previa de actualizaciones de la rama 18.

OQ-PHYS-003..010 están **RESOLVED en su alcance arquitectónico**, por aprobación del responsable en el encargo de consolidación. Tipos, políticas y estrategias se registran en 02..05. Los mecanismos concretos, expresiones, capacidades, contratos y evidencias pendientes de `MP-PHYS-001..016` se especifican en detalle documental en [01-contexto-y-alcance.md](01-contexto-y-alcance.md#registro-de-mecanismos-pendientes) y a lo largo de 02..05, con su balance y dictamen en 06.

**C-PHYS-001 RESOLVED:** se ratifica y mantiene la **prohibición estricta de cambio de correo electrónico durante todo el MVP**. El correo permanece inmutable; no existe funcionalidad ni flujo de cambio de correo. Se mantiene la línea base lógica intacta y congelada. OQ-NFR-001 permanece OPEN.

## Documentos y entregables

- [01-contexto-y-alcance.md](01-contexto-y-alcance.md): objetivos, fuentes, OQ, trazabilidad TF-PHYS, matriz de consolidación, especificación de los 16 mecanismos MP-PHYS, resolución de C-PHYS-001, criterios/riesgos y orden de trabajo.
- [02-decisiones-y-mapeo-fisico.md](02-decisiones-y-mapeo-fisico.md): nombres aprobados y auditoría previa preservados; OQ-PHYS-003..006; 16 familias / 62 usos UUID; correspondencia completa de los 147 atributos, especificación física de email, target_identifier polimórfico, perfiles jsonb de auditoría y análisis de capacidad numérica.
- [03-integridad-e-indices.md](03-integridad-e-indices.md): OQ-PHYS-007/009; protección de claves/invariantes, índices parciales de versión, mecanismos de exclusión concurrente, evaluación rigurosa de las 41 FK (acciones referenciales) y separación estricta de índices de integridad frente a rendimiento.
- [04-transacciones-y-concurrencia.md](04-transacciones-y-concurrencia.md): OQ-PHYS-007; envío atómico, cálculo de puntuación, edición optimista de CheckIn (revision), expiración a 720h, tokens hash y ciclo de vida, idempotencia transversal, orden de bloqueos y supresión DP-TRANS-001.
- [05-privacidad-eliminacion-y-operacion.md](05-privacidad-eliminacion-y-operacion.md): OQ-PHYS-008/010; supresión/desvinculación atómica, registro duradero de supresiones independiente de backups, protocolo de restauración aislada, retenciones 30/180/30 días, separación de identidades técnicas PostgreSQL, evaluación selectiva de RLS y revocación efectiva de accesos.
- [06-verificacion-y-dictamen.md](06-verificacion-y-dictamen.md): matriz exhaustiva de verificación documental de los 147 atributos, 32 relaciones, 88 restricciones/claves, estado justificado de los 16 mecanismos MP-PHYS, aspectos diferidos y dictamen técnico de la fase física v1.0.

La organización conserva la estructura inicial y evita nuevos diccionarios físicos paralelos. Las matrices de correspondencia remiten a la capa lógica, sin redefinir sus reglas.

## Continuidad y límites

Se consolida la especificación documental de los 16 mecanismos sin implementar código, scripts SQL o migraciones. Los aspectos que dependen de carga real (OQ-NFR-001) permanecen debidamente identificados como abiertos.

No se generan SQL, DDL, migraciones, código, entidades JPA ni commits; tampoco se eligen ORM, framework, hosting, proveedor o servicios de sesión. La siguiente etapa de diseño y cualquier implementación requieren su alcance autorizado.

[Índice de Data](../README.md) · [Contexto y trazabilidad](01-contexto-y-alcance.md) · [Verificación y dictamen](06-verificacion-y-dictamen.md).
