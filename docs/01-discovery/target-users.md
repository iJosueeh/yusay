# Usuarios objetivo y actores

## Segmento inicial

Adultos interesados en realizar seguimiento personal y estructurado de determinados aspectos de su bienestar mediante autoevaluaciones y registros periódicos.

- Mayores de 18 años para el MVP.
- No necesariamente pacientes.
- No requieren diagnóstico ni relación previa con profesionales.
- No se presupone ninguna condición médica o psicológica.

El segmento es una definición inicial de producto; sus necesidades y hábitos requieren validación.

## Actores iniciales

- **Visitor:** persona sin sesión autenticada. El detalle de su acceso al catálogo permanece abierto.
- **User:** persona que utiliza las funciones personales, dentro de las reglas de identidad y privacidad.
- **Administrator:** actor encargado de administrar definiciones, publicación y contenido. Este rol no concede automáticamente acceso a registros privados.

## Actores fuera del MVP

`Patient`, `Psychologist`, `Therapist`, `Clinic`, `Organization` y `Moderator` no forman parte del MVP. No se modela una relación asistencial ni una organización clínica.

## Open Questions

### OQ-PROD-001

- **ID:** OQ-PROD-001.
- **Pregunta:** ¿Cómo se verificará o declarará la mayoría de edad?
- **Motivo:** Concretar el límite de 18 años minimizando datos personales.
- **Impacto:** [LIM-002](../02-product/limitations.md#lim-002--adultos), RF-001 y RNF-006.
- **Status:** OPEN.

### OQ-PROD-002

- **ID:** OQ-PROD-002.
- **Pregunta:** ¿Qué información podrá consultar un Visitor?
- **Motivo:** Delimitar acceso público sin asumir permisos.
- **Impacto:** [Catálogo](../02-product/functional-requirements.md#rf-005) y estrategia de autorización.
- **Status:** OPEN.

### OQ-PROD-003

- **ID:** OQ-PROD-003.
- **Pregunta:** ¿Qué necesidades específicas permiten delimitar mejor el segmento inicial?
- **Motivo:** Validar que la definición de usuarios sea útil para el producto.
- **Impacto:** [Problema](problem-statement.md) y [propuesta de valor](value-proposition.md).
- **Status:** OPEN.
