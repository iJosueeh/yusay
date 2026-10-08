# Decisiones y fuentes

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md).

## Fuentes preservadas y autoridad documental

La [especificación maestra v1.0](especificacion-maestra-v1.0.md) define 32 relaciones y sus atributos/claves. Se recibió en «Texto pegado.txt», identificador c5db6ff2-cb16-4157-ae5c-e15d1ebcceca, y permanece íntegra.

SHA-256: EF1781817E94F5593C53E83D4A626BAC596DB22B3B7F2544E1624B1F6EBC8AD2.

AJ-01..04, VF-01..05 y precisiones posteriores del usuario complementan esa fuente. Los seis diccionarios y [dominios](13-dominios-logicos.md) incorporan aprobaciones; [integridad](05-integridad-referencial.md) y [estados](06-estados-y-transiciones.md) son conformes provisionalmente según la revisión del usuario.

El conceptual v0.1 se preserva como antecedente, no se utiliza para reconstruir claves. [C-LOG-001](11-pendientes-y-riesgos.md#conflicto-comprobado) mantiene el cambio explícito de interpretación condicional a obligatoria, con bloqueo de publicación cuando no existe respaldo oficial. Es un cambio entre capas ya documentado, no una contradicción interna nueva.

## Decisiones consolidadas y su aplicación transversal

- **AJ-01 / REV-LOG-004:** SUM entero con signo de respuestas completas, interpretación oficial obligatoria y cobertura exacta. Envío, resolución de Interpretation existente, Result y SUBMITTED atómicos; no se genera una interpretación nueva al enviar.
- **AJ-02 / REV-LOG-007/011/012/013:** publicación por tipo de Resource, Topics mínimos, revisión editorial, correcciones menores y nuevo Resource para cambios sustanciales. RETIRED terminal; sin eliminación física ordinaria de Topics/Resources.
- **AJ-03 / REV-LOG-005:** CheckIn atómico con Measurement mínima, escala histórica, ventana de 168 horas e intervalo original inclusivo; revision + 1 por operación transaccional confirmada. Sin agregar/eliminar Measurements individuales en edición; vínculos ContextTag nuevos solo ACTIVE.
- **AJ-04 / REV-LOG-003:** email canónico y único sin cambio MVP, tokens de último uso válido, plazos estrictos, consumo/sustitución con eliminación y recuperación atómica que invalida otros tokens de recuperación y sesiones.
- **VF-01/02 y REV-LOG-006:** referencias históricas, guardas y terminalidad; continuidad válida de Attempt sobre versión retirada sin nuevos inicios ni migración.
- **VF-03:** coordinación de máximos por estado, publicación/inicio/finalización, reemplazo atómico de DimensionVersion ACTIVE y detección de conflictos sin sobrescritura silenciosa.
- **VF-04:** supresión individual/de cuenta con dependencias y auditoría desvinculada; retenciones de Attempts terminales sin Result, auditoría y backups; restauración con supresiones reaplicadas antes de habilitar servicio.
- **REV-LOG-008/009/010:** orden total estable de pares, declaraciones vigentes sin modificación ordinaria y preservación de catálogos históricos. Revocación trazable no representada es limitación del MVP.
- **REV-LOG-001:** tres actores, 28 acciones, doce destinos y siete perfiles; T/P obligatorios por acción, sin bienestar privado ni IDs personales persistentes en USER_DELETED.
- **REV-LOG-002:** dominios lógicos aprobados, UTC de intercambio, posiciones positivas, límites inclusivos con signo y días de 24 horas transcurridas, sin elegir tipos físicos.

[Transacciones](07-transacciones-y-concurrencia.md) y [privacidad/retención](08-privacidad-eliminacion-retencion.md) aplican esas políticas. El [registro REV-LOG](11-pendientes-y-riesgos.md) mantiene sus cierres y la [matriz](10-matriz-trazabilidad.md) distingue cobertura y hallazgos.

## Clasificación de la revisión de 09

- **Conforme:** PostgreSQL provisional, tecnologías abiertas, fuente preservada, AJ/VF y REV-LOG-001/002 aprobados. No se detecta selección tecnológica prematura.
- **Precisión documental necesaria — incorporada:** sustituir el estado de contraste inicial por revisión transversal y enlazar aplicación de atomicidad, supresión/retención, integridad y estados. El documento anterior enumeraba resoluciones sin mostrar estas conexiones.
- **Contradicción comprobada:** ninguna nueva en la capa lógica revisada. C-LOG-001 permanece como diferencia histórica trazada.
- **Conforme:** DP-TRANS-001/002 RESOLVED para el MVP tras contraste y aprobación expresa. Detalles físicos/operativos siguen diferidos; no se reabren políticas funcionales.

## Resoluciones funcionales transversales

### DP-TRANS-001

**DP-TRANS-001 — RESOLVED para el MVP v1.0.** Las operaciones administrativas o de seguridad auditables requieren auditoría garantizada para confirmar éxito, **excepto la supresión de cuenta**. En esta excepción aprobada (alternativa B), la disponibilidad del registro de USER_DELETED no condiciona la validez de la supresión.

La eliminación de USER, todas sus dependencias personales y la desvinculación de referencias existentes en AUDIT_EVENT se confirman atómicamente. Si la supresión se confirma pero no puede registrarse USER_DELETED, sigue siendo válida y no se fabrica un evento de éxito. Si no se confirma, no se comunica como completada; ante resultado incierto se verifica el estado efectivo antes de comunicar éxito o fracaso definitivo.

No se conservan correos, identificadores personales, tokens ni registros privados para reconstruir después el evento. No se reintroduce información suprimida. Permanecen la retención máxima de auditoría de 180 días desde occurred_at y las supresiones reaplicadas antes de habilitar una restauración de backups. No se agregan entidades, atributos, estados o relaciones; el tratamiento técnico de fallos y la verificación del estado efectivo se difieren.

**Clasificación: Conforme.** [Resolución y contraste](07-transacciones-y-concurrencia.md#dp-trans-001). La alternativa A no se selecciona; B incorpora una excepción explícita aprobada, sin cambiar catálogos, claves ni privacidad. Posible ausencia de USER_DELETED es una limitación aceptada, no un pendiente funcional reabierto.

### DP-TRANS-002

**Status: RESOLVED para el MVP. Clasificación: Conforme.** Cancelación válida confirmada antes de expires_at → CANCELLED con ended_at efectivo; plazo vencido al confirmar → EXPIRED con ended_at = expires_at, aunque status aún sea IN_PROGRESS. Terminales irreversibles, Answers eliminadas al confirmar, orden concurrente coherente y sin SUBMITTED fuera de vigencia. Compatible con expiración/retención aprobadas. [Resolución](07-transacciones-y-concurrencia.md#dp-trans-002).

Estos hallazgos no reabren REV-LOG-001/002: catálogos y dominios están aprobados, pero no resolvían estos comportamientos operativos.

## Decisiones físicas y operativas diferidas

[ADR-001](../../06-decisions/ADR-001-database-engine.md) mantiene PostgreSQL **PROVISIONALLY ACCEPTED**, con sus alternativas preservadas en el ADR. No se convierte aquí en aceptación definitiva.

Backend, frontend, persistence/ORM, authentication, infraestructura y hosting siguen abiertos. Tampoco se eligen tipos, longitudes, colaciones, generación de IDs, comparador concreto, serialización de metadata, algoritmos de hash, mecanismos de coordinación o frecuencia de limpieza.

Restauración debe reaplicar supresiones y respetar retenciones antes de habilitar servicio. La fuente actualizada de información de supresión y su coordinación técnica no están diseñadas; no se crea entidad, ledger ni mecanismo de backup. La política es **Conforme**; su realización técnica se clasifica **Decisión pendiente** de diseño correspondiente.

Estas decisiones diferidas no invalidan dominios/claves aprobados ni autorizan nuevas tecnologías.

## Límite

La consolidación posterior sincroniza Evaluaciones, Auditoría, Estados, transversales y registro de pendientes; añade una revisión de preparación. Fuente, conceptual, atributos y claves se preservan; el dictamen definitivo queda APPROVED y la línea base lógica v1.0 FROZEN por autorización formal del responsable del proyecto (2026-10-07). No se generan SQL, migraciones o código.

[Matriz y hallazgos](10-matriz-trazabilidad.md#hallazgos-de-la-revisión-transversal) · [Índice](00-indice.md).

## Cierre formal y control de cambios

El responsable del proyecto aprobó formalmente el dictamen FAVORABLE y autorizó la línea base lógica v1.0 **APPROVED / FROZEN** el **2026-10-07**. La fecha de aprobación es independiente de las fechas de creación/modificación de los documentos; no se atribuye aprobación retroactiva a sus antecedentes.

REV-LOG-001..013 y DP-TRANS-001/002 conservan **RESOLVED**. Riesgos residuales, limitaciones y asuntos físicos/operativos diferidos mantienen sus categorías. PostgreSQL sigue **PROVISIONALLY ACCEPTED**.

Se permiten correcciones de redacción, formato y referencias sin alterar el significado aprobado. Cambios semánticos o estructurales requieren propuesta documentada, justificación, impacto, trazabilidad, aprobación expresa y nueva versión o revisión formal de la línea base. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md#condiciones-para-cambios-posteriores).
