# Pendientes, diferencias y riesgos

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md).

## Fuente recibida y preservada

La [especificación maestra v1.0](especificacion-maestra-v1.0.md) es la fuente aprobada de las 32 relaciones y sus atributos, PK, AK, FK y URA. Se incorporó sin modificar su contenido. El usuario aprobó la estructura y correspondencia general de la [matriz](10-matriz-trazabilidad.md), precisó REV-LOG-003, 005 y 006 y aprobó provisionalmente Identidad con precisiones posteriores. Evaluaciones incorpora REV-LOG-004 resuelta para MVP; Seguimiento está aprobado provisionalmente con las tres precisiones incorporadas y Compatibilidad incorpora las políticas MVP de REV-LOG-008/009/010, consolidadas tras contraste sin incompatibilidades comprobables; Contenido está aprobado provisionalmente con REV-LOG-007/011/012/013 consolidadas; Auditoría incorpora catálogos/combinaciones aprobados mediante REV-LOG-001 y los seis módulos utilizan dominios aprobados mediante REV-LOG-002. Integridad referencial, estados y 07/08/09/10 son conformes provisionalmente según las revisiones posteriores del usuario. Esas aprobaciones previas se conservan como antecedentes. La autorización formal posterior del responsable del proyecto (2026-10-07) aprueba el dictamen favorable y congela la línea base lógica v1.0.

Los atributos y claves proporcionados no se declaran pendientes de aprobación. Una relación lógica no equivale automáticamente a una Entity o Aggregate Root del conceptual.

## Conflicto comprobado

**C-LOG-001 — Interpretation obligatoria frente a condicional.**

- **Documentación anterior:** [DR-DOM-005](../../03-domain/conceptual-model.md#dr-dom-005), [DR-DOM-006](../../03-domain/conceptual-model.md#dr-dom-006), RN-038 y RF-008 condicionan interpretación a que la fuente la defina.
- **Decisión posterior:** AJ-01 y [ASSESSMENT_RESULT](especificacion-maestra-v1.0.md#16-assessment_result) exigen interpretation_id obligatorio; [INTERPRETATION](especificacion-maestra-v1.0.md#15-interpretation) cubre exactamente una vez cada puntuación alcanzable.
- **Aclaración autorizada:** una versión sin interpretaciones respaldadas no puede publicarse; no se inventan interpretaciones.
- **Tratamiento documental:** se registra el cambio de alcance en la nueva capa; el baseline conceptual original permanece intacto. No se presenta este cambio como una resolución silenciosa ni como una contradicción interna de la nueva fuente.

## Precisiones que no son contradicciones demostradas

VF-02 explicita READY congelada con regreso a DRAFT para corregir; el conceptual no afirmaba que fuese editable directamente. AJ-03/AJ-04 y VF-02..05 precisan estados, ventanas, revisión y retención previamente abiertos.

La preservación de definiciones históricas y eliminación autorizada de registros personales tienen objetos distintos. SCORING_CONTRIBUTION, DIMENSION_ANCHOR y asociaciones lógicas representan definiciones existentes; no confirman nuevas raíces conceptuales. Compatibility por pares no contradice la comparabilidad de una misma versión ni UNKNOWN conservador.

## Pendientes de revisión

Los siguientes IDs son registros de revisión documental, no nuevos requisitos RF/RNF/RN. Los resueltos conservan las reglas aprobadas y su trazabilidad; los abiertos representan pendientes reales.

### REV-LOG-001

- **ID:** REV-LOG-001.
- **Pregunta original:** ¿Cuáles son los catálogos de actor_kind, action, target_type y metadata válidos?
- **Resolución aprobada:** tres actores USER/ADMINISTRATOR/ANONYMOUS, 28 acciones, doce destinos y combinaciones explícitas; SYSTEM fuera del catálogo inicial. Siete perfiles N/F/D/C/E/T/P; CATALOG_UPDATED usa C para generales y E para RESOURCE. T exige topic_id; P exige los dos IDs canónicos.
- **Precisiones:** PASSWORD_CHANGED solo autenticado, PASSWORD_RESET_COMPLETED solo recuperación; CONTEXT_TAG_ACTIVATED solo reactivación; USER_REGISTERED sin sesión previa; USER_DELETED sin IDs personales persistentes. actor_kind conserva clasificación histórica tras desvinculación.
- **Privacidad:** prohibida metadata arbitraria, bienestar privado, contraseñas/hashes/tokens/email/IP/user-agent/sesiones/payloads libres. changed_fields contiene solo nombres/categorías permitidos, sin valores anteriores/nuevos.
- **Retención:** máximo 180 periodos de 24 horas desde occurred_at, sin reinicio al desvincular.
- **Contraste:** compatible con AJ-01..04/VF-01..05; T/P son obligaciones condicionales compatibles con la opcionalidad estructural. Desvinculación y USER_DELETED respetan eliminación inmediata.
- **Impacto:** Auditoría, integridad y estados.
- **Status:** RESOLVED para el MVP.
- **Trazabilidad:** [catálogos aprobados](04-diccionario-datos/06-auditoria.md#rev-log-001--catálogos-aprobados); aprobación explícita posterior del usuario.

### REV-LOG-002

- **ID:** REV-LOG-002.
- **Pregunta original:** ¿Qué dominios lógicos se aplican transversalmente?
- **Resolución aprobada:** identificadores opacos/estables sin significado de negocio; orden total estable documentado en compatibilidad; dominios de códigos, nombres y textos; position/reference_order positivos sin consecutividad; límites de Interpretation enteros con signo inclusivos y lower_bound ≤ upper_bound; instantes inequívocos con UTC de intercambio; 30/180 días como periodos de 24 horas. Email, URLs, símbolos, metadata y nulabilidad conservan las políticas aprobadas.
- **Contraste:** compatible con AJ-01..04/VF-01..05, SUM y escalas enteras, ventanas temporales, claves y privacidad. No cambia atributo ni clave.
- **Impacto:** seis diccionarios, integridad, estados y diseño físico futuro.
- **Status:** RESOLVED para el MVP lógico.
- **Pendiente físico:** tipos PostgreSQL, longitudes, precisión, colaciones, generación/comparador de IDs y representación de metadata. No se eligen UUID ni JSONB.
- **Trazabilidad:** [dominios aprobados](13-dominios-logicos.md); aprobación explícita posterior del usuario.

### REV-LOG-003

- **Pregunta original:** ¿Cuál es la regla exacta de canonicalización de USER.email?
- **Reglas aprobadas (AJ-04, precisión del usuario):** eliminar espacios exteriores, normalizar el dominio y aplicar unicidad sin distinción de mayúsculas. No eliminar puntos ni sufijos + con reglas de proveedores. El correo no puede cambiarse en el MVP.
- **Impacto:** USER.email, AK de email, registro y unicidad.
- **Status:** RESOLVED.
- **Alcance del cierre:** reglas lógicas de email; mecanismos técnicos no seleccionados.
- **Detalles técnicos abiertos:** mecanismo de normalización del dominio y de comparación sin distinción de mayúsculas, tratamiento técnico de caracteres y representación física. No se eligen bibliotecas, colaciones ni tipos. Estos detalles no vuelven pendientes las reglas anteriores.
- **Trazabilidad:** [USER](especificacion-maestra-v1.0.md#1-user) y [diccionario de Identidad](04-diccionario-datos/01-identidad.md#user).

### REV-LOG-004

- **Pregunta original:** ¿Cómo debe tratar SUM una Question opcional omitida?
- **Resolución aprobada para MVP v1.0:** todas las preguntas de una versión que se publique tienen required = true. SUBMITTED exige exactamente una Answer válida por cada Question de su versión; score oficial = SUM de todas las contribuciones seleccionadas, sin respuestas ficticias ni aportes por omisión.
- **Publicación:** fuente y administración deben permitir exigir respuestas completas; se bloquean instrumentos con reglas de omisión que el MVP no representa, sin alterar su metodología. La cobertura de interpretaciones se verifica sobre scores alcanzables mediante respuestas completas válidas.
- **Preservación:** required, atributos, relaciones y claves se conservan. PUBLISHED/RETIRED no se reconfiguran. Respuestas completas no demuestran validez psicométrica ni compatibilidad entre versiones.
- **Impacto:** diccionario de Evaluaciones, validación de publicación/entrega, scoring, cobertura y reproducción histórica.
- **Status:** RESOLVED para el MVP.
- **Opciones futuras no aprobadas:** alternativa 1 (SUM de respuestas presentes sin aporte por omisión) y alternativa 3 (política específica respaldada). No son reglas vigentes ni decisiones pendientes del MVP.
- **Trazabilidad:** [resolución detallada](04-diccionario-datos/02-evaluaciones.md#rev-log-004), AJ-01, VF-01..05 y aprobación posterior explícita del usuario.

### REV-LOG-005

- **Pregunta original:** ¿Qué extremos incluye el intervalo inicial de recorded_at y puede editarse posteriormente ese atributo?
- **Resolución aprobada:** el valor inicial pertenece al intervalo inclusivo [created_at - 168 horas, created_at]. Durante la ventana de edición puede modificarse, pero debe permanecer en ese mismo intervalo original, anclado en created_at.
- **Reglas conservadas:** edición estrictamente anterior a created_at + 168 horas; revision incrementa en uno por operación transaccional de edición confirmada, aunque incluya múltiples cambios. updated_at puede permanecer ausente hasta la primera edición confirmada y luego refleja la última modificación. Editar no desplaza el intervalo ni extiende la ventana.
- **Impacto:** CHECK_IN, validación temporal y revisión concurrente.
- **Status:** RESOLVED.
- **Trazabilidad:** [CHECK_IN](especificacion-maestra-v1.0.md#20-check_in), AJ-03, VF-03 y precisión posterior del usuario.

### REV-LOG-006

- **Pregunta original:** ¿Puede continuarse y enviarse un Attempt iniciado antes de que su versión pase a RETIRED?
- **Resolución aprobada:** un intento iniciado válidamente con su versión PUBLISHED puede continuar y enviarse tras RETIRED si sigue IN_PROGRESS, no ha expirado y sus Answers y Result respetan la versión histórica original.
- **Reglas conservadas:** no se crean nuevos intentos sobre RETIRED; vigencia de 720 horas y entrega estrictamente anterior a expires_at. El retiro no autoriza cambiar la versión del intento.
- **Impacto:** ASSESSMENT_ATTEMPT y ejecución histórica.
- **Status:** RESOLVED.
- **Trazabilidad:** [ASSESSMENT_ATTEMPT](especificacion-maestra-v1.0.md#13-assessment_attempt), VF-02 y precisión posterior del usuario. [OQ-DOM-007](../../02-product/business-rules.md#oq-dom-007) se conserva como antecedente sin reescribir el conceptual.

### REV-LOG-007

- **ID:** REV-LOG-007.
- **Pregunta original:** ¿Qué cambios de Resource son menores o sustanciales?
- **Resolución del MVP:** menores: ortografía, puntuación, formato, claridad superficial y reparación de enlaces que mantengan el mismo contenido informativo. Sustanciales: cambio de propósito, significado, recomendaciones, alcance informativo o type; requieren un nuevo Resource.
- **Preservación:** no alterar Interpretations ni Results históricos; type del Resource original permanece inmutable desde primera publicación.
- **Impacto:** revisión y mutabilidad editorial de Resource.
- **Status:** RESOLVED para el MVP.
- **Contraste:** compatible con AJ-02/VF-01..05 y decisiones anteriores; precisión del usuario sin cambios estructurales.
- **Trazabilidad:** [Contenido consolidado](04-diccionario-datos/05-contenido.md#rev-log-007).

### REV-LOG-008

- **ID:** REV-LOG-008.
- **Pregunta original:** ¿Cómo se garantiza el orden canónico de los pares?
- **Resolución lógica del MVP:** conservar version_a_id < version_b_id; los identificadores deben disponer de un orden total, estable y documentado, compartido al registrar, consultar y validar pares.
- **Orientación:** el orden canónico no representa precedencia temporal ni número de versión. Un par inverso se normaliza a la misma clave; no se agrega fila reflexiva.
- **Impacto:** ambas relaciones, unicidad y futura representación física.
- **Status:** RESOLVED para el MVP, en el alcance lógico.
- **Pendiente de diseño físico:** elección del tipo de identificador y del mecanismo de comparación que materialice y documente el orden. No se afirma que un comparador concreto esté elegido o implementado.

Las alternativas antes presentadas —orden del dominio lógico o comparación de representación canónica— quedan como antecedentes técnicos no seleccionados. No reabren la política lógica ni autorizan cambiar claves.

### REV-LOG-009

- **ID:** REV-LOG-009.
- **Pregunta original:** ¿Cómo se corrigen o revocan declaraciones?
- **Resolución del MVP:** las declaraciones incorporadas se consideran vigentes; no se editan ni eliminan mediante operaciones ordinarias. Detectar una declaración incorrecta requiere escalar a revisión de diseño antes de modificarla.
- **Limitación:** el modelo no representa revocación trazable ni historial de vigencias. No se documentan mecanismos de aprobación, estados nuevos ni revocación histórica; tampoco se promete reconstruir el estado de la política de una fecha pasada.
- **Impacto:** ambas relaciones y administración de compatibilidad.
- **Status:** RESOLVED para el MVP.

### REV-LOG-010

- **ID:** REV-LOG-010.
- **Pregunta original:** ¿Cómo se eliminan catálogos/versiones referenciados históricamente?
- **Resolución del MVP:** no permitir eliminación ordinaria de instrumentos, dimensiones ni versiones referenciadas por información histórica. Utilizar RETIRED cuando corresponda, sin destruir definiciones ni introducir cascadas que eliminen historia accidentalmente.
- **Alcance:** no se agrega status a Instrument/Dimension; se respetan estados de versiones existentes. Tampoco se inventa una política para borrar catálogos no referenciados.
- **Supresión personal:** eliminación autorizada de cuenta, Assessments y CheckIns sigue siendo independiente y debe cumplirse.
- **Impacto:** compatibilidad, catálogos y preservación de referencias en Evaluaciones/Seguimiento.
- **Status:** RESOLVED para el MVP.


### REV-LOG-011

- **ID:** REV-LOG-011.
- **Pregunta original:** ¿Qué contenido es válido para publicar?
- **Resolución del MVP:** ARTICLE exige body no vacío e informativo revisado editorialmente; EXTERNAL_LINK exige URL absoluta HTTPS, sintácticamente válida y revisada editorialmente. Se conservan las exclusiones body/external_url por tipo de AJ-02.
- **Límite:** validez de URL no garantiza seguridad, disponibilidad ni calidad del destino. No se implementan solicitudes automáticas a URLs externas ni se seleccionan formato de body o validadores.
- **Impacto:** publicación y edición de Resource.
- **Status:** RESOLVED para el MVP.
- **Contraste:** compatible con AJ-02/VF-01..05 y decisiones anteriores; precisión del usuario sin cambios estructurales.
- **Trazabilidad:** [Contenido consolidado](04-diccionario-datos/05-contenido.md#rev-log-011).

### REV-LOG-012

- **ID:** REV-LOG-012.
- **Pregunta original:** ¿Qué cambios de Topic y asociaciones están permitidos?
- **Resolución del MVP:** conservar code y significado de Topic estables; permitir correcciones editoriales de name/description sin cambio de significado. Agregar o retirar asociaciones mediante operaciones autorizadas, sin dejar Resource PUBLISHED sin Topics.
- **Preservación:** puede cambiar Guidance actual; no se alteran Interpretations ni Results históricos.
- **Impacto:** Topic y las cuatro asociaciones editoriales.
- **Status:** RESOLVED para el MVP.
- **Contraste:** compatible con AJ-02/VF-01..05 y decisiones anteriores; precisión del usuario sin cambios estructurales.
- **Trazabilidad:** [Contenido consolidado](04-diccionario-datos/05-contenido.md#rev-log-012).

### REV-LOG-013

- **ID:** REV-LOG-013.
- **Pregunta original:** ¿Cómo se eliminan Topic/Resource y asociaciones?
- **Resolución del MVP:** no permitir eliminación física ordinaria de Topics o Resources. Retirar Resources publicados mediante RETIRED, terminal; conservar asociaciones históricas cuando corresponda sin nuevas entidades o estados.
- **Alcance:** retirar una asociación editorial autorizadamente (REV-LOG-012) no equivale a eliminar su Topic/Resource. No se introduce historial temporal de asociaciones ni se garantiza reconstruir Guidance de una fecha pasada.
- **Impacto:** catálogo compartido, asociaciones, publicación y Guidance.
- **Status:** RESOLVED para el MVP.
- **Contraste:** compatible con AJ-02/VF-01..05 y decisiones anteriores; precisión del usuario sin cambios estructurales.
- **Trazabilidad:** [Contenido consolidado](04-diccionario-datos/05-contenido.md#rev-log-013).

## Decisiones transversales posteriores

### DP-TRANS-001

- **ID:** DP-TRANS-001.
- **Pregunta original:** ¿Cómo tratar eliminación de cuenta ante falta de garantía de auditoría o confirmación incierta?
- **Resolución aprobada:** alternativa B, limitada a supresión de cuenta. USER, dependencias personales y desvinculación de referencias existentes en AUDIT_EVENT se confirman atómicamente. Falta de USER_DELETED no invalida supresión confirmada; no se fabrica evento ni se conservan datos personales para reconstruirlo.
- **Resultado comunicado:** solo eliminación confirmada se comunica completada; ante incertidumbre se verifica estado efectivo antes de comunicar éxito o fracaso definitivo.
- **Otras operaciones:** administrativas/de seguridad auditables mantienen auditoría garantizada; excepción no extensible.
- **Retención/restauración:** auditoría máximo 180 días desde occurred_at; supresiones reaplicadas antes de habilitar restauración.
- **Contraste:** compatible con los seis diccionarios, AJ-01..04, VF-01..05 y REV-LOG-001..013. Excepción explícita aprobada sin cambios estructurales.
- **Impacto:** Identidad, Auditoría y transversales; limitación aceptada de posible ausencia de USER_DELETED.
- **Status:** RESOLVED para el MVP v1.0.
- **Antecedente:** alternativa A no seleccionada; [resolución B](07-transacciones-y-concurrencia.md#dp-trans-001). Realización técnica diferida.

### DP-TRANS-002

- **ID:** DP-TRANS-002.
- **Pregunta original:** ¿Qué terminación corresponde al confirmar cancelación antes o después de expires_at?
- **Resolución aprobada tras contraste:** antes del vencimiento CANCELLED/ended_at efectivo; al vencer EXPIRED/ended_at = expires_at aunque status siga IN_PROGRESS. Terminales irreversibles, Answers eliminadas al confirmar y orden de confirmación coherente sin extender plazo ni admitir SUBMITTED fuera de vigencia.
- **Impacto:** Evaluaciones, Estados, transacciones y retención.
- **Status:** RESOLVED para el MVP.
- **Trazabilidad:** [resolución](07-transacciones-y-concurrencia.md#dp-trans-002) y propuesta del usuario autorizada para consolidación si no existen contradicciones; no se identificaron.

## Estado del registro y límites

REV-LOG-001..013 y DP-TRANS-001/002 están RESOLVED en sus alcances documentados del MVP; el dictamen lógico v1.0 está APPROVED y la línea base FROZEN por aprobación formal posterior del responsable del proyecto, sin reabrir sus resoluciones. Permanecen decisiones físicas y técnicas, además de limitaciones explícitas como ausencia de revocación trazable de compatibilidad y de historial de Guidance. Los vacíos operativos no aprobados en los diccionarios no se completan ni se convierten en decisiones nuevas.

## Lo que no se clasifica como información lógica faltante

La falta de tipos PostgreSQL, índices físicos, migraciones o estrategia concreta de bloqueo no invalida los atributos y claves recibidos. Las tecnologías de aplicación siguen abiertas. La cobertura mínima de hijos y otros invariantes de varias filas están aprobados, pero no se atribuyen erróneamente a una FK.

## Riesgos de implementación a documentar posteriormente

La completitud de READY, cobertura de interpretaciones, ACTIVE al registrar, un Topic mínimo al publicar y un Result obligatorio en SUBMITTED requieren coordinación/validación además de pertenencia referencial. Debe contrastarse eliminación con auditoría desvinculada y restauración con supresiones reaplicadas. No se diseñan aquí los mecanismos.

PostgreSQL sigue [PROVISIONALLY ACCEPTED](../../06-decisions/ADR-001-database-engine.md). El dictamen lógico v1.0 está APPROVED y la línea base FROZEN; diseño físico e implementación no están aprobados.

[Índice y fuentes](00-indice.md).

## Cierre formal y control de cambios

El responsable del proyecto aprobó formalmente el dictamen FAVORABLE y autorizó la línea base lógica v1.0 **APPROVED / FROZEN** el **2026-10-07**. La fecha de aprobación es independiente de las fechas de creación/modificación de los documentos; no se atribuye aprobación retroactiva a sus antecedentes.

REV-LOG-001..013 y DP-TRANS-001/002 conservan **RESOLVED**. Riesgos residuales, limitaciones y asuntos físicos/operativos diferidos mantienen sus categorías. PostgreSQL sigue **PROVISIONALLY ACCEPTED**.

Se permiten correcciones de redacción, formato y referencias sin alterar el significado aprobado. Cambios semánticos o estructurales requieren propuesta documentada, justificación, impacto, trazabilidad, aprobación expresa y nueva versión o revisión formal de la línea base. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md#condiciones-para-cambios-posteriores).
