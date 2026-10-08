# Privacidad, eliminación y retención

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md).

## Fuentes y alcance

Se contrasta con [Identidad](04-diccionario-datos/01-identidad.md), [Evaluaciones](04-diccionario-datos/02-evaluaciones.md), [Seguimiento](04-diccionario-datos/03-seguimiento.md), [Compatibilidad](04-diccionario-datos/04-compatibilidad.md), [Contenido](04-diccionario-datos/05-contenido.md) y [Auditoría](04-diccionario-datos/06-auditoria.md); [dominios](13-dominios-logicos.md), [integridad](05-integridad-referencial.md) y [estados](06-estados-y-transiciones.md) preservan valores, referencias y guardas.

Preservar definiciones históricas no autoriza conservar datos personales frente a una eliminación aprobada. Retiro del catálogo, eliminación personal y vencimiento de retención son operaciones distintas; no se fusionan en un estado nuevo.

## Autorización y minimización

Acceso personal requiere USER ACTIVE y correo verificado, además de autorización sobre los datos. ADMINISTRATOR expresa habilitación administrativa; no concede acceso automático a Answers, Results, CheckIns, Measurements, notas o vínculos privados.

No se inventa una matriz de permisos. Topic editorial y ContextTag personal no se confunden. Guidance actual deriva de contenido; no transforma la información personal en dato público ni genera recomendaciones clínicas.

AUDIT_EVENT admite solo administración/seguridad. Se prohíben bienestar privado, IDs de Attempts/CheckIns/Results, contraseñas, hashes, tokens, correos, IP, user-agent, identificadores de sesión, metadata arbitraria y payloads libres. changed_fields solo contiene nombres/categorías permitidos, sin valores anteriores/nuevos.

**Clasificación:** Conforme; las precisiones de aplicación se incorporan sin ampliar permisos. **Trazabilidad:** RN-025/026, VF-04/05, REV-LOG-001.

## Eliminación individual de Assessments

Permitida en cualquier momento, independientemente de que Attempt esté en curso o terminal. La operación coordina eliminación de ASSESSMENT_ATTEMPT, ANSWER y ASSESSMENT_RESULT cuando exista.

No se ofrece borrar solo Result dejando SUBMITTED sin resultado. La definición de InstrumentVersion, preguntas, opciones, ScoringDefinition, contribuciones e Interpretation es compartida y no se elimina por suprimir un Assessment.

Un Result histórico es inmutable mientras se conserva; su eliminación autorizada no es una edición o recálculo. No se conserva una copia identificable del registro privado en auditoría ni se registra su ID como destino.

**Clasificación:** Conforme en fuentes; precisión documental necesaria en 08, incorporada. **Trazabilidad:** VF-04; diccionario de Evaluaciones.

## Eliminación individual de CheckIns

Permitida en cualquier momento, incluso después de created_at + 168 horas. Se coordina CHECK_IN, MEASUREMENT y CHECK_IN_CONTEXT_TAG; la nota pertenece al propio CheckIn.

La prohibición de agregar/eliminar Measurements individuales durante edición no impide eliminar el CheckIn completo. Dimensiones, versiones, Anchors y ContextTags compartidos permanecen.

Borrado concurrente con edición no debe dejar dependencias huérfanas ni reintroducir registros eliminados. Suprimir no se registra como evento privado de bienestar.

**Clasificación:** Conforme en fuentes; precisión documental necesaria en 08, incorporada. **Trazabilidad:** AJ-03, VF-03/04; Seguimiento y Estados.

## Eliminación de cuenta y dependencias

La eliminación completa incluye:

- USER_CREDENTIAL, ADMINISTRATOR y ambos tipos de tokens dependientes.
- ASSESSMENT_ATTEMPT, sus ANSWER y ASSESSMENT_RESULT.
- CHECK_IN, MEASUREMENT y CHECK_IN_CONTEXT_TAG, incluyendo la nota almacenada en CheckIn.
- USER, con referencias de auditoría desvinculadas inmediatamente.

Orden o mecanismo físico de ejecución no se elige. La operación debe cumplir integridad, supresión autorizada y coordinación con escrituras concurrentes. No se conserva una dependencia privada por invocar historia o una FK.

Catálogos de instrumentos/dimensiones, definiciones versionadas, compatibilidades, Topics y Resources no son dependencias personales de la cuenta. No se eliminan por la supresión del usuario. No se agrega relación de sesiones ni otro atributo para implementar el proceso.

**Clasificación:** Conforme en fuentes; precisión documental necesaria en 08, incorporada. **Trazabilidad:** VF-03/04; Identidad e Integridad.

## Desvinculación y conservación de auditoría

La supresión retira actor_user_id de eventos donde la cuenta fue actor y referencias personales de target_identifier/metadata donde fue actor o destino, incluidos eventos emitidos por otro usuario.

USER_DELETED se registra sin identificadores personales persistentes. actor_kind puede conservar su clasificación histórica USER/ADMINISTRATOR después de retirar actor_user_id; no se cambia artificialmente a ANONYMOUS ni se retiene un identificador sustituto.

La metadata futura aprobada no admite IDs personales, pero los datos existentes requieren retirar cualquier referencia personal que contengan. No se limita limpieza al perfil nuevo ni se reemplaza el ID eliminado por correo/hash reidentificable.

T requiere topic_id en acciones de asociación editorial; P requiere version_a_id/version_b_id de catálogo en COMPATIBILITY_DECLARED. Son IDs compartidos, no IDs de bienestar privado, y no crean FKs nuevas. Sus obligaciones condicionales no impiden desvincular cuentas.

La conservación del evento es compatible con eliminar su referencia personal, no con retener identidad por 180 días. No se impone una inmutabilidad de auditoría que impida supresión.

**Clasificación:** Conforme en REV-LOG-001/002; precisión documental necesaria en 08, incorporada.

## Expiración, cancelación y tokens

EXPIRED/CANCELLED no tienen Result y eliminan Answers al confirmar cierre; los 30 días de retención del Attempt no conservan respuestas parciales. En expiración tardía ended_at = expires_at, sin extender vigencia ni retención.

Tokens consumidos o sustituidos se eliminan; expirados se limpian periódicamente. Recuperación exitosa invalida/elimina los demás tokens de recuperación del usuario e invalida sesiones anteriores. No se crea consumed_at, archivo de tokens ni retención de secretos no aprobada.

**Clasificación:** Conforme; precisión documental necesaria en 08, incorporada. **Trazabilidad:** AJ-04, VF-04/05.

## Retenciones y referencias temporales

[REV-LOG-002](13-dominios-logicos.md#instantes-y-duraciones) interpreta días como periodos de 24 horas transcurridas. UTC es referencia de intercambio; horarios locales o limpiezas tardías no cambian los límites.

- **Attempts terminales sin Result:** 30 días = 720 horas desde ended_at. Se aplica a EXPIRED/CANCELLED, no a SUBMITTED. Expiración detectada tarde usa ended_at = expires_at; no reinicia plazo con la detección.
- **AUDIT_EVENT:** máximo 180 días = 4320 horas desde occurred_at. Desvinculación, registro tardío o restauración no reinician periodo. Una frecuencia técnica no definida no permite superar el máximo.
- **Backups cifrados:** retención de 30 días = 720 horas. No se añade una relación/atributo de backup ni se selecciona una programación o infraestructura.
- **Assessments enviados y CheckIns:** no se fija retención automática de 30/180 días; continúan sujetos a eliminación autorizada.
- **Tokens:** los plazos de 24 horas/30 minutos son vigencia de uso, no una nueva política de archivo. Limpieza periódica no renueva validez.

La retención cifrada de backups se aplica junto a la obligación de reaplicar supresiones antes de habilitar una restauración; no se fija un procedimiento nuevo de purga inmediata de copias. Una copia no autoriza reactivar datos suprimidos ni reiniciar sus plazos. La conservación del catálogo tampoco autoriza conservación indefinida de datos de usuario.

**Clasificación:** Conforme en fuentes; precisión documental necesaria en 08, incorporada. **Trazabilidad:** VF-04, REV-LOG-002.

## Backups y restauración

Backups deben estar cifrados y respetar retención aprobada. Antes de habilitar el servicio sobre una restauración deben reaplicarse las supresiones autorizadas, para no reintroducir cuentas, Assessments, CheckIns, dependencias o referencias personales ya eliminadas.

La restauración también debe respetar retenciones vigentes y la desvinculación de auditoría; no convierte eventos vencidos en vigentes ni resetea occurred_at/ended_at. Definiciones históricas conservadas no sirven como autorización para recuperar bienestar privado suprimido.

El resultado exigido está aprobado: servicio habilitado únicamente con supresiones reaplicadas. No se afirma que la copia antigua, por sí sola, contenga conocimiento actualizado de supresiones posteriores. La fuente de esa información, su conservación y coordinación técnica con la restauración quedan al diseño correspondiente; no se introduce un registro, ledger, servicio, tabla o estado nuevo.

**Clasificación:** Conforme en política; precisión documental necesaria en 08, incorporada. Mecanismo técnico: Decisión pendiente de diseño físico/operativo, sin selección en esta revisión.

## Catálogos y límites del MVP

No se elimina ordinariamente Instrument, Dimension o versión referenciada por información histórica. Se usa RETIRED donde el modelo tiene ese estado. Topic/Resource no admiten eliminación física ordinaria; compatibilidades no se editan/eliminan ordinariamente y errores requieren revisión de diseño.

Retirar Resource lo excluye de Guidance, sin cambiar Interpretation/Result histórico. Associations editoriales pueden modificarse autorizadamente conservando las políticas de Topics e historia; no se agrega historial de Guidance o revocación trazable de compatibilidad.

No se introducen cascadas destructivas ni políticas para catálogo no referenciado que todavía no hayan sido aprobadas.

**Clasificación:** Conforme; trazabilidad a REV-LOG-009/010/012/013.

## Resoluciones y resultado de contraste

**DP-TRANS-001 — RESOLVED para el MVP v1.0.** Las operaciones administrativas o de seguridad auditables requieren auditoría garantizada para confirmar éxito, **excepto la supresión de cuenta**. En esta excepción aprobada (alternativa B), la disponibilidad del registro de USER_DELETED no condiciona la validez de la supresión.

La eliminación de USER, todas sus dependencias personales y la desvinculación de referencias existentes en AUDIT_EVENT se confirman atómicamente. Si la supresión se confirma pero no puede registrarse USER_DELETED, sigue siendo válida y no se fabrica un evento de éxito. Si no se confirma, no se comunica como completada; ante resultado incierto se verifica el estado efectivo antes de comunicar éxito o fracaso definitivo.

No se conservan correos, identificadores personales, tokens ni registros privados para reconstruir después el evento. No se reintroduce información suprimida. Permanecen la retención máxima de auditoría de 180 días desde occurred_at y las supresiones reaplicadas antes de habilitar una restauración de backups. No se agregan entidades, atributos, estados o relaciones; el tratamiento técnico de fallos y la verificación del estado efectivo se difieren.

**Trazabilidad:** [resolución aprobada](07-transacciones-y-concurrencia.md#dp-trans-001).

[DP-TRANS-002](07-transacciones-y-concurrencia.md#dp-trans-002) está RESOLVED para el MVP: cancelación válida confirmada antes de expires_at termina CANCELLED con ended_at efectivo; al vencer termina EXPIRED con ended_at = expires_at, aunque status aún indique IN_PROGRESS. Terminales irreversibles y limpieza de Answers al confirmar. Retención de 30 días desde ese ended_at, sin reinicio por detección tardía ni extensión de vigencia.

No se detecta contradicción comprobada interna nueva entre supresión, historia y retención. Las faltas de desarrollo del documento anterior son precisiones documentales necesarias ya incorporadas; mecanismos físicos permanecen pendientes.

[Transacciones](07-transacciones-y-concurrencia.md) · [Decisiones](09-decisiones-arquitectonicas.md) · [Matriz y hallazgos](10-matriz-trazabilidad.md).
