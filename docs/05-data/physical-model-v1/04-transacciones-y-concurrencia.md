# Transacciones y concurrencia — diseño físico v1.0

**Diseño físico: IN PROGRESS. OQ-PHYS-007: RESOLVED en alcance arquitectónico.** Autoridad: responsable del proyecto; registro: 2026-10-07. No se seleccionan aislamiento, triggers, procedimientos ni modos de bloqueo.

## Fuentes y límites

Se preservan los [diccionarios](../logical-model-v1/00-indice.md), [estados](../logical-model-v1/06-estados-y-transiciones.md), [transacciones lógicas](../logical-model-v1/07-transacciones-y-concurrencia.md), [privacidad](../logical-model-v1/08-privacidad-eliminacion-retencion.md), AJ-01..04, VF-01..05 y DP-TRANS-001/002. Las operaciones usan las relaciones y atributos existentes. Una confirmación transaccional es la referencia de éxito; ni asignar un UUID ni validar previamente equivale a confirmar.

## Publicación y retiro

La publicación de InstrumentVersion conserva DRAFT, READY, PUBLISHED y RETIRED y su transición aprobada. Exige respaldo de la fuente, todas las Question.required = true, opciones y contribuciones SUM completas y exactamente una interpretación oficial válida para cada puntuación alcanzable con respuestas completas. No se inventan interpretaciones ni se adapta una metodología que admite omisiones no representables.

Se bloquean transaccionalmente las entidades correspondientes durante publicación/retiro y se coordina el máximo de una PUBLISHED por Instrument. Las versiones PUBLISHED o RETIRED conservan la configuración histórica. Un Attempt válidamente iniciado antes del retiro puede continuar/enviarse si permanece IN_PROGRESS, vigente y fiel a su versión; no se inician nuevos Attempts sobre RETIRED.

En DimensionVersion, retirar la ACTIVE previa y activar su reemplazo forman una operación atómica que mantiene como máximo una ACTIVE por Dimension. Los nuevos Measurements usan una versión ACTIVE; los históricos y sus ediciones conservan la versión original. Los índices únicos parciales aprobados en 03 complementan la coordinación; no sustituyen validaciones metodológicas.

Quedan pendientes la identidad exacta a bloquear, orden de adquisición y compatibilidad con las demás operaciones. No se escoge un modo de bloqueo.

## Inicio, respuestas y envío de evaluación (MP-PHYS-005 y MP-PHYS-006)

El máximo de un `IN_PROGRESS` por User e Instrument aplica entre versiones y exige un mecanismo seguro incluso ante inicios simultáneos (`uxp_assessment_attempt_single_in_progress`). Una consulta previa seguida de inserción no es garantía suficiente. La resolución temporal de un intento vencido debe ser coherente con el inicio de otro, sin extender vigencias. No se añade ninguna columna a `ASSESSMENT_ATTEMPT`.

El envío es una sola operación atómica (`MP-PHYS-006`):
1. Bloquear el Attempt mediante `SELECT ... FOR UPDATE`.
2. Comprobar autorización, `IN_PROGRESS` y vigencia estricta (`clock_timestamp() < expires_at`).
3. Validar exactamente una Answer válida por cada Question de su InstrumentVersion y sus opciones históricas (cobertura 100%).
4. Calcular SUM de todas las contribuciones. En PostgreSQL, la agregación interna se realiza en `bigint` evitando desbordamientos durante el cálculo, y la guarda formal de publicación en `InstrumentVersion` asegura que el total acumulado pertenezca estrictamente al intervalo de 32 bits de `integer`.
5. Determinar exactamente una Interpretation respaldada que cubra el score de manera continua.
6. Crear atómicamente el `ASSESSMENT_RESULT` vinculado a esa Interpretation existente y actualizar el intento a `SUBMITTED`.
7. Si se reciben peticiones concurrentes sobre un intento ya procesado, se detecta `SUBMITTED` bajo el bloqueo y se retorna el resultado existente de forma idempotente.

Estados terminales irreversibles y definiciones históricas inmutables. La supresión autorizada de una evaluación elimina la unidad completa (intento, respuestas y resultado), impidiendo dejar intentos huérfanos.

## Expiración y cancelación — DP-TRANS-002

expires_at corresponde a started_at + 720 horas; la vigencia es estricta. Fuera del plazo se rechazan operaciones aunque la limpieza programada no se haya ejecutado. Si la cancelación se confirma antes de expires_at, termina CANCELLED con ended_at de la cancelación efectiva. Si el plazo ya venció al confirmar, corresponde EXPIRED aunque el estado almacenado siguiera IN_PROGRESS, con ended_at = expires_at.

Cada transición terminal elimina Answers parciales atómicamente. El orden coherente de confirmación de operaciones concurrentes no permite SUBMITTED fuera de plazo, extender la vigencia ni revertir estados. El mecanismo temporal y concurrente se diseñará sin asumir que un job determina el vencimiento.

## Edición optimista de CheckIn (MP-PHYS-007)

1. **Invariante y problema:**
   - La edición de un `CHECK_IN` debe realizarse bajo concurrencia optimista mediante el atributo `revision`.
   - La ventana de edición es estricta: únicamente dentro de las 168 horas posteriores a la creación (`clock_timestamp() < created_at + interval '168 hours'`).
   - El atributo `recorded_at` corregido debe permanecer dentro del intervalo `[created_at - interval '168 hours', created_at]`.
   - Se debe preservar estrictamente el conjunto de dimensiones y mediciones originales (`MEASUREMENT`): no se permite agregar ni remover dimensiones en una edición.
   - Cada edición confirmada debe incrementar `revision` exactamente en uno (`revision = revision + 1`) y registrar `updated_at = clock_timestamp()`.
2. **Mecanismo físico propuesto:**
   - **Sentencia SQL atómica condicional con retorno:**
     ```sql
     UPDATE yusay.check_in
     SET revision = revision + 1,
         updated_at = clock_timestamp(),
         note = $new_note,
         recorded_at = $new_recorded_at
     WHERE check_in_id = $check_in_id
       AND revision = $expected_revision
       AND clock_timestamp() < created_at + interval '168 hours'
       AND $new_recorded_at >= created_at - interval '168 hours'
       AND $new_recorded_at <= created_at;
     ```
   - Si la sentencia retorna `0` filas actualizadas, la transacción detecta el fallo y determina la causa:
     * Si `clock_timestamp() >= created_at + interval '168 hours'`: Rechazo por expiración de la ventana de edición (`409 Conflict` / Regla de negocio).
     * Si la revisión actual en la base de datos es distinta de `$expected_revision`: Conflicto de concurrencia optimista (`409 Conflict`). Rollback limpio.
   - **Edición de Mediciones (`MEASUREMENT`):**
     * En la misma transacción, se actualizan los valores de las mediciones existentes:
       ```sql
       UPDATE yusay.measurement
       SET value = $new_value
       WHERE check_in_id = $check_in_id AND dimension_id = $dimension_id;
       ```
     * Se verifica que el número de filas modificadas coincida exactamente con el total de mediciones del CheckIn original. Si se intenta introducir o retirar una dimensión, la transacción aborta.
   - **Asociaciones de ContextTags (`CHECK_IN_CONTEXT_TAG`):**
     * Se sincronizan las etiquetas dentro de la misma transacción. Las nuevas etiquetas añadidas deben estar en estado `ACTIVE` en `yusay.context_tag`.
3. **Pruebas necesarias:**
   - Positiva: Edición válida con incremento exacto de `revision = 1` a `revision = 2` dentro de la ventana de 168 horas.
   - Negativa: Intento de edición en la hora 169 (rechazo por ventana expirada); intento de edición con `revision` desactualizada.
   - Concurrente: Dos ediciones simultáneas sobre la misma revisión; exactamente una se aplica y la otra falla limpiamente con `409 Conflict`.

---

## Expiración a 720 horas y coordinación temporal (MP-PHYS-008)

1. **Invariante y problema:**
   - Los intentos `ASSESSMENT_ATTEMPT` tienen vigencia estricta de 720 horas desde `started_at` (`expires_at = started_at + interval '720 hours'`).
   - La vigencia no depende de cuándo se ejecute el proceso de limpieza o worker en segundo plano: cualquier operación de usuario fuera de plazo debe ser rechazada inmediatamente.
2. **Mecanismo físico propuesto:**
   - **Evaluación dinámica de vigencia:**
     * En cada operación sobre el intento (guardar respuesta, enviar evaluación, cancelar), la consulta incluye la cláusula de guarda:
       `WHERE attempt_id = $1 AND status = 'IN_PROGRESS' AND clock_timestamp() < expires_at`
     * Si `clock_timestamp() >= expires_at`, la aplicación no permite interactuar con el intento.
   - **Transición a EXPIRED y purga atómica de respuestas:**
     * Al detectar la expiración (sea por intento de acceso del usuario o por el worker de mantenimiento por lotes):
       ```sql
       UPDATE yusay.assessment_attempt
       SET status = 'EXPIRED',
           ended_at = expires_at
       WHERE attempt_id = $1 AND status = 'IN_PROGRESS' AND clock_timestamp() >= expires_at;
       ```
     * De conformidad con `DP-TRANS-002`, la transición a estado terminal (`EXPIRED` o `CANCELLED`) elimina de forma atómica todas las respuestas parciales registradas en `yusay.answer`:
       `DELETE FROM yusay.answer WHERE attempt_id = $1;`
3. **Retención de 30 días para intentos sin resultado:**
   - Transcurridos 30 periodos de 24 horas (720 horas) desde `ended_at` de un intento `EXPIRED` o `CANCELLED`, la fila del intento es purgada definitivamente por el lote de limpieza:
     `DELETE FROM yusay.assessment_attempt WHERE status IN ('EXPIRED', 'CANCELLED') AND ended_at <= clock_timestamp() - interval '30 days';`

---

## Tokens criptográficos, hashes y ciclo de vida (MP-PHYS-009)

1. **Invariante y problema:**
   - Los tokens de verificación de correo (`EMAIL_VERIFICATION_TOKEN`) y restablecimiento de contraseña (`PASSWORD_RESET_TOKEN`) son secretos efímeros de un solo uso.
   - `EMAIL_VERIFICATION_TOKEN`: vigencia estricta de 24 horas.
   - `PASSWORD_RESET_TOKEN`: vigencia estricta de 30 minutos.
   - Seguridad: El secreto en texto plano **nunca se almacena** en la base de datos ni se registra en logs o auditoría. Solo se persiste su digest criptográfico (`token_hash`) SHA-256 en formato canónico hexadecimal (64 caracteres ASCII). El `verification_token_id` o `reset_token_id` (UUID) es el identificador técnico de la fila, no el secreto.
   - Política del último token válido: La emisión de un nuevo token para el mismo usuario y propósito invalida o sustituye los anteriores.
2. **Problema del bloqueo no bloqueante (`SKIP LOCKED` descartado):**
   - El uso de `SKIP LOCKED` en operaciones de consumo de un token específico es inadecuado: si dos solicitudes concurrentes intentan consumir el mismo token (e.g. doble clic o ataque de repetición concurrente), la segunda transacción omitiría la fila bloqueada y respondería erróneamente con «token no encontrado/inválido», ocultando la colisión.
   - **Mecanismo físico propuesto: Bloqueo transaccional con espera y reevaluación:**
     * Cuando el usuario presenta el token plano, el backend calcula el hash SHA-256 (`$computed_hash`).
     * Se abre una transacción atómica y se adquiere un bloqueo exclusivo sobre el token correspondiente al hash:
       ```sql
       -- 1. Bloqueo con espera sobre la fila del token específico:
       SELECT reset_token_id, user_id, expires_at
       FROM yusay.password_reset_token
       WHERE token_hash = $computed_hash
       FOR UPDATE;
       ```
     * **Reevaluación de condiciones:**
       - Si no se retorna ninguna fila: El token no existe, ya fue consumido y purgado, o fue invalidado por la emisión de uno nuevo. Se rechaza con error de credencial no aceptada (`400 Bad Request` / `F: CREDENTIALS_NOT_ACCEPTED`).
       - Si la fila existe pero `clock_timestamp() >= expires_at`: El token está caducado. La transacción purga el token expirado (`DELETE FROM yusay.password_reset_token WHERE reset_token_id = $reset_token_id;`), confirma la transacción y rechaza la solicitud (`422 Unprocessable Entity`).
     * **Consumo atómico y mutación de cuenta:**
       - Si el token es válido y está vigente (`clock_timestamp() < expires_at`):
         1. Se actualiza la contraseña en `user_credential`:
            ```sql
            UPDATE yusay.user_credential
            SET password_hash = $new_password_hash,
                password_changed_at = clock_timestamp()
            WHERE user_id = $user_id;
            ```
         2. Se eliminan atómicamente **todos** los tokens de restablecimiento pendientes de ese usuario (incluyendo el token actual consumido, aplicando la política de consumo único y del último token):
            ```sql
            DELETE FROM yusay.password_reset_token WHERE user_id = $user_id;
            ```
         3. Se emite el evento de auditoría obligatorio `PASSWORD_RESET_COMPLETED` (perfil N, sin datos privados).
         4. Se confirma la transacción (`COMMIT`).
     * **Coordinación de solicitudes concurrentes y reintentos:**
       - Si una segunda transacción concurrente intenta canjear el mismo token, se bloqueará esperando a que la primera transacción termine. Al reanudar la ejecución, la reevaluación detectará que la fila ya no existe (fue eliminada por el `DELETE`), rechazando limpiamente el intento concurrente por token ya consumido.
       - Esto garantiza un consumo único determinista, rechazo estricto de reutilizaciones y consistencia absoluta.

---

## Contenido editorial y publicación de Resources

1. **Diferenciación y validación por tipo:**
   - `ARTICLE`: Requiere `body` no vacío y revisión editorial antes de pasar a `PUBLISHED`.
   - `EXTERNAL_LINK`: Requiere `external_url` no vacía, con esquema HTTPS estricto (`external_url ~ '^https://'`) y sintaxis absoluta válida.
2. **Regla de al menos un Topic para Resources publicados:**
   - Para transicionar un `RESOURCE` a `PUBLISHED`, debe existir al menos una fila en `yusay.resource_topic` para ese `resource_id`.
   - **Impedir retiro del último Topic:**
     * Al ejecutar `EDITORIAL_ASSOCIATION_REMOVED`, se bloquea el recurso:
       `SELECT status FROM yusay.resource WHERE resource_id = $1 FOR UPDATE;`
     * Si `status = 'PUBLISHED'`, se comprueba que el conteo de asociaciones restantes sea estrictamente mayor a 1:
       `SELECT count(*) FROM yusay.resource_topic WHERE resource_id = $1;`
     * Si el conteo es igual a 1, la operación es abortada, impidiendo que un recurso publicado quede sin temas asociados.

---

## Idempotencia transversal, orden de bloqueos y manejo de errores

1. **Identidad lógica de operaciones e idempotencia:**
   - Las operaciones derivan su idempotencia del modelo de datos congelado:
     * *Creación de usuario:* Clave `uq_app_user_email` (`lower(email)`).
     * *Inicio de intento:* Restricción única parcial `uxp_assessment_attempt_single_in_progress`. Si ya existe, se devuelve el intento existente.
     * *Envío de intento:* Al encontrar `status = 'SUBMITTED'`, se retorna el resultado ya persistido.
     * *Declaración de compatibilidad:* PK `(version_a_id, version_b_id)` con orden canónico.
     * *Asociaciones N:M:* PK compuesta `(id_a, id_b)` previene duplicación.
2. **Orden canónico de adquisición de bloqueos (Prevención de Deadlocks):**
   - Para evitar bloqueos mutuos (*deadlocks*) en transacciones concurrentes, se establece una jerarquía estricta de adquisición de bloqueos:
     1. `app_user` (por `user_id`).
     2. Catálogos padres (`instrument`, `dimension`).
     3. Versiones de catálogos (`instrument_version`, `dimension_version`).
     4. Entidades transaccionales raíz (`assessment_attempt`, `check_in`).
     5. Entidades dependientes (`answer`, `assessment_result`, `measurement`).
   - En operaciones que involucran dos entidades del mismo tipo (como declaraciones de compatibilidad entre dos versiones), los bloqueos deben adquirirse **siempre en el orden canónico total de sus UUIDs** (`version_a_id < version_b_id`), eliminando la posibilidad de deadlocks cruzados.
3. **Reintentos de transacciones:**
   - Los fallos transitorios de concurrencia en PostgreSQL (código `40P01` para deadlock y código `40001` para error de serialización) deben ser capturados por la capa de acceso a datos del backend para reintentar la transacción completa desde el inicio, con un límite máximo finito (e.g. 3 reintentos) y retroceso exponencial con jitter (*exponential backoff*).

---

## Supresión de cuenta y DP-TRANS-001

1. **Protocolo transaccional de supresión de cuenta:**
   - La eliminación de `app_user`, sus dependencias personales y la desvinculación de auditoría forman una sola unidad de trabajo atómica:
     1. Bloquear y eliminar `app_user` (dispara cascadas sobre credenciales, tokens, intentos y check-ins).
     2. Actualizar `audit_event` desvinculando referencias personales:
        `UPDATE yusay.audit_event SET actor_user_id = NULL WHERE actor_user_id = $user_id;`
        `UPDATE yusay.audit_event SET target_identifier = NULL WHERE target_type = 'USER' AND target_identifier = $user_id_text;`
     3. Intentar registrar el evento `USER_DELETED` en `audit_event` (con `actor_user_id = NULL`, `target_identifier = NULL`, `metadata IS NULL`).
     4. Si el paso 3 falla (por ejemplo por espacio en disco de auditoría o contingencia externa), **la supresión de la cuenta se confirma igualmente de conformidad con DP-TRANS-001**.
2. **Verificación de resultado incierto:**
   - Si la conexión se interrumpe durante el `COMMIT`, el backend debe consultar si el `user_id` todavía existe antes de reportar un fallo o reintentar. Si el usuario ya no existe, la supresión se considera exitosa.

[Índice físico](00-indice.md) · [Integridad e índices](03-integridad-e-indices.md) · [Privacidad y operación](05-privacidad-eliminacion-y-operacion.md).
