# Requisitos funcionales

Baseline documental derivado del [alcance](scope.md) y la [definición del producto](product-definition.md). Cada registro indica ID, Name, Description, Priority y Status. `PROPOSED` indica propuesta pendiente de aprobación; `ACCEPTED`, aprobación explícita; `DEFERRED`, aplazamiento explícito. Se conservan los estados individuales `PROPOSED`: las decisiones conceptuales aceptadas refinan su descripción, sin aprobar criterios todavía abiertos. Véase [modelo conceptual v0.1](../03-domain/conceptual-model.md).

## Identity

### RF-001
- **Name:** Registro y verificación de correo.
- **Description:** Permitir crear una cuenta y verificar el correo asociado. Los efectos de no verificarlo están abiertos.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-002
- **Name:** Autenticación y logout.
- **Description:** Permitir autenticarse y cerrar la sesión mediante una estrategia por definir.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-003
- **Name:** Recuperación de contraseña.
- **Description:** Permitir recuperar acceso mediante recuperación de contraseña, con controles de seguridad por definir.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-004
- **Name:** Perfil y privacidad.
- **Description:** Permitir al usuario autenticado consultar en modo solo lectura los datos de su perfil (RF-004a), ejercer el derecho a la supresión de la cuenta con validación de contraseña actual y desvinculación de auditoría (RF-004b), y acceder a la información de privacidad desde la interfaz de usuario. Las capacidades backend RF-004a y RF-004b están implementadas; la presentación de privacidad en frontend está pendiente.
- **Priority:** MUST.
- **Status:** ACCEPTED.

#### Criterios de aceptación

- **CA-004-01 (Consulta de perfil — RF-004a):**
  - Dado un usuario autenticado con JWT válido, cuando invoca `GET /users/me`, obtiene código 200 con exactamente seis campos: `userId`, `email`, `emailVerified`, `createdAt`, `adultConfirmedAt` y `status`.
  - La respuesta no expone credenciales, roles, tokens ni datos de bienestar.
  - Peticiones no autenticadas devuelven 401 Unauthorized.
  - No se generan eventos de auditoría por esta consulta.

- **CA-004-02 (Perfil de solo lectura en MVP — OQ-PROD-009):**
  - El perfil no admite operaciones de actualización, edición parcial ni modificación de correo electrónico (conforme a la invariante AJ-04).
  - No se añaden campos adicionales al perfil en el alcance del MVP.

- **CA-004-03 (Supresión de cuenta y desvinculación — RF-004b / OQ-PROD-010):**
  - Dado un usuario autenticado, cuando envía `DELETE /users/me` con su contraseña actual correcta en el cuerpo de la petición, se eliminan físicamente su registro en `user_account` y registros dependientes, desvinculando inmediatamente las referencias en `audit_event` mediante trigger V021 (`target_identifier = NULL`), emitiendo `USER_DELETED` disociado y respondiendo 204 No Content.
  - Si la contraseña es errónea o falta, responde 401 Unauthorized o 400 Bad Request y no altera los datos.
  - La supresión física en base de datos se confirma aun ante fallos recuperables al persistir `USER_DELETED` (aislamiento SAVEPOINT conforme a DP-TRANS-001).
  - Tras el commit, se intenta la revocación de sesión en Redis; fallos posteriores de Redis no revierten la supresión y cualquier solicitud subsiguiente es rechazada con 401 Unauthorized por la ausencia de la cuenta.

- **CA-004-04 (Información de privacidad en frontend — OQ-PROD-010):**
  - La interfaz de usuario frontend debe proporcionar un acceso visible y directo para consultar los términos e información de privacidad de la plataforma. (Capacidad pendiente de implementación en frontend).

## Assessment Catalog

### RF-005
- **Name:** Catálogo y detalle.
- **Description:** Mostrar instrumentos disponibles y su documentación, propósito, versión, condiciones de uso y limitaciones.
- **Priority:** MUST.
- **Status:** PROPOSED.

## Assessment Execution

### RF-006
- **Name:** Iniciar y responder un Assessment.
- **Description:** Crear un AssessmentAttempt asociado al User y a la InstrumentVersion PUBLISHED correspondiente, única como máximo por Instrument para nuevos intentos; permitir responder preguntas SINGLE_CHOICE seleccionando exactamente una AnswerOption válida por Answer y modificar respuestas antes del envío. Máximo una Answer por intento y Question.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-007
- **Name:** Guardar y continuar progreso.
- **Description:** Conservar progreso y permitir continuar un intento conforme a su estado y a las condiciones de vigencia pendientes.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-008
- **Name:** Enviar y obtener resultado.
- **Description:** El submit exige respuestas requeridas y realiza scoring SUM determinista, resolución de Interpretation cuando la fuente la define, creación del resultado oficial y transición a SUBMITTED como operación conceptual atómica usando la versión exacta. El resultado pertenece al límite de consistencia del intento, sin CRUD independiente; los otros estados no tienen resultado oficial.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-009
- **Name:** Historial de evaluaciones.
- **Description:** Permitir al User consultar sus evaluaciones y resultados oficiales sin recalcularlos, conservando Answers, InstrumentVersion exacta y ScoringDefinition histórica para reproducir/auditar el cálculo.
- **Priority:** MUST.
- **Status:** PROPOSED.

## Check-ins

### RF-010
- **Name:** Crear y consultar CheckIn.
- **Description:** Permitir al User registrar y consultar check-ins personales con al menos una Measurement, sin repetir Dimension, y conservar la DimensionVersion exacta usada por cada Measurement. Un CheckIn puede incluir 0..1 Note de texto libre opcional, sin alterar Measurements ni participar en scoring.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-011
- **Name:** Modificar y eliminar CheckIn.
- **Description:** Permitir modificar (PUT) y eliminar (DELETE) check-ins propios conforme a las reglas resueltas en [OQ-DOM-008](business-rules.md#oq-dom-008) y [OQ-DOM-009](business-rules.md#oq-dom-009): propiedad exclusiva del autor autenticado, concurrencia optimista mediante `revision`, ventana absoluta de 168 horas solo para la edición (la eliminación está permitida en cualquier momento), borrado físico en cascada con exclusión de los registros eliminados en Timeline y Trend, 200 con representación actualizada en edición, 204 en eliminación y respuestas uniformes 404/409.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-012
- **Name:** Personalización adicional de check-ins.
- **Description:** Permitir personalización adicional dentro de las Dimensions administradas por Yusay; no habilita dimensiones arbitrarias creadas por usuarios.
- **Priority:** COULD.
- **Status:** PROPOSED.

## Timeline

### RF-013
- **Name:** Timeline y filtros básicos.
- **Description:** Presentar eventos históricos principalmente de AssessmentResults y CheckIns en una Timeline derivada, aunque sus mediciones no sean comparables; permitir filtros básicos pendientes de concreción.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-014
- **Name:** Filtros avanzados.
- **Description:** Ampliar las posibilidades de filtrar el seguimiento; los criterios adicionales deben definirse.
- **Priority:** SHOULD.
- **Status:** PROPOSED.

### RF-015
- **Name:** Context tags.
- **Description:** Asociar etiquetas de contexto personal a registros, con vocabulario y reglas de asociación pendientes.
- **Priority:** SHOULD.
- **Status:** PROPOSED.

## Trends

### RF-016
- **Name:** Tendencias básicas.
- **Description:** Derivar tendencias de AssessmentResults o Measurements compatibles según DR-DOM-003: misma versión exacta, o compatibilidad explícita entre versiones del mismo concepto; UNKNOWN no es comparable. No atribuir causalidad al contexto.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-017
- **Name:** Comparación temporal.
- **Description:** Permitir comparación temporal adicional según reglas explícitas de compatibilidad; su presentación y alcance están abiertos.
- **Priority:** SHOULD.
- **Status:** PROPOSED.

## Guidance

### RF-018
- **Name:** Explicaciones y limitaciones.
- **Description:** Ofrecer Guidance como capacidad compuesta de Interpretation histórica, documentación/limitaciones, Topic y Resource, sin entidad independiente ni diagnóstico, tratamiento o recomendación clínica.
- **Priority:** MUST.
- **Status:** PROPOSED.

## Administration

### RF-019
- **Name:** Administrar definiciones de instrumentos.
- **Description:** Administrar Instrument, InstrumentVersion, Question, AnswerOption, scoring e interpretaciones, preservando la inmutabilidad de versiones publicadas.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-020
- **Name:** Publicar y retirar versiones.
- **Description:** Gestionar DRAFT ⇄ READY → PUBLISHED → RETIRED según DR-DOM-006: READY requiere validación de completitud y ejecutabilidad; máximo una PUBLISHED por Instrument para nuevos intentos. Publicar congela estructura, semántica y scoring; retirar impide nuevos intentos y conserva historia. Registrar operaciones relevantes para auditoría.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-021
- **Name:** Administrar Dimensions.
- **Description:** Permitir a Yusay configurar Dimensions y DimensionVersions sin modificar código, preservando la definición exacta usada por Measurements históricas; los permisos administrativos concretos están abiertos.
- **Priority:** MUST.
- **Status:** PROPOSED.

## Resources

### RF-022
- **Name:** Consultar recursos relacionados.
- **Description:** Mostrar contenido educativo relacionado mediante Topic: AssessmentResult → Interpretation → Topic → Resource, o Dimension → Topic → Resource. No sustituir Interpretation histórica por Resource actual.
- **Priority:** MUST.
- **Status:** PROPOSED.

### RF-023
- **Name:** Administrar recursos.
- **Description:** Permitir administrar, publicar y retirar Resources de forma independiente sin alterar AssessmentResults históricos. Los detalles del ciclo editorial están pendientes.
- **Priority:** MUST.
- **Status:** PROPOSED.

## Export

### RF-024
- **Name:** Exportar historial.
- **Description:** Permitir exportar el historial propio; formato y contenido exportable quedan abiertos.
- **Priority:** SHOULD.
- **Status:** PROPOSED.

### RF-025
- **Name:** Compartir reportes temporalmente.
- **Description:** Permitir enlaces temporales para compartir reportes mediante acceso explícito y limitado; vigencia, revocación y datos incluidos quedan abiertos.
- **Priority:** COULD.
- **Status:** PROPOSED.

## Capacidades transversales del alcance

### RF-026
- **Name:** Recordatorios.
- **Description:** Permitir recordatorios para el seguimiento personal; canales, frecuencia y configuración quedan abiertos.
- **Priority:** SHOULD.
- **Status:** PROPOSED.

### RF-027
- **Name:** OAuth externo.
- **Description:** Permitir acceso mediante OAuth externo si se incorpora esta capacidad opcional; no selecciona la estrategia principal de autenticación.
- **Priority:** COULD.
- **Status:** PROPOSED.

### RF-028
- **Name:** Preferencias avanzadas.
- **Description:** Permitir preferencias adicionales del User; su contenido requiere definición.
- **Priority:** COULD.
- **Status:** PROPOSED.

## Open Questions

### OQ-PROD-008

- **ID:** OQ-PROD-008.
- **Pregunta:** ¿Qué funciones requieren correo verificado?
- **Motivo:** El alcance exige verificación, pero no establece sus efectos.
- **Impacto:** RF-001 y [Identity](../03-domain/bounded-contexts.md).
- **Status:** OPEN.

### OQ-PROD-009

- **ID:** OQ-PROD-009.
- **Pregunta:** ¿Qué campos y operaciones comprende el perfil?
- **Motivo:** Delimitar RF-004 aplicando minimización de datos.
- **Impacto:** RF-004 y [RNF-006](non-functional-requirements.md#rnf-006--data-minimization).
- **Status:** RESOLVED (MVP).
- **Resolución:** Para el alcance del MVP, el perfil es estrictamente de solo lectura a través de `GET /users/me`. Expone exclusivamente los seis campos autorizados bajo minimización de datos: `userId`, `email`, `emailVerified`, `createdAt`, `adultConfirmedAt` y `status`. No se admiten operaciones de actualización, edición de correo (conforme a la invariante AJ-04) ni campos de perfil adicionales (como nombres o datos sociodemográficos). La capacidad backend (RF-004a) se encuentra implementada y verificada; no genera eventos de auditoría y reutiliza los patrones existentes.

### OQ-PROD-010

- **ID:** OQ-PROD-010.
- **Pregunta:** ¿Qué acciones concretas ofrecerán los controles básicos de privacidad?
- **Motivo:** Convertir RF-004 en un requisito verificable sin asumir mecanismos.
- **Impacto:** RF-004, RNF-005 y [Privacy](business-rules.md#privacy).
- **Status:** RESOLVED (MVP).
- **Resolución:** Los controles básicos de privacidad para el MVP comprenden tres capacidades: (1) Consulta autenticada de los datos canónicos del perfil (`GET /users/me` bajo RF-004a); (2) Eliminación física irreversible de la cuenta y sus dependencias privadas (`DELETE /users/me` bajo RF-004b), con validación obligatoria de la contraseña actual, desvinculación a NULL de identificadores en `yusay.audit_event` mediante trigger V021 (SECURITY DEFINER con privilegios mínimos y search_path seguro) y emisión de `USER_DELETED` aislado de fallos no bloqueantes de persistencia conforme a DP-TRANS-001 (SAVEPOINT); y (3) Información de privacidad accesible directamente desde la interfaz frontend. Las capacidades backend RF-004a y RF-004b están implementadas y verificadas; la presentación visual de la política de privacidad en frontend permanece pendiente.

### OQ-PROD-011

- **ID:** OQ-PROD-011.
- **Pregunta:** ¿Cuáles son los filtros básicos y cuáles los avanzados?
- **Motivo:** Distinguir MUST de SHOULD en el seguimiento.
- **Impacto:** RF-013 y RF-014.
- **Status:** OPEN.

### OQ-PROD-012

- **ID:** OQ-PROD-012.
- **Pregunta:** ¿Qué vocabulario y asociaciones admiten los context tags?
- **Motivo:** Concretar la capacidad SHOULD sin decidir la representación de Context.
- **Impacto:** RF-015 y [producto](product-definition.md#tracking).
- **Status:** OPEN.

### OQ-PROD-013

- **ID:** OQ-PROD-013.
- **Pregunta:** ¿Qué interacción adicional comprende la comparación temporal SHOULD?
- **Motivo:** Distinguirla de tendencias básicas MUST; la compatibilidad sigue siendo obligatoria.
- **Impacto:** RF-016, RF-017 y [RN-023](business-rules.md#comparison).
- **Status:** OPEN.

### OQ-PROD-014

- **ID:** OQ-PROD-014.
- **Pregunta:** ¿Qué canales, frecuencia y configuración tendrán los recordatorios?
- **Motivo:** Precisar RF-026 antes de evaluar infraestructura de notificaciones.
- **Impacto:** RF-026 y [decisiones pendientes](../06-decisions/README.md#pending-architectural-decisions).
- **Status:** OPEN.

### OQ-PROD-015

- **ID:** OQ-PROD-015.
- **Pregunta:** ¿Qué datos y formatos incluirá la exportación del historial?
- **Motivo:** Delimitar la capacidad SHOULD y su exposición de información privada.
- **Impacto:** RF-024 y RNF-005.
- **Status:** OPEN.

### OQ-PROD-016

- **ID:** OQ-PROD-016.
- **Pregunta:** ¿Qué personalización adicional de check-ins se ofrecería?
- **Motivo:** Acotar COULD sin habilitar Dimensions arbitrarias.
- **Impacto:** RF-012 y RN-021.
- **Status:** OPEN.

### OQ-PROD-017

- **ID:** OQ-PROD-017.
- **Pregunta:** ¿Qué preferencias avanzadas se ofrecerían?
- **Motivo:** La capacidad COULD está definida solo a nivel de alcance; desacoplada del perfil de solo lectura básico del MVP (RF-004 / OQ-PROD-009).
- **Impacto:** RF-028 y RF-004.
- **Status:** OPEN.

### OQ-PROD-018

- **ID:** OQ-PROD-018.
- **Pregunta:** ¿Qué proveedores y recorrido de acceso comprendería OAuth externo si se incorpora?
- **Motivo:** Precisar COULD sin seleccionar la estrategia principal de autenticación.
- **Impacto:** RF-027 y [Authentication strategy](../06-decisions/README.md#pending-architectural-decisions).
- **Status:** OPEN.

### OQ-PROD-019

- **ID:** OQ-PROD-019.
- **Pregunta:** ¿Qué alcance tiene consultar la colección de CheckIn propios?
- **Motivo:** Concretar la capacidad de consultar de RF-010 como colección paginada sin invadir el read model de Timeline (RF-013), Trend (RF-016) ni el vocabulario de filtros de OQ-PROD-011.
- **Impacto:** RF-010, RF-013, RF-016 y OQ-PROD-011.
- **Status:** RESOLVED.
- **Resolución:** La consulta se materializa como `GET /check-ins` bajo RF-010, con paginación keyset sobre la colección exclusivamente propia, orden fijo `recorded_at DESC, check_in_id DESC`, `limit` predeterminado 20 y máximo 100 —un `limit` fuera de rango devuelve 400— y sin filtros ni orden configurables. El cursor es un valor opaco Base64URL sin relleno, versionado y con validación estricta de formato y versión; un cursor malformado o de versión desconocida devuelve 400. El cursor no se firma: la propiedad se comprueba siempre desde la identidad del servidor, nunca desde el cursor, y el contrato no transporta identificadores de usuario. Cada elemento reutiliza la representación de la consulta por identificador, incluidos `revision` y Measurements; conforme a RN-030, una agregación de Measurements vacía no es una representación válida y se trata como violación de integridad, nunca como resultado legítimo. La colección vacía devuelve 200 sin elementos. La página se obtiene mediante una sentencia única que pagina los CheckIns y agrega sus Measurements sobre el mismo snapshot de comando; la navegación no ofrece snapshot entre solicitudes consecutivas y, como `recorded_at` es editable mediante PUT, una navegación concurrente puede omitir o repetir múltiples registros entre páginas. El cursor ancla la navegación en la última clave devuelta y el índice existente favorece la consulta, sin garantizarse un coste independiente de la profundidad. No se genera `audit_event` y no se requiere migración. No sustituye ni amplía RF-013, RF-016 ni OQ-PROD-011.

### Preguntas relacionadas

- [OQ-PROD-002](../01-discovery/target-users.md#oq-prod-002): ¿Qué información podrá consultar un Visitor?
- [OQ-PROD-005](product-definition.md#oq-prod-005): ¿Cómo se capturará el contexto personal mínimo?
- [OQ-DOM-003](business-rules.md#oq-dom-003): ¿Qué respuestas son obligatorias para enviar un intento?
- [OQ-DOM-013](business-rules.md#oq-dom-013): ¿Qué datos podrá incluir un reporte compartido opcional?
- [OQ-DOM-014](business-rules.md#oq-dom-014): ¿Qué vigencia y revocación tendrá un enlace temporal compartido?
