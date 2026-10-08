# Diccionario de datos — Contenido

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](../12-dictamen-modelo-logico-v1.md).

## Fuentes y convenciones

La [especificación maestra](../especificacion-maestra-v1.0.md#e-contenido), apartados 26..31, fija atributos, obligatoriedad, PK y FKs. AJ-02 y VF-01..05 definen publicación, estados, concurrencia y límites editoriales. [DR-DOM-004](../../../03-domain/conceptual-model.md#dr-dom-004), RN-033..035 y RF-022/023 establecen Guidance y las relaciones mediante Topic.

Los atributos marcados `?` son opcionales en la estructura. body/external_url tienen obligaciones condicionales al publicar según type. title es obligatorio y no vacío desde DRAFT. No se seleccionan tipos físicos, formatos, longitudes, tecnologías de renderizado o validadores. Todos los valores predeterminados están **No especificado**, incluido status: no se presume un DEFAULT DRAFT.

Cada relación conserva sus claves aprobadas; las cuatro asociaciones tienen PK compuesta y dos FKs simples, no FKs compuestas. Este módulo no cambia las FKs compuestas históricas de los módulos anteriores.

## Separación y condiciones comunes

Interpretation conserva la explicación oficial histórica de una versión; Resource proporciona contenido informativo que puede evolucionar y retirarse sin cambiar esa explicación ni Results anteriores. Topic es vocabulario editorial y no ContextTag personal.

Guidance es una capacidad derivada; no se agrega relación GUIDANCE, Recommendation ni perfil personal de recursos. La presencia de un Topic común no demuestra compatibilidad entre versiones, causalidad, diagnóstico o recomendación clínica.

## TOPIC

**Propósito:** Vocabulario controlado para clasificar contenido y conceptos; distinto de ContextTag.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| topic_id | Identificador de Topic | Sí | PK |
| code | Código de Topic | Sí | AK |
| name | Nombre | Sí | — |
| description | Descripción | No | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK topic_id; AK code; sin FK ni URA adicionales.

Topic se asocia con 0..N Resources, Instruments, Interpretations y Dimensions mediante las cuatro relaciones aprobadas. Cada asociación exige un Topic existente.

### Restricciones lógicas

code es único; no se inventa política de normalización ni se asume que name sea único. No se agrega status ni se convierte Topic en contexto personal.

### Mutabilidad

REV-LOG-012: code y significado permanecen estables; name y description admiten correcciones editoriales sin cambio de significado. Las asociaciones se agregan o retiran mediante operaciones autorizadas, preservando los Topics mínimos de Resources PUBLISHED. Topic no modifica definiciones históricas ni autoriza reinterpretar Results.

### Eliminación e invariantes transaccionales

REV-LOG-013 prohíbe eliminación física ordinaria de Topics durante el MVP; no se presume cascada. Eliminar datos personales no elimina el catálogo. Cambios que afecten asociaciones deben coordinar integridad y publicación de Resources.

**Observaciones y trazabilidad:** DR-DOM-004; RN-034; VF-01/03; fuente §26. Se aplican las reglas comunes y las precisiones siguientes.

## RESOURCE

**Propósito:** Contenido informativo o educativo con ciclo editorial independiente de las interpretaciones históricas.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| resource_id | Identificador de Resource | Sí | PK |
| type | ARTICLE / EXTERNAL_LINK | Sí | — |
| status | DRAFT / PUBLISHED / RETIRED | Sí | — |
| title | Título obligatorio y no vacío desde DRAFT | Sí | — |
| summary | Resumen opcional | No | — |
| body | Contenido de artículo; condicional al publicar | No | — |
| external_url | URL de enlace externo; condicional al publicar | No | — |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK resource_id; sin AK, FK ni URA adicionales. No se declara unicidad de title ni external_url.

Resource 1 → 0..N RESOURCE_TOPIC según claves; PUBLISHED exige al menos un Topic. Recursos distintos pueden compartir Topics. No hay relación directa aprobada Resource → Instrument/Interpretation/Dimension.

### Restricciones lógicas

type pertenece a ARTICLE/EXTERNAL_LINK; status a DRAFT/PUBLISHED/RETIRED. title obligatorio y no vacío desde DRAFT. ARTICLE publicado exige body no vacío e informativo revisado editorialmente y no utiliza external_url; EXTERNAL_LINK publicado exige external_url absoluta HTTPS, sintácticamente válida y revisada editorialmente y no utiliza body. summary sigue opcional. PUBLISHED exige al menos un vínculo válido a Topic.

### Mutabilidad

DRAFT → PUBLISHED → RETIRED; RETIRED terminal. type es inmutable desde la primera publicación. Se permiten correcciones editoriales menores; cambios sustanciales requieren un nuevo Resource según AJ-02. REV-LOG-007 define como menores ortografía, puntuación, formato, claridad superficial y reparación de enlaces con el mismo contenido informativo. Cambios de propósito, significado, recomendaciones, alcance informativo o type son sustanciales y requieren un nuevo Resource. Cambiar type no modifica el original publicado; no se autoriza volver PUBLISHED/RETIRED a DRAFT.

### Eliminación e invariantes transaccionales

Publicar valida título, tipo/contenido, Topics y estado y confirma atómicamente sus condiciones. Retiro excluye el recurso de Guidance sin alterar Results ni Interpretations. REV-LOG-013 impide eliminación física ordinaria de Resources; se retiran los publicados mediante RETIRED terminal y se conservan asociaciones históricas cuando corresponda. Eliminar cuentas no elimina contenido compartido.

**Observaciones y trazabilidad:** AJ-02; VF-01/02/03/05; DR-DOM-004; RN-033/035; REV-LOG-007/011; fuente §27. Se aplican las reglas comunes y las precisiones siguientes.

## RESOURCE_TOPIC

**Propósito:** Clasificar un Resource mediante un Topic; soporta el mínimo requerido al publicar.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| resource_id | Identificador de RESOURCE | Sí | PK; FK → RESOURCE |
| topic_id | Identificador de Topic | Sí | PK; FK → TOPIC |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK (resource_id, topic_id); FK resource_id → RESOURCE.resource_id; FK topic_id → TOPIC.topic_id; sin AK/URA adicionales.

Asociación muchos a muchos: RESOURCE 0..N ↔ 0..N TOPIC; cada fila refiere exactamente uno de cada uno. La PK impide duplicar el mismo vínculo.

### Restricciones lógicas

Un Resource PUBLISHED debe conservar al menos un Topic. Las FKs no garantizan ese mínimo; ninguna operación puede dejar PUBLISHED sin vínculos válidos. No se exige un Topic exclusivo ni un número máximo.

### Mutabilidad

Creación/modificación de asociaciones debe preservar el mínimo de un Topic cuando el Resource está PUBLISHED. REV-LOG-007/012 permiten correcciones menores y agregar/retirar asociaciones autorizadamente; no se atribuye inmutabilidad de type a todas las asociaciones.

### Eliminación e invariantes transaccionales

Eliminación de vínculo y publicación/edición concurrentes deben coordinar el mínimo aprobado, sin estado PUBLISHED parcial. REV-LOG-013 impide eliminación física ordinaria del catálogo y conserva asociaciones históricas cuando corresponda; no se elige CASCADE.

**Observaciones y trazabilidad:** DR-DOM-004; RN-034; VF-01/03; AJ-02; fuente §28. Se aplican las reglas comunes y las precisiones siguientes.

## INSTRUMENT_TOPIC

**Propósito:** Relacionar el instrumento estable con Topics, sin vincularlos directamente a InstrumentVersion.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| instrument_id | Identificador de INSTRUMENT | Sí | PK; FK → INSTRUMENT |
| topic_id | Identificador de Topic | Sí | PK; FK → TOPIC |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK (instrument_id, topic_id); FK instrument_id → INSTRUMENT.instrument_id; FK topic_id → TOPIC.topic_id; sin AK/URA adicionales.

Asociación muchos a muchos: INSTRUMENT 0..N ↔ 0..N TOPIC; cada fila refiere exactamente uno de cada uno. La PK impide duplicar el mismo vínculo.

### Restricciones lógicas

Existencia de ambos extremos y unicidad del vínculo. No se exige un mínimo de Topics para este padre ni se infiere estado del padre mediante una FK.

### Mutabilidad

REV-LOG-012 permite agregar o retirar asociaciones mediante operaciones autorizadas. El vínculo no reescribe versiones ni registros históricos y no representa compatibilidad entre versiones.

### Eliminación e invariantes transaccionales

Eliminar datos personales no elimina estos vínculos compartidos. Las referencias históricas de instrumentos, dimensiones y versiones se preservan según REV-LOG-010. REV-LOG-013 impide eliminación física ordinaria de Topics/Resources; se conservan asociaciones históricas cuando corresponda, sin cascadas. Cambios concurrentes respetan PK/FKs y las condiciones editoriales que se aprueben.

**Observaciones y trazabilidad:** DR-DOM-004; RN-034; VF-01/03; fuente §29. Se aplican las reglas comunes y las precisiones siguientes.

## INTERPRETATION_TOPIC

**Propósito:** Relacionar una interpretación histórica con Topics para derivar contenido informativo.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| interpretation_id | Identificador de INTERPRETATION | Sí | PK; FK → INTERPRETATION |
| topic_id | Identificador de Topic | Sí | PK; FK → TOPIC |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK (interpretation_id, topic_id); FK interpretation_id → INTERPRETATION.interpretation_id; FK topic_id → TOPIC.topic_id; sin AK/URA adicionales.

Asociación muchos a muchos: INTERPRETATION 0..N ↔ 0..N TOPIC; cada fila refiere exactamente uno de cada uno. La PK impide duplicar el mismo vínculo.

### Restricciones lógicas

Existencia de ambos extremos y unicidad del vínculo. No se exige un mínimo de Topics para este padre ni se infiere estado del padre mediante una FK.

### Mutabilidad

El vínculo no cambia texto, rango ni pertenencia histórica de Interpretation. REV-LOG-012 permite agregar o retirar asociaciones autorizadamente sin alterar la Interpretation histórica; Guidance actual puede variar. No se incorpora una política temporal ni una nueva relación.

### Eliminación e invariantes transaccionales

Eliminar datos personales no elimina estos vínculos compartidos. Las referencias históricas de instrumentos, dimensiones y versiones se preservan según REV-LOG-010. REV-LOG-013 impide eliminación física ordinaria de Topics/Resources; se conservan asociaciones históricas cuando corresponda, sin cascadas. Cambios concurrentes respetan PK/FKs y las condiciones editoriales que se aprueben.

**Observaciones y trazabilidad:** DR-DOM-004; RN-034; VF-01/03; fuente §30. Se aplican las reglas comunes y las precisiones siguientes.

## DIMENSION_TOPIC

**Propósito:** Relacionar la dimensión estable con Topics, sin vincularlos directamente a DimensionVersion.

### Atributos

| Atributo | Dominio lógico / significado | Obligatorio | Clave o referencia |
| --- | --- | --- | --- |
| dimension_id | Identificador de DIMENSION | Sí | PK; FK → DIMENSION |
| topic_id | Identificador de Topic | Sí | PK; FK → TOPIC |

**Valores predeterminados:** No especificado para todos los atributos.

### Claves, unicidad y cardinalidades

PK (dimension_id, topic_id); FK dimension_id → DIMENSION.dimension_id; FK topic_id → TOPIC.topic_id; sin AK/URA adicionales.

Asociación muchos a muchos: DIMENSION 0..N ↔ 0..N TOPIC; cada fila refiere exactamente uno de cada uno. La PK impide duplicar el mismo vínculo.

### Restricciones lógicas

Existencia de ambos extremos y unicidad del vínculo. No se exige un mínimo de Topics para este padre ni se infiere estado del padre mediante una FK.

### Mutabilidad

REV-LOG-012 permite agregar o retirar asociaciones mediante operaciones autorizadas. El vínculo no reescribe versiones ni registros históricos y no representa compatibilidad entre versiones.

### Eliminación e invariantes transaccionales

Eliminar datos personales no elimina estos vínculos compartidos. Las referencias históricas de instrumentos, dimensiones y versiones se preservan según REV-LOG-010. REV-LOG-013 impide eliminación física ordinaria de Topics/Resources; se conservan asociaciones históricas cuando corresponda, sin cascadas. Cambios concurrentes respetan PK/FKs y las condiciones editoriales que se aprueben.

**Observaciones y trazabilidad:** DR-DOM-004; RN-034; VF-01/03; fuente §31. Se aplican las reglas comunes y las precisiones siguientes.

## Publicación y concurrencia

Publicar Resource requiere simultáneamente título no vacío, tipo válido, contenido correspondiente válido y al menos un RESOURCE_TOPIC válido. La opcionalidad estructural de body/external_url no elimina esas condiciones. La presencia de un FK no asegura el mínimo de Topics ni la validez editorial del contenido.

Publicación, cambios del recurso y cambios de asociaciones deben coordinarse para no confirmar PUBLISHED con contenido inválido o sin Topics. Retiro concurrente no puede producir nuevas disponibilidades de Guidance que ignoren el estado vigente. La coordinación no selecciona bloqueos, aislamiento ni arquitectura.

ARTICLE publicado utiliza body no vacío, informativo y revisado editorialmente, y no external_url; EXTERNAL_LINK publicado utiliza URL absoluta HTTPS sintácticamente válida y revisada editorialmente, y no body. Validez de URL no garantiza seguridad, disponibilidad o calidad del destino; no se realizan solicitudes automáticas a URLs externas. No se inventa una regla universal para los campos no usados durante DRAFT: la fuente condiciona esas restricciones a publicación. type permanece congelado desde su primera publicación aunque el contenido admita correcciones menores.

RETIRED es terminal y excluido de Guidance. No se introduce estado archivado ni reactivación. La clasificación editorial queda consolidada mediante REV-LOG-007; no altera Interpretations ni Results históricos.

## Derivación de Guidance

Las rutas aprobadas utilizan las asociaciones existentes:

- **Interpretación del resultado:** ASSESSMENT_RESULT → INTERPRETATION → INTERPRETATION_TOPIC → TOPIC → RESOURCE_TOPIC → RESOURCE.
- **Instrumento:** INSTRUMENT → INSTRUMENT_TOPIC → TOPIC → RESOURCE_TOPIC → RESOURCE.
- **Dimensión:** DIMENSION → DIMENSION_TOPIC → TOPIC → RESOURCE_TOPIC → RESOURCE.

Solo Resources PUBLISHED son elegibles para Guidance; DRAFT no es contenido publicado y RETIRED queda excluido conforme a AJ-02. Las rutas muestran relaciones existentes, sin fijar prioridades, cuotas, ranking, selección personalizada ni presentación de interfaces. Un Resource alcanzable por varios Topics sigue siendo el mismo recurso; eso no crea varias identidades.

No se requiere un Resource por Interpretation/Instrument/Dimension ni un mínimo de sugerencias: la ausencia de contenido relacionado no altera la interpretación oficial. Los Resources actuales pueden cambiar sin recalcular Results históricos; no se afirma que el modelo almacene la lista de Guidance vista en una fecha pasada.

No se crean asociaciones directas Resource → Instrument/Interpretation/Dimension ni Topic → InstrumentVersion/DimensionVersion. Guidance no fusiona contenido editorial con la interpretación histórica ni con la política de compatibilidad.

## Historia, eliminación y privacidad

REV-LOG-010 impide la eliminación ordinaria de instrumentos, dimensiones y versiones referenciados por información histórica; RETIRED se aplica donde el modelo tiene ese estado. No se añade status a TOPIC, INSTRUMENT o DIMENSION.

Eliminar cuenta, Assessment o CheckIn continúa siendo independiente del catálogo editorial y de las compatibilidades compartidas. No se introducen cascadas que destruyan historia. REV-LOG-013 prohíbe eliminación física ordinaria de TOPIC/RESOURCE. RETIRED retira Resources publicados sin destruir el catálogo; las operaciones editoriales autorizadas sobre asociaciones son independientes y preservan vínculos históricos cuando corresponda.

ADMINISTRATOR representa habilitación administrativa, sin acceso automático a datos privados ni matriz nueva de permisos. La administración editorial relevante se audita según las reglas existentes; los catálogos de AUDIT_EVENT están aprobados para MVP en su diccionario propio, sin permisos adicionales.

## Políticas editoriales consolidadas

### Contraste previo al cierre

No se identifican incompatibilidades comprobables con AJ-02/VF-01..05. Crear un nuevo Resource por cambio de type preserva la inmutabilidad del original. Revisión editorial precisa la validez ya exigida sin cambiar atributos. Correcciones de Topics y asociaciones afectan Guidance actual, no texto/rangos de Interpretation ni Results históricos. Retirar una asociación autorizadamente no elimina físicamente Topic/Resource; conservar vínculos históricos cuando corresponda no añade historial de asociaciones. RETIRED sigue terminal.

Las políticas posteriores del usuario complementan la fuente preservada y cierran los vacíos sin reescribir el conceptual.

### REV-LOG-007

- **ID:** REV-LOG-007.
- **Pregunta original:** ¿Qué cambios de Resource son menores o sustanciales?
- **Resolución del MVP:** menores: ortografía, puntuación, formato, claridad superficial y reparación de enlaces que mantengan el mismo contenido informativo. Sustanciales: cambio de propósito, significado, recomendaciones, alcance informativo o type; requieren un nuevo Resource.
- **Preservación:** no alterar Interpretations ni Results históricos; type del Resource original permanece inmutable desde primera publicación.
- **Impacto:** revisión y mutabilidad editorial de Resource.
- **Status:** RESOLVED para el MVP.

### REV-LOG-011

- **ID:** REV-LOG-011.
- **Pregunta original:** ¿Qué contenido es válido para publicar?
- **Resolución del MVP:** ARTICLE exige body no vacío e informativo revisado editorialmente; EXTERNAL_LINK exige URL absoluta HTTPS, sintácticamente válida y revisada editorialmente. Se conservan las exclusiones body/external_url por tipo de AJ-02.
- **Límite:** validez de URL no garantiza seguridad, disponibilidad ni calidad del destino. No se implementan solicitudes automáticas a URLs externas ni se seleccionan formato de body o validadores.
- **Impacto:** publicación y edición de Resource.
- **Status:** RESOLVED para el MVP.

### REV-LOG-012

- **ID:** REV-LOG-012.
- **Pregunta original:** ¿Qué cambios de Topic y asociaciones están permitidos?
- **Resolución del MVP:** conservar code y significado de Topic estables; permitir correcciones editoriales de name/description sin cambio de significado. Agregar o retirar asociaciones mediante operaciones autorizadas, sin dejar Resource PUBLISHED sin Topics.
- **Preservación:** puede cambiar Guidance actual; no se alteran Interpretations ni Results históricos.
- **Impacto:** Topic y las cuatro asociaciones editoriales.
- **Status:** RESOLVED para el MVP.

### REV-LOG-013

- **ID:** REV-LOG-013.
- **Pregunta original:** ¿Cómo se eliminan Topic/Resource y asociaciones?
- **Resolución del MVP:** no permitir eliminación física ordinaria de Topics o Resources. Retirar Resources publicados mediante RETIRED, terminal; conservar asociaciones históricas cuando corresponda sin nuevas entidades o estados.
- **Alcance:** retirar una asociación editorial autorizadamente (REV-LOG-012) no equivale a eliminar su Topic/Resource. No se introduce historial temporal de asociaciones ni se garantiza reconstruir Guidance de una fecha pasada.
- **Impacto:** catálogo compartido, asociaciones, publicación y Guidance.
- **Status:** RESOLVED para el MVP.

REV-LOG-001/002 están RESOLVED para MVP: catálogos y dominios aprobados; los tipos físicos y mecanismos permanecen abiertos.

## Contraste y límite

No se identifican contradicciones comprobables entre las seis relaciones, AJ-02 y DR-DOM-004. Las cardinalidades permiten borradores incompletos, mientras publicación exige contenido válido y al menos un Topic: no son reglas contradictorias. La evolución de Resources no altera la inmutabilidad de Interpretations/Results.

Se preservan la especificación maestra, el conceptual v0.1 y las 32 relaciones. PostgreSQL sigue [PROVISIONALLY ACCEPTED](../../../06-decisions/ADR-001-database-engine.md); tecnologías de aplicación abiertas. Auditoría tiene diccionario propio y el [dictamen definitivo](../12-dictamen-modelo-logico-v1.md) formaliza APPROVED / FROZEN para la línea base lógica.

Los [dominios transversales aprobados](../13-dominios-logicos.md) precisan IDs, códigos, textos, enteros, UTC y nulabilidad sin modificar atributos o claves; plazos de 30/180 días son periodos de 24 horas transcurridas.

[Inventario](../02-inventario-relaciones.md) · [Matriz](../10-matriz-trazabilidad.md) · [Pendientes](../11-pendientes-y-riesgos.md) · [Índice](../00-indice.md).
