# Diccionario de datos — Seguimiento

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](../12-dictamen-modelo-logico-v1.md).

## Fuentes y convenciones

La [especificación maestra](../especificacion-maestra-v1.0.md#c-seguimiento), apartados 17..23, fija las siete relaciones y sus **33 atributos**, obligatoriedad, PK, AK, FK y URA. AJ-03 y VF-01..05 aportan escalas, estados, transacciones, edición, privacidad y eliminación. [REV-LOG-005](../11-pendientes-y-riesgos.md#rev-log-005) precisa los extremos inclusivos y la corrección de recorded_at.

Los atributos marcados `?` en la fuente son opcionales; todos los demás son obligatorios. Los dominios describen significado lógico, sin elegir tipos físicos, longitudes, precisión temporal ni generación de identificadores. [REV-LOG-002](../11-pendientes-y-riesgos.md#rev-log-002) y los [dominios transversales](../13-dominios-logicos.md) aprueban los dominios lógicos.

**Predeterminados:** únicamente está aprobado el valor inicial revision = 1 de CHECK_IN; es una regla lógica de creación, sin elegir un DEFAULT físico. Para todos los demás atributos no se especifican predeterminados, incluido status. Opcionalidad no implica una estrategia de valor predeterminado.

Las cardinalidades derivadas de claves no prueban mínimos de hijos ni condiciones de estado. Las restricciones de escala, mínimo de Measurements, ventana temporal y concurrencia requieren validación coordinada; no se atribuyen a una FK.

## Separación y reglas comunes

CheckIn registra seguimiento personal; no es AssessmentAttempt y no produce AssessmentResult, SUM ni Interpretation oficial. Measurement es un valor de una escala versionada, no una Answer. Dimension identifica el concepto y DimensionVersion define su escala histórica. ContextTag describe contexto, no Topic editorial.

La creación requiere acceso personal autorizado (USER ACTIVE y correo verificado), al menos una Measurement válida, dimensiones sin duplicados y versiones ACTIVE pertenecientes a las dimensiones elegidas. CheckIn, Measurements y asociaciones incluidas se confirman de forma atómica; una validación fallida no deja un CheckIn vacío.

La edición conserva el conjunto original de Measurements y sus versiones. Cada operación transaccional de edición confirmada incrementa revision exactamente una vez y actualiza el instante de edición, aunque modifique múltiples Measurements, nota, recorded_at o ContextTags. Ventana e intervalo se anclan siempre en created_at original. La eliminación completa sigue permitida en cualquier momento.

## DIMENSION

**Propósito:** Identidad estable de una dimensión de seguimiento, separada de la definición de su escala.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| dimension_id | Identificador de dimensión | Sí | PK |
| code | Código de dimensión | Sí | AK |
| name | Nombre | Sí | — |
| description | Descripción | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK dimension_id; AK code; sin FK ni URA adicionales.

DIMENSION 1 → 0..N DIMENSION_VERSION; cada versión pertenece exactamente a una dimensión. Una Dimension puede figurar en muchos CheckIns, como máximo una vez por CheckIn por la PK de MEASUREMENT.

### Restricciones lógicas

code es único; no se inventa normalización del código. La escala se define en DIMENSION_VERSION, no en la identidad.

### Mutabilidad

Una modificación del catálogo no puede alterar el significado de Measurements históricas. La fuente no detalla reglas adicionales de edición de textos del padre; no se inventan.

### Eliminación e invariantes transaccionales

Eliminar datos personales no elimina el catálogo compartido. REV-LOG-010 prohíbe eliminación ordinaria de Dimension/versiones referenciadas por información histórica; se utiliza retiro de versiones cuando corresponda y no se introducen cascadas destructivas. Activación concurrente se coordina por dimensión.

**Observaciones y trazabilidad:** AJ-03; VF-01/03; fuente §17. Se aplican las reglas comunes y las invariantes transaccionales siguientes.

## DIMENSION_VERSION

**Propósito:** Definición histórica de una escala entera y sus condiciones de uso.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| dimension_version_id | Identificador de versión | Sí | PK |
| dimension_id | Identificador de dimensión | Sí | FK → DIMENSION; AK; URA |
| version | Entero positivo | Sí | Componente AK |
| definition | Definición de la dimensión en esta versión | Sí | — |
| min_value | Entero; extremo inferior | Sí | — |
| max_value | Entero; extremo superior | Sí | — |
| step | Entero positivo; incremento de escala | Sí | — |
| status | DRAFT / ACTIVE / RETIRED | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK dimension_version_id; AK (dimension_id, version); URA (dimension_id, dimension_version_id); FK dimension_id → DIMENSION.dimension_id.

Cada versión pertenece exactamente a una Dimension y tiene 0..N DIMENSION_ANCHOR según claves; puede ser referida por 0..N Measurements. La fuente no establece un mínimo de Anchors.

### Restricciones lógicas

version > 0; min_value < max_value; step > 0; (max_value - min_value) divisible entre step. Escala permitida: min_value + k × step, con k entero no negativo y valor ≤ max_value; ambos extremos son alcanzables. Máximo una versión ACTIVE por Dimension.

### Mutabilidad

DRAFT → ACTIVE → RETIRED; RETIRED es terminal. Definición, escala y Anchors activos/históricos se preservan para reproducir Measurements; no se cambia la escala de registros históricos al activar una nueva versión.

### Eliminación e invariantes transaccionales

Activación valida escala y Anchors, coordina unicidad de ACTIVE y creación concurrente de Measurements. Reemplazar una versión ACTIVE requiere retirar la anterior y activar la nueva en una misma operación atómica, conservando el máximo de una ACTIVE por Dimension. Retiro no elimina versiones ni invalida registros existentes; los cambios de valor siguen usando la versión original. El borrado de una cuenta no elimina esta definición compartida.

**Observaciones y trazabilidad:** AJ-03; VF-01/02/03/05; fuente §18. Se aplican las reglas comunes y las invariantes transaccionales siguientes.

## DIMENSION_ANCHOR

**Propósito:** Etiqueta descriptiva de un valor de la escala de una versión.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| dimension_version_id | Identificador de versión | Sí | PK; FK → DIMENSION_VERSION |
| value | Entero perteneciente a la escala | Sí | PK |
| label | Etiqueta descriptiva | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK (dimension_version_id, value); FK dimension_version_id → DIMENSION_VERSION.dimension_version_id; sin AK/URA adicionales.

DIMENSION_VERSION 1 → 0..N Anchors; cada Anchor pertenece exactamente a una versión. La PK permite como máximo una etiqueta por valor en esa versión.

### Restricciones lógicas

min_value ≤ value ≤ max_value y (value - min_value) divisible entre step de la versión. La FK solo comprueba existencia de la versión; no comprueba pertenencia a su escala. No se exige Anchor para cada valor ni para ambos extremos sin una regla aprobada.

### Mutabilidad

Forma parte de la definición versionada; los Anchors históricos se preservan. Correcciones de una definición no reinterpretan Measurements ya registradas.

### Eliminación e invariantes transaccionales

No se elimina al borrar un CheckIn o una cuenta. La validación conjunta con escala precede a activación; no se introduce anchor_id ni unicidad global de label.

**Observaciones y trazabilidad:** AJ-03; VF-01/03; fuente §19. Se aplican las reglas comunes y las invariantes transaccionales siguientes.

## CHECK_IN

**Propósito:** Registro personal de seguimiento con al menos una Measurement, nota opcional y etiquetas de contexto.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| check_in_id | Identificador de CheckIn | Sí | PK |
| user_id | Identificador del propietario | Sí | FK → USER |
| recorded_at | Instante al que corresponde el registro | Sí | — |
| created_at | Instante original de creación | Sí | — |
| updated_at | Ausente hasta la primera edición confirmada; después, instante de la última modificación | No | — |
| revision | Entero positivo; versión de edición | Sí | — |
| note | Nota personal opcional | No | — |

**Valores predeterminados:** revision inicia en 1; no especificado para los demás atributos.

### Claves, unicidad y cardinalidades

PK check_in_id; FK user_id → USER.user_id; sin AK/URA adicionales.

USER 1 → 0..N CheckIns; cada CheckIn pertenece exactamente a un User. Claves de hijos permiten 0..N Measurements, pero el invariante aprobado exige 1..N. CheckIn tiene 0..N vínculos con ContextTags.

### Restricciones lógicas

revision inicia en 1 y permanece positiva; se incrementa en uno por operación transaccional de edición confirmada del CheckIn o sus dependencias editables, independientemente del número de cambios incluidos. updated_at puede permanecer ausente hasta la primera edición confirmada; posteriormente refleja la última modificación confirmada. recorded_at inicial pertenece al intervalo inclusivo [created_at - 168 horas, created_at]. Creación de CheckIn y Measurements es atómica.

### Mutabilidad

Edición estrictamente antes de created_at + 168 horas. recorded_at puede corregirse, pero permanece en el mismo intervalo original inclusivo; created_at no se desplaza para extender ventana o intervalo. Se pueden corregir valores de Measurements conservando sus versiones; no se agregan ni eliminan Measurements individuales durante edición. Nota y asociaciones editables están sujetas a la misma ventana y revision; agregar tag exige ACTIVE.

### Eliminación e invariantes transaccionales

Eliminación completa permitida en cualquier momento, incluso después de la ventana de edición, junto a Measurements y vínculos de tags; también se elimina con la cuenta. No se impone retención automática de CheckIns. Cada edición coordina cambios, updated_at y aumento de revision; conflictos se detectan sin sobrescritura silenciosa. No se selecciona incremento SQL ni estrategia de bloqueo.

**Observaciones y trazabilidad:** AJ-03; VF-01/03/04/05; REV-LOG-005 RESOLVED; fuente §20. Se aplican las reglas comunes y las invariantes transaccionales siguientes.

## MEASUREMENT

**Propósito:** Valor de una dimensión dentro de un CheckIn, ligado a la versión exacta de la escala.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| check_in_id | Identificador de CheckIn | Sí | PK; FK → CHECK_IN |
| dimension_id | Identificador de dimensión | Sí | PK; Componente FK |
| dimension_version_id | Identificador de versión histórica | Sí | Componente FK |
| value | Entero perteneciente a la escala original | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK (check_in_id, dimension_id); FK check_in_id → CHECK_IN.check_in_id; FK (dimension_id, dimension_version_id) → DIMENSION_VERSION.(dimension_id, dimension_version_id); sin AK/URA adicionales.

Cada Measurement pertenece exactamente a un CheckIn y a una versión de su Dimension. Máximo una Measurement por Dimension en cada CheckIn; múltiples CheckIns pueden usar la misma versión.

### Restricciones lógicas

Todos los componentes son obligatorios. La FK compuesta asegura que la versión pertenece a la Dimension. value debe estar entre min_value y max_value inclusive y (value - min_value) debe ser divisible entre step de esa versión. Al crear la Measurement la versión debe estar ACTIVE; ni pertenencia a escala ni estado ACTIVE son garantías de la FK.

### Mutabilidad

Solo se corrige value dentro de la ventana del CheckIn y en la escala histórica original. No se sustituye Dimension/DimensionVersion, ni se agrega o elimina una Measurement individual durante edición. El posterior RETIRED de la versión no cambia los valores admitidos para corregir el registro original.

### Eliminación e invariantes transaccionales

Se crea con el CheckIn en una transacción y se elimina con el CheckIn/la cuenta. Una operación confirmada de corrección incrementa en uno revision del CheckIn y actualiza su instante de modificación, aunque incluya varias correcciones; no se agrega revision a MEASUREMENT. Creación se coordina con activación/retiro de versiones, y edición con ventana, revisión y borrado.

**Observaciones y trazabilidad:** AJ-03; VF-01/03/04; fuente §21. Se aplican las reglas comunes y las invariantes transaccionales siguientes.

## CONTEXT_TAG

**Propósito:** Etiqueta de contexto de seguimiento, distinta de Topic editorial.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| context_tag_id | Identificador de etiqueta | Sí | PK |
| code | Código de etiqueta | Sí | AK |
| name | Nombre | Sí | — |
| description | Descripción | No | — |
| status | ACTIVE / RETIRED | Sí | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK context_tag_id; AK code; sin FK ni URA adicionales.

Cada ContextTag puede tener 0..N CHECK_IN_CONTEXT_TAG; un CheckIn puede vincular varios tags.

### Restricciones lógicas

status pertenece a ACTIVE/RETIRED; code único. No se inventa política de normalización del código ni mínimo de tags por CheckIn.

### Mutabilidad

ACTIVE ↔ RETIRED, conservando significado. Retiro impide agregar vínculos nuevos, pero no borra vínculos históricos; reactivación no permite reutilizar identidad con otro significado. La fuente no define criterios detallados de cambios de texto; se mantiene el límite semántico aprobado.

### Eliminación e invariantes transaccionales

Borrar CheckIn/la cuenta elimina sus vínculos, no el tag compartido. La eliminación del catálogo no está definida; no se presume CASCADE. Agregar vínculos se coordina con cambio concurrente de estado para validar ACTIVE al confirmar.

**Observaciones y trazabilidad:** VF-01/02/03/04; fuente §22. Se aplican las reglas comunes y las invariantes transaccionales siguientes.

## CHECK_IN_CONTEXT_TAG

**Propósito:** Asociación entre un CheckIn y una etiqueta de contexto.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| check_in_id | Identificador de CheckIn | Sí | PK; FK → CHECK_IN |
| context_tag_id | Identificador de ContextTag | Sí | PK; FK → CONTEXT_TAG |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK (check_in_id, context_tag_id); FK check_in_id → CHECK_IN.check_in_id; FK context_tag_id → CONTEXT_TAG.context_tag_id; sin AK/URA adicionales.

Relación muchos a muchos entre CheckIn y ContextTag; cada vínculo refiere exactamente uno de cada uno. La PK evita duplicar un tag dentro del CheckIn.

### Restricciones lógicas

Solo se agregan ContextTags ACTIVE. La FK comprueba existencia, no estado. Un tag retirado puede conservarse en un vínculo histórico; no se invalida automáticamente el CheckIn.

### Mutabilidad

Las modificaciones de asociaciones editables respetan ventana original y revisión del CheckIn. La condición ACTIVE se aplica a vínculos nuevos; no se exige revalidar como ACTIVE un vínculo histórico conservado ni reactivar el tag para leerlo.

### Eliminación e invariantes transaccionales

Vínculos se eliminan con el CheckIn o la cuenta sin borrar el catálogo. Ediciones confirmadas coordinan vínculo, updated_at y aumento de revision del CheckIn; un cambio de estado del tag no edita por sí mismo los CheckIns históricos.

**Observaciones y trazabilidad:** VF-01..04; fuente §23. Se aplican las reglas comunes y las invariantes transaccionales siguientes.

## Transacciones, concurrencia e historia

- **Activación:** validar escala y Anchors coherentes y coordinar el máximo de una ACTIVE por Dimension. La PK/AK no comprueba este máximo condicionado por estado. Cuando se reemplaza una ACTIVE, retiro de la anterior y activación de la nueva son atómicos; se coordina también la creación concurrente de Measurements. No se selecciona un mecanismo físico.
- **Creación:** coordinar estado ACTIVE de cada versión con activación/retiro concurrentes; verificar pertenencia y escala, PK por dimensión y una Measurement mínima. No se admite un registro parcialmente creado.
- **Edición:** comprobar revisión vigente y tiempo estrictamente anterior a created_at + 168 horas al confirmar. Registrar conjuntamente cambios, updated_at y revision anterior + 1 por operación transaccional confirmada, aunque afecte múltiples dependencias; un conflicto se detecta y no sobrescribe silenciosamente. No se decide HTTP, ORM, aislamiento, bloqueo ni política de reintento.
- **Dependencias:** corregir value no cambia la versión histórica ni el conjunto de dimensiones. Edición de nota, recorded_at o vínculos editables tampoco elude revisión/ventana. Agregar ContextTag exige ACTIVE; conservar uno retirado es válido.
- **Eliminación:** coordinar borrado completo con ediciones concurrentes, eliminando CheckIn, Measurements y asociaciones. El borrado no queda restringido por la ventana; no borra versiones, Anchors, Dimensions ni ContextTags compartidos.
- **Historia:** activación de una nueva escala no transforma ni remapea valores previos. Una corrección valida contra la escala original, aun si la versión está RETIRED. Comparación entre versiones distintas requiere compatibilidad explícita por pares, simétrica y no transitiva; igualdad de valores numéricos no basta. Timeline y Trend siguen siendo read models derivados, sin relaciones nuevas.

## Privacidad y retención

Los registros, notas, valores y asociaciones de contexto son información personal; pertenencia por FK no sustituye autorización. ADMINISTRATOR no otorga automáticamente acceso privado y no se agrega una matriz de permisos.

VF-04 exige eliminación de cuenta y dependencias, desvinculación inmediata de referencias personales en AUDIT_EVENT y prohibición de registrar información privada o identificadores personales de CheckIns, Attempts y Results. Auditoría conserva como máximo 180 días desde occurred_at. Backups cifrados tienen retención de 30 días; restaurar exige reaplicar supresiones antes de habilitar el servicio.

No se impone retención automática de 30 días a CheckIns: esa regla corresponde a Attempts terminales sin Result. Tampoco se agrega borrado independiente de Measurements, historial de revisiones, timestamps o estados no presentes en la fuente.

## Aspectos abiertos y contraste

- **REV-LOG-002 — RESOLVED para MVP:** dominios aprobados; tipos físicos, precisión y mecanismos de implementación no seleccionados.
- **REV-LOG-008 — RESOLVED para MVP:** orden total, estable y documentado; tipo físico y comparación concreta diferidos al diseño físico. Pares y claves aprobados se conservan.
- **REV-LOG-001 — RESOLVED para MVP:** catálogos, combinaciones y perfiles aprobados. No se auditan datos privados ni se agrega matriz de permisos.
- **Detalles técnicos abiertos:** precisión temporal, generación de identificadores, implementación de dominios de códigos y coordinación transaccional. No se eligen tecnologías de aplicación.
- **Información no especificada:** criterios detallados de edición textual que conservan significado y borrado de catálogos no referenciados, que la política no autoriza ni define. REV-LOG-010 ya impide eliminación ordinaria de instrumentos, dimensiones o versiones referenciadas históricamente. Estas ausencias no permiten borrar definiciones históricas ni reinterpretar registros.

Las precisiones posteriores del usuario explicitan updated_at ausente hasta primera edición, incremento único por operación transaccional confirmada y reemplazo atómico de la versión ACTIVE, sin cambiar atributos o claves.

No se identifican contradicciones adicionales entre las siete relaciones de la fuente y AJ-03/VF-01..05. REV-LOG-005 es una precisión aprobada y no un pendiente: recorded_at inicial o corregido queda en [created_at - 168 horas, created_at], inclusivo, y la edición sucede estrictamente antes de created_at + 168 horas.

Se mantiene el conceptual v0.1 sin cambios, la [especificación maestra](../especificacion-maestra-v1.0.md) sin alteraciones y el inventario de 32 relaciones. PostgreSQL sigue [PROVISIONALLY ACCEPTED](../../../06-decisions/ADR-001-database-engine.md). Compatibilidad tiene diccionario propio; Contenido continúa en su archivo y Auditoría tiene diccionario propio con catálogos aprobados para el MVP; el [dictamen definitivo](../12-dictamen-modelo-logico-v1.md) formaliza APPROVED / FROZEN para la línea base lógica.

Los [dominios transversales aprobados](../13-dominios-logicos.md) precisan IDs, códigos, textos, enteros, UTC y nulabilidad sin modificar atributos o claves; plazos de 30/180 días son periodos de 24 horas transcurridas.

[Inventario](../02-inventario-relaciones.md) · [Matriz](../10-matriz-trazabilidad.md) · [Pendientes](../11-pendientes-y-riesgos.md) · [Índice](../00-indice.md).
