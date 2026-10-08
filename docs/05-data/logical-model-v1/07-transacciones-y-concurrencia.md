# Transacciones y concurrencia

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md).

## Fuentes y alcance

Se contrasta con los seis diccionarios: [Identidad](04-diccionario-datos/01-identidad.md), [Evaluaciones](04-diccionario-datos/02-evaluaciones.md), [Seguimiento](04-diccionario-datos/03-seguimiento.md), [Compatibilidad](04-diccionario-datos/04-compatibilidad.md), [Contenido](04-diccionario-datos/05-contenido.md) y [Auditoría](04-diccionario-datos/06-auditoria.md); además de [dominios](13-dominios-logicos.md), [integridad](05-integridad-referencial.md) y [estados](06-estados-y-transiciones.md), estos dos últimos conformes provisionalmente por el usuario.

Atomicidad significa confirmar una operación coherente o no dejar efectos parciales. Coordinación significa preservar invariantes ante operaciones simultáneas. No se seleccionan transacciones físicas, aislamiento, bloqueos, índices, colas, triggers, reintentos ni protocolos de API.

Las FKs preservan pertenencia y existencia; no garantizan estado, autorización, completitud, tiempo o atomicidad. Los plazos se evalúan sobre instantes inequívocos con UTC de intercambio y duraciones transcurridas conforme a REV-LOG-002.

## Registro y tokens

Registro coordina confirmación adulta, cuenta y unicidad de email canónico, incluida concurrencia por variantes de mayúsculas. No se cambia email en MVP ni se crean AK de tokens. USER_REGISTERED no requiere sesión previa.

Emisión de tokens coordina nuevo token e invalidación/eliminación de anteriores de la misma finalidad. Solo el último puede ser válido. Consumo exige token vigente, último válido y uso único; dos consumos concurrentes no pueden confirmar ambos.

- Verificación: expires_at = created_at + 24 horas; consumo < expires_at. Verificación y eliminación del token se coordinan atómicamente; no desbloquea cuenta.
- Recuperación: expires_at = created_at + 30 minutos; consumo < expires_at. Consumo/eliminación, cambio de credencial, invalidación de los demás tokens de recuperación y sesiones anteriores constituyen la operación sensible atómica. No verifica email ni transforma BLOCKED en ACTIVE.
- Ambas: created_at < expires_at; expirados se limpian periódicamente sin frecuencia elegida. No se agrega consumed_at ni relación de sesiones.

**Clasificación:** Conforme en fuentes; precisión documental necesaria en la estructura anterior de 07, incorporada. **Trazabilidad:** AJ-04, VF-03..05, REV-LOG-003.

## Publicación, retiro e inicio de evaluaciones

Publicar InstrumentVersion coordina condiciones de READY, fuente respaldada, método SUM completo, preguntas required = true y exactamente una interpretación oficial por score alcanzable con respuestas completas. Una metodología incompatible o sin interpretaciones respaldadas bloquea publicación; no se inventa adaptación.

READY está congelada y vuelve a DRAFT para corregir. PUBLISHED/RETIRED conservan definición histórica. Publicaciones simultáneas no confirman más de una PUBLISHED por Instrument; publicación, retiro e inicio de Attempts se coordinan.

Inicio exige acceso personal autorizado y versión PUBLISHED. Como máximo existe un Attempt IN_PROGRESS por usuario e instrumento, **independientemente de instrument_version_id**. Dos inicios o un inicio sobre nueva versión no eluden la restricción. La FK no demuestra ese máximo condicionado por estado.

Retiro impide nuevos inicios. Un Attempt iniciado válidamente cuando la versión era PUBLISHED puede continuar y enviarse después de RETIRED si sigue IN_PROGRESS, vigente y ligado a la definición original. No se migra ni se extiende plazo.

**Clasificación:** Conforme en fuentes; precisión documental necesaria en 07, incorporada. **Trazabilidad:** AJ-01, VF-01..03, REV-LOG-004/006.

## Envío y resultado oficial

El envío coordina una vista coherente de Answers y de su definición histórica. Debe comprobar acceso, IN_PROGRESS, tiempo estrictamente anterior a expires_at, exactamente una Answer válida por cada Question y pertenencia de opciones/preguntas/versiones.

La misma operación confirma:

1. Validación de respuestas completas y de sus contribuciones históricas.
2. SUM oficial de las contribuciones de todas las opciones seleccionadas.
3. Resolución de exactamente una Interpretation oficial de esa versión cuyo rango inclusivo contiene score.
4. Creación del único ASSESSMENT_RESULT con score, interpretation_id obligatorio y versión exacta.
5. Transición a SUBMITTED y registro del instante de terminación aplicable.

No se crea una Interpretation nueva al enviar: se selecciona una definición previamente respaldada y conservada. Ante validación fallida no se deja SUBMITTED sin Result ni Result parcial. Envíos simultáneos no producen doble Result ni sobrescriben uno existente.

No se permiten omisiones, Answers ficticias, aportes por ausencia ni recálculo histórico al leer. Las FKs de Result garantizan versión, no cálculo ni coincidencia con rango; su PK garantiza máximo uno, no existencia.

**Clasificación:** Conforme en fuentes; precisión documental necesaria en 07, incorporada. **Trazabilidad:** AJ-01, VF-01/03/05, REV-LOG-004.

## Expiración, cancelación y respuestas parciales

expires_at = started_at + 720 horas. La entrega exige instante < expires_at; en igualdad el intento ya no puede enviarse, aunque la actualización persistida de status se detecte después. Ediciones, retiro o limpieza tardía no prolongan el plazo.

Al confirmar EXPIRED o CANCELLED se eliminan Answers y no existe Result; se registra ended_at. En expiración tardía ended_at = expires_at. Estados terminales no se reactivan ni se transforman entre sí.

Guardado de Answers, envío, expiración, cancelación y borrado deben coordinarse para no confirmar respuestas posteriores al cierre ni estados terminales incompatibles. La retención del Attempt terminal sin Result no conserva sus Answers parciales.

**DP-TRANS-002 — RESOLVED para el MVP:** una cancelación válida confirmada estrictamente antes de expires_at termina CANCELLED y ended_at corresponde a su cancelación efectiva. Si al intentar confirmar ya se alcanzó expires_at, corresponde EXPIRED, incluso si status todavía indica IN_PROGRESS; ended_at = expires_at. No basta solicitar la cancelación antes del plazo si se confirma después. Ambas transiciones eliminan Answers al confirmar y no crean Result.

Las operaciones concurrentes respetan un orden de confirmación coherente: una terminación ya confirmada es irreversible; ninguna operación posterior la transforma. No se extiende expires_at ni se permite SUBMITTED fuera de vigencia. La coordinación física permanece diferida.

**Clasificación:** Conforme en expiración estricta y limpieza; precisión documental necesaria en 07, incorporada. Prioridad específica: DP-TRANS-002 RESOLVED para el MVP.

## Activación y retiro de dimensiones

Activación comprueba escala entera válida y Anchors coherentes. Máximo una DimensionVersion ACTIVE por Dimension. Al reemplazarla, retiro de la anterior y activación de la nueva son atómicos y conservan ese máximo.

La creación concurrente de Measurements comprueba ACTIVE y pertenencia a su Dimension en la operación coordinada. Retiro impide uso en Measurements nuevas, pero no invalida las históricas. Corregir una Measurement dentro de la ventana del CheckIn conserva versión original y escala, incluso RETIRED.

No se remapean valores al activar otra versión ni se selecciona procedimiento físico de reemplazo.

**Clasificación:** Conforme en fuentes; precisión documental necesaria en 07, incorporada. **Trazabilidad:** AJ-03, VF-01..03 y precisión aprobada de reemplazo.

## CheckIn, revisión y dependencias editables

Crear CheckIn con al menos una Measurement y sus asociaciones incluidas es atómico. Cada Dimension aparece como máximo una vez; toda Measurement refiere versión ACTIVE al crear y su valor pertenece a esa escala.

Edición valida revisión vigente y tiempo estrictamente anterior a created_at + 168 horas. recorded_at inicial y corregido permanece en el intervalo inclusivo [created_at - 168 horas, created_at], anclado en created_at original.

La operación confirmada coordina cambios, updated_at y **revision anterior + 1**, aunque incluya varias Measurements, nota, recorded_at o ContextTags. updated_at puede estar ausente hasta primera edición y posteriormente refleja la última. Conflictos se detectan sin sobrescritura silenciosa; una operación fallida no deja cambios parciales confirmados.

No se agregan ni eliminan Measurements individuales durante edición; no se cambian Dimension/DimensionVersion originales. Correcciones de value se validan en la escala histórica. Nuevos vínculos de ContextTag requieren ACTIVE; un vínculo retirado conservado sigue siendo válido. Retirar un ContextTag no edita por sí mismo revisions de CheckIns históricos.

Edición, cierre de ventana, cambios de estado del tag y eliminación se coordinan. Borrado completo sigue permitido después de la ventana. No se añade revision a Measurement ni a otros módulos.

**Clasificación:** Conforme en fuentes; precisión documental necesaria en 07, incorporada. **Trazabilidad:** AJ-03, VF-03/05, REV-LOG-005.

## Resources, Topics y compatibilidad

Publicar Resource coordina estado, título no vacío, contenido válido según type y al menos un Topic. ARTICLE requiere body informativo no vacío y revisión editorial; EXTERNAL_LINK exige URL absoluta HTTPS válida y revisada. No se realizan solicitudes externas automáticas.

Publicación/edición/retiro y operaciones sobre RESOURCE_TOPIC se coordinan: ninguna confirmación puede dejar Resource PUBLISHED sin Topics. Cambios de asociaciones pueden modificar Guidance actual sin alterar interpretación o Result histórico. Correcciones sustanciales crean nuevo Resource; type del original no se cambia. RETIRED es terminal.

Registrar compatibilidad coordina ambas versiones del mismo catálogo, par canónico y unicidad. Solicitudes (a,b)/(b,a) convergen en la misma orientación. Declaraciones incorporadas no admiten edición/eliminación ordinaria; errores se escalan a revisión de diseño.

Las operaciones administrativas usan eventos aprobados; asociaciones editoriales requieren metadata T/topic_id y compatibilidad metadata P/ambos IDs. No se aplican esos eventos a vínculos ContextTag privados.

**Clasificación:** Conforme en fuentes; precisión documental necesaria en 07, incorporada. **Trazabilidad:** AJ-02, VF-01..03, REV-LOG-001/007..013.

## Eliminación y auditoría

Eliminar Assessment coordina Attempt, Answers y Result cuando exista; no deja un Result huérfano ni borra versión compartida. Eliminar CheckIn coordina Measurements y vínculos ContextTag; no elimina dimensiones, versiones, Anchors o tags compartidos.

Eliminar cuenta coordina USER, USER_CREDENTIAL, ADMINISTRATOR, ambos tipos de tokens, Attempts/Answers/Results y CheckIns/Measurements/vínculos. Inicios, ediciones, consumos de token y envíos concurrentes no deben reintroducir dependencias de la cuenta eliminada.

Auditoría se conserva únicamente desvinculada: retirar actor_user_id y referencias personales en target_identifier/metadata donde la cuenta sea actor o destino. USER_DELETED no conserva identificadores personales; actor_kind puede conservar clasificación histórica. Un evento de éxito no describe como confirmada una operación revertida.

No se auditan las operaciones privadas de bienestar ni sus identificadores. La retención de evento no impide supresión. DP-TRANS-001 mantiene auditoría garantizada para las operaciones auditables y exceptúa únicamente supresión de cuenta: borrado personal y desvinculación existentes son atómicos, pero la falta de USER_DELETED no invalida una supresión confirmada. No se fabrica evento, no se conserva identidad para reconstruirlo y no se comunica eliminación no confirmada.

**Clasificación:** Conforme en dependencias/privacidad; precisión documental necesaria en 07, incorporada. Tratamiento funcional de fallo: DP-TRANS-001 RESOLVED para MVP; realización técnica diferida. **Trazabilidad:** VF-03/04, REV-LOG-001.

## Decisiones pendientes para evaluación

### DP-TRANS-001

- **ID:** DP-TRANS-001.
- **Pregunta original:** ¿Cómo tratar la supresión de cuenta ante falta de garantía de auditoría o resultado incierto?
- **Fuente de resolución:** aprobación explícita del usuario de la alternativa B con nueve condiciones para MVP v1.0.
- **Status:** RESOLVED para el MVP.
- **Clasificación:** Conforme tras contraste; excepción expresa a la política general, no contradicción sin resolver.

**DP-TRANS-001 — RESOLVED para el MVP v1.0.** Las operaciones administrativas o de seguridad auditables requieren auditoría garantizada para confirmar éxito, **excepto la supresión de cuenta**. En esta excepción aprobada (alternativa B), la disponibilidad del registro de USER_DELETED no condiciona la validez de la supresión.

La eliminación de USER, todas sus dependencias personales y la desvinculación de referencias existentes en AUDIT_EVENT se confirman atómicamente. Si la supresión se confirma pero no puede registrarse USER_DELETED, sigue siendo válida y no se fabrica un evento de éxito. Si no se confirma, no se comunica como completada; ante resultado incierto se verifica el estado efectivo antes de comunicar éxito o fracaso definitivo.

No se conservan correos, identificadores personales, tokens ni registros privados para reconstruir después el evento. No se reintroduce información suprimida. Permanecen la retención máxima de auditoría de 180 días desde occurred_at y las supresiones reaplicadas antes de habilitar una restauración de backups. No se agregan entidades, atributos, estados o relaciones; el tratamiento técnico de fallos y la verificación del estado efectivo se difieren.

#### Alternativas evaluadas y resolución

La alternativa A (condicionar confirmación a supresión y auditoría garantizada) se conserva como antecedente no seleccionado. La alternativa B queda aprobada exclusivamente para supresión de cuenta con las condiciones anteriores; no se extiende a desbloqueo, recuperación, publicación u otras operaciones auditables.

**Limitación aceptada:** puede faltar USER_DELETED tras una supresión válida. Su ausencia no demuestra que la cuenta persista; un resultado incierto debe comprobarse. No se promete reconstrucción posterior del evento ni se retienen datos personales para ese propósito.

**Contraste con los seis diccionarios y AJ/VF/REV-LOG:** Identidad conserva eliminación íntegra; Evaluaciones y Seguimiento eliminan sus dependencias privadas sin destruir definiciones compartidas. Compatibilidad y Contenido conservan sus catálogos. Auditoría conserva actor_user_id opcional, desvinculación como actor/destino y USER_DELETED sin IDs personales. La excepción no altera catálogos/perfiles de REV-LOG-001, dominios de REV-LOG-002, respuestas completas/SUM, ventanas, estados o claves. AJ-01..04 y VF-01..05 conservan sus políticas; se explicita la separación entre atomicidad del borrado/desvinculación y disponibilidad del evento nuevo.

**Impacto:** Identidad, Auditoría, integridad, estados, transacciones, privacidad, decisiones, trazabilidad, registro y revisión de preparación. Coordinación física y verificación operativa permanecen diferidas; no hay decisión funcional abierta en DP-TRANS-001.

### DP-TRANS-002

- **ID:** DP-TRANS-002.
- **Pregunta original:** ¿Qué terminación corresponde cuando cancelación y expiración compiten o el plazo ya venció con status todavía IN_PROGRESS?
- **Resolución:** cancelación válida confirmada antes de expires_at → CANCELLED con ended_at de cancelación efectiva; al alcanzar o superar expires_at → EXPIRED con ended_at = expires_at. Terminales irreversibles; eliminación de Answers al confirmar cualquiera de esas transiciones.
- **Concurrencia:** orden de confirmación coherente, sin doble cierre, extensión de plazo ni SUBMITTED fuera de vigencia.
- **Contraste:** compatible con VF-02/03/04, entrega estricta, limpieza y retención de 30 días desde ended_at; no cambia relaciones, atributos, claves ni estados.
- **Impacto:** Evaluaciones, Estados, 07/08/09/10 y retención.
- **Status:** RESOLVED para el MVP.
- **Fuente:** propuesta del usuario autorizada para consolidación tras contraste sin contradicciones.

Estos IDs identifican hallazgos de revisión, no nuevos requisitos, atributos o claves. Los detalles físicos de coordinación siguen diferidos.

## Resultado de revisión

Las reglas aprobadas son coherentes; el documento anterior era una estructura insuficiente para aplicación transversal. Se incorporan precisiones: DP-TRANS-002 queda RESOLVED para el MVP; DP-TRANS-001 queda RESOLVED para el MVP por la excepción expresa de supresión de cuenta aprobada por el usuario. No se detecta contradicción comprobada interna nueva.

[Privacidad y retención](08-privacidad-eliminacion-retencion.md) · [Decisiones](09-decisiones-arquitectonicas.md) · [Matriz y hallazgos](10-matriz-trazabilidad.md).
