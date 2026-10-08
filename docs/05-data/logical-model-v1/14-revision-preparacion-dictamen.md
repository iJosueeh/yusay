# Revisión de preparación para dictamen — modelo lógico v1.0

> **Nota posterior de cierre (aprobación formal: 2026-10-07):** la línea base lógica v1.0 tiene dictamen FAVORABLE y estado APPROVED / FROZEN por autorización del responsable del proyecto. El contenido que sigue conserva la revisión preparatoria anterior; sus referencias a aprobación o congelación pendientes describen aquella etapa y no el estado vigente. La fecha de aprobación es distinta de las fechas de creación/modificación del documento. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md).

**Estado: revisión preparatoria, no dictamen definitivo. Modelo no congelado.** Fecha: 2026-10-07. Se preservan 32 relaciones, sus atributos y claves; no se generan SQL, migraciones ni código.

## Alcance, fuentes y criterio

Se contrastan la [fuente maestra preservada](especificacion-maestra-v1.0.md), los seis diccionarios, [integridad](05-integridad-referencial.md), [estados](06-estados-y-transiciones.md), [transacciones](07-transacciones-y-concurrencia.md), [privacidad/retención](08-privacidad-eliminacion-retencion.md), [decisiones](09-decisiones-arquitectonicas.md), [trazabilidad](10-matriz-trazabilidad.md) y [dominios](13-dominios-logicos.md), con AJ-01..04, VF-01..05 y aprobaciones posteriores.

El usuario considera conformes provisionalmente integridad, estados y 07/08/09/10; esa conformidad no equivale a congelación. REV-LOG-001..013 conservan sus cierres y alcances aprobados. DP-TRANS-002 se consolida para MVP tras contraste; DP-TRANS-001 está RESOLVED para MVP por aprobación expresa de la alternativa B; auditoría no bloquea supresión de cuenta.

Clasificaciones: **Conforme**, **Precisión documental necesaria**, **Contradicción comprobada** y **Decisión pendiente**. Una FK no garantiza autorización, estado, mínimos de hijos, cálculo ni atomicidad. Un mecanismo físico diferido no constituye por sí mismo un vacío funcional.

## Inventario de las 32 relaciones

**Conforme.** Se mantienen seis módulos: Identidad 5, Evaluaciones 11, Seguimiento 7, Compatibilidad 2, Contenido 6 y Auditoría 1. Los diccionarios conservan 147 atributos: 20/57/33/10/19/8, respectivamente. El inventario siguiente reproduce los nombres aprobados; no propone entidades conceptuales nuevas.

## Identidad — 5

- USER
- USER_CREDENTIAL
- ADMINISTRATOR
- EMAIL_VERIFICATION_TOKEN
- PASSWORD_RESET_TOKEN

[Documento del módulo](04-diccionario-datos/01-identidad.md).

## Evaluaciones — 11

- INSTRUMENT
- INSTRUMENT_VERSION
- INSTRUMENT_VERSION_REFERENCE
- QUESTION
- ANSWER_OPTION
- SCORING_DEFINITION
- SCORING_CONTRIBUTION
- ASSESSMENT_ATTEMPT
- ANSWER
- INTERPRETATION
- ASSESSMENT_RESULT

[Documento del módulo](04-diccionario-datos/02-evaluaciones.md).

## Seguimiento — 7

- DIMENSION
- DIMENSION_VERSION
- DIMENSION_ANCHOR
- CHECK_IN
- MEASUREMENT
- CONTEXT_TAG
- CHECK_IN_CONTEXT_TAG

[Documento del módulo](04-diccionario-datos/03-seguimiento.md).

## Compatibilidad — 2

- INSTRUMENT_VERSION_COMPATIBILITY
- DIMENSION_VERSION_COMPATIBILITY

[Documento del módulo](04-diccionario-datos/04-compatibilidad.md).

## Contenido — 6

- TOPIC
- RESOURCE
- RESOURCE_TOPIC
- INSTRUMENT_TOPIC
- INTERPRETATION_TOPIC
- DIMENSION_TOPIC

[Documento del módulo](04-diccionario-datos/05-contenido.md).

## Auditoría — 1

- AUDIT_EVENT

[Documento del módulo](04-diccionario-datos/06-auditoria.md).


## Integridad referencial y claves

**Conforme.** [Integridad](05-integridad-referencial.md) registra 32 PK, 9 AK, 6 URA y 41 FK (28 simples y 13 compuestas), con destinos existentes y componentes en el orden aprobado. Se conserva la FK simple de Result a Attempt además de la compuesta. Solo actor_user_id de AUDIT_EVENT es FK opcional; no se permite ausencia parcial en FKs compuestas.

Las cadenas Attempt → versión/instrumento, Answer → Attempt/versión/pregunta/opción, contribución → definición/pregunta/opción y Result → Attempt/versión/Interpretation preservan pertenencia histórica. Measurement conserva Dimension/DimensionVersion; compatibilidades conservan ambas versiones del mismo catálogo.

Mínimos y máximos condicionados por estado requieren validación/coordinación adicional: una versión publicada/activa por padre, un Attempt IN_PROGRESS por usuario/instrumento, Answers completas y Result único en SUBMITTED, Measurement mínima y Topic mínimo para Resource publicado. No se agregan AK ni cascadas físicas para representar esas políticas.

## Estados y transiciones

**Conforme**, con DP-TRANS-002 incorporado en [Estados](06-estados-y-transiciones.md) y Evaluaciones.

- USER: ACTIVE / BLOCKED; no se añade UNVERIFIED o DELETED.
- InstrumentVersion: DRAFT ↔ READY → PUBLISHED → RETIRED; READY congelada y retorno a DRAFT para corregir; configuración PUBLISHED/RETIRED inmutable.
- Attempt: IN_PROGRESS → SUBMITTED, CANCELLED o EXPIRED; terminales irreversibles. Cancelación válida confirmada antes de expires_at usa CANCELLED/ended_at efectivo; al vencer usa EXPIRED/ended_at = expires_at, aunque status aún indique IN_PROGRESS. Limpieza de Answers al confirmar CANCELLED/EXPIRED.
- DimensionVersion: DRAFT → ACTIVE → RETIRED; máximo una ACTIVE por Dimension y reemplazo atómico.
- ContextTag: se conservan los estados y la reactivación aprobados; CONTEXT_TAG_ACTIVATED no representa creación inicial.
- Resource: DRAFT → PUBLISHED → RETIRED terminal; publicación por tipo, contenido revisado y Topic mínimo.

No se crean estados para CheckIn, compatibilidades, Topic, tokens o auditoría. Timeline, Trend y Guidance siguen derivados.

## Atomicidad y concurrencia

**Conforme** en políticas, con un pendiente funcional específico de auditoría/eliminación.

Envío coordina Answers completas válidas, SUM de todas las contribuciones históricas, selección de una Interpretation oficial existente, creación del único Result y SUBMITTED. No hay respuestas ficticias, interpretación nueva al enviar ni Result parcial.

Máximo un Attempt IN_PROGRESS por usuario/instrumento, independiente de versión. Inicio exige PUBLISHED; un Attempt iniciado válidamente puede continuar sobre RETIRED si está en curso y vigente. Envío estrictamente antes de expires_at; orden de confirmación coherente ante envío/cancelación/expiración, sin doble cierre o prolongación.

Publicación y retiro de InstrumentVersion coordinan máximos, estado, respaldo y cobertura exacta de scores alcanzables por respuestas completas. Fuente incompatible o interpretaciones sin respaldo bloquean publicación. Reemplazo de DimensionVersion coordina retiro anterior y activación nueva; nuevas Measurements requieren ACTIVE y mantienen escala original.

CheckIn se crea con al menos una Measurement. Edición se confirma antes de created_at + 168 horas; recorded_at permanece en el intervalo original inclusivo [created_at - 168 horas, created_at]. Cada edición transaccional confirmada incrementa revision una vez, aun con cambios múltiples. updated_at puede faltar hasta la primera edición y después refleja la última. No se agregan/eliminan Measurements individuales ni se sustituyen versiones históricas; nuevos vínculos ContextTag requieren ACTIVE.

Resource publicado nunca queda sin Topics ante asociaciones concurrentes. Recuperación coordina consumo, credencial, invalidación de otros tokens y sesiones. Los mecanismos de coordinación, aislamiento, detección de conflictos e idempotencia física quedan diferidos.

## Privacidad, eliminación y retención

**Conforme** en obligaciones y resolución DP-TRANS-001; realización técnica diferida.

ADMINISTRATOR no concede acceso automático a datos privados. Supresión individual coordina todas las dependencias de Assessment o CheckIn. Supresión de cuenta incluye USER, credencial, habilitación administrativa, tokens y todo bienestar dependiente; catálogos históricos compartidos se preservan.

Auditoría se desvincula inmediatamente cuando la cuenta fue actor o destino; actor_kind puede mantener clasificación histórica. USER_DELETED no conserva IDs personales, tampoco sustitutos reidentificables. Fallos no autorizan retener referencias para completar el evento ni afirmar eliminación no confirmada.

Attempts EXPIRED/CANCELLED sin Result: 30 periodos de 24 horas desde ended_at, sin Answers parciales. Auditoría: máximo 180 periodos desde occurred_at. Backups cifrados: 30 periodos. Desvinculación, detección tardía o restauración no reinician plazos.

Restauración reaplica supresiones y retenciones antes de habilitar servicio, sin reintroducir cuentas, registros privados o referencias de auditoría eliminados. Fuente actualizada de supresiones y coordinación técnica se difieren; no se introduce un ledger o relación adicional.

## Catálogos de auditoría

**Conforme.** REV-LOG-001 mantiene tres actores USER/ADMINISTRATOR/ANONYMOUS, 28 acciones, doce target_type, combinaciones explícitas y siete perfiles N/F/D/C/E/T/P en [Auditoría](04-diccionario-datos/06-auditoria.md#rev-log-001--catálogos-aprobados). SYSTEM permanece fuera del catálogo inicial.

T exige topic_id en asociaciones editoriales; P exige ambos IDs canónicos de catálogo en COMPATIBILITY_DECLARED. CATALOG_UPDATED usa C en generales y E para Resource. PASSWORD_CHANGED corresponde a cambio autenticado y PASSWORD_RESET_COMPLETED a recuperación; USER_REGISTERED no exige sesión previa.

No se amplían eventos a bienestar privado o su eliminación individual. Metadata cerrada, sin secretos, emails, IP, user-agent, sesiones, payloads libres o valores anteriores/nuevos en changed_fields.

**DP-TRANS-001 — RESOLVED para el MVP v1.0.** Las operaciones administrativas o de seguridad auditables requieren auditoría garantizada para confirmar éxito, **excepto la supresión de cuenta**. En esta excepción aprobada (alternativa B), la disponibilidad del registro de USER_DELETED no condiciona la validez de la supresión.

La eliminación de USER, todas sus dependencias personales y la desvinculación de referencias existentes en AUDIT_EVENT se confirman atómicamente. Si la supresión se confirma pero no puede registrarse USER_DELETED, sigue siendo válida y no se fabrica un evento de éxito. Si no se confirma, no se comunica como completada; ante resultado incierto se verifica el estado efectivo antes de comunicar éxito o fracaso definitivo.

No se conservan correos, identificadores personales, tokens ni registros privados para reconstruir después el evento. No se reintroduce información suprimida. Permanecen la retención máxima de auditoría de 180 días desde occurred_at y las supresiones reaplicadas antes de habilitar una restauración de backups. No se agregan entidades, atributos, estados o relaciones; el tratamiento técnico de fallos y la verificación del estado efectivo se difieren.

## Dominios lógicos

**Conforme.** REV-LOG-002: IDs opacos estables sin significado de negocio; orden total estable documentado para compatibilidad, sin significado temporal. Posiciones/reference_order enteros positivos sin consecutividad; contribuciones/score/límites enteros con signo y límites inclusivos lower_bound ≤ upper_bound.

Instantes inequívocos y UTC de intercambio; días de 24 horas transcurridas. Se preservan email canónico/único sin cambio MVP ni reglas específicas de proveedores, URLs HTTPS, enumeraciones cerradas, nulabilidad y metadata condicionada por acción.

No se eligen tipos PostgreSQL, longitudes, colaciones, UUID, JSONB, precisión temporal física o comparador concreto.

## Trazabilidad de requisitos y decisiones

**Conforme en la correspondencia documentada.** La [matriz](10-matriz-trazabilidad.md) conserva las 32 correspondencias con antecedente conceptual, RF/RN/RNF aplicables y AJ/VF; registra REV-LOG-001..013 y hallazgos RT/DP. Esta revisión comprueba esa correspondencia y las invariantes transversales; no convierte la matriz en prueba de implementación ni en dictamen global.

DP-TRANS-002 se enlaza a Evaluaciones, Estados, Transacciones y Retención; no crea requisito, atributo o estado nuevo. DP-TRANS-001 distingue regla general de auditoría garantizada y excepción aprobada solo para supresión de cuenta. Las clasificaciones originales RT-001..009/012 conservan evidencia de precisiones ya incorporadas; 07/08/09/10 cuentan con conformidad provisional posterior.

**C-LOG-001:** diferencia histórica comprobada entre interpretación condicional conceptual y obligatoriedad lógica posterior. Sigue documentada sin editar el conceptual v0.1. Bloqueo de publicación sin interpretación respaldada mantiene AJ-01. No se detecta contradicción interna nueva por DP-TRANS-002 o por la política general de DP-TRANS-001.

## Pendientes reales y diseño diferido

### Resolución funcional — DP-TRANS-001

- **ID:** DP-TRANS-001.
- **Status:** RESOLVED para el MVP v1.0.
- **Resolución:** alternativa B aprobada expresamente; supresión completa/desvinculación atómicas y válidas sin registro USER_DELETED cuando este no sea posible. No se comunican supresiones no confirmadas; resultado incierto se verifica antes de comunicación definitiva. No se conservan datos personales para reconstruir auditoría.
- **Excepción:** exclusiva de cuenta; otras operaciones auditables requieren registro garantizado.
- **Limitación aceptada:** puede faltar USER_DELETED después de una supresión válida.
- **Impacto:** Identidad, Auditoría, integridad, estados y transversales.
- **Trazabilidad:** [resolución completa](07-transacciones-y-concurrencia.md#dp-trans-001).

El pendiente funcional se cierra sin cambiar 32 relaciones o claves. No se identifica otro pendiente funcional bloqueante dentro de las políticas MVP revisadas; los asuntos que los diccionarios no autorizan ni representan no se completan por inferencia. La aprobación global y congelación siguen pendientes del usuario.

### Resoluciones y limitaciones del MVP

DP-TRANS-001/002 están RESOLVED para MVP; REV-LOG-001..013 continúan RESOLVED en sus alcances. Opciones futuras de omisiones en SUM, revocación trazable de compatibilidad e historial de Guidance no están aprobadas ni se convierten en requisitos pendientes del MVP.

### Detalles físicos y operativos diferidos

Tipos, colaciones, longitudes, representación/generación de IDs, comparador, representación de metadata, índices, coordinación/aislamiento, limpieza, hash, sesiones, infraestructura de backups y realización técnica de restauración corresponden a diseño posterior. No se marca pendiente lo ya decidido lógicamente: plazos, catálogo, dominios, claves, orden canónico y resultado de restauración.

PostgreSQL permanece [PROVISIONALLY ACCEPTED](../../06-decisions/ADR-001-database-engine.md), con alternativas del ADR preservadas; backend, frontend, persistence y authentication siguen abiertos.

## Cambios documentales de esta revisión

Se sincronizan Evaluaciones, Auditoría, Estados, 07/08/09/10, registro de pendientes e índice. Se incorpora esta revisión preparatoria. DP-TRANS-002 queda consolidado sin cambios estructurales; DP-TRANS-001 queda RESOLVED mediante aprobación expresa de la alternativa B y sus condiciones.

La fuente maestra, el conceptual original, las 32 relaciones y sus claves se preservan. El [documento de dictamen](12-dictamen-modelo-logico-v1.md) contiene una propuesta para aprobación final; no es un dictamen definitivo aprobado ni una congelación.

[Índice](00-indice.md) · [Pendientes y riesgos](11-pendientes-y-riesgos.md).
