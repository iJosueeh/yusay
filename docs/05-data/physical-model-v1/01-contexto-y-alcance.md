# Contexto y alcance — diseño físico v1.0

**Estado de la fase: IN PROGRESS.** Fecha de inicio documental: 2026-10-07. No APPROVED ni FROZEN para esta fase; la fecha no representa aprobación ni prueba de implementación.

## Punto de partida y objetivos

Yusay inicia la preparación documental de su diseño físico PostgreSQL. Son dependencias confirmadas el [dictamen lógico v1.0](../logical-model-v1/12-dictamen-modelo-logico-v1.md), FAVORABLE / APPROVED / FROZEN, y [ADR-001](../../06-decisions/ADR-001-database-engine.md), ACCEPTED por el responsable del proyecto. El [inventario lógico](../logical-model-v1/02-inventario-relaciones.md) contiene 32 relaciones en seis módulos, 147 atributos, 32 PK, 9 AK, 6 URA y 41 FK, incluidas 13 compuestas.

Objetivos de la fase: preparar una correspondencia física verificable sin cambiar significado lógico; justificar futuras decisiones con las reglas existentes; identificar protección de integridad y coordinación; anticipar dependencias de privacidad/operación y definir qué evidencia permitirá revisar el diseño. La fase incorpora ahora las decisiones arquitectónicas de OQ-PHYS-003..010 y su correspondencia documental, sin implementar el esquema.

## Alcance

- Correspondencia futura de cada relación, atributo, clave, referencia, nulabilidad y dominio lógico con su representación física.
- Versión objetivo PostgreSQL 18 aprobada mediante OQ-PHYS-001; organización y nombres aprobados por OQ-PHYS-002; tipos y políticas aprobados mediante OQ-PHYS-003..006, con validación de capacidad/precisión, intercambio, expresiones y realización todavía pendiente.
- Justificación posterior de protección de integridad, unicidad condicionada e índices, distinguiendo invariantes entre filas/estados de restricciones estructurales.
- Diseño posterior de atomicidad y concurrencia de las operaciones aprobadas.
- Diseño posterior de supresión y desvinculación, retenciones y restauración sin reintroducir datos suprimidos.
- Trazabilidad y revisión documental de las decisiones físicas, sus limitaciones y dependencias de implementación.

El alcance conserva Instrument/InstrumentVersion, Assessment/CheckIn y Dimension/DimensionVersion separados. Timeline, Trend y Guidance no se convierten en relaciones nuevas. No se añade estado, atributo o relación para facilitar un mecanismo.

## Exclusiones y evolución documental

No SQL/DDL, migraciones, datos de prueba, entidades JPA, código de aplicación, APIs o despliegue. El inicio no seleccionó tipos ni mecanismos. La aprobación posterior permite documentar uuid, text, integer de base, timestamptz, boolean y jsonb, y las estrategias expresamente autorizadas; no permite implementar ni cerrar expresiones, índices concretos, aislamiento, modos de bloqueo o infraestructura pendientes. Tampoco se eligen ORM, framework, hosting, proveedor, autenticación o infraestructura.

El inicio creó 00/01. El documento 02 conserva organización/nombres y auditoría previa, y añade decisiones y correspondencia de los 147 atributos. Los documentos 03/04/05 consolidan integridad, transacciones y operación; 06 permanece planificado, sin dictamen. Pruebas técnicas y validación de carga se ejecutarán en una etapa autorizada posterior; su evidencia no se da por obtenida al iniciar esta fase.

No se reabren decisiones lógicas resueltas ni se cambia ADR-001. Las menciones provisionales de PostgreSQL en la carpeta lógica son antecedentes del momento de congelación; ADR-001 define la selección vigente. No existe contradicción entre esos estados secuenciales.

## Dependencias y autoridad de las fuentes

1. **Estructura aprobada:** [fuente maestra](../logical-model-v1/especificacion-maestra-v1.0.md), [seis diccionarios](../logical-model-v1/00-indice.md#documentos) e [integridad](../logical-model-v1/05-integridad-referencial.md). La fuente recibida se complementa con las resoluciones posteriores; no se usa el conceptual para reconstruir claves.
2. **Comportamiento aprobado:** [estados](../logical-model-v1/06-estados-y-transiciones.md), [transacciones](../logical-model-v1/07-transacciones-y-concurrencia.md) y [privacidad/retención](../logical-model-v1/08-privacidad-eliminacion-retencion.md).
3. **Semántica y cierres:** [dominios](../logical-model-v1/13-dominios-logicos.md), [REV-LOG/DP-TRANS](../logical-model-v1/11-pendientes-y-riesgos.md), [trazabilidad lógica](../logical-model-v1/10-matriz-trazabilidad.md) y AJ-01..04/VF-01..05.
4. **Motor y límites:** ADR-001 selecciona PostgreSQL. Su pregunta OQ-ARCH-006 se concreta en esta fase mediante OQ-PHYS-001: rama 18, referencia inicial 18.6. OQ-PHYS-002 formaliza organización/nombres aprobados; ADR-001 permanece intacto y OQ-PHYS-003..010 quedan RESOLVED en alcance arquitectónico; los mecanismos residuales siguen OPEN.
5. **Dependencias externas al mapeo:** [estrategias de acceso a datos, autenticación y operación](../../06-decisions/README.md#pending-architectural-decisions) permanecen abiertas. Deben coordinarse cuando afecten la realización, sin elegirlas aquí.
6. **Rendimiento:** [OQ-NFR-001](../../02-product/non-functional-requirements.md#oq-nfr-001) mantiene carga, volumen y criterios de medición pendientes. No se inventa SLA. Los estados históricos de requisitos no se transforman en aprobaciones globales por esta fase.

El responsable del proyecto evalúa futuras decisiones cuando se presenten. IN PROGRESS autoriza preparación documental, no presume aprobadas sus soluciones. Un conflicto real se informa antes de modificar cualquier elemento aprobado.

## Matriz inicial de trazabilidad

Los IDs TF-PHYS identifican filas de trabajo documental, no requisitos nuevos. Cada fila enlaza la línea base con decisiones arquitectónicas aprobadas y aspectos físicos aún pendientes. El cierre de una OQ no equivale a cerrar su implementación. OQ-PHYS identifica preguntas de esta fase; remiten a preguntas arquitectónicas existentes sin sustituir sus registros.

| ID | Fuente lógica o decisión vigente | Decisión aprobada / trabajo residual | Decisión / entregable |
| --- | --- | --- | --- |
| TF-PHYS-001 | ADR-001 ACCEPTED; inventario de 32 relaciones; OQ-PHYS-001/002 RESOLVED | PostgreSQL 18 / referencia 18.6; esquema yusay, escenario B, 32 tablas/147 columnas y 88 nombres objetivo aprobados; tipos/políticas 003..010 aprobados; mecanismos pendientes | OQ-PHYS-001/002 RESOLVED; [02](02-decisiones-y-mapeo-fisico.md) |
| TF-PHYS-002 | VF-01; 32 PK, 9 AK, 6 URA y 41 FK | uuid en familias, claves y referencias; validar realización exacta, capacidad de componentes de dominio y nulabilidad | OQ-PHYS-003/005 RESOLVED (arquitectura); 02/03 |
| TF-PHYS-003 | REV-LOG-008; pares canónicos, simétricos y no transitivos | Orden canónico UUID de 16 bytes sin signo aprobado; comparador/serialización backend por verificar | OQ-PHYS-003 RESOLVED (arquitectura); 02/03 |
| TF-PHYS-004 | AJ-04; REV-LOG-003; email único sin distinción de mayúsculas, codes sensibles a mayúsculas | Email text/canonicalización aprobados; expresión/colación funcional candidatas pendientes; revisión funcional C-PHYS-001 | OQ-PHYS-004 RESOLVED (arquitectura); 02/03 |
| TF-PHYS-005 | REV-LOG-002; UTC, enteros, textos, opcionalidad y valores controlados | Tipos base y nulabilidad mapeados en 147 filas; capacidad, precisión y destino polimórfico pendientes | OQ-PHYS-005 RESOLVED (arquitectura); 02 |
| TF-PHYS-006 | REV-LOG-001; tres actores, 28 acciones, doce destinos, perfiles N/F/D/C/E/T/P | metadata jsonb y validación estricta aprobadas; guardas por perfil y representación de IDs pendientes | OQ-PHYS-006/007 RESOLVED (arquitectura); 02/03/05 |
| TF-PHYS-007 | AJ-01; REV-LOG-004; todas las respuestas, SUM y cobertura oficial | Envío completo/SUM/Result/Interpretation atómicos aprobados; capacidad y protección física conjunta pendientes | OQ-PHYS-005/007 RESOLVED (arquitectura); 03/04 |
| TF-PHYS-008 | VF-02/03; máximos PUBLISHED, ACTIVE e IN_PROGRESS | Índices únicos parciales PUBLISHED/ACTIVE y coordinación aprobados; definiciones/bloqueos y máximo IN_PROGRESS pendientes | OQ-PHYS-007 RESOLVED (arquitectura); 03/04 |
| TF-PHYS-009 | DP-TRANS-002; expiración estricta, cancelación y limpieza | Límites temporales estrictos y limpieza atómica aprobados; reloj, confirmación concurrente y lotes pendientes | OQ-PHYS-005/007/008 RESOLVED (arquitectura); 04/05 |
| TF-PHYS-010 | AJ-03; REV-LOG-005; revision y ventana de CheckIn | Control optimista revision aprobado; operación efectiva completa, ventana y protección histórica pendientes | OQ-PHYS-005/007 RESOLVED (arquitectura); 03/04 |
| TF-PHYS-011 | AJ-04; tokens y recuperación atómica | Tokens backend criptográficos y consumo único aprobados; hash, emisión/consumo y revocación pendientes | OQ-PHYS-005/007/010 RESOLVED (arquitectura); 04/05 |
| TF-PHYS-012 | AJ-02; REV-LOG-007/011/012/013; publicación editorial | Validación editorial y Topic mínimo PUBLISHED aprobados; coordinación concreta entre filas pendiente | OQ-PHYS-005/007 RESOLVED (arquitectura); 03/04 |
| TF-PHYS-013 | VF-04; DP-TRANS-001; supresión prioritaria | Supresión/desvinculación atómicas y excepción de auditoría preservadas; aislamiento de fallos y resultado incierto pendientes | OQ-PHYS-007/008/010 RESOLVED (arquitectura); 04/05 |
| TF-PHYS-014 | VF-04; retenciones 30/180/30 días y restauración con supresiones | Lotes/registro duradero/restauración aislada aprobados; coordinación externa y verificación operativa pendientes | OQ-PHYS-008 RESOLVED (arquitectura); 05 |
| TF-PHYS-015 | Correspondencia lógica y consultas previstas; OQ-NFR-001 | Criterios de justificación de índices aprobados; OQ-NFR-001 y evidencia de carga/planes pendientes | OQ-PHYS-009 RESOLVED (arquitectura); 03/06 |
| TF-PHYS-016 | Dictamen FROZEN; REV-LOG/DP-TRANS RESOLVED | Trazabilidad consolidada; validación de mecanismos y propuesta de dictamen aún pendientes | Todas; 06 |

El entregable 02 contiene nombres y tipos/políticas aprobados; 03..05 están documentados y 06 sigue planificado. TF-PHYS conserva sus IDs. La matriz de consolidación siguiente distingue decisiones resueltas de mecanismos abiertos.

## Decisiones y mecanismos de la fase

La clasificación indica **cuándo una respuesta resulta necesaria**, no una decisión de producto. Bloqueante del mapeo significa necesaria antes de consolidar la parte física correspondiente; no bloquea esta preparación ni el análisis de otras partes independientes. Diferible al mapeo significa que puede estudiarse después de la representación básica; sigue pendiente antes de cerrar su entregable. No se exige elegir hosting/ORM para iniciar representación lógica-física.

### OQ-PHYS-001

- **ID:** OQ-PHYS-001.
- **Pregunta original:** ¿Qué versión objetivo de PostgreSQL servirá de referencia verificable para el diseño?
- **Status:** RESOLVED.
- **Decisión:** PostgreSQL 18; versión principal objetivo: 18.
- **Versión menor de referencia inicial:** 18.6.
- **Autoridad:** responsable del proyecto.
- **Fecha de aprobación:** 2026-10-07. Corresponde a la aprobación de esta decisión, no a la creación o modificación del archivo.
- **Alcance:** diseño físico v1.0 de Yusay; no aprobación global de la fase.
- **Justificación:** rama estable y soportada, referencia menor disponible, documentación oficial y capacidades relacionales/transaccionales adecuadas para evaluar el diseño de Yusay. No constituye validación técnica completa.
- **Verificación oficial (2026-10-07):** las [notas de PostgreSQL 18.6](https://www.postgresql.org/docs/18/release-18-6.html) indican publicación el 2026-08-13. La [política de versiones y soporte](https://www.postgresql.org/support/versioning/) identifica 18.6 como menor vigente y la rama 18 como soportada; lanzamiento principal 2025-09-25 y fin de soporte previsto 2030-11-14. La comunidad mantiene cada versión principal durante cinco años con correcciones y actualizaciones de seguridad aplicables.
- **Referencias para el diseño:** [manual oficial de la rama 18](https://www.postgresql.org/docs/18/) —identificado como documentación 18.6 al verificar— y notas de la versión menor utilizada. Se contrastarán capacidades exclusivamente con la rama 18, sin atribuirle características exclusivas de versiones posteriores.
- **Consecuencias:** mapeo y mecanismos futuros deben comprobarse frente a PostgreSQL 18 y sus notas pertinentes. La selección eliminó el bloqueo de versión. La posterior resolución de OQ-PHYS-002 elimina el bloqueo de organización/nombres; OQ-PHYS-003..010 están RESOLVED en alcance arquitectónico; los mecanismos residuales permanecen OPEN.
- **Política de actualización:** se permiten actualizaciones menores dentro de la rama 18, previa comprobación de compatibilidad y aplicación de las pruebas pertinentes cuando exista implementación. 18.6 es referencia documental inicial, no una fijación permanente. Un cambio de versión principal exige nueva evaluación y aprobación expresa.
- **Límites:** no selecciona hosting, proveedor, tipos, identificadores, índices, aislamiento, ORM ni mecanismos; no inicia el mapeo de las 32 relaciones.
- **Impacto:** entregables planificados 02..06; TF-PHYS-001.
- **Trazabilidad:** [OQ-ARCH-006](../../06-decisions/ADR-001-database-engine.md#oq-arch-006) es el antecedente arquitectónico; su concreción se registra aquí, sin modificar ADR-001. OQ-PHYS-002 está RESOLVED por aprobación posterior de organización/nombres; OQ-PHYS-003..010 están RESOLVED en alcance arquitectónico; diseño físico IN PROGRESS y modelo lógico FROZEN.

### OQ-PHYS-002

- **ID:** OQ-PHYS-002.
- **Pregunta original:** ¿Qué convenciones de nombres y organización de objetos físicos se utilizarán?
- **Status:** RESOLVED.
- **Fecha de aprobación:** 2026-10-07; corresponde a la aprobación, no a la creación del archivo.
- **Autoridad:** responsable del proyecto.
- **Alcance:** convenciones de nombres y organización de objetos del diseño físico v1.0.
- **Decisión:** aprobación expresa de OQ-PHYS-002-A..F y del escenario B: esquema único `yusay`, USER → `yusay.app_user` conservando `user_id`, singular/minúsculas/snake_case/sin comillas, columnas literales, patrones y 88 nombres objetivo de PK/AK/URA/FK, marcador `ref_` en las seis URA, prefijo `uxp_`, referencias de tablas calificadas y conservación exacta de URA largas sin abreviaciones.
- **Registro y evidencia:** [Formalización, seis resoluciones y matrices](02-decisiones-y-mapeo-fisico.md); conserva alternativas históricas no seleccionadas y corrige etiquetas D01..D06 sin alterar destinos.
- **Motivo:** correspondencia uniforme y rastreable, diferenciando nomenclatura física de identidad lógica.
- **Clasificación original:** BLOQUEANTE para consolidar nombres; bloqueo resuelto exclusivamente en organización/nomenclatura.
- **Impacto:** 02/03; TF-PHYS-001. OQ-PHYS-002-A..F están RESOLVED.
- **Dependencias restantes:** mecanismos residuales de OQ-PHYS-003..010 OPEN. OQ-PHYS-002 por sí sola no aprueba tipos, generación/orden físico de IDs, colaciones/email, CHECK concretos, índices/columnas/predicados, transacciones/concurrencia, supresión/retención físicas, roles/permisos/search_path, proveedor, ORM, frameworks, SQL o migraciones. Los CHECK ilustrativos no forman un inventario físico aprobado.
- **Límite:** diseño físico IN PROGRESS; no aprobación/congelación global ni autorización de implementación. Modelo lógico, conceptual y ADR-001 intactos.

### OQ-PHYS-003

- **ID:** OQ-PHYS-003 — Identificadores.
- **Status:** RESOLVED — exclusivamente decisión arquitectónica.
- **Autoridad / fecha de registro:** responsable del proyecto / 2026-10-07; aprobación expresa en el encargo de consolidación.
- **Decisión:** uuid nativo, UUID v4 y generación centralizada en PostgreSQL para 16 raíces. Cinco identidades compartidas reutilizan valores; once PK compuestas conservan componentes. Orden de los 16 bytes sin signo, sin significado temporal ni autorización implícita.
- **Motivo:** materializar las reglas aprobadas sin ampliar ni modificar el modelo congelado.
- **Trazabilidad:** REV-LOG-002/008; VF-01.
- **Impacto / mecanismos pendientes:** 02, 03 y MP-PHYS-016.
- **Límite:** el cierre no aprueba implementación, expresiones definitivas, pruebas inexistentes ni el diseño físico global.

### OQ-PHYS-004

- **ID:** OQ-PHYS-004 — Correo.
- **Status:** RESOLVED — exclusivamente decisión arquitectónica.
- **Autoridad / fecha de registro:** responsable del proyecto / 2026-10-07; aprobación expresa en el encargo de consolidación.
- **Decisión:** text; unicidad sin distinción de mayúsculas; parte local ASCII simplificada sin comillas y con puntos válidos; dominio IDNA/Punycode ASCII en minúsculas; conservación de parte local y ausencia de reglas de proveedores. Índice funcional lower(email) es candidato, sin expresión/colación/regex/límites cerrados. El cambio de correo está en revisión formal C-PHYS-001 y no se habilita.
- **Motivo:** materializar las reglas aprobadas sin ampliar ni modificar el modelo congelado.
- **Trazabilidad:** AJ-04; REV-LOG-003.
- **Impacto / mecanismos pendientes:** 02, 03 y MP-PHYS-002; C-PHYS-001.
- **Límite:** el cierre no aprueba implementación, expresiones definitivas, pruebas inexistentes ni el diseño físico global.

### OQ-PHYS-005

- **ID:** OQ-PHYS-005 — Tipos.
- **Status:** RESOLVED — exclusivamente decisión arquitectónica.
- **Autoridad / fecha de registro:** responsable del proyecto / 2026-10-07; aprobación expresa en el encargo de consolidación.
- **Decisión:** text general; integer como base de enteros, smallint/bigint solo con justificación de dominio/cálculos; timestamptz y UTC de conexión; boolean; valores controlados text + CHECK. Sin ENUM, defaults inventados, longitudes nuevas o alteración de nulabilidad.
- **Motivo:** materializar las reglas aprobadas sin ampliar ni modificar el modelo congelado.
- **Trazabilidad:** REV-LOG-002; seis diccionarios.
- **Impacto / mecanismos pendientes:** 02; MP-PHYS-001/008.
- **Límite:** el cierre no aprueba implementación, expresiones definitivas, pruebas inexistentes ni el diseño físico global.

### OQ-PHYS-006

- **ID:** OQ-PHYS-006 — Auditoría.
- **Status:** RESOLVED — exclusivamente decisión arquitectónica.
- **Autoridad / fecha de registro:** responsable del proyecto / 2026-10-07; aprobación expresa en el encargo de consolidación.
- **Decisión:** metadata jsonb y validación estricta de N/F/D/C/E/T/P; tres actores, 28 acciones y doce destinos y combinaciones aprobados. PostgreSQL protege estructura apropiada y backend construye metadata autorizada. No hay payload libre ni datos privados.
- **Motivo:** materializar las reglas aprobadas sin ampliar ni modificar el modelo congelado.
- **Trazabilidad:** REV-LOG-001/002; VF-04/05.
- **Impacto / mecanismos pendientes:** 02, 03 y 05; MP-PHYS-003.
- **Límite:** el cierre no aprueba implementación, expresiones definitivas, pruebas inexistentes ni el diseño físico global.

### OQ-PHYS-007

- **ID:** OQ-PHYS-007 — Integridad y concurrencia.
- **Status:** RESOLVED — exclusivamente decisión arquitectónica.
- **Autoridad / fecha de registro:** responsable del proyecto / 2026-10-07; aprobación expresa en el encargo de consolidación.
- **Decisión:** Integridad respaldada por PostgreSQL cuando sea adecuado y coordinación/autorización backend. Índices únicos parciales para máximo PUBLISHED/ACTIVE y bloqueo transaccional de entidades al publicar/retirar; envío atómico; control optimista de CheckIn; expiración estricta; tokens de uso único y supresión conforme a DP-TRANS. No se cierran mecanismos específicos restantes.
- **Motivo:** materializar las reglas aprobadas sin ampliar ni modificar el modelo congelado.
- **Trazabilidad:** AJ-01..04; VF-01..05; DP-TRANS-001/002.
- **Impacto / mecanismos pendientes:** 03, 04 y 05; MP-PHYS-004..010.
- **Límite:** el cierre no aprueba implementación, expresiones definitivas, pruebas inexistentes ni el diseño físico global.

### OQ-PHYS-008

- **ID:** OQ-PHYS-008 — Retención y recuperación.
- **Status:** RESOLVED — exclusivamente decisión arquitectónica.
- **Autoridad / fecha de registro:** responsable del proyecto / 2026-10-07; aprobación expresa en el encargo de consolidación.
- **Decisión:** Retenciones 30/180/30 períodos de 24 horas, limpieza programada por lotes transaccional e idempotente; supervisión privada. Registro duradero de supresiones independiente de respaldos, restauración aislada y aplicación/verificación antes de accesos. Rotación, cifrado, acceso restringido y pruebas de backups; no proveedor/frecuencia seleccionados.
- **Motivo:** materializar las reglas aprobadas sin ampliar ni modificar el modelo congelado.
- **Trazabilidad:** VF-04/05; DP-TRANS-001.
- **Impacto / mecanismos pendientes:** 05; MP-PHYS-008/011/012.
- **Límite:** el cierre no aprueba implementación, expresiones definitivas, pruebas inexistentes ni el diseño físico global.

### OQ-PHYS-009

- **ID:** OQ-PHYS-009 — Índices y rendimiento.
- **Status:** RESOLVED — exclusivamente decisión arquitectónica.
- **Autoridad / fecha de registro:** responsable del proyecto / 2026-10-07; aprobación expresa en el encargo de consolidación.
- **Decisión:** Separar integridad de candidatos de rendimiento. Revisar cobertura, cada FK, orden compuesto, redundancia y costos; justificar con consultas/carga/planes, sin cifras inventadas. OQ-NFR-001 sigue OPEN.
- **Motivo:** materializar las reglas aprobadas sin ampliar ni modificar el modelo congelado.
- **Trazabilidad:** Claves aprobadas; OQ-NFR-001.
- **Impacto / mecanismos pendientes:** 03 y futuro 06; MP-PHYS-013.
- **Límite:** el cierre no aprueba implementación, expresiones definitivas, pruebas inexistentes ni el diseño físico global.

### OQ-PHYS-010

- **ID:** OQ-PHYS-010 — Seguridad y operación.
- **Status:** RESOLVED — exclusivamente decisión arquitectónica.
- **Autoridad / fecha de registro:** responsable del proyecto / 2026-10-07; aprobación expresa en el encargo de consolidación.
- **Decisión:** Autenticación/autorización backend, controles complementarios PostgreSQL y privilegios mínimos. Evaluación RLS por relación/operación y aislamiento de identidad en conexiones reutilizadas. Revocación efectiva de sesiones/credenciales y protección tras restaurar; separación de responsabilidades técnicas sin roles definitivos ni tecnología de sesiones elegida.
- **Motivo:** materializar las reglas aprobadas sin ampliar ni modificar el modelo congelado.
- **Trazabilidad:** AJ-04; VF-04/05; privacidad lógica.
- **Impacto / mecanismos pendientes:** 04 y 05; MP-PHYS-014/015.
- **Límite:** el cierre no aprueba implementación, expresiones definitivas, pruebas inexistentes ni el diseño físico global.

## Registro de mecanismos pendientes

Los IDs MP-PHYS separan trabajo de implementación no resuelto de las decisiones OQ-PHYS-003..010 RESOLVED. Son preguntas de diseño, no requisitos nuevos ni autorizaciones de implementación.

| ID | Invariante o Pregunta Concreta | Especificación Física Concretada | Impacto / Documentos | Clasificación | Status Documental |
| --- | --- | --- | --- | --- | --- |
| MP-PHYS-001 | Capacidad numérica de los 15 enteros y representación de `target_identifier` polimórfico. | `integer` (32-bit signed: ±2.14×10⁹). `SUM(integer)` retorna `bigint` pero también puede desbordarse y no garantiza caber en `integer`. Validación segura de cotas obligatoria [Score_min, Score_max] antes de publicar instrumentos en catálogo editorial. `target_identifier` resuelto como `text` nativo UUIDv4. | 02; Atributos numéricos y Auditoría | Integridad y Tipos | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-002 | Expresión, colación ASCII, canonicalización IDNA y unicidad de email. | `text`, parte local ASCII simplificada `[A-Za-z0-9._+-]` (sin `%`, sin puntos extremos ni consecutivos, casing preservado), dominio IDNA/Punycode minúsculas (soporte TLD `xn--...`). Índice funcional candidato `lower(email) COLLATE "C"`. C-PHYS-001 inmutable. | 02/03; `app_user.email` y AK | Integridad y Privacidad | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-003 | Validación estructural de los 7 perfiles de auditoría N/F/D/C/E/T/P en `jsonb`. | Distinción `NULL` SQL vs `jsonb null` (prohibido). Guardas estructurales por acción, validación cruzada y serialización canónica. | 02/03/05; `audit_event` | Auditoría y Seguridad | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-004 | Máximo una versión publicada/activa por Instrument y Dimension. | Índices únicos parciales declarativos (`uxp_instrument_version_single_published` y `uxp_dimension_version_single_active`) más bloqueo pesimista `FOR UPDATE` en catálogo padre. | 03/04; `instrument_version`, `dimension_version` | Integridad y Concurrencia | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-005 | Máximo un intento IN_PROGRESS por usuario e instrumento entre versiones. | Índice único parcial `uxp_assessment_attempt_single_in_progress` sobre `(user_id, instrument_id) WHERE status = 'IN_PROGRESS'`. Guarda temporal en transacción. | 03/04; `assessment_attempt` | Concurrencia e Integridad | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-006 | Envío atómico de evaluación y coherencia estricta SUBMITTED / Result. | Transacción atómica con `SELECT ... FOR UPDATE`, verificación al 100% de respuestas obligatorias, cálculo seguro, enlace a interpretación oficial e inserción atómica de Result. Equivalencia bidireccional reforzada mediante guarda/trigger diferible a COMMIT. | 03/04; Envío de evaluación | Integridad y Concurrencia | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-007 | Edición optimista de CheckIn mediante `revision` y ventana de 168 horas. | `UPDATE ... SET revision = revision + 1 WHERE revision = $rev AND clock_timestamp() < created_at + 168h`. Preservación estricta del conjunto de mediciones y escalas. | 03/04; `check_in`, `measurement` | Concurrencia | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-008 | Expiración de intentos a las 720h, precisión temporal y limpieza por lotes. | Vigencia estricta por `clock_timestamp() < expires_at` independiente del worker. Transición a `EXPIRED` con purga atómica de respuestas. Lote de retención a 30 días. | 02/04/05; Timestamps y Ciclo de vida | Operación y Concurrencia | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-009 | Formato de hash, emisión y consumo único atómico de tokens. | Secreto CSPRNG 256 bits no persistido; almacenamiento de `token_hash` SHA-256 (64 hex). Consumo atómico con bloqueo pesimista `SELECT ... FOR UPDATE` (sin `SKIP LOCKED`), reevaluación estricta de vigencia, borrado de todos los tokens del usuario y revocación de sesiones. | 02/04/05; Tokens de verificación y reset | Seguridad y Concurrencia | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-010 | Aislamiento de fallos de auditoría en supresión de cuenta (DP-TRANS-001). | Eliminación atómica de `app_user` (cascada sobre datos personales) y desvinculación de auditoría; fallo de `USER_DELETED` aislado sin bloquear la supresión. | 04/05; Supresión y Auditoría | Privacidad y Transacciones | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-011 | Registro duradero de supresiones independiente de backups. | Log de supresión externo con `user_id` y `deleted_at` sin datos personales. Retención vinculada al ciclo de backups restaurables (30 días + rotación) y purga segura. | 04/05; Recuperación y Privacidad | Privacidad y Operación | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-012 | Protocolo de restauración en entorno aislado con reaplicación de supresiones. | Restauración en red aislada, reaplicación obligatoria de supresiones y revocaciones antes de habilitar tráfico. Verificación de conteo cero de tombstones. | 05/06; Operación y Recuperación | Operación y Privacidad | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-013 | Criterios y consultas para justificar índices de rendimiento (OQ-NFR-001). | Separación estricta de índices de integridad. Índices de rendimiento diferidos por ausencia de métricas y perfiles de carga aprobados. OQ-NFR-001 permanece abierto. | 03/06; Rendimiento | Rendimiento | OPEN / DIFERIDO |
| MP-PHYS-014 | Separación de roles técnicos, control de search_path y evaluación RLS. | Roles con privilegios mínimos (`yusay_app`, `yusay_migrator`, `yusay_worker`, `yusay_backup`). `search_path` fijo `yusay, pg_temp`. RLS selectivo sin sobrecarga global. | 05; Seguridad y Roles | Seguridad | ESPECIFICADO DOCUMENTALMENTE |
| MP-PHYS-015 | Revocación efectiva de accesos tras operaciones sensibles y restauraciones. | Política de revocación JWT conjunta: regla temporal (`to_timestamp(token.iat) >= date_trunc('second', password_changed_at)`) acumulada a la huella de versión de credencial `pwd_at` en microsegundos Unix exactos, con consulta a PostgreSQL en cada uso, rechazo de tokens sin el claim y avance estrictamente creciente de `password_changed_at`; más `status = 'BLOCKED'`. Revocación global resuelta en BD; revocación selectiva por sesión en `SIGN_OUT` implementada en la capa de backend con denylist Redis compartida por `jti` (TTL hasta la expiración del token, *fail-closed* ante indisponibilidad, persistencia AOF) sin modificar el modelo lógico normativo congelado. | 04/05; Autenticación y Sesiones | Seguridad y Control de Acceso | RESUELTO |
| MP-PHYS-016 | Contratos de intercambio UUID v4, compatibilidad de bytes y no re-generación. | Intercambio textual canónico RFC 9562 (36 caracteres minúsculas). Comparación binaria exacta memcmp de 16 bytes sin signo. Prohibición de regenerar IDs compartidos. | 02/04/05; Identificadores | Integración y Datos | ESPECIFICADO DOCUMENTALMENTE |

### C-PHYS-001 — Inmutabilidad del correo electrónico en el MVP

- **ID:** C-PHYS-001.
- **Status:** RESOLVED — decisión ratificada por el responsable del proyecto (2026-10-07).
- **Decisión:** El correo electrónico permanece **estrictamente inmutable durante todo el MVP**. No existe funcionalidad, endpoint, servicio ni flujo de cambio de correo electrónico.
- **Motivo:** Preservar la simplicidad, seguridad y congruencia de la línea base congelada del MVP (AJ-04, REV-LOG-003, D01-Identidad). La revisión formal solicitada concluye ratificando la prohibición vigente.
- **Impacto y línea base:** Se mantiene la línea base lógica 100% intacta y congelada. No se añaden tokens de cambio de email, estados de verificación transitoria ni tablas auxiliares. La unicidad y canonicalización se resuelven exclusivamente para el registro/alta de cuenta (`MP-PHYS-002`).

## Matriz de decisiones y documentos consolidados

| Decisión | Fuente preservada | Documento que formaliza | Pendientes / Mecanismos asociados |
| --- | --- | --- | --- |
| OQ-PHYS-001/002 RESOLVED | ADR-001; nombres aprobados previos | 01/02; 00 navega | Sin cambios de rama, esquema, columnas o 88 nombres |
| OQ-PHYS-003 RESOLVED | PK/FK/URA; REV-LOG-002/008 | [02](02-decisiones-y-mapeo-fisico.md#oq-phys-003), 03 | MP-PHYS-016 |
| OQ-PHYS-004 RESOLVED | AJ-04; REV-LOG-003; C-PHYS-001 RESOLVED | [02](02-decisiones-y-mapeo-fisico.md#oq-phys-004), 03 | MP-PHYS-002; C-PHYS-001 inmutable |
| OQ-PHYS-005 RESOLVED | Diccionarios; REV-LOG-002 | [02: 147 atributos](02-decisiones-y-mapeo-fisico.md#correspondencia-de-los-147-atributos) | MP-PHYS-001/008 |
| OQ-PHYS-006 RESOLVED | REV-LOG-001/002; Auditoría | [02](02-decisiones-y-mapeo-fisico.md#oq-phys-006), 03/05 | MP-PHYS-003 |
| OQ-PHYS-007 RESOLVED | AJ/VF; DP-TRANS-001/002 | [03](03-integridad-e-indices.md), [04](04-transacciones-y-concurrencia.md) | MP-PHYS-004..010 |
| OQ-PHYS-008 RESOLVED | VF-04/05; privacidad lógica | [05](05-privacidad-eliminacion-y-operacion.md) | MP-PHYS-008/011/012 |
| OQ-PHYS-009 RESOLVED | Claves; OQ-NFR-001 OPEN | [03](03-integridad-e-indices.md) | MP-PHYS-013; sin índices de rendimiento aprobados (OQ-NFR-001 OPEN) |
| OQ-PHYS-010 RESOLVED | AJ-04; privacidad y revocación | [04](04-transacciones-y-concurrencia.md), [05](05-privacidad-eliminacion-y-operacion.md) | MP-PHYS-014/015 |

Las filas TF-PHYS originales siguen trazando fuentes hacia el trabajo físico. Esta matriz registra su consolidación sin reemplazar ni ampliar requisitos. Los cierres arquitectónicos no cierran OQ-NFR-001, mecanismos MP-PHYS ni la revisión funcional C-PHYS-001.

## Criterios de aceptación de la fase

### Evidencia documental de esta consolidación — 2026-10-07

- Correspondencia verificada de los 147 atributos, en orden, con obligatoriedad idéntica a los seis diccionarios; 32 relaciones conservadas. Tipos: 62 uuid, 51 text, 16 timestamptz, 15 integer de base sujetos a capacidad, un boolean, un jsonb y un destino polimórfico pendiente.
- Las 88 filas de nombres aprobados de PK/AK/URA/FK permanecen exactamente iguales a las de antes de esta consolidación: 32/9/6/41, incluidas 13 FK compuestas. Se conservan las 16 familias y sus 62 usos.
- Se comprobaron 290 referencias locales de los seis documentos físicos, sin archivos de destino ausentes. Esta comprobación verifica archivos, no demuestra funcionamiento de APIs ni ejecución de mecanismos.
- Comparación SHA-256 previa/posterior: 41 documentos existentes fuera de la capa física permanecen intactos, incluidos el conceptual, todos los documentos lógicos y ADR-001. Solo se modificaron los tres documentos físicos existentes y se crearon 03/04/05; ningún archivo se eliminó.
- No se ejecutaron pruebas de un esquema, ni se generaron SQL, migraciones, código, commits o dictamen físico. La verificación documental no sustituye la validación posterior de capacidad, concurrencia, seguridad, recuperación y rendimiento.

### Condiciones para revisar el diseño completo

Son criterios propuestos para revisar los entregables de diseño, no una aprobación anticipada ni resultados de pruebas.

1. Correspondencia completa y verificable de las 32 relaciones/147 atributos, PK/AK/URA/FK, cardinalidades, dominios y reglas, sin cambios semánticos o estructurales de la línea base.
2. Cada decisión física tiene fuente, alternativas, justificación, impacto, estado y trazabilidad; bloqueantes pertinentes están resueltas antes de consolidar su mapeo.
3. Invariantes clasificadas según protección requerida y coordinación; estados, publicación, versiones históricas, límites temporales, revisión y DP-TRANS conservan significado.
4. Supresión/desvinculación, retención y restauración están cubiertas por diseño documentado; asuntos operativos no resueltos permanecen visibles y no se presentan como garantías cumplidas.
5. Índices propuestos se justifican por integridad o accesos identificados; evaluación de rendimiento reconoce carga y métricas pendientes cuando no exista evidencia.
6. Plan de verificación identifica escenarios normales, límites, fallos y concurrencia, sin afirmar pruebas ejecutadas. Pruebas sobre esquema/código requieren autorización posterior y no son una salida de esta preparación.
7. Fuentes aprobadas permanecen intactas, enlaces válidos y matriz cubre entregables. Dictamen físico y cualquier APPROVED/FROZEN requieren aprobación expresa del responsable del proyecto.

## Riesgos de la fase

- **Deriva semántica:** tipos, comparación o límites podrían cambiar igualdad, alcance numérico o temporal. Contrastar con dominios aprobados y presentar incompatibilidades antes de alterar la línea base.
- **Protección insuficiente:** claves no garantizan completitud, publicación ni atomicidad. Hacer visible la asignación pendiente en TF-PHYS/OQ-PHYS-007.
- **Soluciones anticipadas:** adoptar capacidades del motor por disponibilidad podría generar decisiones sin justificación. ADR-001 acepta el motor, no sus mecanismos concretos.
- **Privacidad/operación:** diseño incompleto de borrado, auditoría, resultados inciertos o restauración puede incumplir políticas. DP-TRANS-001 está resuelto funcionalmente; su realización técnica sigue pendiente.
- **Optimización sin evidencia:** desconocer carga o consultas impide justificar rendimiento. OQ-NFR-001 permanece abierto, sin SLA inventado.
- **Dependencias de integración:** sesiones y seguridad pueden quedar fuera de la protección puramente relacional. Identificar responsabilidades sin elegir tecnologías o ampliar permisos.

Se mantienen las limitaciones lógicas aceptadas: bienestar no clínico, mayoría de edad declarativa, metodología respaldada, compatibilidad explícita sin revocación trazable, Guidance derivado, ausencia de versionado editorial completo y posible ausencia de USER_DELETED tras supresión válida. No se reclasifican como defectos nuevos.

## Propuesta de orden de trabajo

1. Revisar C-PHYS-001 mediante control formal de cambios sin tocar la línea base congelada.
2. Validar capacidad numérica, destino polimórfico, canonicalización/email y guardas de metadata (MP-PHYS-001..003), usando los 147 atributos de 02.
3. Diseñar y justificar mecanismos de integridad y concurrencia (MP-PHYS-004..010), incluidos todos los límites y fallos de 03/04.
4. Diseñar coordinación duradera de supresiones, restauración y revocaciones, y controles de acceso (MP-PHYS-011/012/014/015).
5. Concretar contratos de intercambio y generación UUID (MP-PHYS-016) sin regenerar claves compartidas.
6. Recoger consultas/carga y criterios de OQ-NFR-001 para justificar rendimiento (MP-PHYS-013).
7. Preparar 06 con evidencia documental y plan de verificación, sin dar por realizadas pruebas. Presentar futuro dictamen al responsable; implementación requiere una etapa autorizada posterior.

El orden es una propuesta de trabajo. No autoriza SQL, migraciones, código, selección de tecnologías de aplicación ni aprobación global.

[Índice de fase](00-indice.md) · [Data](../README.md).
