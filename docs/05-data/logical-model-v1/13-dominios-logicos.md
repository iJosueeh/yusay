# Dominios lógicos transversales

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md).

## Fuente y contraste

La aprobación explícita del usuario incorpora la propuesta transversal con precisiones sobre IDs, orden, posiciones, límites, UTC, duraciones y nulabilidad. Se contrastó con AJ-01..04, VF-01..05 y las políticas posteriores sin contradicciones comprobables.

Enteros con signo mantienen SUM; límites inclusivos no cambian Interpretation. Orden canónico mantiene las claves de compatibilidad. UTC y duración transcurrida precisan ventanas existentes. Metadata obligatoria por acción T/P es una restricción condicional, compatible con su opcionalidad estructural, como body/external_url por tipo de Resource. Desvinculación de actor preserva el evento sin conservar identidad personal.

Esta capa complementa la [especificación maestra](especificacion-maestra-v1.0.md), preservada literalmente. No agrega atributos, relaciones, claves ni nuevos IDs de requisitos.

## Identificadores y orden canónico

Identificadores opacos, estables, no vacíos y sin significado de negocio. Su igualdad es coherente en PK, AK/URA que los incluyen y referencias; cada FK utiliza el dominio de su destino. Unicidad según las claves existentes, sin imponer unicidad global entre relaciones.

Los IDs de compatibilidad disponen de un orden total estricto, estable y documentado. Se conserva version_a_id < version_b_id y se orienta cada par en una única dirección. El orden no representa precedencia temporal, número de versión ni llegada de solicitudes. Consultar el par inverso produce el mismo par canónico; un par reflexivo no se almacena.

Tipo físico, generación, codificación y mecanismo concreto de comparación quedan al diseño físico. No se elige UUID, numeración secuencial, colación ni orden textual incidental.

## Códigos, nombres y etiquetas

code es texto identificador no vacío, con espacios exteriores eliminados al incorporar el valor; comparación sensible a mayúsculas para las AK de códigos. No se impone ASCII, expresión regular ni longitud. La unicidad se limita a cada clave aprobada, sin unicidad transversal.

Topic conserva code y significado estables. Los otros catálogos no reciben una regla de mutabilidad nueva por compartir dominio de código. No se renormalizan claves históricas ni se permite producir colisiones silenciosas.

Nombres y etiquetas representan texto Unicode significativo, sin unicidad cuando la fuente no declara AK. No se selecciona normalización Unicode ni formato físico.

## Textos obligatorios y opcionales

Obligatorio significa presente; no equivale automáticamente a no vacío. Para restricciones explícitas de no vacío, un texto sin caracteres distintos de espacios se considera vacío.

RESOURCE.title es obligatorio y no vacío desde DRAFT. ARTICLE publicado requiere body no vacío, informativo y revisado editorialmente. Nombres, enunciados, etiquetas, citas y justificaciones tienen contenido significativo cuando son necesarios para uso válido; esto no agrega una prohibición universal de borradores incompletos.

Opcional permite ausencia; si el atributo está presente cumple su dominio. No se convierten globalmente textos vacíos en ausencia ni se selecciona formato de body. Nota personal y documentación oficial no se normalizan de manera que cambie su significado.

## Enteros y rangos

- **Enteros positivos:** version de InstrumentVersion/DimensionVersion, step, revision, position de Question/AnswerOption y reference_order. Todos > 0; posiciones y referencias no tienen que ser consecutivas.
- **Enteros con signo:** contribution, score y lower_bound/upper_bound de Interpretation. Admiten negativos y cero; lower_bound ≤ upper_bound y ambos límites inclusivos.
- **Escalas:** min_value, max_value, Anchor.value y Measurement.value son enteros; min_value < max_value, step > 0 y el extremo superior es alcanzable mediante step. Pertenecer a la escala exige límites inclusivos y divisibilidad.
- **Revisión:** CHECK_IN.revision inicia en 1 e incrementa en uno por operación transaccional de edición confirmada, aunque incluya múltiples cambios.

No se fija capacidad física ni cota numérica artificial. La futura representación debe admitir todo score alcanzable sin truncamiento o desbordamiento; la cobertura de Interpretation se verifica sobre respuestas completas válidas, conforme a REV-LOG-004.

## Instantes y duraciones

Todos los timestamps representan instantes inequívocos en una línea temporal común; UTC es la referencia de intercambio. Comparaciones y duraciones se evalúan sobre instantes, no horas locales ambiguas. Presentación en otra zona no modifica el instante ni agrega un atributo de zona.

- Token de verificación: expires_at = created_at + 24 horas; created_at < expires_at y consumo estrictamente antes de expires_at.
- Token de recuperación: expires_at = created_at + 30 minutos; created_at < expires_at y consumo estrictamente antes de expires_at.
- Attempt: expires_at = started_at + 720 horas; entrega estrictamente anterior a expires_at; expiración tardía registra ended_at = expires_at.
- CheckIn: recorded_at inicial o corregido en [created_at - 168 horas, created_at], inclusivo; edición estrictamente antes de created_at + 168 horas.
- Retenciones de 30 y 180 días: periodos de 24 horas transcurridas, equivalentes a 720 y 4320 horas respectivamente. Auditoría se cuenta desde occurred_at; Attempts terminales sin Result desde ended_at. Backups mantienen el periodo aprobado de 30 días.
- updated_at puede estar ausente hasta primera edición confirmada y luego refleja la última modificación.

No se fija precisión temporal, tipo PostgreSQL, infraestructura de reloj ni función de generación automática. No se agregan restricciones temporales entre atributos que las decisiones anteriores no exijan.

## Correo canónico

USER.email elimina espacios exteriores, normaliza dominio y tiene unicidad sin distinción de mayúsculas. Se conserva representación canónica e igualdad lógica coherente; diferencias solo de mayúsculas no pueden crear cuentas distintas.

No se eliminan puntos ni sufijos + mediante políticas de proveedores; no se cambia email en el MVP. Tratamiento técnico de caracteres internacionales y mecanismo de normalización/comparación se difieren sin reabrir la política lógica.

Esta igualdad no se aplica a los code de catálogos, cuyo dominio aprobado es sensible a mayúsculas.

## URLs

external_url de EXTERNAL_LINK publicado es absoluta, HTTPS, sintácticamente válida y revisada editorialmente. Se conserva la dirección revisada sin reescrituras que cambien destino, ruta o parámetros. Validez no garantiza seguridad, disponibilidad o calidad; no se realizan solicitudes automáticas externas.

La obligación HTTPS no se extiende automáticamente a INSTRUMENT_VERSION_REFERENCE.url ni convierte reference/citation en URLs. No se selecciona parser, longitud ni formato de almacenamiento.

## Enumeraciones y booleanos

- USER.status: ACTIVE / BLOCKED.
- INSTRUMENT_VERSION.status: DRAFT / READY / PUBLISHED / RETIRED.
- ASSESSMENT_ATTEMPT.status: IN_PROGRESS / SUBMITTED / EXPIRED / CANCELLED.
- DIMENSION_VERSION.status: DRAFT / ACTIVE / RETIRED.
- CONTEXT_TAG.status: ACTIVE / RETIRED.
- RESOURCE.type: ARTICLE / EXTERNAL_LINK.
- RESOURCE.status: DRAFT / PUBLISHED / RETIRED.
- SCORING_DEFINITION.method: SUM.
- QUESTION.required: booleano lógico; true para toda pregunta de una versión que se publique en MVP.
- AUDIT_EVENT.actor_kind/action/target_type y perfiles: [catálogos aprobados](04-diccionario-datos/06-auditoria.md#rev-log-001--catálogos-aprobados).

Comparación exacta de símbolos, sin aliases, valores libres o defaults implícitos. No se eligen enum físico ni tablas auxiliares.

## Metadata y referencias de auditoría

metadata es estructurada y validada por action y, cuando corresponda, target_type. Solo admite claves y dominios de sus perfiles N/F/D/C/E/T/P, sin payloads libres, valores anteriores/nuevos o bienestar privado.

Para EDITORIAL_ASSOCIATION_ADDED/REMOVED, perfil T exige metadata con topic_id. Para COMPATIBILITY_DECLARED, perfil P exige metadata con version_a_id y version_b_id. En otras acciones se conserva la opcionalidad conforme a sus perfiles; N exige ausencia. Esto no cambia la marca opcional del atributo ni añade columnas.

IDs de catálogo internos mantienen el mismo dominio lógico de sus objetos; no son FKs nuevas. Ausencia, colección vacía y contenido válido se distinguen; ninguna colección vacía satisface una clave requerida de T/P. target_identifier sigue opcional y no es FK.

USER_DELETED no conserva identificadores personales; actor_kind puede conservar su clasificación cuando se desvincula actor_user_id. No se elige JSON, JSONB ni esquema físico de serialización.

## Nulabilidad y restricciones comunes

Se conserva exactamente la opcionalidad de la fuente: PK y componentes de FKs compuestas obligatorios; referencias opcionales únicamente donde se declaran. No se usan IDs especiales, cadenas vacías, cero o fechas ficticias para representar ausencia.

Obligatoriedad no implica default ni generación. Se conservan obligaciones condicionales por tipo/estado/acción: contenido publicado, ended_at terminal, Result obligatorio en SUBMITTED y metadata T/P. Desvincular una cuenta no se impide por retención de auditoría.

Las FKs aseguran existencia/pertenencia referencial; no sustituyen autorización, estado, completitud, cobertura, mínimos de hijos ni atomicidad. No se agregan AK para nombres, hashes o tokens.

## Aplicación y límite físico

Los seis diccionarios usan estos dominios; [integridad](05-integridad-referencial.md) conserva claves y [estados](06-estados-y-transiciones.md) consolida las transiciones.

REV-LOG-002 queda RESOLVED para MVP lógico. Permanecen decisiones físicas de tipos, longitudes, precisión, colaciones, representación y mecanismos de implementación. Estas decisiones pendientes no convierten de nuevo en pendientes los dominios aprobados.

PostgreSQL sigue [PROVISIONALLY ACCEPTED](../../06-decisions/ADR-001-database-engine.md). No se desarrollan SQL, migraciones, entidades ni tecnologías de aplicación. El [dictamen definitivo](12-dictamen-modelo-logico-v1.md) formaliza la línea base lógica APPROVED / FROZEN.

[Índice](00-indice.md) · [Registro de resoluciones](11-pendientes-y-riesgos.md).
