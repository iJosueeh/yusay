# Dictamen definitivo aprobado — modelo lógico v1.0

**Versión: v1.0. Dictamen: FAVORABLE. Estado: APPROVED / FROZEN.**

**Fecha de aprobación: 2026-10-07. Autoridad de aprobación: responsable del proyecto.** La fecha identifica la autorización formal de la línea base, no la fecha de creación ni de modificación de cada documento.

**Alcance aprobado:** modelo lógico de datos del MVP de Yusay. **Línea base oficial:** las 32 relaciones y sus reglas documentadas en esta carpeta.

El responsable del proyecto aprobó formalmente el dictamen **FAVORABLE** y autorizó la congelación **FROZEN** como línea base oficial. Se conservan las limitaciones aceptadas y riesgos residuales. La autorización cierra la fase lógica v1.0; no aprueba diseño físico, implementación, despliegue ni validación productiva.

## Identificación de la línea base y alcance aprobado

Se evalúa la documentación del modelo lógico recibido: [especificación maestra](especificacion-maestra-v1.0.md), seis diccionarios, [integridad](05-integridad-referencial.md), [estados](06-estados-y-transiciones.md), [atomicidad/concurrencia](07-transacciones-y-concurrencia.md), [privacidad/retención](08-privacidad-eliminacion-retencion.md), [decisiones](09-decisiones-arquitectonicas.md), [trazabilidad](10-matriz-trazabilidad.md), [resoluciones](11-pendientes-y-riesgos.md) y [dominios](13-dominios-logicos.md). La [revisión de preparación](14-revision-preparacion-dictamen.md) documenta la evidencia transversal.

AJ-01..04, VF-01..05 y las aprobaciones posteriores del usuario complementan la fuente preservada. Los cierres de REV-LOG-001..013 y DP-TRANS-001/002 aplican solo a sus alcances documentados. Las conformidades provisionales por módulo/transversal son antecedentes; la aprobación global del alcance lógico y su congelación provienen de la autorización formal posterior del responsable del proyecto.

Se preservan conceptual v0.1 y especificación maestra, cuyo SHA-256 es EF1781817E94F5593C53E83D4A626BAC596DB22B3B7F2544E1624B1F6EBC8AD2. No se deducen claves del conceptual ni se modifican relaciones, atributos o claves recibidos.

## Resultado de las validaciones VF-01 a VF-05

Los resultados siguientes corresponden a consistencia **documental lógica**, según los documentos consolidados y las revisiones previas del usuario. No se atribuyen pruebas de ejecución o aprobaciones a otras personas.

- **VF-01 — Integridad referencial: CONFORME.** PK/AK/URA/FK, cardinalidades, obligatoriedad y pertenencia histórica están documentadas en [05](05-integridad-referencial.md). Se distingue integridad de invariantes transaccionales.
- **VF-02 — Estados: CONFORME.** Estados, guardas, terminalidad, READY congelada y corrección previa a publicación están consolidados en [06](06-estados-y-transiciones.md), incluyendo DP-TRANS-002.
- **VF-03 — Concurrencia: CONFORME en alcance lógico.** Operaciones atómicas y coordinación de registro/tokens, publicación, Attempts, dimensiones, CheckIn, Resources y supresión están en [07](07-transacciones-y-concurrencia.md). Mecanismos físicos siguen diferidos.
- **VF-04 — Eliminación y retención: CONFORME.** Supresión individual/de cuenta, desvinculación inmediata, eliminación de Answers/tokens, retenciones y restauración están en [08](08-privacidad-eliminacion-retencion.md), con excepción DP-TRANS-001 aprobada.
- **VF-05 — Diccionario de datos: CONFORME.** Se mantienen los seis módulos y 147 atributos, tokens sin consumed_at, scoring con enteros con signo, versiones positivas, CHECK_IN.revision, RESOURCE.title desde DRAFT y catálogos cerrados de auditoría. Dominios en [13](13-dominios-logicos.md), sin elegir tipos físicos.

## Decisiones AJ y REV-LOG consolidadas

- **AJ-01:** definición histórica ejecutable, SUM, interpretación oficial respaldada y cobertura exacta; publicación bloqueada si el MVP no representa fielmente la metodología.
- **AJ-02:** tipos y publicación de Resource, Topics y Guidance derivado; correcciones menores y cambios sustanciales según políticas editoriales aprobadas.
- **AJ-03:** CheckIn/Measurements, escala histórica, ventanas, revision y edición protegida.
- **AJ-04:** identidad, email canónico, tokens y recuperación sin ampliar autorizaciones privadas.

**REV-LOG-001..013: RESOLVED en sus alcances existentes.** 001 catálogos de auditoría; 002 dominios; 003 email; 004 respuestas completas; 005 recorded_at; 006 Attempts sobre versiones retiradas; 007 correcciones editoriales; 008 orden canónico; 009 compatibilidades sin revocación ordinaria; 010 preservación de catálogos históricos; 011 validez de contenido; 012 Topics/asociaciones; 013 eliminación editorial. El [registro](11-pendientes-y-riesgos.md) conserva resoluciones completas y detalles físicos diferidos.

**DP-TRANS-002: RESOLVED.** Cancelación válida confirmada antes de expires_at → CANCELLED/ended_at efectivo; al vencer → EXPIRED/ended_at = expires_at, incluso con status todavía IN_PROGRESS. Terminales irreversibles, Answers eliminadas al confirmar y orden coherente; no se extiende plazo ni se envía fuera de vigencia.

## Evidencia de consistencia documental

**Inventario y atributos — Conforme.** 32 relaciones en seis módulos: Identidad 5/20 atributos, Evaluaciones 11/57, Seguimiento 7/33, Compatibilidad 2/10, Contenido 6/19 y Auditoría 1/8; total 147 atributos. El [inventario nominal](02-inventario-relaciones.md), fuente, diccionarios y matriz conservan la misma correspondencia.

**Claves e integridad — Conforme.** 32 PK, 9 AK, 6 URA y 41 FK (28 simples y 13 compuestas). Referencias compuestas conservan pertenencia histórica y componentes obligatorios; AUDIT_EVENT.actor_user_id mantiene su opcionalidad. Se conserva la FK simple adicional de Result a Attempt. La unicidad de hijos no se confunde con su existencia obligatoria por estado.

**Estados e historia — Conforme.** Se conservan las seis relaciones con status y sus transiciones autorizadas; no se crean estados de eliminación o de fallo de auditoría. READY congelada vuelve a DRAFT para corrección; versiones publicadas/retiradas mantienen configuración histórica. Retiro impide nuevos usos sin invalidar Attempts iniciados válidamente ni Measurements históricas.

**Evaluaciones — Conforme.** Publicación exige metodología compatible con respuestas completas, required = true y exactamente una Interpretation oficial respaldada por cada score alcanzable. SUBMITTED exige exactamente una Answer válida por pregunta y coordina SUM completo, interpretación existente, Result único y estado. Sin respaldos no se publica ni se inventa interpretación. Las respuestas completas no certifican validez psicométrica o comparabilidad.

**Concurrencia y ventanas — Conforme.** Máximo un Attempt IN_PROGRESS por usuario/instrumento, sin depender de versión; máximo una PUBLISHED/ACTIVE por catálogo correspondiente. Reemplazo de DimensionVersion ACTIVE es atómico. CheckIn tiene Measurement mínima, revisión + 1 por edición transaccional confirmada, protección del conjunto y versiones de Measurements, y vínculos nuevos de ContextTag ACTIVE. Ventana de edición estricta de 168 horas e intervalo original inclusivo de recorded_at; updated_at ausente hasta primera edición y después última modificación.

**Cancelación/expiración — Conforme.** DP-TRANS-002 RESOLVED: cancelación válida confirmada antes de expires_at produce CANCELLED/ended_at efectivo; al vencer corresponde EXPIRED/ended_at = expires_at, aunque status siga IN_PROGRESS. Terminales irreversibles, limpieza de Answers al confirmar y orden de confirmación coherente; no se permite SUBMITTED fuera de vigencia.

**Contenido/compatibilidad — Conforme.** Resource se publica con contenido revisado válido por tipo y Topic mínimo que se preserva ante cambios concurrentes. Correcciones menores conservan significado; cambio sustancial requiere nuevo Resource. Pares de compatibilidad del mismo catálogo, canónicos, simétricos, no transitivos; comparación consigo misma no requiere fila reflexiva. No se alteran definiciones/resultados históricos por cambios editoriales de Guidance actual.

**Privacidad y retención — Conforme.** ADMINISTRATOR no concede acceso privado automático. Borrado individual/de cuenta incluye dependencias personales y desvincula referencias de auditoría como actor o destino, preservando catálogos. Attempts terminales sin Result: 30 días desde ended_at; auditoría: máximo 180 desde occurred_at; backups cifrados: 30. Días de 24 horas transcurridas. Restauración reaplica supresiones y retenciones antes de habilitar servicio; no reinicia plazos.

**Auditoría y dominios — Conforme.** Tres actor_kind, 28 acciones, doce target_type y siete perfiles N/F/D/C/E/T/P, combinaciones cerradas y T/P obligatorios según acción. Metadata restringida y changed_fields sin valores anteriores/nuevos; no datos de bienestar o secretos. IDs opacos, orden total estable de compatibilidad, posiciones positivas sin consecutividad, enteros con signo, límites inclusivos, UTC, email canónico, URLs y nulabilidad conforme a dominios aprobados. No se eligen tipos físicos.

**Trazabilidad — Conforme en la correspondencia revisada.** La matriz mantiene 32 relaciones con antecedentes y RF/RN/RNF aplicables, decisiones AJ/VF y revisiones. DP-TRANS-001/002 están sincronizados con transversales y diccionarios afectados. Esta evidencia documental no constituye validación de una implementación ni certifica cobertura de aspectos de producto fuera del alcance lógico revisado.

## DP-TRANS-001: excepción aprobada y cierre

**Status: RESOLVED para el MVP v1.0.** La alternativa B fue aprobada expresamente por el usuario, con nueve condiciones.

1. Supresión de cuenta tiene prioridad sobre disponibilidad del nuevo registro de auditoría; la excepción es exclusiva de esta operación. Las demás operaciones administrativas/de seguridad auditables conservan auditoría garantizada antes de confirmar éxito.
2. USER, dependencias personales y desvinculación de referencias existentes en AUDIT_EVENT se confirman atómicamente. La excepción no permite borrados parciales o desvinculación diferida.
3. Supresión confirmada sigue válida si no puede registrarse USER_DELETED; no se fabrica evento de éxito.
4. Supresión no confirmada no se comunica completada; ante incertidumbre se verifica estado efectivo antes de comunicar éxito o fracaso definitivo.
5. No se conservan correos, identificadores personales, tokens o registros privados para reconstruir el evento.
6. Se conservan retención de 180 días y restauración con supresiones reaplicadas; no se agregan entidades, atributos, estados o relaciones.

**Contraste:** Identidad cumple eliminación íntegra; Evaluaciones/Seguimiento suprimen dependencias privadas sin borrar definiciones compartidas; Compatibilidad/Contenido preservan catálogos; Auditoría mantiene desvinculación, minimización y catálogos. Compatible con AJ-01..04, VF-01..05 y REV-LOG-001..013. La excepción reemplaza expresamente la exigencia general solo para supresión de cuenta; no es una contradicción interna sin resolver.

## Diferencias históricas y pendientes funcionales

**C-LOG-001 permanece documentado:** conceptual v0.1 con interpretación condicional frente a interpretación obligatoria posterior. AJ-01 bloquea publicación si no hay respaldo; el antecedente conceptual no se modifica ni se presenta como actualizado.

No se identifican contradicciones internas nuevas ni pendientes funcionales bloqueantes en las políticas MVP objeto de esta revisión. REV-LOG-001..013 y DP-TRANS-001/002 están RESOLVED en sus alcances; esto no cierra automáticamente Open Questions del baseline, otras capacidades no definidas o mecanismos físicos.

Los diccionarios conservan límites de especificación como cobertura total de credenciales no impuesta por PK/FK, procedimiento de asignación/revocación de ADMINISTRATOR y operaciones sobre catálogos no referenciados no autorizadas. No se inventan reglas para esos asuntos ni se presenta este dictamen como una autorización para implementarlos.

## Limitaciones aceptadas del MVP

- **Bienestar no clínico:** Yusay no constituye evaluación clínica; la confirmación de mayoría de edad es declarativa. Las interpretaciones dependen de fuentes y metodologías respaldadas.
- **Modelos derivados y edición:** Guidance es derivado y no tiene entidad persistente independiente; Resources no cuentan con versionado editorial completo en el MVP.
- **Alcance de validación:** diseño físico, estrategias de implementación y pruebas técnicas siguen pendientes. La auditoría no almacena información privada de bienestar.

- **Auditoría de supresión:** USER_DELETED puede faltar después de una supresión válida; no hay garantía de reconstrucción posterior ni datos personales retenidos para ello. Ausencia de evento no prueba persistencia de cuenta.
- **Scoring:** solo respuestas completas con SUM; instrumentos con metodología incompatible o sin interpretaciones oficiales completas no se publican. Alternativas futuras de omisiones siguen no aprobadas.
- **Comparabilidad:** requiere respaldo y compatibilidad explícita entre versiones distintas; simetría no implica transitividad y orden canónico no expresa cronología. Respuestas completas o SUM no acreditan validez psicométrica.
- **Compatibilidad histórica:** no existe revocación trazable o historial de vigencias; una declaración incorrecta requiere revisión de diseño antes de modificarla.
- **Contenido:** Guidance actual puede cambiar por edición/asociaciones; no se garantiza reconstruir Guidance de una fecha pasada ni se alteran Interpretation/Result históricos. URL válida no garantiza seguridad, disponibilidad o calidad; no se realizan solicitudes externas automáticas.
- **Catálogos y privacidad:** supresión personal no elimina catálogo compartido. Eliminación física ordinaria de Topics/Resources y de instrumentos/dimensiones/versiones históricamente referenciados no está autorizada.

Estas limitaciones provienen de decisiones existentes y no añaden políticas de producto.

## Riesgos residuales y validación posterior

- **Atomicidad y concurrencia:** una implementación incorrecta podría permitir doble cierre, envío tardío, duplicidad de estados máximos o cambios parciales. Claves/FKs no bastan; deberán verificarse las invariantes aprobadas al diseñar y validar mecanismos futuros.
- **Resultado incierto de supresión:** comprobación efectiva debe respetar la ausencia de datos personales retenidos para reconstruir auditoría. El mecanismo y procedimiento concreto siguen diferidos; el comportamiento funcional ya está resuelto.
- **Restauración:** una copia antigua no demuestra conocimiento actualizado de supresiones posteriores. Falta diseñar su fuente y coordinación técnica antes de habilitar restauraciones; la política no permite reintroducir información eliminada.
- **Retención:** limpieza tardía no autoriza superar máximos o reiniciar plazos; mecanismos/frecuencias deberán materializar los límites existentes.
- **Publicación y contenido:** comprobación de scores alcanzables, respaldo metodológico y revisión editorial requieren validación real, además de existencia referencial. Este dictamen documental no sustituye esas comprobaciones para cada versión/recurso.

Los riesgos no autorizan entidades, atributos, acciones o estados nuevos. No se certifica funcionamiento, rendimiento, seguridad ejecutada o validez psicométrica por revisar Markdown.

## Asuntos diferidos al diseño físico

PostgreSQL sigue [PROVISIONALLY ACCEPTED](../../06-decisions/ADR-001-database-engine.md), con alternativas preservadas. Backend, frontend, persistence/ORM, authentication, infraestructura y hosting siguen abiertos. Tipos, longitudes, colaciones, UUID/JSONB, comparador de IDs, serialización de metadata, índices, coordinación/aislamiento, limpieza, hashing, sesiones y realización de backups/restauración no se eligen.

## Condiciones para cambios posteriores

La congelación comprende relaciones/atributos, PK/AK/FK/unicidades, cardinalidades, dominios lógicos, integridad, estados/transiciones, publicación/versionado, concurrencia, privacidad/eliminación/retención, catálogos/restricciones de auditoría y decisiones funcionales aprobadas.

FROZEN no significa archivos físicamente inmodificables. Se permiten correcciones de redacción, formato y referencias que no alteren el significado aprobado. Todo cambio semántico o estructural requiere propuesta de cambio documentada, justificación, impacto, trazabilidad, aprobación expresa y una nueva versión o revisión formal de la línea base. Esta formalización no implementa cambios de alcance.

La especificación maestra permanece como fuente histórica recibida; decisiones posteriores se trazan en documentos vigentes sin atribuirles aprobación retroactiva. El conceptual v0.1 sigue cerrado e intacto. La revisión preparatoria conserva su carácter de antecedente, no sustituye esta aprobación.

## Conclusión formal de aprobación

Por autorización definitiva del responsable del proyecto del **2026-10-07**, el modelo lógico de datos del MVP de Yusay **v1.0** tiene dictamen **FAVORABLE**, queda **APPROVED** y su línea base oficial queda **FROZEN**.

No se identificaron discrepancias de inventario ni contradicciones internas que impidan formalizar este cierre. C-LOG-001 sigue registrado como diferencia histórica aprobada entre capas; no se borra ni se modifica el conceptual para ocultarla.

La línea base queda trazable y preparada documentalmente para una fase independiente de diseño físico; esa fase no se inicia automáticamente. Esta aprobación no constituye prueba técnica, validación productiva ni aprobación de tecnologías de aplicación.

No se genera SQL, migraciones, entidades de implementación ni código. La fuente y el conceptual original se conservan.

[Índice](00-indice.md) · [Revisión preparatoria](14-revision-preparacion-dictamen.md) · [Resoluciones](11-pendientes-y-riesgos.md).
