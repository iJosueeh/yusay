# Diccionario de datos — Compatibilidad

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](../12-dictamen-modelo-logico-v1.md).

## Fuentes y convenciones

La [especificación maestra](../especificacion-maestra-v1.0.md#d-compatibilidad), apartados 24 y 25, establece atributos, obligatoriedad, PK y FKs compuestas. [DR-DOM-003](../../../03-domain/conceptual-model.md#dr-dom-003) y RN-023 establecen comparabilidad de la misma versión, compatibilidad explícita entre versiones distintas y tratamiento conservador de UNKNOWN. VF-01..04 aportan integridad, estados, coordinación y preservación/eliminación.

reference es el único atributo opcional de cada relación; todos los componentes de PK y FKs son obligatorios. No se seleccionan tipos físicos, algoritmos de generación de IDs, colaciones ni mecanismos de coordinación. Ningún atributo tiene predeterminado aprobado. Las claves se reproducen sin ampliarlas con el identificador del catálogo ni agregar AK/URA.

Estas relaciones representan declaraciones positivas de compatibilidad entre pares distintos. No incorporan status, vigencias, registros negativos, una entidad Comparison ni grupos de equivalencia. El orden de almacenamiento no expresa dirección de la compatibilidad ni prioridad temporal.

## INSTRUMENT_VERSION_COMPATIBILITY

**Propósito:** declarar y justificar compatibilidad entre dos versiones distintas del mismo Instrument.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| version_a_id | Identificador de la versión situada primero según el orden canónico total y estable | Sí | PK; componente FK |
| version_b_id | Identificador de la otra versión según ese mismo orden | Sí | PK; componente FK |
| instrument_id | Identificador del Instrument común | Sí | Componente de ambas FKs |
| rationale | Justificación documentada de compatibilidad | Sí | — |
| reference | Referencia de respaldo | No | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves e integridad referencial

- **PK:** (version_a_id, version_b_id).
- **FK:** (instrument_id, version_a_id) → INSTRUMENT_VERSION.(instrument_id, instrument_version_id).
- **FK:** (instrument_id, version_b_id) → INSTRUMENT_VERSION.(instrument_id, instrument_version_id).
- **AK / URA adicionales:** ninguna.
- **Clave de destino:** ambas FKs se apoyan en la URA (instrument_id, instrument_version_id) aprobada en INSTRUMENT_VERSION.
- **Pertenencia:** ambas versiones deben existir y pertenecer al mismo Instrument. Compartir instrument_id en las dos FKs impide pares de catálogos diferentes. No se agrega una FK simple a INSTRUMENT, porque la fuente no la declara.

### Unicidad, restricciones y cardinalidades

La PK impide duplicar el par ordenado. La restricción aprobada version_a_id < version_b_id excluye igualdad y exige una única orientación canónica; los IDs deben disponer de un orden total, estable y documentado. La elección del tipo físico y del mecanismo de comparación se difiere al diseño físico (REV-LOG-008 RESOLVED en el alcance lógico). La PK sola no impide almacenar también el par inverso.

Cada declaración refiere exactamente dos versiones distintas de un mismo Instrument. Una versión puede participar en 0..N declaraciones, en cualquiera de los dos componentes. El catálogo tiene 0..N pares; no se exige una declaración entre cada combinación de sus versiones.

rationale es obligatorio y reference opcional. No se inventan mínimos de longitud ni se vuelve obligatorio reference. La existencia de los dos destinos no demuestra compatibilidad metodológica: debe existir una declaración justificada.

### Mutabilidad y preservación histórica

Las versiones históricas preservan sus definiciones; una declaración no cambia escalas, scores, interpretaciones ni registros personales. Retirar una versión no implica borrar su declaración de compatibilidad ni negar su posible uso para comparaciones históricas.

REV-LOG-009 consolidada: las declaraciones incorporadas se consideran vigentes y no pueden editarse ni eliminarse mediante operaciones ordinarias. Una declaración incorrecta se escala a revisión de diseño antes de modificarla. El MVP carece de revocación trazable; no se agregan estados ni mecanismos de aprobación o revocación histórica. No se inventan condiciones de estado para ambas versiones al declarar.

### Eliminación e invariantes transaccionales

Eliminar una cuenta, Assessment o CheckIn no elimina compatibilidad compartida. REV-LOG-010 consolidada: no se permite eliminación ordinaria de instrumentos, dimensiones ni versiones referenciadas por información histórica. Se utiliza RETIRED cuando corresponda para impedir nuevos usos preservando definiciones. INSTRUMENT y DIMENSION no reciben un atributo status nuevo: el retiro se aplica a sus versiones según el modelo. No se introducen cascadas que eliminen historia; la supresión personal autorizada es independiente. No se elige un mecanismo físico de FK.

Registrar un par debe coordinar pertenencia, orden canónico, justificación y unicidad. Solicitudes concurrentes (a, b) y (b, a) deben converger en la misma orientación y no producir dos declaraciones. La PK protege duplicados del par ya canónico; coordinación y validación del orden no se atribuyen a la FK.

**Observaciones y trazabilidad:** fuente §24; DR-DOM-003, RN-023, VF-01/03/04; REV-LOG-008/009/010. No se interpreta el orden como versión anterior/posterior ni se infiere compatibilidad por igualdad numérica.

## DIMENSION_VERSION_COMPATIBILITY

**Propósito:** declarar y justificar compatibilidad entre dos versiones distintas del mismo Dimension.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| version_a_id | Identificador de la versión situada primero según el orden canónico total y estable | Sí | PK; componente FK |
| version_b_id | Identificador de la otra versión según ese mismo orden | Sí | PK; componente FK |
| dimension_id | Identificador del Dimension común | Sí | Componente de ambas FKs |
| rationale | Justificación documentada de compatibilidad | Sí | — |
| reference | Referencia de respaldo | No | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves e integridad referencial

- **PK:** (version_a_id, version_b_id).
- **FK:** (dimension_id, version_a_id) → DIMENSION_VERSION.(dimension_id, dimension_version_id).
- **FK:** (dimension_id, version_b_id) → DIMENSION_VERSION.(dimension_id, dimension_version_id).
- **AK / URA adicionales:** ninguna.
- **Clave de destino:** ambas FKs se apoyan en la URA (dimension_id, dimension_version_id) aprobada en DIMENSION_VERSION.
- **Pertenencia:** ambas versiones deben existir y pertenecer al mismo Dimension. Compartir dimension_id en las dos FKs impide pares de catálogos diferentes. No se agrega una FK simple a DIMENSION, porque la fuente no la declara.

### Unicidad, restricciones y cardinalidades

La PK impide duplicar el par ordenado. La restricción aprobada version_a_id < version_b_id excluye igualdad y exige una única orientación canónica; los IDs deben disponer de un orden total, estable y documentado. La elección del tipo físico y del mecanismo de comparación se difiere al diseño físico (REV-LOG-008 RESOLVED en el alcance lógico). La PK sola no impide almacenar también el par inverso.

Cada declaración refiere exactamente dos versiones distintas de un mismo Dimension. Una versión puede participar en 0..N declaraciones, en cualquiera de los dos componentes. El catálogo tiene 0..N pares; no se exige una declaración entre cada combinación de sus versiones.

rationale es obligatorio y reference opcional. No se inventan mínimos de longitud ni se vuelve obligatorio reference. La existencia de los dos destinos no demuestra compatibilidad metodológica: debe existir una declaración justificada.

### Mutabilidad y preservación histórica

Las versiones históricas preservan sus definiciones; una declaración no cambia escalas, scores, interpretaciones ni registros personales. Retirar una versión no implica borrar su declaración de compatibilidad ni negar su posible uso para comparaciones históricas.

REV-LOG-009 consolidada: las declaraciones incorporadas se consideran vigentes y no pueden editarse ni eliminarse mediante operaciones ordinarias. Una declaración incorrecta se escala a revisión de diseño antes de modificarla. El MVP carece de revocación trazable; no se agregan estados ni mecanismos de aprobación o revocación histórica. No se inventan condiciones de estado para ambas versiones al declarar.

### Eliminación e invariantes transaccionales

Eliminar una cuenta, Assessment o CheckIn no elimina compatibilidad compartida. REV-LOG-010 consolidada: no se permite eliminación ordinaria de instrumentos, dimensiones ni versiones referenciadas por información histórica. Se utiliza RETIRED cuando corresponda para impedir nuevos usos preservando definiciones. INSTRUMENT y DIMENSION no reciben un atributo status nuevo: el retiro se aplica a sus versiones según el modelo. No se introducen cascadas que eliminen historia; la supresión personal autorizada es independiente. No se elige un mecanismo físico de FK.

Registrar un par debe coordinar pertenencia, orden canónico, justificación y unicidad. Solicitudes concurrentes (a, b) y (b, a) deben converger en la misma orientación y no producir dos declaraciones. La PK protege duplicados del par ya canónico; coordinación y validación del orden no se atribuyen a la FK.

**Observaciones y trazabilidad:** fuente §25; DR-DOM-003, RN-023, VF-01/03/04; REV-LOG-008/009/010. No se interpreta el orden como versión anterior/posterior ni se infiere compatibilidad por igualdad numérica.

## Semántica de comparación

- **Misma versión:** dos AssessmentResults de la misma InstrumentVersion y dos Measurements de la misma DimensionVersion son comparables según DR-DOM-003. No requieren declaración ni fila (v, v); esa fila incumpliría version_a_id < version_b_id. Compararse consigo misma es un caso de la política de comparabilidad, no una excepción al orden de las relaciones.
- **Versiones distintas:** requieren una declaración explícita del par correspondiente. Pertenecer al mismo catálogo es necesario, pero no suficiente. Rango numérico compartido, nombres similares, respuestas completas o normalización matemática no prueban compatibilidad.
- **Simetría:** una fila canónica (a, b) autoriza reconocer el mismo par al consultarlo como (b, a); no se almacena una fila inversa adicional.
- **Sin transitividad:** declarar (a, b) y (b, c) no declara (a, c). Tampoco se infiere compatibilidad por caminos, grupos o cierre transitivo.
- **Ausencia:** no encontrar declaración entre versiones distintas significa UNKNOWN, tratado conservadoramente como NOT COMPARABLE. No significa que exista una declaración negativa persistida.
- **Catálogos distintos:** no se comparan automáticamente resultados de distintos Instruments ni valores de distintas Dimensions en el MVP. Las FKs compuestas excluyen esas declaraciones en estas relaciones.
- **Historia:** usar versiones RETIRED no cambia su definición. Compatibilidad no recalcula Results ni remapea Measurements. Timeline no exige comparabilidad; Trend usa registros compatibles y sigue siendo read model, sin relación adicional.
- **Respaldo:** compatibilidad explícita y respuesta completa no demuestran por sí solas validez psicométrica. Se mantienen las condiciones metodológicas aprobadas.

## Políticas consolidadas del MVP

### Contraste previo al cierre

No se identifica incompatibilidad comprobable con las decisiones anteriores: la fuente ya exige version_a_id < version_b_id y no prescribe tipo físico; DR-DOM-003 ya establece simetría, ausencia de transitividad y comparabilidad de una misma versión. No existía una política aprobada que permitiera edición/revocación ordinaria de declaraciones. Preservar catálogos históricos coincide con VF-04, que permite eliminar información personal sin destruir definiciones compartidas.

Estas políticas posteriores precisan vacíos documentados; no agregan atributos, relaciones, estados ni claves. La especificación maestra y el conceptual se conservan intactos.

### REV-LOG-008

- **ID:** REV-LOG-008.
- **Pregunta original:** ¿Cómo se garantiza el orden canónico de los pares?
- **Resolución lógica del MVP:** conservar version_a_id < version_b_id; los identificadores deben disponer de un orden total, estable y documentado, compartido al registrar, consultar y validar pares.
- **Orientación:** el orden canónico no representa precedencia temporal ni número de versión. Un par inverso se normaliza a la misma clave; no se agrega fila reflexiva.
- **Impacto:** ambas relaciones, unicidad y futura representación física.
- **Status:** RESOLVED para el MVP, en el alcance lógico.
- **Pendiente de diseño físico:** elección del tipo de identificador y del mecanismo de comparación que materialice y documente el orden. No se afirma que un comparador concreto esté elegido o implementado.

Las alternativas antes presentadas —orden del dominio lógico o comparación de representación canónica— quedan como antecedentes técnicos no seleccionados. No reabren la política lógica ni autorizan cambiar claves.

### REV-LOG-009

- **ID:** REV-LOG-009.
- **Pregunta original:** ¿Cómo se corrigen o revocan declaraciones?
- **Resolución del MVP:** las declaraciones incorporadas se consideran vigentes; no se editan ni eliminan mediante operaciones ordinarias. Detectar una declaración incorrecta requiere escalar a revisión de diseño antes de modificarla.
- **Limitación:** el modelo no representa revocación trazable ni historial de vigencias. No se documentan mecanismos de aprobación, estados nuevos ni revocación histórica; tampoco se promete reconstruir el estado de la política de una fecha pasada.
- **Impacto:** ambas relaciones y administración de compatibilidad.
- **Status:** RESOLVED para el MVP.

### REV-LOG-010

- **ID:** REV-LOG-010.
- **Pregunta original:** ¿Cómo se eliminan catálogos/versiones referenciados históricamente?
- **Resolución del MVP:** no permitir eliminación ordinaria de instrumentos, dimensiones ni versiones referenciadas por información histórica. Utilizar RETIRED cuando corresponda, sin destruir definiciones ni introducir cascadas que eliminen historia accidentalmente.
- **Alcance:** no se agrega status a Instrument/Dimension; se respetan estados de versiones existentes. Tampoco se inventa una política para borrar catálogos no referenciados.
- **Supresión personal:** eliminación autorizada de cuenta, Assessments y CheckIns sigue siendo independiente y debe cumplirse.
- **Impacto:** compatibilidad, catálogos y preservación de referencias en Evaluaciones/Seguimiento.
- **Status:** RESOLVED para el MVP.

## Contraste y límite de la entrega

No se detecta contradicción entre simetría, exclusión de pares reflexivos y comparabilidad de una misma versión: describen niveles distintos de la política. Las cuatro FKs compuestas tienen destinos URA aprobados y conservan pertenencia al catálogo común.

REV-LOG-008/009/010 quedan resueltos en el alcance lógico del MVP mediante las políticas propuestas por el usuario y contrastadas sin incompatibilidades comprobables. Tipo físico y comparación concreta siguen pendientes del diseño físico; la ausencia de revocación trazable queda como limitación. El cambio C-LOG-001 sigue registrado en su capa y el conceptual v0.1 permanece intacto.

Se conservan las 32 relaciones, diez atributos de Compatibilidad, PK y FKs de la fuente. PostgreSQL sigue [PROVISIONALLY ACCEPTED](../../../06-decisions/ADR-001-database-engine.md); tecnologías de aplicación abiertas. Contenido y Auditoría tienen diccionarios propios. El [dictamen definitivo](../12-dictamen-modelo-logico-v1.md) formaliza APPROVED / FROZEN para la línea base lógica.

Los [dominios transversales aprobados](../13-dominios-logicos.md) precisan IDs, códigos, textos, enteros, UTC y nulabilidad sin modificar atributos o claves; plazos de 30/180 días son periodos de 24 horas transcurridas.

[Inventario](../02-inventario-relaciones.md) · [Matriz](../10-matriz-trazabilidad.md) · [Pendientes](../11-pendientes-y-riesgos.md) · [Índice](../00-indice.md).
