# Privacidad, eliminación y operación — diseño físico v1.0

**Diseño físico: IN PROGRESS. OQ-PHYS-008/010: RESOLVED en alcance arquitectónico.** Registro autorizado: 2026-10-07. Los mecanismos y pruebas operativas siguen pendientes.

## Fuentes y responsabilidades

Se preservan los [diccionarios](../logical-model-v1/00-indice.md), [privacidad lógica](../logical-model-v1/08-privacidad-eliminacion-retencion.md), [transacciones](../logical-model-v1/07-transacciones-y-concurrencia.md), AJ-04, VF-04/05, REV-LOG-001/002 y DP-TRANS-001. UUID no concede acceso ni anonimiza registros personales vinculados. Administrator habilita operaciones administrativas autorizadas, sin acceso automático a datos privados de bienestar.

## Supresión individual y de cuenta

La eliminación individual de una evaluación suprime Attempt, Answers y Result dependiente; la de un CheckIn suprime Measurements y asociaciones de ContextTag. No elimina catálogos compartidos, versiones, interpretaciones o contenido editorial. La cuenta elimina USER y sus dependencias personales —credenciales, habilitación administrativa, tokens y registros personales— y desvincula referencias existentes en AUDIT_EVENT en la misma confirmación atómica.

La desvinculación afecta actor_user_id y los destinos personales en target_identifier/metadata, incluidos eventos cuyo actor sea otra persona. actor_kind puede conservar su clasificación histórica tras perder actor_user_id; no se exige una referencia no nula por ser USER o ADMINISTRATOR. USER_DELETED no conserva identificadores personales persistentes ni metadatos de reconstrucción.

La retención de auditoría no bloquea la supresión. DP-TRANS-001 permite que falte USER_DELETED tras una eliminación confirmada; no excusa pérdida de auditoría para otras operaciones ni autoriza supresión parcial. El mecanismo de aislamiento y comunicación de resultados inciertos está pendiente en 04.

## Especificación de mecanismos de privacidad, supresión y restauración

### MP-PHYS-010 — Aislamiento de fallos en supresión y DP-TRANS-001

1. **Invariante y problema:**
   - La supresión de cuenta (`app_user`) tiene prioridad absoluta. La transacción debe purgar los datos personales y desvincular referencias de auditoría.
   - De conformidad con la excepción aprobada `DP-TRANS-001`, si la eliminación de la cuenta se confirma pero el intento de registrar el evento `USER_DELETED` en `AUDIT_EVENT` falla, la supresión se mantiene válida.
   - No se debe tolerar supresión parcial (e.g. borrar el usuario pero dejar huérfanas sus credenciales o mediciones), ni se deben conservar datos personales para reconstruir el evento con posterioridad.
2. **Mecanismo físico propuesto:**
   - En una transacción PostgreSQL:
     ```sql
     -- 1. Eliminación en cascada de app_user (purga credenciales, tokens, intentos, check-ins)
     DELETE FROM yusay.app_user WHERE user_id = $1;
     
     -- 2. Desvinculación de referencias en AUDIT_EVENT
     UPDATE yusay.audit_event SET actor_user_id = NULL WHERE actor_user_id = $1;
     UPDATE yusay.audit_event SET target_identifier = NULL WHERE target_type = 'USER' AND target_identifier = $1_text;
     ```
   - El registro de `USER_DELETED` se ejecuta dentro de un bloque protegido o mediante savepoint transaccional:
     * Si la inserción tiene éxito: evento registrado con `actor_user_id = NULL` y `target_identifier = NULL`.
     * Si la inserción de auditoría falla por contingencia de espacio o restricción no bloqueante de auditoría, se captura la excepción a nivel de backend, permitiendo que el `COMMIT` de la eliminación de la cuenta concluya con éxito.
3. **Manejo de resultado incierto:**
   - Si la red se cae durante el `COMMIT`, el cliente/backend consulta si el usuario existe: `SELECT 1 FROM yusay.app_user WHERE user_id = $1;`. Si no existe, la operación concluyó exitosamente.

---

### MP-PHYS-011 — Registro duradero de supresiones independiente de backups

1. **Invariante y problema:**
   - Los backups físicos/lógicos de PostgreSQL (pg_dump, pg_basebackup, snapshots) tienen una retención de 30 días y son estáticos: contienen datos personales que existían en el momento del respaldo.
   - Si se restaura un backup de hace 15 días, un usuario eliminado hace 5 días reaparecería ilícitamente en la base de datos restaurada.
   - Es mandatorio contar con un registro duradero de supresiones independiente del soporte de backups para reaplicar las eliminaciones.
2. **Mecanismo físico propuesto:**
   - **Almacenamiento independiente fuera de los backups de BD:**
     * Registro persistente (*tombstone log*) almacenado en un medio de almacenamiento duradero con replicación y retención propia (e.g. bucket inmutable / tabla de control dedicada con ciclo de backup desacoplado).
     * Mínimo de datos por registro: `user_id` (UUID) y `deleted_at` (timestamptz). No contiene emails, nombres, contraseñas ni datos privados.
   - **Retención y purga segura del registro:**
     * El registro de una supresión debe conservarse mientras exista **al menos un backup restaurable** que pueda contener los datos de ese `user_id` (retención de 30 días + ventana de rotación, típicamente 35 días).
     * Una vez que todos los backups que contenían dicho usuario han sido destruidos y purgados de forma verificable, el registro de supresión de ese `user_id` se elimina de forma segura.

---

### MP-PHYS-012 — Protocolo de restauración en entorno aislado

1. **Invariante y problema:**
   - Garantizar que ninguna restauración de base de datos permita el acceso a datos de usuarios eliminados o reactive credenciales/sesiones revocadas.
2. **Mecanismo físico propuesto:**
   - **Procedimiento de restauración de fallo seguro (*fail-safe*):**
     1. *Entorno aislado:* La restauración del backup se realiza en una red o instancia aislada, **sin exponer puertos hacia la aplicación ni usuarios**.
     2. *Extracción de supresiones duraderas:* Se obtiene la lista completa de `user_id` registrados en el registro duradero de supresiones (MP-PHYS-011).
     3. *Reaplicación de supresiones:* Para cada `user_id` suprimido posteriormente a la fecha del backup:
        `DELETE FROM yusay.app_user WHERE user_id = $deleted_user_id;`
        (con la correspondiente desvinculación en `audit_event`).
     4. *Reaplicación de revocaciones:* Invalidación masiva de tokens y credenciales de usuarios que sufrieron reseteo de contraseña posterior al backup.
     5. *Verificación de integridad y privacidad:* Se ejecuta una consulta de validación cruzada:
        `SELECT count(*) FROM yusay.app_user WHERE user_id = ANY($tombstone_ids);`
        Debe retornar estrictamente `0`.
     6. *Apertura de accesos:* Únicamente tras la verificación exitosa se habilitan las conexiones y el tráfico de aplicación. Si el paso 5 detecta inconsistencias o no se puede acceder al registro duradero, el sistema falla de forma segura (*fail-closed*) y el servicio permanece bloqueado.

---

### MP-PHYS-014 — Privilegios técnicos, search_path y RLS selectivo

1. **Separación de identidades técnicas en PostgreSQL:**
   - Se prohíbe el uso de `superuser` o del propietario del esquema para las operaciones habituales del backend.
   - Se definen roles técnicos con privilegios mínimos de principio de menor privilegio:
     * `yusay_app`: Rol para la API de aplicación. Privilegios `SELECT, INSERT, UPDATE, DELETE` en tablas de `yusay`. Sin permisos DDL ni `SUPERUSER` ni `BYPASSRLS`.
     * `yusay_migrator`: Rol para migraciones DDL. Propietario del esquema o con permisos `CREATE, ALTER` controlados. Uso exclusivo durante despliegues controlados.
     * `yusay_worker`: Rol para tareas de mantenimiento y limpieza por lotes (`DELETE` sobre intentos caducados y retención de auditoría).
     * `yusay_backup`: Rol exclusivo para utilidades de copia de seguridad (`SELECT` sobre tablas o pertenencia a `pg_read_all_data`).
2. **Control estricto de `search_path`:**
   - Para evitar ataques de suplantación de funciones u objetos (*search_path hijacking*), las conexiones de aplicación se configuran de forma fija y segura:
     `ALTER ROLE yusay_app SET search_path = yusay, pg_temp;`
   - Toda invocación en DDL o código califica explícitamente los objetos (`yusay.app_user`, `yusay.instrument_version`, etc.).
3. **Evaluación de Row-Level Security (RLS) selectivo:**
   - RLS se evalúa como una capa complementaria de defensa en profundidad, no como un sustituto del control de autorización del backend.
   - Habilitación indiscriminada de RLS degrada planes de ejecución y complica transacciones administrativas.
   - En Yusay, la autorización principal de pertenencia (`user_id = current_user_id`) reside en la lógica de negocio del backend; PostgreSQL refuerza la integridad mediante restricciones y permisos por tabla, reservando políticas RLS únicamente para tablas críticas si se especifica en fases de despliegue.

---

### MP-PHYS-015 — Revocación de sesiones y credenciales

1. **Invariante y problema:**
   - La mera firma criptográfica válida de un token de acceso (e.g. JWT) no acredita autorización vigente si la sesión, cuenta o credencial ha sido revocada.
   - Es necesario coordinar la base de datos relacional con la capa de seguridad ante operaciones sensibles:
     * Restablecimiento de contraseña (`PASSWORD_RESET_COMPLETED`).
     * Cambio autenticado de credenciales (`PASSWORD_CHANGED`).
     * Bloqueo administrativo de cuenta (`USER_BLOCKED`).
     * Supresión de cuenta (`USER_DELETED`).
     * Cierre de sesión (`SIGN_OUT`).
2. **Evaluación de escenarios y mecanismos relacionales:**
   - **A. Revocación global de credenciales por usuario (`password_changed_at`):**
     * En `yusay.user_credential`, la columna `password_changed_at timestamptz` registra el instante exacto en que se modificó la credencial, con resolución de microsegundos.
     * Cada access token JWT transporta, además de `iat`, el claim **`pwd_at`**: la **versión temporal de la credencial** contra la que se verificó la contraseña, expresada en **microsegundos Unix enteros** de `password_changed_at` en el momento de la emisión.
     * En cada validación de token de acceso el backend **consulta PostgreSQL** (fila de `yusay.user_credential`) y exige **acumulativamente** las dos condiciones de revocación:
       1. Regla temporal en segundos (se conserva literal): `to_timestamp(token.iat) >= date_trunc('second', user_credential.password_changed_at)` (o numéricamente: `token.iat >= floor(extract(epoch from user_credential.password_changed_at))`).
       2. Huella de versión de credencial: `token.pwd_at = floor(extract(epoch from user_credential.password_changed_at) * 1000000)`, comparando **microsegundos Unix exactos** (`pwd_at == password_changed_at`).
     * **Motivo de la política conjunta — precisión de `iat`:**
       - El estándar JWT (RFC 7519, campo `iat`) define el tiempo de emisión en **segundos Unix enteros**, mientras que PostgreSQL almacena `timestamptz` con resolución de **microsegundos**.
       - Por sí solo, `iat` **no permite distinguir** un token emitido antes de un cambio de contraseña de otro emitido después de ese mismo cambio cuando ambos caen en el mismo segundo: ambos comparten el mismo valor entero. Aceptar ese segundo toleraría un token emitido milisegundos *antes* del cambio; rechazarlo descartaría un token legítimo emitido milisegundos *después*. Es información perdida por la granularidad, no un problema de umbrales.
       - La política conjunta resuelve ese segundo ambiguo con `pwd_at` sin alterar la regla aprobada en segundos: el conjunto de tokens aceptados es siempre un **subconjunto estricto** del que admitiría la regla sola, es decir, nunca se acepta un token que aquella rechazase.
     * **Semántica de `pwd_at`:**
       - El claim fija la versión de la credencial con la que se autenticó: si la credencial cambia después de la emisión, la huella deja de coincidir y el token queda **revocado de inmediato**, aunque su firma, `iat` y `exp` sigan siendo correctos.
       - **Los tokens sin el claim `pwd_at` se rechazan** en la validación: sin huella no pueden acreditar vigencia (token emitido por otra instancia o alterado).
       - La validación se apoya en la base de datos **en cada uso** del token: el backend no mantiene estado de sesiones en memoria, de modo que cualquier instancia valida cualquier token y el resultado depende únicamente de la fila de `yusay.user_credential`.
     * **Avance estrictamente creciente de `password_changed_at`:**
       - `password_changed_at` actúa como contador de versiones monotónico: ante una petición de cambio cuyo instante no supera al registrado (reloj repetido, concurrencia o desfase entre instancias), el backend persiste el valor anterior **+1 microsegundo** (la resolución propia de la columna).
       - Todo cambio produce así una versión única y estrictamente mayor, con lo que la revocación alcanza exactamente a los tokens anteriores al cambio de forma determinista, sin esperas artificiales ni denylists en memoria.
     * **Compatibilidad con múltiples instancias:** `pwd_at` es un dato persistido en la fila, no derivado del reloj local de cada instancia; ante la misma fila y el mismo token la respuesta es idéntica sea cual sea la instancia que emita o valide. Único requisito operativo: relojes sincronizados por debajo de un segundo, exigencia que ya impone la validación de `exp` e `iat` de cualquier JWT.
   - **B. Bloqueo de cuenta (`USER_BLOCKED`):**
     * Al cambiar `yusay.app_user.status = 'BLOCKED'`, toda autenticación o validación de token rechaza inmediatamente solicitudes verificando `status = 'ACTIVE'`. No requiere revocación de clave criptográfica: la comprobación del estado de cuenta en la base de datos bloquea el acceso en tiempo real.
   - **C. Supresión de cuenta (`USER_DELETED`):**
     * La eliminación atómica de la fila en `yusay.app_user` (y en cascada `user_credential`) hace que cualquier consulta de autenticación falle inmediatamente al no existir el `user_id`.
   - **D. Cierre de sesión individual (`SIGN_OUT`):**
     * **Responsabilidad exclusiva de la capa de backend (sin alterar el modelo lógico):**
       El modelo lógico normativo congelado **no cuenta con una entidad de sesiones** (`SESSION` o `DEVICE_TOKEN`), y su línea base de 32 relaciones y 147 atributos no debe alterarse. La relación `AUDIT_EVENT` registra el evento `SIGN_OUT` (perfil N), pero la regla de privacidad prohíbe terminantemente almacenar identificadores o tokens de sesión en auditoría.
       Por lo tanto, la revocación selectiva de una **única sesión individual** (sin afectar a las demás sesiones del usuario) se define formalmente como **responsabilidad exclusiva de la capa de backend / infraestructura de aplicación** (p. ej. mediante denylist en caché efímera Redis o memoria, con TTL igual a la vida del access token).
       La base de datos relacional no gestiona ni persiste sesiones individuales en el MVP, preservando 100% intacta la frontera del modelo normativo relacional.
     * **Implementación — denylist Redis compartida por `jti`:**
       * Cada access token JWT transporta el claim **`jti`**, identidad de su sesión; **los tokens sin `jti` se rechazan** en la validación (razón `MissingTokenId`), del mismo modo que los que carecen de `pwd_at`.
       * El cierre de sesión registra el `jti` presentado en la clave `yusay:access_token:revoked:{jti}` de Redis con **TTL exactamente igual a la vida restante del token (claim `exp`)**: la entrada caduca cuando el propio JWT deja de ser válido, de modo que la memoria queda acotada por las sesiones vivas.
       * La escritura es atómica e idempotente (`SET ... NX`): de varias llamadas concurrentes con el mismo `jti` solo una registra la primera revocación, y **solo esa produce el evento `SIGN_OUT`** en `AUDIT_EVENT` (perfil N: `target_identifier` y `metadata` obligatoriamente NULL; el `jti` nunca se persiste en auditoría).
       * En cada validación de token de acceso, tras la verificación criptográfica y **antes** de consultar PostgreSQL, el backend consulta la denylist; si el `jti` está revocado, el token se rechaza con el mismo mensaje genérico único empleado en el resto de rechazos.
       * El cierre **no modifica `password_changed_at`** ni ningún otro dato relacional salvo la inserción del evento `SIGN_OUT`: la revocación global (por versión de credencial) y la selectiva (por sesión) son mecanismos independientes, acumulativos y sin interferencias cruzadas.
       * La denylist es **compartida por todas las instancias**: una revocación efectuada en una instancia se observa de inmediato en cualquier otra.
     * **Disponibilidad — política *fail-closed*:** si Redis **no está accesible**, la validación de cualquier token protegido **no acepta tokens** (se niega el acceso en lugar de asumir que el token no está revocado) y el cierre de sesión se aborta sin registrar auditoría: nunca se afirma una revocación que no pudo efectuarse.
     * **Consistencia revocación ↔ auditoría:** una revocación confirmada en Redis **nunca se deshace** por un fallo posterior de PostgreSQL: si el evento `SIGN_OUT` no puede persistirse, la sesión permanece cerrada y se responde con error (`503`), de modo que la seguridad nunca depende de PostgreSQL ni ningún reintento o llamada concurrente puede reactivar un JWT ya intentado cerrar —con la compensación por borrado, la reversión habría reactivado el token durante todo el *timeout* de PostgreSQL, de segundos, y habría convertido en falso el éxito de una llamada concurrente—. Sin transacciones distribuidas: la atomicidad Redis↔PostgreSQL queda fuera de alcance y se prioriza la integridad de la revocación sobre la completitud de la auditoría. **Limitación de completitud registrada explícitamente:** en ese caso puede faltar el evento `SIGN_OUT` para el `jti` revocado y **el reintento del cliente no lo regenera** (el siguiente `SET NX` es falso y no audita); V011 no se viola, pues sus restricciones son condicionales a la existencia de la fila.
     * **Garantías y limitaciones ante reinicios:**
       * Redis se despliega con **persistencia AOF** (`appendonly yes`, `appendfsync everysec`) sobre un volumen dedicado: tras un reinicio del servicio se reconstruye el estado desde el AOF y, en condiciones normales, las revocaciones recientes se conservan con una pérdida habitual de hasta ~1 s de escrituras. **Esta cifra es un comportamiento esperado, no una garantía absoluta:** `everysec` delega en el `fsync` del sistema operativo y del hardware, por lo que un corte de energía o un fallo del volumen puede perder más escrituras (incluso las no volcadas); la durabilidad real depende del disco subyacente y no puede exigírsele a Redis.
       * **Recuperación del cliente:** el multiplexer se reconecta automáticamente cuando el endpoint vuelve a ser alcanzable (mapeo de puerto fijo en docker-compose; en la validación de referencia se restablece en ~1 s tras el arranque del servicio); hasta entonces —y mientras dure cualquier interrupción— la política *fail-closed* anterior mantiene el rechazo de todo token.
       * **Limitación:** si el volumen se perdiera por completo, las claves de revocación desaparecerían y un token revocado volvería a ser válido hasta su `exp` — ventana acotada por la vida restante del token (3600 s por defecto, ajustable con `JWT_ACCESS_TOKEN_TTL_SECONDS`). Mientras Redis esté inaccesible, en cambio, el *fail-closed* impide por completo el uso de cualquier token.
   - **E. Restauración de backups y consistencia de revocaciones:**
     * Al restaurar un backup antiguo, el protocolo `MP-PHYS-012` consulta el registro duradero de supresiones y eventos recientes. Cualquier credencial reseteada con posterioridad a la fecha del backup es invalidada forzosamente para evitar revivir accesos revocados.
3. **Clasificación y estado del mecanismo:**
   - **RESUELTO:**
     * La revocación global por cambio/reseteo de contraseña se resuelve con la política conjunta (`iat` en segundos acumulado a la huella `pwd_at` en microsegundos sobre `password_changed_at`); el bloqueo de usuario y la supresión de cuenta, con `app_user.status` y el protocolo de restauración.
     * La revocación selectiva de una sola sesión individual para `SIGN_OUT` se resuelve en la capa de backend con denylist Redis compartida por `jti` (TTL hasta la expiración del token, *fail-closed* ante indisponibilidad y persistencia AOF), sin modificar el modelo lógico.
   - **Estado de implementación:** la política conjunta de revocación JWT de este mecanismo está **implementada en el backend y verificada con pruebas unitarias y de integración contra PostgreSQL 18**: emisión y validación inmediatamente después del restablecimiento, revocación de tokens anteriores al cambio (incluido el emitido en el mismo segundo), doble cambio consecutivo, rechazo de tokens sin `pwd_at` y comportamiento determinista bajo concurrencia. El cierre selectivo de sesión (`SIGN_OUT`) está **implementado con denylist Redis y verificado** contra PostgreSQL 18 y Redis: selectividad (las demás sesiones del usuario siguen activas), inmediatez, idempotencia sin duplicar la auditoría `SIGN_OUT`, concurrencia, TTL hasta la expiración del JWT, supervivencia de la revocación tras un reinicio de Redis (AOF), comportamiento *fail-closed* con Redis inaccesible, la desconexión y la reconexión automática del cliente sobre la misma conexión, y la revocación **definitiva** (nunca revertida) cuando el evento `SIGN_OUT` no puede persistirse, con su limitación de completitud de auditoría documentada en §D.

[Índice físico](00-indice.md) · [Integridad e índices](03-integridad-e-indices.md) · [Transacciones y concurrencia](04-transacciones-y-concurrencia.md).
