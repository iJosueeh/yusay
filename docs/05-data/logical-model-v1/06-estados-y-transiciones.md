# Estados y transiciones

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md).

## Fuentes y convenciones

La [especificación maestra](especificacion-maestra-v1.0.md), AJ-01..04, VF-01..05, las resoluciones REV-LOG y [dominios aprobados](13-dominios-logicos.md) son fuentes. Los catálogos de [Auditoría](04-diccionario-datos/06-auditoria.md) están aprobados para el MVP.

Una transición exige estado de origen válido, autorización y guardas aplicables. Las enumeraciones restringen valores; una FK no comprueba transiciones. Default físico no se presume. El conjunto de transiciones documentado no autoriza saltos ni reactivación terminal.

Solo USER, INSTRUMENT_VERSION, ASSESSMENT_ATTEMPT, DIMENSION_VERSION, CONTEXT_TAG y RESOURCE tienen status aprobado. No se agregan estados a instrumentos, dimensiones, Topics, tokens, Results, compatibilidades ni AUDIT_EVENT. Los símbolos de actor/action/target no son estados de producto.

## USER

**Estados:** ACTIVE / BLOCKED. No hay estado persistido UNVERIFIED: verificación se expresa con email_verified_at opcional.

- **ACTIVE → BLOCKED:** operación administrativa autorizada; evento USER_BLOCKED, actor ADMINISTRATOR y target USER.
- **BLOCKED → ACTIVE:** únicamente acción administrativa autorizada y auditada; USER_UNBLOCKED con el mismo actor/destino.

Funciones personales requieren simultáneamente ACTIVE y correo verificado. Estar ACTIVE no demuestra verificación; ser ADMINISTRATOR no permite omitir autorización ni acceder automáticamente a datos privados.

Verificar correo no desbloquea USER. Recuperar contraseña tampoco verifica correo ni desbloquea. Registrar no requiere sesión previa; USER_REGISTERED usa USER para la identidad creada. No se deduce un default físico de status ni una política de bloqueo automático.

Eliminar cuenta no es transición a un estado DELETED nuevo. Se eliminan dependencias personales y se desvincula auditoría atómicamente; USER_DELETED no conserva identificadores personales. DP-TRANS-001 permite supresión confirmada válida si no puede registrarse ese evento, exclusivamente para eliminación de cuenta; ante incertidumbre se verifica estado efectivo antes de comunicar resultado definitivo. Bloquear cuenta no se documenta como cancelación automática de sus Attempts ni borrado de CheckIns.

**Trazabilidad:** AJ-04, VF-02/03/04, RN-026 y REV-LOG-001/003.

## INSTRUMENT_VERSION

**Estados y transiciones:** DRAFT ↔ READY → PUBLISHED → RETIRED.

- **DRAFT → READY:** superar la validación de definición ejecutable y publicable. Evento INSTRUMENT_VERSION_READY.
- **READY → DRAFT:** retorno previo a correcciones; READY permanece congelada. Evento INSTRUMENT_VERSION_RETURNED_TO_DRAFT.
- **READY → PUBLISHED:** conservar validaciones, respaldo y cobertura; coordinar máximo una PUBLISHED por Instrument. Evento INSTRUMENT_VERSION_PUBLISHED.
- **PUBLISHED → RETIRED:** impedir nuevos Attempts sin destruir definición o registros históricos. Evento INSTRUMENT_VERSION_RETIRED.

Los cuatro eventos son administrativos, actor ADMINISTRATOR y target INSTRUMENT_VERSION. Crear una versión se clasifica CATALOG_CREATED; editar en DRAFT, CATALOG_UPDATED/C. No se usa actualización genérica para duplicar las transiciones.

### Guardas de READY y publicación

Definición con al menos una Question, opciones válidas y orden coherente; dominios positivos no exigen posiciones consecutivas. Todas las preguntas de una versión que se publique tienen required = true. Fuente y administración deben permitir respuestas completas; una metodología incompatible con MVP bloquea publicación sin adaptación artificial.

ScoringDefinition usa SUM y dispone de contribuciones enteras con signo para las opciones admitidas. Se valida cada puntuación alcanzable mediante respuestas completas válidas y exactamente una interpretación oficial respaldada; rangos inclusivos con lower_bound ≤ upper_bound.

No inventar interpretaciones si la fuente no las aporta. Se conservan respaldo documental, población, condiciones y limitaciones aplicables; atributos opcionales no se convierten globalmente en obligatorios.

READY no se corrige directamente: regresa a DRAFT. PUBLISHED y RETIRED conservan definición histórica inmutable, incluyendo preguntas, opciones, referencias, scoring e interpretaciones; no vuelven a estados editables. RETIRED es terminal.

### Retiro y ejecución histórica

Una versión retirada no admite inicio de nuevos Attempts. Un Attempt iniciado válidamente cuando estaba PUBLISHED puede continuar/enviarse si sigue IN_PROGRESS, no ha expirado y respeta su versión histórica exacta. Retiro no cambia su version_id, no extiende plazo ni exige migración.

Publicación, retiro e inicio concurrentes deben coordinarse. No se decide un procedimiento físico para reemplazar versiones PUBLISHED ni se permite confirmar dos PUBLISHED por instrumento.

**Trazabilidad:** AJ-01, VF-01/02/03, REV-LOG-004/006 y C-LOG-001.

## ASSESSMENT_ATTEMPT

**Estados:** IN_PROGRESS / SUBMITTED / EXPIRED / CANCELLED. Transiciones únicamente desde IN_PROGRESS.

- **IN_PROGRESS → SUBMITTED:** acceso personal autorizado, instante de entrega estrictamente anterior a expires_at y exactamente una Answer válida por cada Question histórica. Calcular SUM de todas las contribuciones seleccionadas, resolver una Interpretation oficial y crear exactamente un Result junto al cambio de estado de forma atómica.
- **IN_PROGRESS → EXPIRED:** plazo agotado, incluido al intentar confirmar una cancelación en expires_at o después aunque status aún sea IN_PROGRESS; no crear Result, eliminar Answers y registrar ended_at = expires_at.
- **IN_PROGRESS → CANCELLED:** cancelación válida confirmada estrictamente antes de expires_at; no crear Result, eliminar Answers y registrar ended_at de la cancelación efectiva. DP-TRANS-002 está RESOLVED para MVP; el instante de solicitud no sustituye al de confirmación.

SUBMITTED, EXPIRED y CANCELLED son terminales. No regresan a IN_PROGRESS ni se transforman entre sí. El borrado individual autorizado o con la cuenta es supresión, no una transición nueva.

### Guardas temporales y de completitud

expires_at = started_at + 720 horas. En expires_at ya no puede enviarse aunque status aún no haya sido actualizado. Solo puede existir un IN_PROGRESS por usuario/instrumento, incluso entre versiones distintas; inicio exige versión PUBLISHED.

En curso puede haber respuestas incompletas. SUBMITTED exige cobertura total; no se crean respuestas ficticias ni contribuciones por omisión. La PK de Answer limita a una por pregunta; validación comprueba que exista para todas y las FKs conservan pregunta/opción/versión.

SUBMITTED tiene exactamente un Result inmutable y Answers históricas; los demás estados no tienen Result. Al terminar se registra el instante ended_at aplicable; no se agrega submitted_at. La lectura de Result no recalcula ni reasigna interpretación.

### Concurrencia, eliminación y auditoría

Edición de Answers, entrega, expiración, cancelación y borrado se coordinan con un orden de confirmación coherente para impedir doble cierre o respuestas posteriores al cierre. Ninguna operación posterior cambia un estado terminal confirmado; no se extiende expires_at ni se permite SUBMITTED fuera de vigencia. Fallar validación no deja un Result parcial ni SUBMITTED sin Result. Mecanismos técnicos siguen abiertos.

EXPIRED/CANCELLED son terminales sin Result y tienen retención de 30 periodos de 24 horas desde ended_at; expiración tardía no reinicia ese plazo. Eliminación individual puede ocurrir en cualquier momento.

**No se auditan estas transiciones como eventos de bienestar**, ni sus identificadores, respuestas o resultados. AUDIT_EVENT se limita a administración y seguridad.

**Trazabilidad:** AJ-01, VF-01..05, REV-LOG-004/006.

## DIMENSION_VERSION

**Estados y transiciones:** DRAFT → ACTIVE → RETIRED.

- **DRAFT → ACTIVE:** validar escala entera y Anchors; coordinar máximo una ACTIVE por Dimension. Evento DIMENSION_VERSION_ACTIVATED.
- **ACTIVE → RETIRED:** impedir uso en nuevas Measurements sin destruir definición ni registros. Evento DIMENSION_VERSION_RETIRED.

Ambos eventos: ADMINISTRATOR / DIMENSION_VERSION. Crear y editar borrador se clasifican CATALOG_CREATED y CATALOG_UPDATED/C respectivamente.

### Activación y reemplazo

version > 0, min_value < max_value, step > 0 y (max_value - min_value) divisible entre step. Anchors pertenecen a la escala. No se agrega un mínimo no aprobado de Anchors.

Si se reemplaza la ACTIVE, retiro de la anterior y activación de la nueva ocurren en una operación atómica, manteniendo máximo una ACTIVE. Los dos hechos pueden tener sus eventos respectivos, sin agregar ID transaccional al modelo.

Creación concurrente de Measurements comprueba ACTIVE en la operación coordinada y mantiene pertenencia a Dimension. Activar una nueva versión no remapea ni normaliza valores históricos.

### Historia y terminalidad

ACTIVE y RETIRED conservan definición de escala y Anchors históricos. RETIRED terminal; no retorna a DRAFT/ACTIVE. Una Measurement histórica puede corregirse dentro de la ventana del CheckIn usando su versión original aun retirada; no se sustituye por la versión vigente.

**Trazabilidad:** AJ-03, VF-01/02/03/05 y precisión posterior de reemplazo atómico.

## CONTEXT_TAG

**Estados y transiciones:** ACTIVE ↔ RETIRED, conservando significado.

- **ACTIVE → RETIRED:** impedir vínculos nuevos y conservar vínculos históricos; CONTEXT_TAG_RETIRED.
- **RETIRED → ACTIVE:** reactivación de la misma etiqueta y significado; CONTEXT_TAG_ACTIVATED.

Ambos eventos: ADMINISTRATOR / CONTEXT_TAG. La creación inicial se clasifica CATALOG_CREATED; no se duplica como reactivación. Edición autorizada de name/description preserva significado y usa CATALOG_UPDATED/C.

Solo se agregan tags ACTIVE. Conservar uno retirado en un CheckIn no requiere reactivarlo; retirar el tag no edita por sí mismo las revisiones de CheckIns históricos. Agregar/retirar vínculos personales respeta ventana y revision del CheckIn y no se audita con acciones editoriales de Topics.

RETIRED no es terminal para ContextTag: esta excepción está aprobada. No se usa una reactivación para reutilizar identidad con significado distinto.

**Trazabilidad:** VF-01/02/03, REV-LOG-001; diccionario de Seguimiento.

## RESOURCE

**Estados y transiciones:** DRAFT → PUBLISHED → RETIRED.

- **DRAFT → PUBLISHED:** title no vacío, contenido válido según type y al menos un Topic. Evento RESOURCE_PUBLISHED.
- **PUBLISHED → RETIRED:** exclusión de Guidance conservando contenido/relaciones históricas cuando corresponda. Evento RESOURCE_RETIRED.

Eventos: ADMINISTRATOR / RESOURCE. Creación usa CATALOG_CREATED; edición permitida usa CATALOG_UPDATED/E.

### Publicación y edición

ARTICLE requiere body no vacío, informativo y revisado editorialmente; no utiliza external_url al publicar. EXTERNAL_LINK exige URL absoluta HTTPS sintácticamente válida y revisada editorialmente; no utiliza body al publicar. No se realizan solicitudes automáticas a destinos externos.

type queda inmutable desde primera publicación. REV-LOG-007 considera sustancial un cambio de propósito, significado, recomendaciones, alcance informativo o type y exige nuevo Resource. Correcciones menores de ortografía, puntuación, formato, claridad superficial o enlace con el mismo contenido no autorizan cambiar el significado.

Agregar/retirar asociaciones editoriales es autorizado, usa eventos EDITORIAL_ASSOCIATION_ADDED/REMOVED y metadata T obligatoria; nunca puede dejar un Resource PUBLISHED sin Topics. El cambio puede afectar Guidance actual, no Interpretations ni Results históricos.

RETIRED terminal; no vuelve a PUBLISHED ni DRAFT. Correcciones editoriales no se documentan como transiciones nuevas ni como reactivación de un retirado. No se permite eliminación física ordinaria de Resource/Topic.

**Trazabilidad:** AJ-02, VF-01..05, REV-LOG-007/011/012/013.

## Relaciones sin status y reglas independientes

- **CHECK_IN:** no tiene lifecycle persistido adicional. Creación atómica con Measurement mínima; edición antes de created_at + 168 horas, revision + 1 por operación confirmada y updated_at refleja última modificación. recorded_at conserva intervalo original inclusivo. Supresión completa en cualquier momento. No se auditan ediciones privadas.
- **Tokens:** uso único, último válido por finalidad y consumo estrictamente vigente. Consumo/sustitución elimina filas; no se agrega consumed_at ni status. Recuperación exitosa elimina otros tokens de recuperación e invalida sesiones.
- **Compatibilidad:** declaraciones incorporadas vigentes, sin edición/eliminación ordinaria. Error requiere revisión de diseño; no existe revocación trazable, status ni historial de vigencias.
- **Topic:** code/significado estables, correcciones textuales y asociaciones autorizadas; sin status nuevo ni eliminación física ordinaria.
- **AUDIT_EVENT:** actor_kind conserva clasificación histórica tras desvinculación. USER_DELETED sin IDs personales; T/P obligatorios por acción. La retención máxima es 180 periodos de 24 horas desde occurred_at. No se agrega estado expirado.
- **Timeline, Trend y Guidance:** read models/capacidades derivadas; sin máquinas de estados o relaciones nuevas.

## Contraste y alcance

No se identifican contradicciones entre guardas, terminalidad, supresión personal y preservación histórica. La continuidad tras retiro no permite inicios nuevos; reversibilidad de ContextTag no se extiende a versiones o Resource.

La aprobación de dominios/catálogos cierra REV-LOG-001/002 sin cambiar enumeraciones de producto ni claves. C-LOG-001 conserva el cambio entre capas sin reescribir el conceptual.

[Integridad](05-integridad-referencial.md) documenta referencias; mecanismos transaccionales y políticas físicas siguen diferidos; el [dictamen definitivo](12-dictamen-modelo-logico-v1.md) registra la aprobación formal y congelación de la línea base lógica. PostgreSQL sigue [PROVISIONALLY ACCEPTED](../../06-decisions/ADR-001-database-engine.md).

[Índice](00-indice.md) · [Matriz](10-matriz-trazabilidad.md) · [Resoluciones](11-pendientes-y-riesgos.md).
