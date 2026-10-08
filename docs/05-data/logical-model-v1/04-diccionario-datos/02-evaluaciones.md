# Diccionario de datos — Evaluaciones

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](../12-dictamen-modelo-logico-v1.md).

## Fuentes y convenciones

La [especificación maestra](../especificacion-maestra-v1.0.md#b-evaluaciones), apartados 6..16, es la fuente de relaciones, atributos, obligatoriedad, PK, AK, FK y URA. AJ-01 y VF-01..05 aportan estados, ejecución, concurrencia y eliminación. Las precisiones posteriores sobre interpretación oficial y [REV-LOG-006](../11-pendientes-y-riesgos.md#rev-log-006) complementan la fuente preservada.

Se reproducen las **11 relaciones y 57 atributos**, sin modificar claves. `?` en la fuente significa opcional; el resto es obligatorio. ended_at es opcional en la estructura porque un intento en curso no ha terminado; las reglas de terminación y retención necesitan el instante correspondiente.

**Predeterminados:** la fuente no declara defaults para ninguno de estos atributos; todos quedan **No especificado**, incluido status, method y required. El dominio SUM aprobado no implica un default físico. Identificadores, formatos de texto/URL, precisión temporal y dominios de orden no se convierten en tipos SQL. [REV-LOG-002](../11-pendientes-y-riesgos.md#rev-log-002) aprueba los dominios lógicos; no se inventan longitudes, secuencias contiguas ni generación automática.

Las cardinalidades 0..N o 0..1 derivadas de claves expresan posibilidades estructurales, no mínimos para publicar. Las restricciones entre filas/estados se documentan como invariantes lógicas y transaccionales, sin atribuirlas a FKs ni seleccionar bloqueos, aislamiento u ORM.

## Reglas comunes de definición y ejecución

Instrument identifica el instrumento; InstrumentVersion fija su definición histórica. AssessmentAttempt registra una ejecución de esa versión; no es un CheckIn. Timeline y Trend continúan como read models; no se agregan relaciones.

La validación de READY/publicación comprueba preguntas y opciones ejecutables, todas las preguntas con QUESTION.required = true, definición SUM completa y contribuciones coherentes, respaldo documental y cobertura oficial exacta de scores alcanzables. No se inventan mínimos de referencias, valores de contribución ni interpretaciones. Una estrategia no soportada por SUM o una fuente sin interpretaciones respaldadas bloquea publicación hasta resolver la incompatibilidad.

La entrega valida acceso personal (USER ACTIVE y correo verificado), pertenencia histórica, IN_PROGRESS, vigencia estricta y exactamente una Answer válida por cada Question de la versión. Calcula el score oficial mediante SUM de las contribuciones históricas de todas las opciones seleccionadas, encuentra exactamente una interpretación válida y crea Result junto a SUBMITTED en una operación atómica. No se generan Answers ficticias ni se asignan contribuciones a omisiones. Solo se publican versiones cuya fuente y condiciones de administración permitan exigir respuestas completas; si la metodología admite omisiones específicas que el MVP no representa, la publicación queda bloqueada sin adaptar artificialmente el instrumento.

## INSTRUMENT

**Propósito:** Identidad estable del instrumento, separada de su definición versionada.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| instrument_id | Identificador de instrumento | Sí | PK |
| code | Código del instrumento | Sí | AK |
| name | Nombre | Sí | — |
| description | Descripción | Sí | — |
| purpose | Propósito | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK instrument_id; AK code; sin FK ni URA adicionales.

INSTRUMENT 1 → 0..N INSTRUMENT_VERSION; cada versión pertenece exactamente a un instrumento.

### Restricciones lógicas

La unicidad de code no implica una política no aprobada de mayúsculas o normalización. La definición ejecutable pertenece a InstrumentVersion, no a esta relación.

### Mutabilidad

No se trasladan preguntas ni scoring a INSTRUMENT. Las reglas específicas de edición de sus textos no están detalladas en la fuente; cambiar el padre no puede reescribir la definición histórica de una versión.

### Eliminación e invariantes transaccionales

Eliminar una evaluación personal no elimina el instrumento. REV-LOG-010 impide eliminación ordinaria de instrumentos/versiones referenciados por información histórica; se utiliza retiro de versiones cuando corresponda sin cascadas destructivas. La publicación concurrente de versiones se coordina por instrumento.

**Observaciones y trazabilidad:** AJ-01, VF-01/03; fuente §6. Se aplican las reglas comunes de definición y ejecución y las garantías transaccionales siguientes.

## INSTRUMENT_VERSION

**Propósito:** Definición identificable y reproducible de una versión del instrumento.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| instrument_version_id | Identificador de versión | Sí | PK |
| instrument_id | Identificador de instrumento | Sí | FK → INSTRUMENT |
| version | Entero positivo | Sí | Componente AK |
| status | DRAFT / READY / PUBLISHED / RETIRED | Sí | — |
| source_description | Descripción de la fuente | Sí | — |
| population | Población de referencia | Sí | — |
| administration_conditions | Condiciones de administración | No | — |
| license_information | Información de licencia | No | — |
| limitations | Limitaciones | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK instrument_version_id; AK (instrument_id, version); URA (instrument_id, instrument_version_id); FK instrument_id → INSTRUMENT.instrument_id.

Cada versión tiene 0..N referencias, preguntas e interpretaciones y 0..1 SCORING_DEFINITION según sus claves. Estas cardinalidades estructurales no prueban completitud para publicación. Una versión puede tener 0..N Attempts históricos.

### Restricciones lógicas

version > 0; status pertenece al conjunto declarado; máximo una PUBLISHED por instrumento. La FK compuesta de Attempt usa la URA y evita referir una versión de otro instrumento.

### Mutabilidad

DRAFT ↔ READY → PUBLISHED → RETIRED. READY permanece congelada y debe volver a DRAFT para corregirse; PUBLISHED y RETIRED conservan su definición histórica inmutable, incluidas preguntas, opciones, referencias, scoring e interpretaciones. RETIRED es terminal. No se migra un Attempt a otra versión.

### Eliminación e invariantes transaccionales

Publicación debe validar completitud y cobertura oficial de interpretaciones y coordinarse con otras publicaciones e inicios de Attempts. Retirar no equivale a eliminar: se conserva la definición necesaria para historia y Attempts válidos. No se selecciona una política física de borrado.

**Observaciones y trazabilidad:** AJ-01, VF-01/02/03; REV-LOG-006; fuente §7. Se aplican las reglas comunes de definición y ejecución y las garantías transaccionales siguientes.

## INSTRUMENT_VERSION_REFERENCE

**Propósito:** Referencias de respaldo de una versión, conservando su orden.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| instrument_version_id | Identificador de versión | Sí | PK; FK → INSTRUMENT_VERSION |
| reference_order | Orden de referencia; entero positivo, sin consecutividad obligatoria | Sí | PK |
| citation | Cita de la fuente | Sí | — |
| url | URL de referencia; detalles de validación no especificados | No | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK (instrument_version_id, reference_order); FK instrument_version_id → INSTRUMENT_VERSION.instrument_version_id; sin AK/URA adicionales.

INSTRUMENT_VERSION 1 → 0..N referencias; cada referencia pertenece exactamente a una versión.

### Restricciones lógicas

La PK impide duplicar un reference_order dentro de la misma versión. No declara unicidad de citation/url, secuencia contigua ni un mínimo numérico de referencias.

### Mutabilidad

Se aplica el ciclo editorial de la versión: cambios en DRAFT; READY exige retorno a DRAFT; referencias históricas de PUBLISHED/RETIRED se preservan.

### Eliminación e invariantes transaccionales

Forma parte de la definición histórica, no de los datos privados del usuario; eliminar un Attempt no elimina sus referencias. La validación de respaldo se realiza con la versión, no se deduce de la mera existencia de una fila.

**Observaciones y trazabilidad:** VF-01/02; fuente §8. Se aplican las reglas comunes de definición y ejecución y las garantías transaccionales siguientes.

## QUESTION

**Propósito:** Pregunta de una versión concreta y carácter requerido de su respuesta.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| question_id | Identificador de pregunta | Sí | PK |
| instrument_version_id | Identificador de versión | Sí | FK → INSTRUMENT_VERSION |
| position | Posición en la versión; entero positivo, sin consecutividad obligatoria | Sí | Componente AK |
| prompt | Enunciado | Sí | — |
| required | Indicador lógico de respuesta requerida | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK question_id; AK (instrument_version_id, position); URA (instrument_version_id, question_id); FK instrument_version_id → INSTRUMENT_VERSION.instrument_version_id.

INSTRUMENT_VERSION 1 → 0..N QUESTION; QUESTION 1 → 0..N ANSWER_OPTION. Cada pregunta pertenece a exactamente una versión y puede recibir Answers de diferentes Attempts de esa misma versión.

### Restricciones lógicas

La AK evita posiciones duplicadas dentro de una versión. En el MVP toda pregunta de una versión que se publique debe tener required = true; la entrega exige respuesta válida a cada pregunta. required permanece en el modelo y no se modifica una configuración histórica PUBLISHED/RETIRED (REV-LOG-004 resuelta). No se impone orden contiguo ni valor inicial de position.

### Mutabilidad

Enunciado, posición, pertenencia y required se conservan con la definición publicada. No se reutiliza una pregunta alterando su versión histórica.

### Eliminación e invariantes transaccionales

Una pregunta sin opciones válidas no constituye una definición ejecutable completa. Borrar respuestas personales no borra QUESTION; cambios editoriales se coordinan con validación de READY/publicación, según el ciclo de versión.

**Observaciones y trazabilidad:** AJ-01, VF-01/02; REV-LOG-004; fuente §9. Se aplican las reglas comunes de definición y ejecución y las garantías transaccionales siguientes.

## ANSWER_OPTION

**Propósito:** Opción de respuesta perteneciente a una pregunta, sin alojar la contribución del scoring.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| option_id | Identificador de opción | Sí | PK |
| question_id | Identificador de pregunta | Sí | FK → QUESTION |
| position | Posición dentro de la pregunta; entero positivo, sin consecutividad obligatoria | Sí | Componente AK |
| label | Texto de la opción | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK option_id; AK (question_id, position); URA (question_id, option_id); FK question_id → QUESTION.question_id.

QUESTION 1 → 0..N ANSWER_OPTION; cada opción pertenece exactamente a una pregunta. Una opción admite 0..1 SCORING_CONTRIBUTION por la PK option_id de esa relación.

### Restricciones lógicas

La AK impide posiciones duplicadas por pregunta. Su URA permite verificar que una Answer o contribución selecciona una opción de la pregunta correcta. No se exige aquí un número fijo de opciones.

### Mutabilidad

Se congela con la versión de su pregunta. No se agrega score ni contribution como atributo de ANSWER_OPTION; el valor depende de SCORING_CONTRIBUTION.

### Eliminación e invariantes transaccionales

Se conserva para reproducir respuestas históricas. La disponibilidad de todas las contribuciones necesarias para opciones seleccionables es una validación de completitud del scoring, no una consecuencia de esta FK.

**Observaciones y trazabilidad:** AJ-01, VF-01/02/05; fuente §10. Se aplican las reglas comunes de definición y ejecución y las garantías transaccionales siguientes.

## SCORING_DEFINITION

**Propósito:** Método de puntuación asociado a una única versión.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| instrument_version_id | Identificador de versión | Sí | PK; FK → INSTRUMENT_VERSION |
| method | SUM | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK instrument_version_id; FK instrument_version_id → INSTRUMENT_VERSION.instrument_version_id; sin AK/URA adicionales.

INSTRUMENT_VERSION 1 → 0..1 SCORING_DEFINITION; cada definición pertenece exactamente a una versión y tiene 0..N contribuciones. La PK limita a una definición, pero no obliga a que toda versión tenga una.

### Restricciones lógicas

method = SUM. La ejecución válida requiere definición y contribuciones suficientes. No se incorporan subescalas, múltiples scores, fórmulas arbitrarias ni scripts.

### Mutabilidad

Definición congelada con la versión. Una corrección no puede cambiar el cálculo de Attempts históricos.

### Eliminación e invariantes transaccionales

Completar y validar el scoring precede a la publicación; su existencia sola no garantiza que SUM esté definido para todos los casos. Se conserva al eliminar datos personales.

**Observaciones y trazabilidad:** AJ-01, VF-01/02/05; REV-LOG-004; fuente §11. Se aplican las reglas comunes de definición y ejecución y las garantías transaccionales siguientes.

## SCORING_CONTRIBUTION

**Propósito:** Contribución de una opción al SUM de la versión a la que pertenece su pregunta.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| option_id | Identificador de opción | Sí | PK; Componente FK |
| question_id | Identificador de pregunta | Sí | Componente FK |
| instrument_version_id | Identificador de versión | Sí | FK; Componente FK |
| contribution | Entero con signo | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK option_id; sin AK/URA adicionales. FK instrument_version_id → SCORING_DEFINITION.instrument_version_id; FK (instrument_version_id, question_id) → QUESTION.(instrument_version_id, question_id); FK (question_id, option_id) → ANSWER_OPTION.(question_id, option_id).

Cada contribución refiere exactamente una definición, una pregunta y una opción coherentes; una opción tiene como máximo una contribución. Una definición tiene 0..N contribuciones.

### Restricciones lógicas

Las dos FKs compuestas verifican simultáneamente versión de la pregunta y pertenencia de la opción. La FK simple exige la definición de scoring de esa versión. contribution admite valores negativos y cero; no se inventa una cota.

### Mutabilidad

Contribuciones históricas inmutables desde publicación; el scoring normal o invertido se expresa con los valores respaldados del instrumento, sin modificar la etiqueta de opción ni cambiar la versión.

### Eliminación e invariantes transaccionales

La PK no obliga a que todas las opciones tengan contribución: esa cobertura se valida antes de ejecutar/publicar. Se conserva ante eliminación de Attempts; no hay una contribución implícita para una ausencia de Answer.

**Observaciones y trazabilidad:** AJ-01, VF-01/02/05; REV-LOG-004; fuente §12. Se aplican las reglas comunes de definición y ejecución y las garantías transaccionales siguientes.

## ASSESSMENT_ATTEMPT

**Propósito:** Ejecución personal de un instrumento ligada a su versión exacta desde el inicio.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| attempt_id | Identificador de intento | Sí | PK |
| user_id | Identificador de propietario | Sí | FK → USER |
| instrument_id | Identificador de instrumento | Sí | Componente FK |
| instrument_version_id | Identificador de versión histórica | Sí | Componente FK; URA |
| status | IN_PROGRESS / SUBMITTED / EXPIRED / CANCELLED | Sí | — |
| started_at | Instante de inicio | Sí | — |
| expires_at | Instante de expiración | Sí | — |
| ended_at | Instante de terminación; condicional según estado | No | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK attempt_id; URA (attempt_id, instrument_version_id); FK user_id → USER.user_id; FK (instrument_id, instrument_version_id) → INSTRUMENT_VERSION.(instrument_id, instrument_version_id); sin AK adicionales.

USER 1 → 0..N Attempts; cada Attempt pertenece exactamente a un User y una versión de su instrumento. Attempt 1 → 0..N ANSWER y 0..1 ASSESSMENT_RESULT según claves; SUBMITTED exige exactamente un Result.

### Restricciones lógicas

Máximo un IN_PROGRESS por usuario e instrumento, independientemente de versión; expires_at = started_at + 720 horas. La entrega exige exactamente una Answer válida por cada Question de la versión e instante de envío < expires_at. En expires_at ya no puede enviarse aunque la actualización de status todavía no se haya ejecutado.

### Mutabilidad

Solo IN_PROGRESS → SUBMITTED, EXPIRED o CANCELLED; terminales sin reactivación. Al iniciar se requiere versión PUBLISHED y acceso personal autorizado. Una versión RETIRED no admite nuevos Attempts; uno iniciado válidamente en PUBLISHED puede continuar/enviarse tras retiro si sigue IN_PROGRESS, no expiró y conserva versión/respuestas históricas (REV-LOG-006).

### Eliminación e invariantes transaccionales

**DP-TRANS-002 — RESOLVED para el MVP:** una cancelación válida confirmada estrictamente antes de expires_at termina CANCELLED y ended_at corresponde a su cancelación efectiva. Si al intentar confirmar ya se alcanzó expires_at, corresponde EXPIRED, incluso si status todavía indica IN_PROGRESS; ended_at = expires_at. No basta solicitar la cancelación antes del plazo si se confirma después. Ambas transiciones eliminan Answers al confirmar y no crean Result.

Las operaciones concurrentes respetan un orden de confirmación coherente: una terminación ya confirmada es irreversible; ninguna operación posterior la transforma. No se extiende expires_at ni se permite SUBMITTED fuera de vigencia. La coordinación física permanece diferida.

Al expirar o cancelar se eliminan Answers, no se crea Result y se registra ended_at; en expiración tardía ended_at = expires_at. Terminales sin Result se retienen 30 días desde ended_at. Eliminación individual permitida en cualquier momento y eliminación con la cuenta, coordinando Answers y Result. Inicio/finalización, retiro, borrado y solicitudes simultáneas deben evitar intentos activos duplicados o estados parciales; no se añade revision ni submitted_at.

**Observaciones y trazabilidad:** AJ-01, VF-01..04; REV-LOG-006; fuente §13. Se aplican las reglas comunes de definición y ejecución y las garantías transaccionales siguientes.

## ANSWER

**Propósito:** Selección de una opción por pregunta dentro del Attempt y su versión histórica.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| attempt_id | Identificador de intento | Sí | PK; Componente FK |
| instrument_version_id | Identificador de versión histórica | Sí | Componente FK |
| question_id | Identificador de pregunta | Sí | PK; Componente FK |
| option_id | Identificador de opción seleccionada | Sí | Componente FK |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK (attempt_id, question_id); sin AK/URA adicionales. FK (attempt_id, instrument_version_id) → ASSESSMENT_ATTEMPT.(attempt_id, instrument_version_id); FK (instrument_version_id, question_id) → QUESTION.(instrument_version_id, question_id); FK (question_id, option_id) → ANSWER_OPTION.(question_id, option_id).

Cada Answer pertenece exactamente a un Attempt, una pregunta de su versión y una opción de esa pregunta. La PK permite como máximo una respuesta por pregunta en cada Attempt.

### Restricciones lógicas

Todos los componentes de las FKs son obligatorios. No cabe una opción sin pregunta, ni una pregunta de otra versión, ni un Answer de distinta versión del Attempt. La ausencia de Answer se permite durante la preparación del Attempt, pero impide SUBMITTED en el MVP. Una fila con option_id nulo no está permitida. La PK prueba como máximo una respuesta por pregunta; la validación de completitud prueba la existencia de una para cada Question.

### Mutabilidad

Respuestas editables solo en Attempt IN_PROGRESS vigente; se conservan históricas al enviar. No se cambia instrument_version_id para adaptar un Attempt a una publicación nueva. Conflictos concurrentes no deben sobrescribir silenciosamente.

### Eliminación e invariantes transaccionales

Se eliminan al expirar/cancelar o eliminar el Attempt/la cuenta. Guardado, entrega y expiración concurrentes deben coordinarse: no se admite guardar después del cierre ni crear un Result con un conjunto de respuestas incoherente. No se añade answer_id ni revision.

**Observaciones y trazabilidad:** AJ-01, VF-01..04; REV-LOG-004; fuente §14. Se aplican las reglas comunes de definición y ejecución y las garantías transaccionales siguientes.

## INTERPRETATION

**Propósito:** Interpretación oficial informativa de un rango de scores en una versión concreta.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| interpretation_id | Identificador de interpretación | Sí | PK |
| instrument_version_id | Identificador de versión | Sí | FK → INSTRUMENT_VERSION; URA |
| label | Etiqueta oficial | Sí | — |
| description | Descripción respaldada | Sí | — |
| limitations | Limitaciones de interpretación | No | — |
| lower_bound | Entero con signo; límite inclusivo | Sí | — |
| upper_bound | Entero con signo; límite inclusivo | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK interpretation_id; URA (instrument_version_id, interpretation_id); FK instrument_version_id → INSTRUMENT_VERSION.instrument_version_id; sin AK adicionales.

INSTRUMENT_VERSION 1 → 0..N INTERPRETATION; cada interpretación pertenece exactamente a una versión y puede ser referida por 0..N Results de esa misma versión.

### Restricciones lógicas

Para cada score alcanzable s mediante respuestas completas válidas debe existir exactamente una interpretación de esa versión tal que lower_bound ≤ s ≤ upper_bound. Los límites son inclusivos; deben definir un rango válido. REV-LOG-002 aprueba límites enteros con signo y lower_bound ≤ upper_bound; no se elige su tipo físico. La cobertura no se confunde con cubrir todos los enteros entre el mínimo y el máximo si hay valores inalcanzables.

### Mutabilidad

Rangos, texto y pertenencia se congelan con la versión publicada; no se recalifican Results históricos cambiando interpretaciones. La información es orientativa, no un diagnóstico.

### Eliminación e invariantes transaccionales

Sin interpretaciones oficiales respaldadas que cubran cada score alcanzable, la versión no puede publicarse (AJ-01 aclarado). No se crean etiquetas/rangos inventados. Existencia de una FK no demuestra cobertura ni coincidencia entre score y rango: se validan con scoring y al finalizar. Eliminación personal no borra la definición histórica.

**Observaciones y trazabilidad:** AJ-01, VF-01/02; C-LOG-001, REV-LOG-002/004; fuente §15. Se aplican las reglas comunes de definición y ejecución y las garantías transaccionales siguientes.

## ASSESSMENT_RESULT

**Propósito:** Resultado histórico único de un Attempt enviado, con score e interpretación de la versión original.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| attempt_id | Identificador de intento | Sí | PK; FK → ASSESSMENT_ATTEMPT; Componente FK |
| instrument_version_id | Identificador de versión histórica | Sí | Componente FK |
| score | Entero con signo | Sí | — |
| interpretation_id | Identificador de interpretación oficial | Sí | Componente FK |
| calculated_at | Instante de cálculo | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK attempt_id; FK simple attempt_id → ASSESSMENT_ATTEMPT.attempt_id, conservada tal como fue aprobada; FK (attempt_id, instrument_version_id) → ASSESSMENT_ATTEMPT.(attempt_id, instrument_version_id); FK (instrument_version_id, interpretation_id) → INTERPRETATION.(instrument_version_id, interpretation_id); sin AK/URA adicionales.

Attempt 1 → 0..1 Result por PK; SUBMITTED exige exactamente uno y los otros estados no tienen Result. Cada Result tiene exactamente una interpretación de su misma versión.

### Restricciones lógicas

score entero con signo; interpretation_id obligatorio. Las FKs impiden cruce de versiones, pero no calculan SUM ni verifican que score pertenezca al rango de interpretation_id. La unicidad de PK tampoco garantiza por sí sola existencia del Result en SUBMITTED.

### Mutabilidad

Se crea al enviar y permanece histórico e inmutable, al igual que las respuestas enviadas y su versión. Leerlo no recalcula ni cambia su interpretación. No se agrega result_id, estado o timestamp de envío.

### Eliminación e invariantes transaccionales

Cálculo SUM, selección de la interpretación, inserción de Result y transición a SUBMITTED son atómicos. Finalizaciones concurrentes no producen doble Result ni sobrescriben uno existente. Borrado individual del Assessment o de la cuenta elimina Result junto al Attempt y Answers; no altera definiciones históricas.

**Observaciones y trazabilidad:** AJ-01, VF-01..05; fuente §16. Se aplican las reglas comunes de definición y ejecución y las garantías transaccionales siguientes.

## Concurrencia, privacidad y conservación

- **Publicación/inicio:** coordinar el máximo de una PUBLISHED por instrumento y de un IN_PROGRESS por usuario/instrumento. La FK no comprueba el estado de versión al iniciar. La coordinación determina si el inicio ocurre válidamente antes del retiro; una versión ya retirada no admite inicios nuevos.
- **Edición/finalización:** validar estado y tiempo al confirmar la operación; coordinar guardado de Answers, envío, expiración, cancelación y borrado. No permitir doble finalización, sobrescritura silenciosa ni respuestas añadidas a un Attempt cerrado. La estrategia técnica de detección de conflictos permanece abierta; no se añade revision al módulo.
- **Resultado:** conservar una vista coherente de las respuestas y su definición histórica durante SUM y resolución de interpretación. Si falla alguna validación no se deja SUBMITTED sin Result ni un Result parcial. Las FKs compuestas aseguran pertenencia, no atomicidad.
- **Expiración:** el plazo no se prolonga por retiro, edición ni ejecución tardía de limpieza. En expiración tardía ended_at = expires_at; eliminar Answers y no crear Result. CANCELLED y EXPIRED son terminales sin resultado y sujetos a retención de 30 días desde ended_at.
- **Supresión:** eliminar individualmente el Assessment en cualquier momento o con la cuenta, coordinando Attempt, Answers y Result. No conservar un Result huérfano ni borrar el catálogo histórico compartido como consecuencia del borrado personal. No se define CASCADE físico ni una operación aislada para borrar Result dejando SUBMITTED.
- **Privacidad:** el propietario y la autorización se validan por separado de las FKs. ADMINISTRATOR representa habilitación administrativa y no otorga acceso automático a información privada. No se inventa matriz de permisos.
- **Auditoría y backups:** no almacenar información privada de bienestar ni identificadores personales de Attempts/Results en AUDIT_EVENT; desvincular referencias personales inmediatamente al eliminar la cuenta. Auditoría: 180 días desde occurred_at; backups cifrados: 30 días, con supresiones reaplicadas antes de habilitar una restauración. No se fija retención automática de Results enviados: siguen sujetos a eliminación autorizada.

## REV-LOG-004

- **ID:** REV-LOG-004.
- **Pregunta original:** ¿Cómo debe tratar SUM una Question opcional omitida?
- **Resolución aprobada:** respuestas completas obligatorias para el alcance MVP v1.0.
- **Status:** RESOLVED para el MVP.
- **Impacto:** QUESTION, ANSWER, SCORING_DEFINITION, SCORING_CONTRIBUTION, INTERPRETATION, ASSESSMENT_ATTEMPT y ASSESSMENT_RESULT; publicación, completitud y cobertura.
- **Fuente:** resolución explícita del usuario posterior a la revisión de las tres alternativas; complementa la especificación maestra sin modificarla.

### Reglas aprobadas y alcance del cierre

1. Toda Question de una InstrumentVersion que se publique debe tener required = true.
2. SUBMITTED exige exactamente una Answer válida por cada Question de la versión histórica del Attempt. Las FKs y PK evitan cruces y duplicados; la existencia de todas las respuestas requiere validación transaccional.
3. El score oficial es SUM de las contribuciones de todas las opciones seleccionadas, con enteros con signo. No se generan Answers ficticias ni se asignan contribuciones a omisiones.
4. La cobertura oficial de interpretaciones se comprueba sobre todas las puntuaciones alcanzables mediante respuestas completas válidas: cada una corresponde exactamente a una interpretación respaldada.
5. Solo pueden publicarse instrumentos cuya fuente y condiciones de administración permitan exigir respuestas completas. Si las reglas específicas de omisión no son representables en el MVP, se bloquea publicación; no se modifica la metodología para adaptarla artificialmente.
6. QUESTION.required permanece como atributo aprobado; no se agrega ni elimina ningún atributo, relación o clave.
7. InstrumentVersions PUBLISHED y RETIRED conservan su configuración histórica. La política no autoriza migrarlas ni reconfigurar sus Attempts/Results.
8. Respuesta completa no demuestra validez psicométrica ni comparabilidad entre versiones. Se mantienen respaldo metodológico y compatibilidad explícita por pares, simétrica y no transitiva.

La alternativa 2 fue aprobada con estas condiciones para el MVP. No se conserva como pendiente el tratamiento de omisiones en este alcance.

### Opciones futuras no aprobadas

- **Alternativa 1:** SUM de respuestas presentes, omisión sin aporte. No está autorizada para el MVP; requeriría respaldo metodológico, validar de nuevo scores alcanzables, cobertura y comparabilidad, y aprobar su trazabilidad histórica antes de aplicarla.
- **Alternativa 3:** política específica de omisión respaldada por el instrumento. No está autorizada para el MVP; habría que evaluar compatibilidad con SUM y representación en el modelo, presentando para aprobación cualquier cambio necesario. No se presupone una contribución ni se incorporan atributos.

## Diferencias y aspectos abiertos

**C-LOG-001** sigue siendo un cambio explícito entre versiones documentales: el conceptual v0.1 condicionaba Interpretation a la fuente; AJ-01 exige interpretación oficial obligatoria y cobertura exacta, bloqueando publicación si no puede respaldarse. Se conserva el conceptual original y se remite al [registro del cambio](../11-pendientes-y-riesgos.md#conflicto-comprobado).

No se detecta una contradicción adicional entre las once relaciones de la fuente y las precisiones aprobadas. REV-LOG-004 queda resuelto para el MVP por decisión explícita del usuario, sin alterar FKs. REV-LOG-001/002 aprueban dominios y catálogos de auditoría; no se eligen tipos, formatos ni mecanismos de concurrencia.

PostgreSQL sigue [PROVISIONALLY ACCEPTED](../../../06-decisions/ADR-001-database-engine.md); backend, frontend, persistence y authentication continúan abiertos. El diccionario de Seguimiento continúa en su archivo propio; los demás módulos no se desarrollan en esta entrega. El [dictamen definitivo](../12-dictamen-modelo-logico-v1.md) registra APPROVED / FROZEN para la línea base lógica; los detalles físicos siguen diferidos.

Los [dominios transversales aprobados](../13-dominios-logicos.md) precisan IDs, códigos, textos, enteros, UTC y nulabilidad sin modificar atributos o claves; plazos de 30/180 días son periodos de 24 horas transcurridas.

[Inventario](../02-inventario-relaciones.md) · [Matriz](../10-matriz-trazabilidad.md) · [Pendientes](../11-pendientes-y-riesgos.md) · [Índice](../00-indice.md).
