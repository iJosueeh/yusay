# Yusay — Logical Data Model v1.0

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. La aprobación no comprende diseño físico, implementación, despliegue ni validación productiva. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md).

## Fuentes y condiciones de trabajo

- [Baseline conceptual v0.1 CLOSED](../../03-domain/conceptual-model.md): antecedente preservado, no fuente para deducir atributos o claves.
- Prompt maestro proporcionado por el usuario: inventario de 32 relaciones, AJ-01..04, VF-01..05 y reglas adicionales.
- Confirmación posterior del usuario: ubicación de esta carpeta, preservación de los documentos previos y aclaración de AJ-01.
- [Especificación maestra del modelo lógico v1.0](especificacion-maestra-v1.0.md): fuente aprobada recibida y preservada sin cambios; define atributos, PK, AK, FK y URA.

La fuente se contrastó con el repositorio en la [matriz de correspondencia](10-matriz-trazabilidad.md). El usuario aprobó la correspondencia general y precisó email, recorded_at y continuidad tras RETIRED. El [diccionario de Identidad](04-diccionario-datos/01-identidad.md) está aprobado provisionalmente con las precisiones incorporadas; [Evaluaciones](04-diccionario-datos/02-evaluaciones.md) incorpora REV-LOG-004 RESOLVED para el MVP; [Seguimiento](04-diccionario-datos/03-seguimiento.md) está aprobado provisionalmente con precisiones incorporadas; [Compatibilidad](04-diccionario-datos/04-compatibilidad.md) incorpora REV-LOG-008/009/010 consolidadas para MVP; [Contenido](04-diccionario-datos/05-contenido.md) está aprobado provisionalmente con políticas consolidadas; [Auditoría](04-diccionario-datos/06-auditoria.md) incorpora catálogos aprobados (REV-LOG-001 RESOLVED). [Dominios](13-dominios-logicos.md) incorpora REV-LOG-002 RESOLVED; integridad, estados y 07/08/09/10 son conformes provisionalmente. DP-TRANS-001/002 están RESOLVED para MVP; la excepción de auditoría solo aplica a supresión de cuenta. El dictamen favorable está aprobado formalmente y la línea base lógica v1.0 está FROZEN desde 2026-10-07. Las aprobaciones provisionales anteriores describen la secuencia de revisión, no limitan este estado oficial.

Las precisiones posteriores complementan la fuente preservada y se trazan en [pendientes y resoluciones](11-pendientes-y-riesgos.md). No alteran el inventario ni sus claves.

## Documentos

- [Contexto y alcance](01-contexto-y-alcance.md).
- [Inventario de relaciones](02-inventario-relaciones.md).
- [Modelo lógico](03-modelo-logico.md).
- [Diccionario: Identidad](04-diccionario-datos/01-identidad.md).
- [Diccionario: Evaluaciones](04-diccionario-datos/02-evaluaciones.md).
- [Diccionario: Seguimiento](04-diccionario-datos/03-seguimiento.md).
- [Diccionario: Compatibilidad](04-diccionario-datos/04-compatibilidad.md).
- [Diccionario: Contenido](04-diccionario-datos/05-contenido.md).
- [Diccionario: Auditoría](04-diccionario-datos/06-auditoria.md).
- [Integridad referencial](05-integridad-referencial.md).
- [Estados y transiciones](06-estados-y-transiciones.md).
- [Transacciones y concurrencia](07-transacciones-y-concurrencia.md).
- [Privacidad, eliminación y retención](08-privacidad-eliminacion-retencion.md).
- [Decisiones y fuentes](09-decisiones-arquitectonicas.md).
- [Matriz de correspondencia y trazabilidad](10-matriz-trazabilidad.md).
- [Pendientes y riesgos](11-pendientes-y-riesgos.md).
- [Dictamen definitivo aprobado del modelo lógico v1.0](12-dictamen-modelo-logico-v1.md).
- [Dominios lógicos aprobados](13-dominios-logicos.md).
- [Revisión de preparación para dictamen](14-revision-preparacion-dictamen.md).

No se mueve ni elimina documentación existente. PostgreSQL sigue PROVISIONALLY ACCEPTED; aplicación, persistencia, autenticación, hosting e infraestructura continúan abiertos.
