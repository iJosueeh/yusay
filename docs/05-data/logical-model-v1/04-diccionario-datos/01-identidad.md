# Diccionario de datos — Identidad

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](../12-dictamen-modelo-logico-v1.md).

## Fuentes y convenciones

- [Especificación maestra](../especificacion-maestra-v1.0.md#1-user): fuente de los atributos y claves de las cinco relaciones.
- AJ-04, VF-01..05 y reglas adicionales del prompt maestro: identidad, tokens, estados, concurrencia, supresión y retención.
- [REV-LOG-003](../11-pendientes-y-riesgos.md#rev-log-003): precisión aprobada de email.
- [RF-001..004](../../../02-product/functional-requirements.md#identity), [RN-025/026](../../../02-product/business-rules.md#privacy) y [RNF-003..007](../../../02-product/non-functional-requirements.md#rnf-003--seguridad): antecedentes conservados.

`?` identifica opcionalidad en la fuente; el resto de los atributos es obligatorio. PK, AK y FK se reproducen sin agregar claves. Los dominios de identificador, correo, hash e instante describen su significado lógico; representación, formato técnico, precisión temporal y tipos PostgreSQL no están seleccionados. [REV-LOG-002](../11-pendientes-y-riesgos.md#rev-log-002) y los [dominios transversales](../13-dominios-logicos.md) aprueban significado lógico; representación física sigue abierta.

No se presupone un valor predeterminado por ausencia de una marca `?`: obligatoriedad no equivale a default ni a valor generado automáticamente. Los atributos temporales documentados no determinan el uso de una función de base de datos.

## USER

**Propósito:** identidad de la cuenta personal, correo, verificación, confirmación adulta y habilitación de acceso.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia | Predeterminado aprobado |
| --- | --- | --- | --- | --- |
| user_id | Identificador de User; identificador opaco y estable | Sí | PK | No especificado |
| email | Correo canónico; unicidad sin distinción de mayúsculas | Sí | AK | No especificado |
| email_verified_at | Instante de verificación del correo | No | — | No especificado |
| created_at | Instante de creación de la cuenta | Sí | — | No especificado |
| adult_confirmed_at | Instante de confirmación de mayoría de edad por el usuario | Sí | — | No especificado |
| status | ACTIVE o BLOCKED | Sí | — | No especificado |

### Claves, integridad y unicidad

- **PK:** user_id.
- **AK:** email, con la igualdad sin distinción de mayúsculas aprobada. No se agrega otra clave.
- **FK:** ninguna en esta relación.
- **Restricciones lógicas:** status pertenece al conjunto ACTIVE/BLOCKED; registro requiere confirmación de 18 años o más y adult_confirmed_at obligatorio. No se almacena fecha de nacimiento para verificar edad en el MVP.
- **Canonicalización aprobada:** eliminar espacios exteriores, normalizar dominio y aplicar política consistente de unicidad sin distinción de mayúsculas. No eliminar puntos ni sufijos `+` mediante reglas propias de proveedores. El mecanismo técnico queda abierto; no se elige una colación ni se modifica la clave.
- **Acceso:** las funciones personales requieren simultáneamente ACTIVE y correo verificado. La FK de una dependencia no prueba esas condiciones de autorización.

### Relaciones y cardinalidades

Cada USER_CREDENTIAL y ADMINISTRATOR refiere exactamente un USER; sus PK/FK permiten como máximo una fila de cada relación por User. Las claves suministradas, por sí solas, no obligan a que cada USER tenga una de esas filas: cobertura de credenciales no se inventa.

Las FKs de EMAIL_VERIFICATION_TOKEN y PASSWORD_RESET_TOKEN permiten cero o muchas filas por USER; la política de emisión/validez limita los tokens operativamente y no se transforma en una AK no aprobada.

ASSESSMENT_ATTEMPT y CHECK_IN pertenecen a USER y AUDIT_EVENT puede referirlo opcionalmente como actor. Los detalles de esos módulos no se reconstruyen aquí.

### Mutabilidad y estados

Email no puede cambiarse en el MVP. El estado puede ser ACTIVE o BLOCKED; el regreso BLOCKED → ACTIVE requiere acción administrativa autorizada y auditada. email_verified_at registra una verificación válida. La fuente no define reglas adicionales de edición para user_id, created_at o adult_confirmed_at; no se agregan.

### Eliminación y retención

VF-04 permite eliminar completamente cuenta y dependencias personales. Eliminar USER incluye credenciales, habilitación administrativa, tokens y registros personales relacionados. Antes de conservar AUDIT_EVENT deben desvincularse inmediatamente actor_user_id y referencias personales en target_identifier/metadata; no se conservan identificadores de registros privados de bienestar en auditoría.

La auditoría tiene retención de 180 días desde occurred_at. Backups cifrados conservan 30 días; una restauración reaplica supresiones antes de habilitar el servicio. Estas políticas no determinan cascadas FK ni infraestructura de backups.

### Invariantes transaccionales y observaciones

Registro debe coordinar confirmación adulta y unicidad del correo canónico, incluso ante solicitudes concurrentes; no se permiten cuentas duplicadas por diferencias de mayúsculas. VF-03 exige atomicidad del registro y de la eliminación de cuenta. [DP-TRANS-001 RESOLVED](../07-transacciones-y-concurrencia.md#dp-trans-001) confirma atómicamente USER, dependencias personales y desvinculación de auditoría existente; la falta del nuevo USER_DELETED no invalida una supresión confirmada. Ante resultado incierto se verifica el estado efectivo antes de comunicar éxito o fracaso definitivo, sin conservar datos personales para reconstruir auditoría. No se seleccionan bloqueos ni nivel de aislamiento.

**Trazabilidad:** AJ-04, VF-01..04, RF-001/002/004, RN-025/026 y RNF-003..007. La ausencia de formato físico no vuelve pendientes los atributos ni la AK aprobados.

## USER_CREDENTIAL

**Propósito:** información de autenticación por contraseña asociada a la cuenta, separada de los datos de USER.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia | Predeterminado aprobado |
| --- | --- | --- | --- | --- |
| user_id | Identificador de la cuenta | Sí | PK; FK → USER.user_id | No especificado |
| password_hash | Hash de contraseña; formato y algoritmo no seleccionados | Sí | — | No especificado |
| password_changed_at | Instante de cambio de contraseña registrado | Sí | — | No especificado |

### Claves, integridad y cardinalidad

- **PK:** user_id.
- **AK / URA adicionales:** ninguna documentada.
- **FK:** user_id → USER.user_id, obligatoria.
- **Unicidad:** máximo una credencial por USER, garantizada por la PK; no múltiples credenciales de contraseña por cuenta en este modelo.
- **Cardinalidad derivada de claves:** USER 1 → 0..1 USER_CREDENTIAL; cada credencial pertenece a exactamente un USER. No se impone cobertura total de USER.
- **Restricciones equivalentes:** obligatoriedad de hash y timestamp; no se guarda la contraseña en texto. No se inventan longitud, algoritmo ni formato de hash como CHECK aprobado.

### Mutabilidad

Cambiar contraseña actualiza la información de credencial y su instante de cambio; invalida sesiones anteriores, sin seleccionar un mecanismo de autenticación o una relación adicional para sesiones. Recuperar contraseña no verifica correo ni desbloquea USER.

### Eliminación

Es dependencia de cuenta y se elimina con ella conforme a VF-04. No se establece una política de retención histórica de contraseñas ni una operación de eliminación independiente no aprobada.

### Invariantes transaccionales y observaciones

AJ-04/VF-03 exigen atomicidad de operaciones sensibles: consumo del token de recuperación, invalidación de los demás tokens de recuperación del usuario, cambio de credencial e invalidación de sesiones anteriores deben coordinarse sin resultados parciales. La FK no comprueba token vigente, estado de cuenta ni autorización.

La igualdad PK/FK identifica la cuenta; no permite agregar AK, historial de credenciales ni atributos de sesiones.

**Trazabilidad:** AJ-04, VF-01/03/04, RF-002/003, RNF-003/004/005.

## ADMINISTRATOR

**Propósito:** representar la habilitación administrativa de un USER sin crear una identidad personal diferente.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia | Predeterminado aprobado |
| --- | --- | --- | --- | --- |
| user_id | Identificador de User habilitado como administrador | Sí | PK; FK → USER.user_id | No especificado |

### Claves, restricciones y cardinalidad

- **PK:** user_id.
- **AK / URA adicionales:** ninguna documentada.
- **FK:** user_id → USER.user_id, obligatoria.
- **Unicidad:** máximo una habilitación ADMINISTRATOR por USER.
- **Cardinalidad:** USER 1 → 0..1 ADMINISTRATOR; cada ADMINISTRATOR refiere exactamente un USER.
- **Integridad y autorización:** la habilitación administrativa se distingue de la autorización para acceder a información privada. La pertenencia a una cuenta existente no concede automáticamente acceso a Answers, Results o CheckIns privados. Los permisos se verifican server-side; no se inventa una matriz de permisos.

### Mutabilidad, eliminación y transacciones

La fuente no define atributos editables ni el procedimiento para otorgar/revocar la habilitación. Las operaciones administrativas relevantes y el desbloqueo de USER requieren autorización y auditoría; no se define aquí el catálogo exacto de acciones.

La habilitación se elimina como dependencia al eliminar la cuenta. VF-03 exige coordinación con la eliminación de cuenta; no se determina un comportamiento físico de FK ni se agrega una relación de roles.

**Trazabilidad:** VF-01..04, RN-026/027, RNF-004/007. Catálogos exactos aprobados en [Auditoría](06-auditoria.md) mediante REV-LOG-001.

## Reglas comunes de tokens

AJ-04 y VF-03..05 establecen:

- Persistencia mediante hash, uso único y validez únicamente del último token emitido por usuario y finalidad.
- Emitir uno nuevo invalida los anteriores de esa finalidad; los tokens sustituidos se eliminan.
- Consumir un token lo elimina: ninguna relación contiene consumed_at.
- Limpieza periódica de tokens expirados, sin frecuencia técnica seleccionada.
- Emisión y consumo son operaciones sensibles atómicas; solicitudes concurrentes no deben permitir doble consumo ni más de un token válido de la misma finalidad.
- Eliminación de cuenta incluye los tokens dependientes.
- No se agrega AK/UNIQUE para token_hash o user_id: la fuente no los declara. La política de validez exige coordinación, no se atribuye a una FK o PK que no la garantiza.

Las relaciones separadas expresan finalidad de verificación o recuperación; no se añade un atributo purpose. No se seleccionan algoritmo de hash, formato de token, canal de entrega, job de limpieza ni mecanismo de coordinación.

## EMAIL_VERIFICATION_TOKEN

**Propósito:** habilitar verificación del correo de la cuenta mediante token de uso único.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia | Predeterminado aprobado |
| --- | --- | --- | --- | --- |
| verification_token_id | Identificador del token; identificador opaco y estable | Sí | PK | No especificado |
| user_id | Identificador de la cuenta destinataria | Sí | FK → USER.user_id | No especificado |
| token_hash | Hash del token; formato y algoritmo abiertos | Sí | — | No especificado |
| created_at | Instante de emisión | Sí | — | No especificado |
| expires_at | Instante de vencimiento; vigencia de 24 horas | Sí | — | No especificado |

### Claves, restricciones y relaciones

- **PK:** verification_token_id.
- **AK / URA:** ninguna adicional documentada.
- **FK:** user_id → USER.user_id, obligatoria.
- **Unicidad:** la PK distingue tokens; no se declara unicidad adicional de hash o usuario.
- **Cardinalidad permitida por claves:** USER 1 → 0..N EMAIL_VERIFICATION_TOKEN; cada fila refiere exactamente un USER. La política común impide más de un token válido por usuario para verificación, sin agregar claves.
- **Restricciones equivalentes:** hash obligatorio; `expires_at = created_at + 24 horas` y `created_at < expires_at`. Al consumir debe cumplirse estrictamente `instante_consumo < expires_at`, además de la política de última emisión y uso único. En expires_at el token ya no es vigente. No se añade consumed_at.

### Mutabilidad, eliminación e invariantes transaccionales

El token se elimina al consumirlo o sustituirlo; los expirados se limpian periódicamente y todos se eliminan con la cuenta. No se introduce un estado persistido de consumo ni una política de renovación de expires_at.

La emisión coordina invalidación/eliminación de anteriores y registro del nuevo token. El consumo coordina validación del token vigente, verificación del correo mediante email_verified_at y eliminación del token, de forma atómica. No define un desbloqueo de USER ni una política no aprobada para cuentas bloqueadas.

**Trazabilidad:** AJ-04, VF-01/03/04/05, RF-001, RNF-003/005/007.

## PASSWORD_RESET_TOKEN

**Propósito:** habilitar recuperación de contraseña con un token de uso único, sin alterar verificación ni bloqueo de la cuenta.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia | Predeterminado aprobado |
| --- | --- | --- | --- | --- |
| reset_token_id | Identificador del token; identificador opaco y estable | Sí | PK | No especificado |
| user_id | Identificador de la cuenta destinataria | Sí | FK → USER.user_id | No especificado |
| token_hash | Hash del token; formato y algoritmo abiertos | Sí | — | No especificado |
| created_at | Instante de emisión | Sí | — | No especificado |
| expires_at | Instante de vencimiento; vigencia de 30 minutos | Sí | — | No especificado |

### Claves, restricciones y relaciones

- **PK:** reset_token_id.
- **AK / URA:** ninguna adicional documentada.
- **FK:** user_id → USER.user_id, obligatoria.
- **Unicidad:** solo las claves suministradas; no se declara una AK para hash o usuario.
- **Cardinalidad permitida por claves:** USER 1 → 0..N PASSWORD_RESET_TOKEN; cada token refiere exactamente un USER. La política común permite únicamente el último token válido por usuario para recuperación.
- **Restricciones equivalentes:** hash obligatorio; `expires_at = created_at + 30 minutos` y `created_at < expires_at`. Al consumir debe cumplirse estrictamente `instante_consumo < expires_at`, además de la política de última emisión y uso único. En expires_at el token ya no es vigente; no se añade consumed_at.

### Mutabilidad, eliminación e invariantes transaccionales

Emitir uno nuevo invalida y elimina los anteriores de recuperación. Consumirlo elimina la fila; expirados se limpian periódicamente y todos se eliminan con la cuenta.

Recuperar contraseña coordina validación/consumo del token, actualización de USER_CREDENTIAL, invalidación de todos los demás tokens de recuperación del usuario (eliminando los invalidados) e invalidación de sesiones anteriores como operación sensible atómica. No modifica email_verified_at ni transforma BLOCKED en ACTIVE. No se definen un almacenamiento de sesiones o un mecanismo de invalidación.

**Trazabilidad:** AJ-04, VF-01/03/04/05, RF-003, RNF-003/004/005.

## Aspectos abiertos del módulo

Los atributos, obligatoriedad y claves anteriores están aprobados. REV-LOG-002 aprueba los dominios lógicos. Permanecen abiertos mecanismos técnicos de email ([REV-LOG-003](../11-pendientes-y-riesgos.md#rev-log-003)), algoritmo/formato de hashes y tokens, representación temporal, defaults no declarados y mecanismos de coordinación e invalidación de sesiones. No se convierten en atributos ni decisiones de tecnología.

La fuente no define cobertura total de credenciales por USER ni procedimiento de asignación/revocación de ADMINISTRATOR; el diccionario conserva lo que garantizan las claves sin completar esas reglas. REV-LOG-001 aprueba los catálogos exactos de auditoría.

El módulo no declara congelado ni aprobado globalmente el modelo lógico. [PostgreSQL](../../../06-decisions/ADR-001-database-engine.md) sigue PROVISIONALLY ACCEPTED.

Los [dominios transversales aprobados](../13-dominios-logicos.md) precisan IDs, códigos, textos, enteros, UTC y nulabilidad sin modificar atributos o claves; plazos de 30/180 días son periodos de 24 horas transcurridas.

[Inventario](../02-inventario-relaciones.md) · [Matriz](../10-matriz-trazabilidad.md) · [Índice](../00-indice.md).
