# Inventario aprobado de relaciones

**Estado oficial de la línea base lógica v1.0: APPROVED / FROZEN. Dictamen: FAVORABLE.**

**Fecha de aprobación: 2026-10-07. Autoridad: responsable del proyecto.** Esta fecha corresponde a la aprobación formal, no a la creación o modificación de este documento. Alcance: modelo lógico de datos del MVP de Yusay. Véase el [dictamen definitivo](12-dictamen-modelo-logico-v1.md).

El inventario consta de **32 relaciones en seis módulos**. Se preservan sus nombres y separación. No se agregan, eliminan, fusionan ni renombran relaciones sin identificar una contradicción y solicitar aprobación.

## Identidad — 5

- USER
- USER_CREDENTIAL
- ADMINISTRATOR
- EMAIL_VERIFICATION_TOKEN
- PASSWORD_RESET_TOKEN

[Documento del módulo](04-diccionario-datos/01-identidad.md).

## Evaluaciones — 11

- INSTRUMENT
- INSTRUMENT_VERSION
- INSTRUMENT_VERSION_REFERENCE
- QUESTION
- ANSWER_OPTION
- SCORING_DEFINITION
- SCORING_CONTRIBUTION
- ASSESSMENT_ATTEMPT
- ANSWER
- INTERPRETATION
- ASSESSMENT_RESULT

[Documento del módulo](04-diccionario-datos/02-evaluaciones.md).

## Seguimiento — 7

- DIMENSION
- DIMENSION_VERSION
- DIMENSION_ANCHOR
- CHECK_IN
- MEASUREMENT
- CONTEXT_TAG
- CHECK_IN_CONTEXT_TAG

[Documento del módulo](04-diccionario-datos/03-seguimiento.md).

## Compatibilidad — 2

- INSTRUMENT_VERSION_COMPATIBILITY
- DIMENSION_VERSION_COMPATIBILITY

[Documento del módulo](04-diccionario-datos/04-compatibilidad.md).

## Contenido — 6

- TOPIC
- RESOURCE
- RESOURCE_TOPIC
- INSTRUMENT_TOPIC
- INTERPRETATION_TOPIC
- DIMENSION_TOPIC

[Documento del módulo](04-diccionario-datos/05-contenido.md).

## Auditoría — 1

- AUDIT_EVENT

[Documento del módulo](04-diccionario-datos/06-auditoria.md).

Este inventario no define atributos, claves ni cardinalidades. Constan en la [especificación maestra recibida](especificacion-maestra-v1.0.md) y están documentados en los seis diccionarios, con correspondencia en la [matriz de correspondencia](10-matriz-trazabilidad.md).
