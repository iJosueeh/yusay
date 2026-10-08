# Diccionario de datos — Auditoría

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](../12-dictamen-modelo-logico-v1.md).

## Fuentes y separación de estados

La [especificación maestra](../especificacion-maestra-v1.0.md#32-audit_event) fija la relación, ocho atributos, PK, FK opcional y reglas de privacidad. VF-01/03/04/05, RN-027 y RNF-007 respaldan integridad, desvinculación, retención y catálogos cerrados.

La aprobación explícita del usuario consolida tres actor_kind, 28 action, doce target_type y siete perfiles N/F/D/C/E/T/P, con las precisiones registradas en este documento. Su clasificación no crea permisos, endpoints, estados ni mecanismos de autenticación. [Dominios aprobados](../13-dominios-logicos.md) complementan la fuente preservada.

## AUDIT_EVENT

**Propósito:** registrar eventos administrativos y de seguridad, excluyendo información privada de bienestar.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| audit_event_id | Identificador de evento; opaco, estable, sin significado de negocio | Sí | PK |
| actor_user_id | Identificador de User actor, desvinculable | No | FK → USER |
| actor_kind | Clasificación del actor; catálogo cerrado aprobado | Sí | — |
| action | Acción administrativa/de seguridad; catálogo cerrado aprobado | Sí | — |
| target_type | Tipo de destino; catálogo cerrado aprobado | Sí | — |
| target_identifier | Identificador del destino, sujeto a supresión personal | No | — |
| occurred_at | Instante de ocurrencia del evento | Sí | — |
| metadata | Información permitida por acción, sin datos privados | No | — |

**Valores predeterminados:** No especificado para todos los atributos; no se presupone generación de ID, actor ni timestamp.

### Claves, unicidad y cardinalidades

- **PK:** audit_event_id.
- **FK:** actor_user_id → USER.user_id, opcional.
- **AK / URA adicionales:** ninguna.
- Cada evento refiere 0..1 USER como actor; cada USER puede ser actor de 0..N eventos.
- target_identifier no es una FK ni garantiza existencia del destino. No se agregan referencias polimórficas, claves o relaciones de sesiones/tokens.
- La PK identifica eventos, pero no garantiza por sí sola que una operación produzca exactamente un evento. No se inventa una AK de idempotencia.

### Restricciones aprobadas

Solo eventos administrativos y de seguridad. actor_kind/action/target_type deben pertenecer a catálogos cerrados y su combinación y metadata deben ser válidas para la acción. Los valores concretos se aprueban mediante la resolución posterior de REV-LOG-001, sin alterar la fuente.

No almacenar datos privados de bienestar ni identificadores de Attempts, CheckIns o Results personales, incluso dentro de metadata o target_identifier. No se usa auditoría para registrar creación, edición, respuesta, puntuación o supresión individual de esos registros.

actor_user_id y otras referencias personales deben poder desvincularse al eliminar una cuenta. Conservar un evento no autoriza conservar sus referencias identificables.

### Mutabilidad y desvinculación

Eliminar una cuenta requiere desvincular inmediatamente actor_user_id en eventos donde esa cuenta fue actor y retirar referencias personales en target_identifier/metadata. También deben tratarse eventos donde la cuenta era destino aunque el actor sea otra persona.

La eliminación de USER no debe quedar impedida por conservación de AUDIT_EVENT. La solución física de FK sigue pendiente; no se selecciona cascada ni se elimina el evento por asumir una relación obligatoria.

La fuente no define una política general de edición de eventos. No se declara una inmutabilidad absoluta incompatible con la desvinculación y limpieza exigidas, ni se crea historial de auditoría de la propia anonimización.

### Eliminación y retención

Retención máxima de **180 días de 24 horas transcurridas desde occurred_at**, sin prolongarla por desvinculación o fecha de registro. El vencimiento se determina por occurred_at + 180 días; no se mantiene el evento más allá del máximo por una frecuencia de limpieza no elegida. No se agrega expires_at ni se decide job de eliminación.

Backups cifrados tienen retención de 30 días y las restauraciones reaplican supresiones antes de habilitar el servicio, conforme a VF-04. Restaurar no reinicia la retención del evento ni autoriza recuperar referencias personales suprimidas.

### Invariantes transaccionales y observaciones

**DP-TRANS-001 — RESOLVED para el MVP v1.0.** Las operaciones administrativas o de seguridad auditables requieren auditoría garantizada para confirmar éxito, **excepto la supresión de cuenta**. En esta excepción aprobada (alternativa B), la disponibilidad del registro de USER_DELETED no condiciona la validez de la supresión.

La eliminación de USER, todas sus dependencias personales y la desvinculación de referencias existentes en AUDIT_EVENT se confirman atómicamente. Si la supresión se confirma pero no puede registrarse USER_DELETED, sigue siendo válida y no se fabrica un evento de éxito. Si no se confirma, no se comunica como completada; ante resultado incierto se verifica el estado efectivo antes de comunicar éxito o fracaso definitivo.

No se conservan correos, identificadores personales, tokens ni registros privados para reconstruir después el evento. No se reintroduce información suprimida. Permanecen la retención máxima de auditoría de 180 días desde occurred_at y las supresiones reaplicadas antes de habilitar una restauración de backups. No se agregan entidades, atributos, estados o relaciones; el tratamiento técnico de fallos y la verificación del estado efectivo se difieren.

**Trazabilidad:** [DP-TRANS-001](../07-transacciones-y-concurrencia.md#dp-trans-001), excepción aprobada por el usuario; no cambia REV-LOG-001/002 ni el catálogo USER_DELETED.

Eliminación de cuenta y desvinculación inmediata deben coordinarse sin exponer referencias remanentes. La validación de privacidad se aplica antes de registrar metadata y también al suprimir referencias. actor_kind no concede permisos ni acceso privado.

**Trazabilidad:** fuente §32; VF-01/03/04/05, RN-027 y RNF-007. [REV-LOG-002](../11-pendientes-y-riesgos.md#rev-log-002) aprueba los dominios lógicos; tipos y mecanismos físicos continúan abiertos.

## REV-LOG-001 — Catálogos aprobados

- **ID:** REV-LOG-001.
- **Pregunta original:** ¿Cuáles son los catálogos, combinaciones y metadata válidos?
- **Resolución:** aprobación explícita del usuario con modificaciones, contrastada con AJ-01..04/VF-01..05 y privacidad/eliminación sin contradicciones.
- **Impacto:** AUDIT_EVENT, dominios, integridad y estados.
- **Status:** RESOLVED para el MVP.

### actor_kind aprobado

- **USER:** actor personal identificado al emitir el evento; no implica habilitación administrativa.
- **ADMINISTRATOR:** actor que ejecuta una operación administrativa autorizada; no implica acceso a bienestar privado.
- **ANONYMOUS:** actor sin identidad autenticada, por ejemplo una solicitud de recuperación o un acceso fallido; sin actor_user_id.

USER/ADMINISTRATOR se asocian al actor identificado al emitir el evento, excepto USER_DELETED, que se registra sin identificadores personales persistentes. USER_REGISTERED no requiere una sesión previa: identifica al usuario recién creado. actor_user_id puede quedar ausente en eventos previos tras eliminación de cuenta. actor_kind conserva la clasificación del hecho, sin identificar a la persona desvinculada. Una cuenta destino conocida no convierte al solicitante anónimo en USER; actor y destino son distintos.

SYSTEM queda fuera del catálogo inicial porque no tiene acciones válidas; no se agregan eventos automáticos.

### target_type aprobado

Catálogo cerrado de doce valores:

USER; AUTHENTICATION; INSTRUMENT; INSTRUMENT_VERSION; DIMENSION; DIMENSION_VERSION; CONTEXT_TAG; TOPIC; RESOURCE; INTERPRETATION; INSTRUMENT_VERSION_COMPATIBILITY; DIMENSION_VERSION_COMPATIBILITY.

AUTHENTICATION es una categoría de evento, no una relación nueva. INTERPRETATION designa una definición de catálogo, nunca el Result personal que la utiliza. Los tipos de compatibilidad designan las dos relaciones aprobadas, no una entidad adicional.

No se admiten ATTEMPT, CHECK_IN, ASSESSMENT_RESULT ni equivalentes para bienestar privado.

### action y combinaciones válidas aprobadas

Cada fila admite solo los actores y tipos de destino indicados; las demás combinaciones quedan excluidas. El perfil de metadata se define después. Todos los eventos de éxito representan operaciones confirmadas.

| action | actor_kind admitidos | target_type admitidos | Perfil de metadata |
| --- | --- | --- | --- |
| USER_REGISTERED | USER | USER | N |
| EMAIL_VERIFICATION_TOKEN_ISSUED | USER, ANONYMOUS | USER | N |
| EMAIL_VERIFIED | USER, ANONYMOUS | USER | N |
| PASSWORD_RESET_TOKEN_ISSUED | USER, ANONYMOUS | USER | N |
| PASSWORD_RESET_COMPLETED | USER, ANONYMOUS | USER | N |
| PASSWORD_CHANGED | USER | USER | N |
| SIGN_IN_SUCCEEDED | USER | AUTHENTICATION | N |
| SIGN_IN_FAILED | ANONYMOUS | AUTHENTICATION | F |
| SIGN_OUT | USER | AUTHENTICATION | N |
| AUTHORIZATION_DENIED | USER, ADMINISTRATOR, ANONYMOUS | AUTHENTICATION | D |
| USER_BLOCKED | ADMINISTRATOR | USER | N |
| USER_UNBLOCKED | ADMINISTRATOR | USER | N |
| USER_DELETED | USER | USER | N |
| CATALOG_CREATED | ADMINISTRATOR | INSTRUMENT, INSTRUMENT_VERSION, DIMENSION, DIMENSION_VERSION, CONTEXT_TAG, TOPIC, RESOURCE | N |
| CATALOG_UPDATED | ADMINISTRATOR | INSTRUMENT, INSTRUMENT_VERSION, DIMENSION, DIMENSION_VERSION, CONTEXT_TAG, TOPIC | C |
| CATALOG_UPDATED | ADMINISTRATOR | RESOURCE | E |
| INSTRUMENT_VERSION_READY | ADMINISTRATOR | INSTRUMENT_VERSION | N |
| INSTRUMENT_VERSION_RETURNED_TO_DRAFT | ADMINISTRATOR | INSTRUMENT_VERSION | N |
| INSTRUMENT_VERSION_PUBLISHED | ADMINISTRATOR | INSTRUMENT_VERSION | N |
| INSTRUMENT_VERSION_RETIRED | ADMINISTRATOR | INSTRUMENT_VERSION | N |
| DIMENSION_VERSION_ACTIVATED | ADMINISTRATOR | DIMENSION_VERSION | N |
| DIMENSION_VERSION_RETIRED | ADMINISTRATOR | DIMENSION_VERSION | N |
| CONTEXT_TAG_ACTIVATED | ADMINISTRATOR | CONTEXT_TAG | N |
| CONTEXT_TAG_RETIRED | ADMINISTRATOR | CONTEXT_TAG | N |
| RESOURCE_PUBLISHED | ADMINISTRATOR | RESOURCE | N |
| RESOURCE_RETIRED | ADMINISTRATOR | RESOURCE | N |
| EDITORIAL_ASSOCIATION_ADDED | ADMINISTRATOR | RESOURCE, INSTRUMENT, INTERPRETATION, DIMENSION | T |
| EDITORIAL_ASSOCIATION_REMOVED | ADMINISTRATOR | RESOURCE, INSTRUMENT, INTERPRETATION, DIMENSION | T |
| COMPATIBILITY_DECLARED | ADMINISTRATOR | INSTRUMENT_VERSION_COMPATIBILITY, DIMENSION_VERSION_COMPATIBILITY | P |

Los 28 valores de action de esta tabla constituyen el catálogo aprobado (CATALOG_UPDATED ocupa dos filas para distinguir C/E, sin una acción adicional); no se acepta una acción genérica de texto libre. CATALOG_UPDATED no autoriza modificar definiciones históricas, compatibilidades ni type publicado. USER_BLOCKED/UNBLOCKED no concede una política de bloqueo automático ni nuevas facultades administrativas. USER_DELETED registra el hecho de eliminación de cuenta, sin inventariar sus datos privados.

Las operaciones de edición de definiciones internas de InstrumentVersion se describen a nivel de versión, sin convertir Question, opciones o ScoringDefinition en raíces nuevas. No se agregan acciones de revocación de compatibilidad, eliminación física de Topics/Resources ni reactivación de versiones terminales.

### Significado de las 28 acciones

- **USER_REGISTERED:** Creación confirmada de cuenta, sin sesión previa.
- **EMAIL_VERIFICATION_TOKEN_ISSUED:** Emisión de token de verificación; no confirma entrega de un mensaje.
- **EMAIL_VERIFIED:** Verificación confirmada del correo mediante token válido.
- **PASSWORD_RESET_TOKEN_ISSUED:** Emisión de token de recuperación; no cambia contraseña.
- **PASSWORD_RESET_COMPLETED:** Recuperación únicamente: consumo, cambio de credencial e invalidación de otros tokens de recuperación y sesiones.
- **PASSWORD_CHANGED:** Cambio autenticado únicamente; no duplicar PASSWORD_RESET_COMPLETED.
- **SIGN_IN_SUCCEEDED:** Autenticación exitosa; USER es la identidad comprobada.
- **SIGN_IN_FAILED:** Autenticación rechazada; el solicitante no se identifica como la cuenta intentada.
- **SIGN_OUT:** Cierre confirmado de sesión; no evento por cada sesión invalidada.
- **AUTHORIZATION_DENIED:** Denegación por autenticación, autorización, estado de cuenta o correo, sin identificar el registro privado solicitado.
- **USER_BLOCKED:** Bloqueo administrativo autorizado.
- **USER_UNBLOCKED:** Regreso BLOCKED → ACTIVE administrativo autorizado y auditado.
- **USER_DELETED:** Eliminación confirmada de cuenta sin identificadores personales persistentes.
- **CATALOG_CREATED:** Creación de elemento/definición; no publicación, activación ni READY.
- **CATALOG_UPDATED:** Edición permitida de contenido/definición; excluye transiciones, asociaciones y compatibilidad. C para generales y E para RESOURCE.
- **INSTRUMENT_VERSION_READY:** DRAFT → READY tras validar.
- **INSTRUMENT_VERSION_RETURNED_TO_DRAFT:** READY → DRAFT para corregir.
- **INSTRUMENT_VERSION_PUBLISHED:** READY → PUBLISHED con condiciones aprobadas.
- **INSTRUMENT_VERSION_RETIRED:** PUBLISHED → RETIRED sin destruir historia.
- **DIMENSION_VERSION_ACTIVATED:** DRAFT → ACTIVE; reemplazo coordinado atómicamente con retiro anterior.
- **DIMENSION_VERSION_RETIRED:** ACTIVE → RETIRED preservando escala.
- **CONTEXT_TAG_ACTIVATED:** Reactivación RETIRED → ACTIVE, conservando significado; no creación inicial.
- **CONTEXT_TAG_RETIRED:** ACTIVE → RETIRED, conservando vínculos históricos.
- **RESOURCE_PUBLISHED:** DRAFT → PUBLISHED con contenido válido y Topics.
- **RESOURCE_RETIRED:** PUBLISHED → RETIRED terminal, fuera de Guidance.
- **EDITORIAL_ASSOCIATION_ADDED:** Agregar asociación autorizada con Topic.
- **EDITORIAL_ASSOCIATION_REMOVED:** Retirar asociación autorizada sin dejar PUBLISHED sin Topics.
- **COMPATIBILITY_DECLARED:** Incorporar declaración vigente, justificada, canónica y del mismo catálogo; no revocarla.

### target_identifier por destino

- **USER:** puede señalar la cuenta destino mientras exista y conforme a autorización; al eliminarla se desvincula de todos los eventos donde aparezca. USER_DELETED no conserva su identificador tras confirmar la eliminación.
- **AUTHENTICATION:** ausente. No guardar correo introducido, identificador de sesión, token o hash.
- **Catálogos simples:** puede referir únicamente el identificador lógico del catálogo/definición objetivo; su formato físico queda abierto.
- **Compatibilidad:** target_identifier puede estar ausente; el par se identifica obligatoriamente en metadata mediante el perfil P. No se inventa una representación física del par ni una FK adicional.

No registrar como destino el ID de un registro privado mediante un tipo de catálogo encubierto. Un ID de Interpretation de catálogo es diferente del ID de un Result personal; no incluir una lista de usuarios/resultados que la usan.

### Metadata permitida por acción — aprobada

metadata permanece opcional estructuralmente, pero las acciones T/P exigen su presencia y claves obligatorias. Para las demás acciones se aplica el perfil indicado. Si está presente, solo admite las claves de su perfil y valores de sus listas; no se aceptan claves extra, objetos libres, snapshots o texto libre. No se elige JSON ni otro tipo físico.

- **N — sin metadata:** metadata ausente. Aplica exclusivamente a las acciones N de la tabla.
- **F — fallo de acceso:** una clave opcional reason_code; único valor CREDENTIALS_NOT_ACCEPTED. No se distingue cuenta inexistente de contraseña incorrecta ni se guarda el correo intentado.
- **D — denegación:** una clave opcional reason_code; valores AUTHENTICATION_REQUIRED, ACCOUNT_NOT_ACTIVE, EMAIL_NOT_VERIFIED o OPERATION_NOT_AUTHORIZED. No identifica datos privados consultados ni añade matriz de permisos.
- **C — edición de catálogo general:** clave opcional changed_fields, lista de nombres o categorías de elementos modificados permitidos según target_type. Excluye RESOURCE; no guarda valores anteriores/nuevos.
- **E — edición editorial de RESOURCE:** claves opcionales changed_fields (title, summary, body, external_url) y correction_kind (lista cerrada siguiente). No guarda contenido, valores anteriores/nuevos ni autoriza cambios sustanciales. En un borrador correction_kind puede estar ausente; no se presupone corrección de una publicación.
- **T — asociación editorial:** exige metadata con topic_id para EDITORIAL_ASSOCIATION_ADDED y EDITORIAL_ASSOCIATION_REMOVED. topic_id identifica el Topic de catálogo asociado; el destino principal identifica Resource/Instrument/Interpretation/Dimension. No contiene datos personales ni añade FK/atributo. No se admite metadata ausente ni una clave vacía.
- **P — declaración de compatibilidad:** exige metadata con version_a_id y version_b_id para COMPATIBILITY_DECLARED, ambos IDs de catálogo coherentes con target_type, el mismo Instrument/Dimension y el par canónico. No admite ausencia de metadata o de cualquiera de las dos claves. No contiene rationale libre ni referencias personales; las claves aprobadas de compatibilidad siguen intactas.

**changed_fields permitidos para C**, sin valores de contenido:

- INSTRUMENT: name, description, purpose.
- INSTRUMENT_VERSION: version, source_description, population, administration_conditions, license_information, limitations; o categorías de componentes REFERENCES, QUESTIONS, ANSWER_OPTIONS, SCORING, INTERPRETATIONS. Solo cambios permitidos en DRAFT; status se audita con sus acciones específicas.
- DIMENSION: name, description.
- DIMENSION_VERSION: version, definition, min_value, max_value, step; solo cambios de definición en DRAFT.
- CONTEXT_TAG: name, description, conservando significado; cambios de status usan acciones específicas.
- TOPIC: name, description; code y significado estables.
**changed_fields permitidos para E:** title, summary, body, external_url. Cambiar type es sustancial y requiere nuevo Resource según REV-LOG-007; no se encubre como CATALOG_UPDATED. Los cambios sustanciales no se registran como corrección del original.

**correction_kind aprobado para E:** SPELLING, PUNCTUATION, FORMAT, SURFACE_CLARITY, SAME_CONTENT_LINK_REPAIR. Estos nombres codifican las correcciones menores consolidadas, sin agregar otras categorías. No se exige correction_kind para creación ni se utiliza para autorizar una modificación sustancial.

**Exclusiones para todos los perfiles:** passwords/hashes, tokens/hashes, email, IP, user-agent, URLs personales, cuerpos o títulos de contenido como snapshots, notas, respuestas, scores, valores de Measurements, contexto privado y IDs de Attempts/CheckIns/Results. No se incluyen identificadores personales en metadata en los perfiles aprobados, simplificando su desvinculación; la regla aprobada sigue exigiendo retirar cualquiera que aparezca en datos existentes.

### Desvinculación y clasificación histórica

Al eliminar una cuenta, actor_user_id se elimina como referencia en todos sus eventos y target_identifier se retira donde target_type = USER identifique esa cuenta, incluidos eventos de otros actores. USER_DELETED se registra sin actor_user_id, target_identifier personal ni copia en metadata. actor_kind puede conservar la clasificación USER o ADMINISTRATOR del hecho después de desvincular el actor, sin cambiarlo artificialmente a ANONYMOUS. Mantener actor_kind/action/occurred_at no autoriza mantener identificadores sustitutos o hashes reidentificables.

Los perfiles aprobados no contienen IDs personales ni bienestar privado. Antes de conservar eventos existentes debe revisarse metadata y retirar sus referencias personales, sin limitar la supresión al esquema propuesto. Suprimir referencias no reinicia los 180 días.

### Ejemplos lógicos de perfiles

- N: RESOURCE_PUBLISHED sin metadata.
- F: SIGN_IN_FAILED con reason_code = CREDENTIALS_NOT_ACCEPTED.
- D: AUTHORIZATION_DENIED con reason_code = EMAIL_NOT_VERIFIED.
- C: CATALOG_UPDATED/TOPIC con changed_fields que contiene name y description.
- E: CATALOG_UPDATED/RESOURCE con changed_fields que contiene body y correction_kind = SPELLING.
- T: EDITORIAL_ASSOCIATION_ADDED/RESOURCE con topic_id del catálogo y target_identifier del Resource.
- P: COMPATIBILITY_DECLARED/DIMENSION_VERSION_COMPATIBILITY con los dos IDs canónicos de versiones de la misma Dimension.

Estos ejemplos describen valores, no serialización ni tipos físicos. T/P requieren sus claves aunque metadata sea opcional en la estructura.

## Contraste y límites

No se identifican contradicciones estructurales entre AUDIT_EVENT, VF-04 y desvinculación: actor_user_id es opcional y target_identifier/metadata no tienen FK aprobada. La retención del evento y supresión de referencias personales son obligaciones compatibles.

REV-LOG-001/002 quedan RESOLVED para el MVP. Los catálogos evitan bienestar privado y no agregan permisos, relaciones, atributos ni estados. Tipo físico, comparador concreto y representación de metadata siguen en diseño físico. El perfil E separa Resource de C; los siete perfiles son N/F/D/C/E/T/P. La antigua propuesta contenía seis perfiles y el resumen decía siete: esta aprobación consolida explícitamente la separación correcta.

Se preservan las 32 relaciones, el conceptual v0.1 y la [especificación maestra](../especificacion-maestra-v1.0.md). PostgreSQL sigue [PROVISIONALLY ACCEPTED](../../../06-decisions/ADR-001-database-engine.md); tecnologías de aplicación abiertas. El [dictamen definitivo](../12-dictamen-modelo-logico-v1.md) formaliza APPROVED / FROZEN para la línea base lógica.

[Inventario](../02-inventario-relaciones.md) · [Matriz](../10-matriz-trazabilidad.md) · [Pendientes](../11-pendientes-y-riesgos.md) · [Índice](../00-indice.md).
