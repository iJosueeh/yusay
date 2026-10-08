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
     * En `yusay.user_credential`, la columna `password_changed_at timestamptz` registra el instante exacto en que se modificó la credencial.
     * En cada validación de token de acceso, el backend verifica:
       `to_timestamp(token.iat) >= date_trunc('second', user_credential.password_changed_at)` (o numéricamente: `token.iat >= floor(extract(epoch from user_credential.password_changed_at))`).
     * **Análisis crítico de precisión temporal:**
       - El estándar JWT (RFC 7519, campo `iat`) define el tiempo de emisión en **segundos Unix enteros**, mientras que PostgreSQL almacena `timestamptz` con resolución de **microsegundos**.
       - Si un token se emite en el mismo segundo en que se actualiza `password_changed_at`, una comparación estricta puede incurrir en falsos rechazos o tolerar un token emitido milisegundos antes del cambio.
       - *Regla de diseño:* Al actualizar `password_changed_at`, la base de datos o backend debe normalizar o redondear hacia el siguiente segundo entero (`date_trunc('second', clock_timestamp()) + interval '1 second'`), asegurando que ningún token emitido en el mismo segundo o con anterioridad sea aceptado.
   - **B. Bloqueo de cuenta (`USER_BLOCKED`):**
     * Al cambiar `yusay.app_user.status = 'BLOCKED'`, toda autenticación o validación de token rechaza inmediatamente solicitudes verificando `status = 'ACTIVE'`. No requiere revocación de clave criptográfica: la comprobación del estado de cuenta en la base de datos bloquea el acceso en tiempo real.
   - **C. Supresión de cuenta (`USER_DELETED`):**
     * La eliminación atómica de la fila en `yusay.app_user` (y en cascada `user_credential`) hace que cualquier consulta de autenticación falle inmediatamente al no existir el `user_id`.
   - **D. Cierre de sesión individual (`SIGN_OUT`):**
     * **Responsabilidad exclusiva de la capa de backend (sin alterar el modelo lógico):**
       El modelo lógico normativo congelado **no cuenta con una entidad de sesiones** (`SESSION` o `DEVICE_TOKEN`), y su línea base de 32 relaciones y 147 atributos no debe alterarse. La relación `AUDIT_EVENT` registra el evento `SIGN_OUT` (perfil N), pero la regla de privacidad prohíbe terminantemente almacenar identificadores o tokens de sesión en auditoría.
       Por lo tanto, la revocación selectiva de una **única sesión individual** (sin afectar a las demás sesiones del usuario) se define formalmente como **responsabilidad exclusiva de la capa de backend / infraestructura de aplicación** (p. ej. mediante denylist en caché efímera Redis o memoria, con TTL igual a la vida del access token).
       La base de datos relacional no gestiona ni persiste sesiones individuales en el MVP, preservando 100% intacta la frontera del modelo normativo relacional.
   - **E. Restauración de backups y consistencia de revocaciones:**
     * Al restaurar un backup antiguo, el protocolo `MP-PHYS-012` consulta el registro duradero de supresiones y eventos recientes. Cualquier credencial reseteada con posterioridad a la fecha del backup es invalidada forzosamente para evitar revivir accesos revocados.
3. **Clasificación y estado del mecanismo:**
   - **PARCIALMENTE RESUELTO DOCUMENTALMENTE:**
     * La revocación global por cambio/reseteo de contraseña, bloqueo de usuario y supresión de cuenta está resuelta mediante `password_changed_at`, `app_user.status` y el protocolo de restauración.
     * La revocación selectiva de una sola sesión individual para `SIGN_OUT` se mantiene formalmente como responsabilidad exclusiva de la capa de backend, sin modificar el modelo lógico.

[Índice físico](00-indice.md) · [Integridad e índices](03-integridad-e-indices.md) · [Transacciones y concurrencia](04-transacciones-y-concurrencia.md).
